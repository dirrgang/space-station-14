using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Restrict;

[RegisterComponent, NetworkedComponent]
public sealed partial class RestrictNestingCarrierComponent : Component
{
    /// <summary>
    ///     Optional override for how much weight this entity can carry relative to its own mass.
    ///     If absent or null, the species' <see cref="SpeciesPrototype.CarryWeightMultiplier"/> (or the default) is used.
    ///     Only entities that need a custom multiplier require this component.
    /// </summary>
    [DataField("carryWeightMultiplier")]
    public float? CarryWeightMultiplier;
}