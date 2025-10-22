namespace Content.Shared.Combat.Penetration;

/// <summary>
/// Provides species-level core armor values that every entity has regardless of worn items.
/// These values gate through-and-through outcomes during the penetration pre-pass.
/// </summary>
[RegisterComponent]
public sealed partial class SpeciesCoreArmorComponent : Component
{
    [DataField("coreHeadArMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float CoreHeadArmorMm = 0f;

    [DataField("coreBodyArMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float CoreBodyArmorMm = 0f;
}
