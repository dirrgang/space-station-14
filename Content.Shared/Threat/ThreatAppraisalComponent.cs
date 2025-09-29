using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Threat;

/// <summary>
/// Attached to entities that can appraise targets for their perceived threat.
/// Stores which <see cref=\"ThreatProfilePrototype\"/> should be applied.
/// </summary>
[RegisterComponent]
public sealed partial class ThreatAppraisalComponent : Component
{
    /// <summary>
    /// Profile that describes how this entity evaluates threats.
    /// </summary>
    [DataField("profile", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<ThreatProfilePrototype>))]
    public string ProfileId = "ThreatProfileSecurity";

    /// <summary>
    /// Optional cooldown in seconds between consecutive threat evaluations.
    /// Systems may enforce this to avoid excessive recomputation.
    /// </summary>
    [DataField("cooldown")]
    public TimeSpan Cooldown;

    /// <summary>
    /// Stores when the last evaluation occurred, allowing systems to handle throttling if required.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastEvaluatedAt;

    /// <summary>
    /// Holds the most recent computed report when diagnostics are enabled.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ThreatReport? LastReport;

    /// <summary>
    /// Strongly typed view of <see cref="ProfileId"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<ThreatProfilePrototype> Profile
    {
        get => ProfileId;
        set => ProfileId = value;
    }
}
