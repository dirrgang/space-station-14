using Content.Shared.UserInterface;
using Robust.Shared.Serialization;

namespace Content.Shared.PowerTransmissionLaser;

/// <summary>
///     State information pushed to the power transmission laser UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserBuiState : BoundUserInterfaceState
{
    public readonly bool Enabled;
    public readonly bool ApcPowered;
    public readonly bool Anchored;
    public readonly bool LaserActive;
    public readonly float PricePerMegawatt;
    public readonly float MinPrice;
    public readonly float MaxPrice;
    public readonly float StepSmall;
    public readonly float StepLarge;
    public readonly double LastPowerMegawatts;
    public readonly double TotalEnergyMegajoules;
    public readonly double TotalRevenue;
    public readonly double PendingCredits;
    public readonly int PendingWholeCredits;
    public readonly int LastPayout;
    public readonly float MaxPowerDrawMegawatts;
    public readonly bool BankAvailable;

    public PowerTransmissionLaserBuiState(
        bool enabled,
        bool apcPowered,
        bool anchored,
        bool laserActive,
        float pricePerMegawatt,
        float minPrice,
        float maxPrice,
        float stepSmall,
        float stepLarge,
        double lastPowerMegawatts,
        double totalEnergyMegajoules,
        double totalRevenue,
        double pendingCredits,
        int pendingWholeCredits,
        int lastPayout,
        float maxPowerDrawMegawatts,
        bool bankAvailable)
    {
        Enabled = enabled;
        ApcPowered = apcPowered;
        Anchored = anchored;
        LaserActive = laserActive;
        PricePerMegawatt = pricePerMegawatt;
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        StepSmall = stepSmall;
        StepLarge = stepLarge;
        LastPowerMegawatts = lastPowerMegawatts;
        TotalEnergyMegajoules = totalEnergyMegajoules;
        TotalRevenue = totalRevenue;
        PendingCredits = pendingCredits;
        PendingWholeCredits = pendingWholeCredits;
        LastPayout = lastPayout;
        MaxPowerDrawMegawatts = maxPowerDrawMegawatts;
        BankAvailable = bankAvailable;
    }
}

/// <summary>
///     Requests that the laser be enabled or disabled.
/// </summary>
[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserSetEnabledMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public PowerTransmissionLaserSetEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

/// <summary>
///     Sets the price per exported megawatt.
/// </summary>
[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserSetPriceMessage : BoundUserInterfaceMessage
{
    public float Price { get; }

    public PowerTransmissionLaserSetPriceMessage(float price)
    {
        Price = price;
    }
}

/// <summary>
///     Adjusts the price by a relative delta.
/// </summary>
[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserAdjustPriceMessage : BoundUserInterfaceMessage
{
    public float Delta { get; }

    public PowerTransmissionLaserAdjustPriceMessage(float delta)
    {
        Delta = delta;
    }
}
