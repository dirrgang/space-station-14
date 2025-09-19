using Content.Client.UserInterface;
using Content.Shared.PowerTransmissionLaser;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.PowerTransmissionLaser;

/// <summary>
///     Client-side entry point for the power transmission laser UI.
/// </summary>
[UsedImplicitly]
public sealed class PowerTransmissionLaserBoundUserInterface : BoundUserInterface
{
    private PowerTransmissionLaserWindow? _window;
    private bool _suppressPriceUpdate;

    public PowerTransmissionLaserBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PowerTransmissionLaserWindow>();
        _window.OnToggle += enabled => SendMessage(new PowerTransmissionLaserSetEnabledMessage(enabled));
        _window.OnSetPrice += price =>
        {
            if (_suppressPriceUpdate)
                return;
            SendMessage(new PowerTransmissionLaserSetPriceMessage(price));
        };
        _window.OnAdjustPrice += delta => SendMessage(new PowerTransmissionLaserAdjustPriceMessage(delta));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PowerTransmissionLaserBuiState laserState)
            return;

        if (_window == null)
            return;

        _suppressPriceUpdate = true;
        _window.Update(laserState);
        _suppressPriceUpdate = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}
