using System.Collections.Generic;

namespace Content.Shared.Combat.Penetration;

/// <summary>
/// Declares CE-style armor resistance values for clothing, equipment, shields, or species layers.
/// </summary>
[RegisterComponent]
public sealed partial class ArmorRatingComponent : Component
{
    /// <summary>
    /// Resistance against penetrating sharp threats (mm RHA equivalent).
    /// </summary>
    [DataField("arSharpMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float SharpRatingMm = 0f;

    /// <summary>
    /// Resistance against crushing / blunt penetration (mm RHA equivalent).
    /// </summary>
    [DataField("arBluntMm")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BluntRatingMm = 0f;

    /// <summary>
    /// Areas this layer covers relative to the body hit resolver.
    /// </summary>
    [DataField("coverage")]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<PenetrationArmorRegion> Coverage = new();

    /// <summary>
    /// True if the projectile should lodge when this layer deflects sharp penetration.
    /// </summary>
    [DataField("stopsOnDeflect")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool StopsOnDeflect = true;

    /// <summary>
    /// Ordering hint so layers can be processed deterministically.
    /// </summary>
    [DataField("layerOrder")]
    [ViewVariables(VVAccess.ReadWrite)]
    public PenetrationArmorLayerOrder LayerOrder = PenetrationArmorLayerOrder.SuitOuter;

    /// <summary>
    /// Optional toggle to disable legacy armor coefficients when this component is present.
    /// </summary>
    [DataField("disableLegacyCoefficients")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool DisableLegacyCoefficients = true;
}
