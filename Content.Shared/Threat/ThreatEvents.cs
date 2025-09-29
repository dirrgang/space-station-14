namespace Content.Shared.Threat;

/// <summary>
/// Raised on a target entity before threat tokens are resolved, giving systems a chance
/// to contribute additional tokens or flat threat adjustments.
/// </summary>
[ByRefEvent]
public record struct CollectThreatTokensEvent(EntityUid Appraiser, ThreatProfilePrototype Profile, ThreatReportBuilder Builder, List<ThreatTokenEntry> Tokens)
{
    public EntityUid Appraiser = Appraiser;
    public ThreatProfilePrototype Profile = Profile;
    public ThreatReportBuilder Builder = Builder;
    public List<ThreatTokenEntry> Tokens = Tokens;
}

/// <summary>
/// Raised on a target entity after the core threat calculation has been performed,
/// allowing systems to tweak the report before it is finalized.
/// </summary>
[ByRefEvent]
public record struct ModifyThreatAssessmentEvent(EntityUid Appraiser, ThreatProfilePrototype Profile, ThreatReportBuilder Builder)
{
    public EntityUid Appraiser = Appraiser;
    public ThreatProfilePrototype Profile = Profile;
    public ThreatReportBuilder Builder = Builder;
}
