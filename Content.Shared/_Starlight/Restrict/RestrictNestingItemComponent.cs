using System;
using System.Collections.Generic;
using Content.Shared.Item;
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
    ///     Ordered mass thresholds that control which item size (and required
    ///     number of hands) are applied when the entity is picked up. The first
    ///     entry whose <see cref="RestrictNestingMassBand.MaxMass"/> is greater
    ///     than or equal to the entity's current mass is selected.
    /// </summary>
    [DataField]
    public List<RestrictNestingMassBand> MassThresholds = new()
    {
        new RestrictNestingMassBand
        {
            MaxMass = 40f,
            Size = "Large",
            HandsNeeded = 1,
        },
        new RestrictNestingMassBand
        {
            MaxMass = 65f,
            Size = "Huge",
            HandsNeeded = 2,
        },
    };

    /// <summary>
    ///     Fallback size that is applied when the entity's mass exceeds every
    ///     configured <see cref="MassThresholds"/> entry.
    /// </summary>
    [DataField]
    public ProtoId<ItemSizePrototype> FallbackSize = "Ginormous";

    /// <summary>
    ///     Number of hands required when the fallback size is used.
    /// </summary>
    [DataField]
    public int FallbackHandsNeeded = 2;
}

/// <summary>
///     Mass-based sizing band used by <see cref="RestrictNestingItemComponent"/>.
/// </summary>
[DataDefinition]
public sealed partial class RestrictNestingMassBand
{
    /// <summary>
    ///     Inclusive upper mass bound for this band.
    /// </summary>
    [DataField(required: true)]
    public float MaxMass;

    /// <summary>
    ///     Item size to apply when this band is selected.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ItemSizePrototype> Size = "Huge";

    /// <summary>
    ///     Number of hands needed to carry items sized by this band.
    /// </summary>
    [DataField]
    public int HandsNeeded = 2;
}