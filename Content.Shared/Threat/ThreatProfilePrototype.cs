using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Security;
using Robust.Shared.Prototypes;

namespace Content.Shared.Threat;

/// <summary>
/// Configuration describing how a given entity evaluates potential threats.
/// Profiles are referenced by <see cref=\"ThreatAppraisalComponent\"/>.
/// </summary>
[Prototype("threatProfile")]
public sealed partial class ThreatProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Flat base threat that is applied to every evaluation.
    /// </summary>
    [DataField("baseThreat")]
    public float BaseThreat;

    /// <summary>
    /// Added when the target has no identifiable ID card or station record.
    /// </summary>
    [DataField("missingIdThreat")]
    public float MissingIdThreat;

    /// <summary>
    /// Added when the target's ID intentionally hides their identity (e.g. agent ID).
    /// </summary>
    [DataField("anonymousThreat")]
    public float AnonymousThreat;

    /// <summary>
    /// Added when the target is currently in combat mode.
    /// </summary>
    [DataField("combatModeThreat")]
    public float CombatModeThreat;

    /// <summary>
    /// Optional clamp applied after all contributions.
    /// </summary>
    [DataField("minimumScore")]
    public float MinimumScore;

    [DataField("maximumScore")]
    public float? MaximumScore;

    /// <summary>
    /// Threat contributions based on the target's criminal record status.
    /// </summary>
    [DataField("securityStatusWeights")]
    public Dictionary<SecurityStatus, float> SecurityStatusWeights { get; private set; } = new();

    /// <summary>
    /// Threat contributions mapped by job prototypes carried on the ID card.
    /// </summary>
    [DataField("jobWeights")]
    public Dictionary<ProtoId<JobPrototype>, float> JobWeights { get; private set; } = new();

    /// <summary>
    /// Threat contributions mapped by departments listed on the ID card.
    /// </summary>
    [DataField("departmentWeights")]
    public Dictionary<ProtoId<DepartmentPrototype>, float> DepartmentWeights { get; private set; } = new();

    /// <summary>
    /// Optional weighting based on species prototypes.
    /// </summary>
    [DataField("speciesWeights")]
    public Dictionary<ProtoId<SpeciesPrototype>, float> SpeciesWeights { get; private set; } = new();

    /// <summary>
    /// Additional weighting based on <see cref=\"TagComponent\"/> entries.
    /// </summary>
    [DataField("tagWeights")]
    public Dictionary<string, float> TagWeights { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Weights assigned to threat tokens discovered on the target or their equipment.
    /// </summary>
    [DataField("tokenWeights")]
    public Dictionary<string, ThreatTokenWeight> TokenWeights { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional penalty applied for tokens that were discovered but not configured.
    /// </summary>
    [DataField("unknownTokenPenalty")]
    public float UnknownTokenPenalty;

    /// <summary>
    /// Optional multiplier applied after token contributions.
    /// </summary>
    [DataField("tokenMultiplier")]
    public float TokenMultiplier = 1f;

    /// <summary>
    /// Applies configured clamps to the resulting score.
    /// </summary>
    public float ClampScore(float value)
    {
        if (MaximumScore.HasValue)
            value = MathF.Min(value, MaximumScore.Value);

        if (value < MinimumScore)
            value = MinimumScore;

        return value;
    }
}

/// <summary>
/// Describes how a specific threat token contributes to the final score.
/// </summary>
[DataDefinition]
public sealed partial class ThreatTokenWeight
{
    /// <summary>
    /// Base value applied whenever the token is detected, regardless of context.
    /// </summary>
    [DataField("base")]
    public float Base;

    /// <summary>
    /// Additional value when the token originates from an item held in hand.
    /// </summary>
    [DataField("held")]
    public float Held;

    /// <summary>
    /// Additional value when the token originates from worn equipment.
    /// </summary>
    [DataField("worn")]
    public float Worn;

    /// <summary>
    /// Additional value when the token originates from stored inventory.
    /// </summary>
    [DataField("inventory")]
    public float Inventory;

    /// <summary>
    /// Additional value when the token originates from an actively wielded item.
    /// </summary>
    [DataField("wielded")]
    public float Wielded;

    /// <summary>
    /// Optional cap on the number of times this token may be applied.
    /// </summary>
    [DataField("maxStacks")]
    public int? MaxStacks;
}
