using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Combat.Penetration;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Server.Combat.Penetration;

/// <summary>
/// Server-side entry point for the CE-style armor penetration pre-pass.
/// Currently wires up feature gating, gathers layer stacks, and resolves deterministic
/// armor penetration for projectiles. Melee integration will land once melee data tags exist.
/// </summary>
public sealed class PenetrationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private bool _enabled;

    private const string DamageTypePiercing = "Piercing";
    private const string DamageTypeSlash = "Slash";
    private const string DamageTypeBlunt = "Blunt";

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CombatPenetrationPrepassEnabled, value => _enabled = value, true);

        SubscribeLocalEvent<PenetratingProjectileComponent, ProjectileHitEvent>(OnProjectileHit);

        // Forward melee hits once CE-specific melee data exists; this keeps the hook in place so weapon components
        // can opt-in later.
    }

    private void OnProjectileHit(Entity<PenetratingProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (!_enabled)
            return;

        if (!TryComp(projectile, out ProjectileComponent? projectileComponent))
            return;

        var damage = projectile.Comp.DamageOverride != null
            ? new DamageSpecifier(projectile.Comp.DamageOverride)
            : new DamageSpecifier(args.Damage);

        // Resolve penetration only if we have sharp damage to process.
        if (!HasRelevantSharpDamage(damage))
            return;

        var result = ResolveProjectilePenetration(projectile, projectileComponent, args.Target, damage);

        if (!result.Handled)
            return;

        args.Damage = result.Damage;
        args.PenetrationHandled = true;

        if (result.ProjectileStops)
        {
            projectileComponent.ProjectileSpent = true;
            Dirty(projectile.Owner, projectileComponent);
        }
    }

    public bool TryProcessMelee(
        EntityUid attacker,
        EntityUid weapon,
        EntityUid target,
        ref DamageSpecifier damage,
        out bool skipArmorModifiers)
    {
        skipArmorModifiers = false;

        if (!_enabled)
            return false;

        // TODO COMBAT-EXTENDED: integrate melee penetration once melee components provide AP/AR data.
        return false;
    }

    private bool HasRelevantSharpDamage(DamageSpecifier damage)
    {
        return damage.DamageDict.TryGetValue(DamageTypePiercing, out var piercing) && piercing > FixedPoint2.Zero
               || damage.DamageDict.TryGetValue(DamageTypeSlash, out var slash) && slash > FixedPoint2.Zero;
    }

    private PenetrationResult ResolveProjectilePenetration(
        Entity<PenetratingProjectileComponent> projectile,
        ProjectileComponent projectileComponent,
        EntityUid target,
        DamageSpecifier workingDamage)
    {
        var penResult = new PenetrationResult(workingDamage);

        if (projectile.Comp.SharpPenetrationMm <= 0f)
            return penResult;

        var attackDirection = GetProjectileDirection(projectileComponent);
        var targetFacing = GetTargetFacing(target);
        var hitFromFront = DetermineFrontHit(attackDirection, targetFacing);

        var region = ResolveRegion(target);

        var entryLayers = new List<ArmorLayer>();
        var exitLayers = new List<ArmorLayer>();

        try
        {
            CollectArmorLayers(target, region, hitFromFront, entryLayers);
            CollectArmorLayers(target, region, !hitFromFront, exitLayers);

            entryLayers.Sort(static (a, b) => a.Order.CompareTo(b.Order));
            exitLayers.Sort(static (a, b) => b.Order.CompareTo(a.Order));

            var apRemaining = projectile.Comp.SharpPenetrationMm;

            if (TryDeflect(ref apRemaining, projectile.Comp.DeflectBluntScalar, entryLayers, ref workingDamage, out var deflected))
            {
                penResult = penResult with
                {
                    ProjectileStops = deflected,
                    Handled = true,
                    Damage = workingDamage
                };
                return penResult;
            }

            if (TryProcessCoreLayer(target, region, ref apRemaining, projectile.Comp.DeflectBluntScalar, ref workingDamage))
            {
                penResult = penResult with
                {
                    ProjectileStops = true,
                    Handled = true,
                    Damage = workingDamage
                };
                return penResult;
            }

            if (TryDeflect(ref apRemaining, projectile.Comp.DeflectBluntScalar, exitLayers, ref workingDamage, out var exitStopped))
            {
                penResult = penResult with
                {
                    ProjectileStops = exitStopped,
                    Handled = true,
                    Damage = workingDamage
                };
                return penResult;
            }

            // Penetrated through-and-through. No further changes to damage, but mark as handled so future systems
            // can skip coefficient-based mitigation when they detect penetration metadata.
            penResult = penResult with { Handled = true, Damage = workingDamage };
            return penResult;
        }
        finally
        {
            entryLayers.Clear();
            exitLayers.Clear();
        }
    }

    private Vector2 GetProjectileDirection(ProjectileComponent component)
    {
        // Angle is guaranteed to be valid. Convert to world-space unit vector.
        return component.Angle.ToWorldVec();
    }

    private Vector2 GetTargetFacing(EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? xform))
            return Vector2.Zero;

        return xform.WorldRotation.ToWorldVec();
    }

    private bool DetermineFrontHit(Vector2 attackDirection, Vector2 targetFacing)
    {
        if (attackDirection == Vector2.Zero || targetFacing == Vector2.Zero)
            return true;

        attackDirection = Vector2.Normalize(attackDirection);
        targetFacing = Vector2.Normalize(targetFacing);

        // If the projectile is travelling roughly opposite the target's facing direction, it's a front hit.
        return Vector2.Dot(targetFacing, attackDirection) <= 0f;
    }

    private PenetrationArmorRegion ResolveRegion(EntityUid target)
    {
        // TODO COMBAT-EXTENDED: implement proper zone resolution once hitboxes are exposed.
        return PenetrationArmorRegion.Body;
    }

    private void CollectArmorLayers(
        EntityUid target,
        PenetrationArmorRegion region,
        bool frontFacing,
        List<ArmorLayer> buffer)
    {
        buffer.Clear();

        if (TryComp(target, out ArmorRatingComponent? targetArmor))
            TryAddLayer(target, targetArmor, region, frontFacing, buffer);

        if (TryComp(target, out InventoryComponent? inventory))
        {
            var enumerator = new InventorySystem.InventorySlotEnumerator(inventory);
            while (enumerator.NextItem(out var item))
            {
                if (!TryComp(item, out ArmorRatingComponent? armor))
                    continue;

                TryAddLayer(item, armor, region, frontFacing, buffer);
            }
        }
    }

    private void TryAddLayer(
        EntityUid provider,
        ArmorRatingComponent armor,
        PenetrationArmorRegion region,
        bool frontFacing,
        List<ArmorLayer> buffer)
    {
        if (!CoversRegion(armor, region, frontFacing))
            return;

        buffer.Add(new ArmorLayer(
            provider,
            armor.SharpRatingMm,
            armor.BluntRatingMm,
            armor.LayerOrder,
            armor.StopsOnDeflect));
    }

    private bool CoversRegion(ArmorRatingComponent armor, PenetrationArmorRegion region, bool frontFacing)
    {
        var coverage = armor.Coverage;
        if (coverage.Count == 0)
            return CoversDefault(region, frontFacing);

        return region switch
        {
            PenetrationArmorRegion.Head => ContainsRegion(coverage, PenetrationArmorRegion.Head),
            PenetrationArmorRegion.Body => CoversBody(coverage, frontFacing),
            PenetrationArmorRegion.TorsoFront => frontFacing && ContainsRegion(coverage, PenetrationArmorRegion.TorsoFront),
            PenetrationArmorRegion.TorsoBack => !frontFacing && ContainsRegion(coverage, PenetrationArmorRegion.TorsoBack),
            _ => true
        };
    }

    private bool CoversBody(IReadOnlyList<PenetrationArmorRegion> coverage, bool frontFacing)
    {
        var hasBody = false;
        foreach (var entry in coverage)
        {
            switch (entry)
            {
                case PenetrationArmorRegion.Body:
                    hasBody = true;
                    break;
                case PenetrationArmorRegion.TorsoFront when frontFacing:
                    return true;
                case PenetrationArmorRegion.TorsoBack when !frontFacing:
                    return true;
            }
        }

        return hasBody;
    }

    private bool CoversDefault(PenetrationArmorRegion region, bool frontFacing)
    {
        return region switch
        {
            PenetrationArmorRegion.Head => true,
            PenetrationArmorRegion.Body => true,
            PenetrationArmorRegion.TorsoFront => frontFacing,
            PenetrationArmorRegion.TorsoBack => !frontFacing,
            _ => true
        };
    }

    private static bool ContainsRegion(IEnumerable<PenetrationArmorRegion> coverage, PenetrationArmorRegion target)
    {
        foreach (var entry in coverage)
        {
            if (entry == target)
                return true;
        }

        return false;
    }

    private bool TryDeflect(
        ref float apRemaining,
        float deflectScalar,
        List<ArmorLayer> layers,
        ref DamageSpecifier damage,
        out bool projectileStops)
    {
        projectileStops = false;

        foreach (var layer in layers)
        {
            if (layer.SharpRating <= 0)
                continue;

            if (apRemaining > layer.SharpRating)
            {
                apRemaining -= layer.SharpRating;
                continue;
            }

            ApplyDeflection(ref damage, deflectScalar);

            projectileStops = layer.StopsOnDeflect;
            return true;
        }

        return false;
    }

    private bool TryProcessCoreLayer(
        EntityUid target,
        PenetrationArmorRegion region,
        ref float apRemaining,
        float deflectScalar,
        ref DamageSpecifier damage)
    {
        if (!TryComp(target, out SpeciesCoreArmorComponent? core))
            return false;

        var coreAr = region == PenetrationArmorRegion.Head
            ? core.CoreHeadArmorMm
            : core.CoreBodyArmorMm;

        if (coreAr <= 0f)
            return false;

        if (apRemaining > coreAr)
        {
            apRemaining -= coreAr;
            return false;
        }

        ApplyDeflection(ref damage, deflectScalar);
        return true;
    }

    private void ApplyDeflection(ref DamageSpecifier damage, float deflectScalar)
    {
        var redirected = FixedPoint2.Zero;

        redirected += ExtractDamage(ref damage, DamageTypePiercing);
        redirected += ExtractDamage(ref damage, DamageTypeSlash);

        if (redirected <= FixedPoint2.Zero || deflectScalar <= 0f)
            return;

        var bluntAddition = redirected * deflectScalar;

        if (bluntAddition <= FixedPoint2.Zero)
            return;

        if (damage.DamageDict.TryGetValue(DamageTypeBlunt, out var existing))
            damage.DamageDict[DamageTypeBlunt] = existing + bluntAddition;
        else
            damage.DamageDict[DamageTypeBlunt] = bluntAddition;
    }

    private FixedPoint2 ExtractDamage(ref DamageSpecifier damage, string type)
    {
        if (!damage.DamageDict.TryGetValue(type, out var value))
            return FixedPoint2.Zero;

        if (value <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        damage.DamageDict[type] = FixedPoint2.Zero;
        return value;
    }

    private readonly record struct ArmorLayer(
        EntityUid Provider,
        float SharpRating,
        float BluntRating,
        PenetrationArmorLayerOrder Order,
        bool StopsOnDeflect);

    private readonly record struct PenetrationResult(DamageSpecifier Damage)
    {
        public bool ProjectileStops { get; init; }
        public bool Handled { get; init; }
    }
}
