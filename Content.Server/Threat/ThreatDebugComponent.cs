using Content.Shared.Threat;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.ViewVariables;

namespace Content.Server.Threat;

/// <summary>
/// Debug helper that enables admin verbs for inspecting threat scores of an entity.
/// </summary>
[RegisterComponent]
public sealed partial class ThreatDebugComponent : Component
{
    /// <summary>
    /// Threat profile used when evaluating this entity.
    /// </summary>
    [DataField("profile", customTypeSerializer: typeof(PrototypeIdSerializer<ThreatProfilePrototype>))]
    public string ProfileId = "ThreatProfileSecurity";

    /// <summary>
    /// If true, uses the existing <see cref=\"ThreatAppraisalComponent\"/> on the appraiser when generating the report.
    /// </summary>
    [DataField("useAppraiserComponent")]
    public bool UseAppraiserComponent;

    /// <summary>
    /// Optional override for the entity to treat as the appraiser when producing a report.
    /// Defaults to the entity with this component.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? AppraiserOverride;

    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<ThreatProfilePrototype> Profile
    {
        get => ProfileId;
        set => ProfileId = value;
    }
}
