using Content.Shared.UserInterface;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.PowerTransmissionLaser;

/// <summary>
///     Tracks the configuration and accounting data for a power transmission laser.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PowerTransmissionLaserComponent : Component
{
    /// <summary>
    ///     Whether the laser should attempt to draw power from the grid.
    /// </summary>
    [DataField("enabled"), AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    ///     Maximum desired draw in watts while the laser is enabled.
    /// </summary>
    [DataField("maxPowerDraw")]
    public float MaxPowerDraw = 5_000_000f;

    /// <summary>
    ///     Minimum allowed price when configuring the unit.
    /// </summary>
    [DataField("minPrice")]
    public float MinPrice = 0f;

    /// <summary>
    ///     Maximum allowed price when configuring the unit.
    /// </summary>
    [DataField("maxPrice")]
    public float MaxPrice = 100f;

    /// <summary>
    ///     Price paid in credits for every megawatt of power exported per second.
    /// </summary>
    [DataField("pricePerMegawatt"), AutoNetworkedField]
    public float PricePerMegawatt = 5f;

    /// <summary>
    ///     Increment used by the small adjustment buttons in the UI.
    /// </summary>
    [DataField("stepSmall")]
    public float StepSmall = 1f;

    /// <summary>
    ///     Increment used by the large adjustment buttons in the UI.
    /// </summary>
    [DataField("stepLarge")]
    public float StepLarge = 5f;

    /// <summary>
    ///     Minimum interval between automatic UI refreshes while the window is open.
    /// </summary>
    [DataField("uiUpdateInterval")]
    public float UiUpdateInterval = 1f;

    /// <summary>
    ///     Tracks if the APC supplying the control electronics currently has power.
    /// </summary>
    [ViewVariables]
    public bool ApcPowered = true;

    /// <summary>
    ///     True when the laser successfully consumed power during the last update tick.
    /// </summary>
    [ViewVariables]
    public bool LaserActive;

    /// <summary>
    ///     Power draw reported by <see cref="PowerConsumerComponent"/> during the last update tick.
    /// </summary>
    [ViewVariables]
    public float LastPower;

    /// <summary>
    ///     Credits deposited during the last successful payout.
    /// </summary>
    [ViewVariables]
    public int LastPayout;

    /// <summary>
    ///     Total joules exported since the component was created.
    /// </summary>
    [ViewVariables]
    public double TotalEnergy;

    /// <summary>
    ///     Total credits deposited into the station bank by this laser.
    /// </summary>
    [ViewVariables]
    public double TotalRevenue;

    /// <summary>
    ///     Whole credits waiting for the station account to become available.
    /// </summary>
    [ViewVariables]
    public int PendingWholeCredits;

    /// <summary>
    ///     Fractional credits that have not yet added up to a whole credit.
    /// </summary>
    [ViewVariables]
    public double PendingFractionalCredits;

    /// <summary>
    ///     Used to throttle automatic UI updates.
    /// </summary>
    [ViewVariables]
    public float UiAccumulator;

    /// <summary>
    ///     Forces the UI to update on the next tick, ignoring <see cref="UiAccumulator"/>.
    /// </summary>
    [ViewVariables]
    public bool UiDirty;
}

/// <summary>
///     UI key for the power transmission laser console.
/// </summary>
[Serializable, NetSerializable]
public enum PowerTransmissionLaserUiKey : byte
{
    Key
}

/// <summary>
///     Visual appearance states for the power transmission laser sprite layers.
/// </summary>
[Serializable, NetSerializable]
public enum PowerTransmissionLaserVisualState : byte
{
    Unpowered,
    Disabled,
    Idle,
    Active
}
