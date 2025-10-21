namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Attached to individual game rule entities spawned by the dynamic director so we can
/// trace them back to the controlling <see cref="DynamicRuleComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DynamicDifficultyParticipantComponent : Component
{
    /// <summary>
    /// Entity uid of the dynamic director that is managing this rule.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Controller;
}
