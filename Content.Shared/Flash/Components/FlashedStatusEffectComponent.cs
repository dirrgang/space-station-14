using System;
using Content.Shared.Flash;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Flash.Components;

/// <summary>
///     Attached to the status effect entity that applies <see cref="FlashedComponent"/> to targets.
///     Tracks the start time and last known end time so clients can render the flash overlay consistently.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause, Access(typeof(SharedFlashSystem))]
public sealed partial class FlashedStatusEffectComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? LastEndTime;
}
