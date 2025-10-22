using Content.Shared.Damage;

namespace Content.Shared.Combat.Penetration;

/// <summary>
/// Annotates projectiles (and melee thrown variants) with CE-style penetration data.
/// Behaviour is server-authoritative; this component only carries data and toggles.
/// </summary>
[RegisterComponent]
public sealed partial class PenetratingProjectileComponent : Component
{
    /// <summary>
    /// Armor penetration capacity against sharp layers (mm RHA equivalent).
    /// </summary>
    [DataField("apSharpMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float SharpPenetrationMm = 0f;

    /// <summary>
    /// Armor penetration capacity against blunt-only layers (mm RHA equivalent).
    /// </summary>
    [DataField("apBluntMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BluntPenetrationMm = 0f;

    /// <summary>
    /// Fraction [0,1] of the remaining sharp damage that converts to blunt on deflection.
    /// </summary>
    [DataField("deflectBluntScalar")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float DeflectBluntScalar = 0f;

    /// <summary>
    /// Optional content tag to help UI/telemetry buckets (e.g. HP, FMJ, AP).
    /// </summary>
    [DataField("familyTag")]
    [ViewVariables(VVAccess.ReadWrite)]
    public string? FamilyTag;

    /// <summary>
    /// When true, the projectile requests sharp damage to be considered the primary physical channel during the pre-pass.
    /// </summary>
    [DataField("preferSharp")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool PreferSharp = true;

    /// <summary>
    /// Optional override for the projectile's outgoing damage specifier. Allows authoring ammo with no legacy damage component.
    /// </summary>
    [DataField("damageOverride")]
    public DamageSpecifier? DamageOverride;
}
