namespace Content.Shared.Combat.Penetration;

/// <summary>
/// Scales stamina gain created by behind-armor blunt trauma (BABT) for rigid armor or exosuits.
/// </summary>
[RegisterComponent]
public sealed partial class KineticDissipationComponent : Component
{
    /// <summary>
    /// Multiplier applied to stamina generated from blunt damage caused by deflections.
    /// </summary>
    [DataField("dissipationFactor")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float DissipationFactor = 1f;
}
