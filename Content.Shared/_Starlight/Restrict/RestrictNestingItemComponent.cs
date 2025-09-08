using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.Restrict;

[RegisterComponent, NetworkedComponent]
public sealed partial class RestrictNestingItemComponent : Component
{
    /// <summary>
    ///     How long it takes to pick up an entity with this component.
    /// </summary>
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(5.0);

    /// <summary>
    ///     Maximum fraction of the user's mass that this entity can be and still be
    ///     eligible to be picked up. For example, a value of <c>0.75f</c> means the
    ///     target must weigh less than or equal to 75% of the user's mass.
    /// </summary>
    [DataField]
    public float MaxMassRatio = 0.75f;
}
