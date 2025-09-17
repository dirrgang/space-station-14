using System.Collections.Generic;
using Content.Shared._CD.Records;
using Robust.Shared.GameObjects;

namespace Content.Server._CD.Records;

/// <summary>
/// The component on the station that stores Cosmatic Drift character records in-round.
/// </summary>
[RegisterComponent]
[Access(typeof(CharacterRecordsSystem))]
public sealed partial class CharacterRecordsComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<uint, FullCharacterRecords> Records = new();

    [ViewVariables(VVAccess.ReadOnly)]
    private uint _nextKey = 1;

    /// <summary>
    /// Creates a key that has never been used previously.
    /// </summary>
    public uint CreateNewKey()
    {
        return _nextKey++;
    }
}

public sealed record CharacterRecordKey
{
    public EntityUid Station { get; init; }
    public uint Index { get; init; }
}
