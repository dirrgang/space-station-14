using Content.Server.Hands.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CombatMode;
using Content.Shared.CriminalRecords;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.Tag;
using Content.Shared.Threat;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Threat;

/// <summary>
/// Computes per-appraiser threat scores for observed entities.
/// </summary>
public sealed class ThreatAssessmentSystem : EntitySystem
{
    private const float Epsilon = 0.0001f;

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCards = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill _sawmill = default!;

    private EntityQuery<ThreatTokenComponent> _tokenQuery = default!;
    private EntityQuery<TagComponent> _tagQuery = default!;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery = default!;
    private EntityQuery<HandsComponent> _handsQuery = default!;
    private EntityQuery<InventoryComponent> _inventoryQuery = default!;
    private EntityQuery<WieldableComponent> _wieldableQuery = default!;
    private EntityQuery<StationRecordKeyStorageComponent> _recordKeyQuery = default!;
    private EntityQuery<CombatModeComponent> _combatQuery = default!;
    private EntityQuery<GunComponent> _gunQuery = default!;
    private EntityQuery<MeleeWeaponComponent> _meleeQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("threat");

        _tokenQuery = GetEntityQuery<ThreatTokenComponent>();
        _tagQuery = GetEntityQuery<TagComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _wieldableQuery = GetEntityQuery<WieldableComponent>();
        _recordKeyQuery = GetEntityQuery<StationRecordKeyStorageComponent>();
        _combatQuery = GetEntityQuery<CombatModeComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();
        _meleeQuery = GetEntityQuery<MeleeWeaponComponent>();
    }

    /// <summary>
    /// Attempts to compute a threat score using the caller's <see cref="ThreatAppraisalComponent"/>.
    /// </summary>
    public bool TryGetThreatScore(EntityUid appraiser, EntityUid target, out float score, bool reuseOnCooldown = true, bool storeReport = true)
    {
        score = default;
        if (!TryGetThreatReport(appraiser, target, out var report, reuseOnCooldown, storeReport))
            return false;

        score = report.Score;
        return true;
    }

    /// <summary>
    /// Attempts to produce a full threat report using the caller's <see cref="ThreatAppraisalComponent"/>.
    /// </summary>
    public bool TryGetThreatReport(EntityUid appraiser, EntityUid target, out ThreatReport report, bool reuseOnCooldown = true, bool storeReport = true)
    {
        report = default!;

        if (!TryComp(appraiser, out ThreatAppraisalComponent? appraisal))
        {
            _sawmill.Warning("Entity {Appraiser} requested threat report without ThreatAppraisalComponent.", ("Appraiser", appraiser));
            return false;
        }

        if (!_prototype.TryIndex(appraisal.Profile, out var profile))
        {
            _sawmill.Error("Threat profile {Profile} requested by {Appraiser} is missing.", ("Profile", appraisal.Profile), ("Appraiser", appraiser));
            return false;
        }

        var now = _timing.CurTime;
        if (reuseOnCooldown && appraisal.Cooldown > TimeSpan.Zero)
        {
            var nextEvaluation = appraisal.LastEvaluatedAt + appraisal.Cooldown;
            if (now < nextEvaluation && appraisal.LastReport != null)
            {
                _sawmill.Debug("Returning cached threat report for {Appraiser} -> {Target} (cooldown).", ("Appraiser", appraiser), ("Target", target));
                report = appraisal.LastReport;
                return true;
            }
        }

        report = GenerateReport(profile, appraiser, target);

        if (storeReport)
        {
            appraisal.LastEvaluatedAt = now;
            appraisal.LastReport = report;
            _sawmill.Debug("Stored threat report for {Appraiser} -> {Target} with score {Score}.", ("Appraiser", appraiser), ("Target", target), ("Score", report.Score));
        }

        return true;
    }

    /// <summary>
    /// Generates a report without requiring a <see cref="ThreatAppraisalComponent"/>.
    /// Useful for debugging tools and scripted checks.
    /// </summary>
    public bool TryDebugReport(ProtoId<ThreatProfilePrototype> profileId, EntityUid target, out ThreatReport report, EntityUid? appraiserOverride = null)
    {
        report = default!;

        if (!_prototype.TryIndex(profileId, out var profile))
        {
            _sawmill.Error("Threat debug requested unknown profile {Profile}.", ("Profile", profileId));
            return false;
        }

        var appraiser = appraiserOverride ?? EntityUid.Invalid;
        report = GenerateReport(profile, appraiser, target);
        return true;
    }

    /// <summary>
    /// Aggregates basic token sources (entity, held items, worn items).
    /// </summary>
    private void GatherDefaultTokens(EntityUid target, List<ThreatTokenEntry> tokens)
    {
        if (_tokenQuery.TryGetComponent(target, out var tokenComp))
        {
            foreach (var token in tokenComp.Tokens)
            {
                tokens.Add(new ThreatTokenEntry(token, ThreatSourceContext.Entity, target));
            }
        }

        if (_handsQuery.TryGetComponent(target, out var hands))
        {
            var handEntity = (target, hands);
            foreach (var (handId, _) in hands.Hands)
            {
                if (!_hands.TryGetHeldItem(handEntity, handId, out var held))
                    continue;

                var context = ThreatSourceContext.Held;
                if (_wieldableQuery.TryGetComponent(held.Value, out var wieldable) && wieldable.Wielded)
                    context |= ThreatSourceContext.Wielded;

                AddTokensFromSource(held.Value, context, tokens, handId);
            }
        }

        if (!_inventoryQuery.TryGetComponent(target, out var inventory))
            return;

        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory);
        while (enumerator.NextItem(out var item, out var slot))
        {
            if (slot == null)
                continue;

            var context = GetContextForSlot(slot);
            AddTokensFromSource(item, context, tokens, slot.Name);
        }
    }

    private void AddTokensFromSource(EntityUid source, ThreatSourceContext context, List<ThreatTokenEntry> tokens, string? detail)
    {
        HashSet<string>? added = null;

        void AddToken(string token)
        {
            added ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!added.Add(token))
                return;

            // Record the token so profiles can evaluate it later.
            tokens.Add(new ThreatTokenEntry(token, context, source, detail));
        }

        if (_tokenQuery.TryGetComponent(source, out var tokenComp))
        {
            foreach (var token in tokenComp.Tokens)
            {
                AddToken(token);
            }
        }

        if (_gunQuery.HasComponent(source))
        {
            AddToken("weapon");
            AddToken("weapon-gun");
        }

        if (_meleeQuery.HasComponent(source))
        {
            AddToken("weapon");
            AddToken("weapon-melee");
        }
    }

    private static ThreatSourceContext GetContextForSlot(SlotDefinition slot)
    {
        var flags = slot.SlotFlags;
        if ((flags & SlotFlags.POCKET) != 0 || (flags & SlotFlags.SUITSTORAGE) != 0)
            return ThreatSourceContext.Inventory;

        return ThreatSourceContext.Worn;
    }

    private ThreatReport GenerateReport(ThreatProfilePrototype profile, EntityUid appraiser, EntityUid target)
    {
        var builder = new ThreatReportBuilder(appraiser, target, profile.ID);

        if (!NearZero(profile.BaseThreat))
            builder.Add("base", profile.BaseThreat, target, ThreatSourceContext.Entity);

        var tokens = new List<ThreatTokenEntry>(8);
        GatherDefaultTokens(target, tokens);

        var collectEvent = new CollectThreatTokensEvent(appraiser, profile, builder, tokens);
        try
        {
            RaiseLocalEvent(target, ref collectEvent);
        }
        catch (Exception ex)
        {
            var targetName = EntityManager.ToPrettyString(target);
            var appraiserName = appraiser.IsValid() ? EntityManager.ToPrettyString(appraiser) : "<null>";
            _sawmill.Error($"CollectThreatTokensEvent handler threw for {targetName} (appraiser {appraiserName}): {ex}");
        }

        EvaluateTokens(profile, tokens, builder);
        EvaluateIdentity(target, profile, builder);
        EvaluateSpecies(target, profile, builder);
        EvaluateTags(target, profile, builder);
        EvaluateCombatMode(target, profile, builder);

        var modifyEvent = new ModifyThreatAssessmentEvent(appraiser, profile, builder);
        try
        {
            RaiseLocalEvent(target, ref modifyEvent);
        }
        catch (Exception ex)
        {
            var targetName = EntityManager.ToPrettyString(target);
            var appraiserName = appraiser.IsValid() ? EntityManager.ToPrettyString(appraiser) : "<null>";
            _sawmill.Error($"ModifyThreatAssessmentEvent handler threw for {targetName} (appraiser {appraiserName}): {ex}");
        }

        var total = profile.ClampScore(builder.Score);
        if (!NearEqual(total, builder.Score))
        {
            var delta = total - builder.Score;
            if (!NearZero(delta))
                builder.Add("clamp", delta);
        }

        return builder.ToReport();
    }
    private void EvaluateTokens(ThreatProfilePrototype profile, List<ThreatTokenEntry> tokens, ThreatReportBuilder builder)
    {
        if (tokens.Count == 0)
            return;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in tokens)
        {
            if (!profile.TokenWeights.TryGetValue(entry.Token, out var weight))
            {
                if (!NearZero(profile.UnknownTokenPenalty))
                {
                    builder.Add($"token:{entry.Token}:unknown", profile.UnknownTokenPenalty, entry.Source, entry.Context, entry.Detail);
                    _sawmill.Debug("Applied unknown token penalty for {Token} on {Target}.", ("Token", entry.Token), ("Target", builder.Target));
                }

                continue;
            }

            counts.TryGetValue(entry.Token, out var stackCount);
            if (weight.MaxStacks.HasValue && stackCount >= weight.MaxStacks.Value)
                continue;

            counts[entry.Token] = stackCount + 1;

            var value = weight.Base;
            if ((entry.Context & ThreatSourceContext.Held) != 0)
                value += weight.Held;
            if ((entry.Context & ThreatSourceContext.Worn) != 0)
                value += weight.Worn;
            if ((entry.Context & ThreatSourceContext.Inventory) != 0)
                value += weight.Inventory;
            if ((entry.Context & ThreatSourceContext.Wielded) != 0)
                value += weight.Wielded;

            value *= profile.TokenMultiplier;

            if (!NearZero(value))
                builder.Add($"token:{entry.Token}", value, entry.Source, entry.Context, entry.Detail);
        }
    }

    private void EvaluateIdentity(EntityUid target, ThreatProfilePrototype profile, ThreatReportBuilder builder)
    {
        StationRecordKey? key = null;
        EntityUid? idEntity = null;

        if (_idCards.TryFindIdCard(target, out var idCard))
        {
            idEntity = idCard.Owner;

            if (_recordKeyQuery.TryGetComponent(idCard.Owner, out var storage) && storage.Key != null)
                key = storage.Key;
        }
        else if (_recordKeyQuery.TryGetComponent(target, out var targetStorage) && targetStorage.Key != null)
        {
            key = targetStorage.Key;
            idEntity = target;
        }

        if (idEntity == null)
        {
            if (!NearZero(profile.MissingIdThreat))
            {
                builder.Add("identity:missing-id", profile.MissingIdThreat, target, ThreatSourceContext.Entity);
                _sawmill.Debug("Target {Target} is missing an ID; applied missing-id threat.", ("Target", target));
            }
            return;
        }

        if (key == null)
        {
            if (!NearZero(profile.AnonymousThreat))
            {
                builder.Add("identity:anonymous", profile.AnonymousThreat, idEntity.Value, ThreatSourceContext.Worn);
                _sawmill.Debug("Target {Target} has anonymous credentials; applied anonymous threat.", ("Target", target));
            }
        }
        else if (!_stationRecords.TryGetRecord(key.Value, out CriminalRecord? record))
        {
            if (!NearZero(profile.AnonymousThreat))
            {
                builder.Add("identity:unlinked", profile.AnonymousThreat, idEntity.Value, ThreatSourceContext.Worn);
                _sawmill.Debug("Target {Target} has ID without matching station record; applied anonymous threat.", ("Target", target));
            }
        }
        else if (profile.SecurityStatusWeights.TryGetValue(record.Status, out var statusWeight) && !NearZero(statusWeight))
        {
            builder.Add($"security:{record.Status}", statusWeight, idEntity.Value, ThreatSourceContext.Worn, record.Status.ToString());
            _sawmill.Debug("Applied security status weight {Status} for {Target}.", ("Status", record.Status), ("Target", target));
        }

        if (TryComp<IdCardComponent>(idEntity.Value, out var idComp))
        {
            if (idComp.JobPrototype is { } jobProto && profile.JobWeights.TryGetValue(jobProto, out var jobWeight) && !NearZero(jobWeight))
            {
                builder.Add($"job:{jobProto.Id}", jobWeight, idEntity.Value, ThreatSourceContext.Worn, jobProto.Id);
                _sawmill.Debug("Applied job weight {Job} for {Target}.", ("Job", jobProto.Id), ("Target", target));
            }

            foreach (var department in idComp.JobDepartments)
            {
                if (!profile.DepartmentWeights.TryGetValue(department, out var deptWeight) || NearZero(deptWeight))
                    continue;

                builder.Add($"department:{department.Id}", deptWeight, idEntity.Value, ThreatSourceContext.Worn, department.Id);
                _sawmill.Debug("Applied department weight {Department} for {Target}.", ("Department", department.Id), ("Target", target));
            }
        }
    }

    private void EvaluateSpecies(EntityUid target, ThreatProfilePrototype profile, ThreatReportBuilder builder)
    {
        if (!_humanoidQuery.TryGetComponent(target, out var humanoid))
            return;

        if (profile.SpeciesWeights.TryGetValue(humanoid.Species, out var weight) && !NearZero(weight))
        {
            builder.Add($"species:{humanoid.Species.Id}", weight, target, ThreatSourceContext.Entity, humanoid.Species.Id);
            _sawmill.Debug("Applied species weight {Species} for {Target}.", ("Species", humanoid.Species.Id), ("Target", target));
        }
    }

    private void EvaluateTags(EntityUid target, ThreatProfilePrototype profile, ThreatReportBuilder builder)
    {
        if (!_tagQuery.TryGetComponent(target, out var tags))
            return;

        foreach (var tag in tags.Tags)
        {
            if (!profile.TagWeights.TryGetValue(tag.Id, out var weight) || NearZero(weight))
                continue;

            builder.Add($"tag:{tag.Id}", weight, target, ThreatSourceContext.Entity, tag.Id);
            _sawmill.Debug("Applied tag weight {Tag} for {Target}.", ("Tag", tag.Id), ("Target", target));
        }
    }

    private void EvaluateCombatMode(EntityUid target, ThreatProfilePrototype profile, ThreatReportBuilder builder)
    {
        if (NearZero(profile.CombatModeThreat))
            return;

        if (_combatQuery.TryGetComponent(target, out var combat) && combat.IsInCombatMode)
        {
            builder.Add("combat", profile.CombatModeThreat, target, ThreatSourceContext.Entity);
            _sawmill.Debug("Applied combat-mode threat for {Target}.", ("Target", target));
        }
    }

    private static bool NearZero(float value) => MathF.Abs(value) <= Epsilon;
    private static bool NearEqual(float a, float b) => MathF.Abs(a - b) <= Epsilon;
}

