using Robust.Shared.Prototypes;

namespace Content.Shared.Threat;

/// <summary>
/// Indicates the context in which a threat token was discovered.
/// Helps profiles decide how heavily to weight a token.
/// </summary>
[Flags]
public enum ThreatSourceContext : byte
{
    None = 0,

    /// <summary>
    /// The token originates from the entity itself (species, traits, etc.).
    /// </summary>
    Entity = 1 << 0,

    /// <summary>
    /// The token originates from an item currently held in hand.
    /// </summary>
    Held = 1 << 1,

    /// <summary>
    /// The token originates from an equipped wearable slot (uniform, helmet, etc.).
    /// </summary>
    Worn = 1 << 2,

    /// <summary>
    /// The token originates from inventory storage (pockets, backpack, implants, etc.).
    /// </summary>
    Inventory = 1 << 3,

    /// <summary>
    /// The token originates from an actively wielded item.
    /// </summary>
    Wielded = 1 << 4,
}

/// <summary>
/// Represents a token describing a potential source of threat on an entity.
/// </summary>
public readonly record struct ThreatTokenEntry(string Token, ThreatSourceContext Context, EntityUid? Source = null, string? Detail = null);

/// <summary>
/// Represents a single additive contribution to an assessed threat score.
/// </summary>
public readonly record struct ThreatContribution(string Reason, float Value, EntityUid? Source = null, ThreatSourceContext Context = ThreatSourceContext.None, string? Detail = null);

/// <summary>
/// Read-only report containing the final threat score and the individual contributions.
/// </summary>
public sealed class ThreatReport
{
    /// <summary>
    /// Entity requesting the assessment.
    /// </summary>
    public EntityUid Appraiser { get; }

    /// <summary>
    /// Entity that was assessed.
    /// </summary>
    public EntityUid Target { get; }

    /// <summary>
    /// Profile used during the evaluation.
    /// </summary>
    public ProtoId<ThreatProfilePrototype> ProfileId { get; }

    /// <summary>
    /// Final aggregated score.
    /// </summary>
    public float Score { get; }

    /// <summary>
    /// Individual contributions that make up the final score.
    /// </summary>
    public IReadOnlyList<ThreatContribution> Contributions { get; }

    internal ThreatReport(EntityUid appraiser, EntityUid target, ProtoId<ThreatProfilePrototype> profileId, float score, List<ThreatContribution> contributions)
    {
        Appraiser = appraiser;
        Target = target;
        ProfileId = profileId;
        Score = score;
        Contributions = contributions;
    }
}

/// <summary>
/// Helper used while computing a threat score. Systems may add additional contributions through this builder.
/// </summary>
public sealed class ThreatReportBuilder
{
    private const float Epsilon = 0.0001f;
    private readonly List<ThreatContribution> _contributions = new();

    public EntityUid Appraiser { get; }
    public EntityUid Target { get; }
    public ProtoId<ThreatProfilePrototype> ProfileId { get; }

    public float Score { get; private set; }

    public ThreatReportBuilder(EntityUid appraiser, EntityUid target, ProtoId<ThreatProfilePrototype> profileId)
    {
        Appraiser = appraiser;
        Target = target;
        ProfileId = profileId;
    }

    /// <summary>
    /// Adds a contribution to the current score. Zero valued contributions are ignored.
    /// </summary>
    public bool TryAdd(string reason, float value, EntityUid? source = null, ThreatSourceContext context = ThreatSourceContext.None, string? detail = null)
    {
        if (MathF.Abs(value) <= Epsilon)
            return false;

        Score += value;
        _contributions.Add(new ThreatContribution(reason, value, source, context, detail));
        return true;
    }

    public void Add(string reason, float value, EntityUid? source = null, ThreatSourceContext context = ThreatSourceContext.None, string? detail = null)
    {
        TryAdd(reason, value, source, context, detail);
    }

    public IReadOnlyList<ThreatContribution> Contributions => _contributions;

    public ThreatReport ToReport()
    {
        return new ThreatReport(Appraiser, Target, ProfileId, Score, new List<ThreatContribution>(_contributions));
    }
}
