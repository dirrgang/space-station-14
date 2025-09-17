namespace Content.Server._CD.Records;

/// <summary>
/// Applied to mobs that should not receive Cosmatic Drift records on spawn.
/// </summary>
[RegisterComponent]
public sealed partial class SkipLoadingCharacterRecordsComponent : Component;
