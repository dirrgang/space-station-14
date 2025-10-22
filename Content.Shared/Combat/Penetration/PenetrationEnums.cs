using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Combat.Penetration;

/// <summary>
/// Defines the coverage regions a penetration-capable armor layer can protect.
/// </summary>
[Serializable, NetSerializable]
public enum PenetrationArmorRegion : byte
{
    Head,
    Body,
    TorsoFront,
    TorsoBack,
}

/// <summary>
/// High-level ordering for penetration armor layers so entry / exit stacks can be assembled deterministically.
/// </summary>
[Serializable, NetSerializable]
public enum PenetrationArmorLayerOrder : byte
{
    Shield = 0,
    HardsuitOuter = 10,
    Helmet = 20,
    SuitOuter = 30,
    SuitInner = 40,
    SpeciesSurface = 50,
    SpeciesSubdermal = 60,
}
