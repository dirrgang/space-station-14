using System;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Examine;
using Content.Shared.Power;
using Content.Shared.PowerTransmissionLaser;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server.PowerTransmissionLaser;

/// <summary>
///     Handles converting exported power into station funds for <see cref="PowerTransmissionLaserComponent"/>.
/// </summary>
public sealed class PowerTransmissionLaserSystem : EntitySystem
{
    private const double JoulesPerMegawattSecond = 1_000_000d;

    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Run after the power net has updated receiving power values.
        UpdatesAfter.Add(typeof(PowerNetSystem));

        SubscribeLocalEvent<PowerTransmissionLaserComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PowerTransmissionLaserComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<PowerTransmissionLaserComponent, ExaminedEvent>(OnExamined);

        Subs.BuiEvents<PowerTransmissionLaserComponent>(
            PowerTransmissionLaserUiKey.Key,
            subs =>
            {
                subs.Event<PowerTransmissionLaserSetEnabledMessage>(HandleSetEnabled);
                subs.Event<PowerTransmissionLaserSetPriceMessage>(HandleSetPrice);
                subs.Event<PowerTransmissionLaserAdjustPriceMessage>(HandleAdjustPrice);
                subs.Event<BoundUIClosedEvent>(HandleUiClosed);
            });
    }

    private void OnInit(EntityUid uid, PowerTransmissionLaserComponent component, ComponentInit args)
    {
        component.UiAccumulator = component.UiUpdateInterval;
        component.UiDirty = true;
    }

    private void OnPowerChanged(EntityUid uid, PowerTransmissionLaserComponent component, ref PowerChangedEvent args)
    {
        component.ApcPowered = args.Powered;
        component.UiDirty = true;
        TryUpdateAppearance(uid, component);
    }

    private void OnExamined(EntityUid uid, PowerTransmissionLaserComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var state = component.ApcPowered
            ? component.Enabled
                ? component.LaserActive
                    ? "power-transmission-laser-examine-state-active"
                    : "power-transmission-laser-examine-state-idle"
                : "power-transmission-laser-examine-state-disabled"
            : "power-transmission-laser-examine-state-unpowered";

        args.PushMarkup(Loc.GetString(
            "power-transmission-laser-examine-price",
            ("price", component.PricePerMegawatt)));
        args.PushMarkup(Loc.GetString(
            "power-transmission-laser-examine-status",
            ("state", Loc.GetString(state))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerTransmissionLaserComponent, PowerConsumerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var component, out var consumer, out var xform))
        {
            var anchored = xform.Anchored;

            // Always allow pending credits to flush if a bank becomes available.
            var deposited = TryDeposit(uid, component);

            if (!anchored || !component.Enabled)
            {
                consumer.DrawRate = 0f;
                component.LastPower = 0f;
                component.LaserActive = false;

                if (deposited)
                    component.UiDirty = true;

                TryUpdateAppearance(uid, component);
                TryUpdateUi(uid, component, anchored, frameTime, force: deposited);
                continue;
            }

            if (!component.ApcPowered)
            {
                consumer.DrawRate = 0f;
                component.LastPower = 0f;
                component.LaserActive = false;

                if (deposited)
                    component.UiDirty = true;

                TryUpdateAppearance(uid, component);
                TryUpdateUi(uid, component, anchored, frameTime, force: deposited);
                continue;
            }

            consumer.DrawRate = component.MaxPowerDraw;
            component.LastPower = consumer.ReceivedPower;
            component.LaserActive = component.LastPower > 1f;

            if (component.LastPower > 0f)
            {
                var energy = component.LastPower * frameTime;
                component.TotalEnergy += energy;
                component.PendingFractionalCredits += energy / JoulesPerMegawattSecond * component.PricePerMegawatt;

                var whole = (int) Math.Floor(component.PendingFractionalCredits);
                if (whole > 0)
                {
                    component.PendingFractionalCredits -= whole;
                    component.PendingWholeCredits += whole;
                }
            }

            deposited |= TryDeposit(uid, component);

            if (component.PendingWholeCredits > 0)
            {
                // If we could not deposit, notify the UI so players understand the hold-up.
                component.UiDirty = true;
            }

            if (component.LaserActive)
                component.UiDirty = true;

            TryUpdateAppearance(uid, component);
            TryUpdateUi(uid, component, anchored, frameTime, force: deposited);
        }
    }

    private void HandleSetEnabled(Entity<PowerTransmissionLaserComponent> ent, ref PowerTransmissionLaserSetEnabledMessage args)
    {
        var component = ent.Comp;

        if (component.Enabled == args.Enabled)
            return;

        component.Enabled = args.Enabled;
        component.UiDirty = true;
        TryUpdateAppearance(ent.Owner, component);
        Dirty(ent.Owner, component);

        var popup = Loc.GetString(args.Enabled
            ? "power-transmission-laser-popup-enabled"
            : "power-transmission-laser-popup-disabled");
        _popup.PopupEntity(popup, ent.Owner, args.Actor);
    }

    private void HandleSetPrice(Entity<PowerTransmissionLaserComponent> ent, ref PowerTransmissionLaserSetPriceMessage args)
    {
        SetPrice(ent.Owner, ent.Comp, args.Price);
    }

    private void HandleAdjustPrice(Entity<PowerTransmissionLaserComponent> ent, ref PowerTransmissionLaserAdjustPriceMessage args)
    {
        SetPrice(ent.Owner, ent.Comp, ent.Comp.PricePerMegawatt + args.Delta);
    }

    private void HandleUiClosed(Entity<PowerTransmissionLaserComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.UiAccumulator = ent.Comp.UiUpdateInterval;
    }

    private void SetPrice(EntityUid uid, PowerTransmissionLaserComponent component, float price)
    {
        var clamped = Math.Clamp(price, component.MinPrice, component.MaxPrice);
        if (MathHelper.CloseTo(component.PricePerMegawatt, clamped))
            return;

        component.PricePerMegawatt = clamped;
        component.UiDirty = true;
        Dirty(uid, component);
    }

    private bool TryDeposit(EntityUid uid, PowerTransmissionLaserComponent component)
    {
        if (component.PendingWholeCredits <= 0)
        {
            component.LastPayout = 0;
            return false;
        }

        if (_station.GetOwningStation(uid) is not { } station ||
            !TryComp(station, out StationBankAccountComponent? bank))
        {
            component.LastPayout = 0;
            return false;
        }

        _cargo.UpdateBankAccount((station, bank), component.PendingWholeCredits, bank.RevenueDistribution);
        component.TotalRevenue += component.PendingWholeCredits;
        component.LastPayout = component.PendingWholeCredits;
        component.PendingWholeCredits = 0;
        component.UiDirty = true;
        return true;
    }

    private void TryUpdateUi(
        EntityUid uid,
        PowerTransmissionLaserComponent component,
        bool anchored,
        float frameTime,
        bool force = false)
    {
        component.UiAccumulator += frameTime;

        if (!force && !component.UiDirty && component.UiAccumulator < component.UiUpdateInterval)
            return;

        if (!_uiSystem.IsUiOpen(uid, PowerTransmissionLaserUiKey.Key))
            return;

        component.UiAccumulator = 0f;
        component.UiDirty = false;

        var bankAvailable = _station.GetOwningStation(uid) is { } station && HasComp<StationBankAccountComponent>(station);

        _uiSystem.SetUiState(
            uid,
            PowerTransmissionLaserUiKey.Key,
            new PowerTransmissionLaserBuiState(
                component.Enabled,
                component.ApcPowered,
                anchored,
                component.LaserActive,
                component.PricePerMegawatt,
                component.MinPrice,
                component.MaxPrice,
                component.StepSmall,
                component.StepLarge,
                component.LastPower / JoulesPerMegawattSecond,
                component.TotalEnergy / 1_000_000d,
                component.TotalRevenue,
                component.PendingWholeCredits + component.PendingFractionalCredits,
                component.PendingWholeCredits,
                component.LastPayout,
                component.MaxPowerDraw / 1_000_000f,
                bankAvailable));
    }

    private void TryUpdateAppearance(EntityUid uid, PowerTransmissionLaserComponent component)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        var state = component.ApcPowered
            ? component.Enabled
                ? component.LaserActive
                    ? PowerTransmissionLaserVisualState.Active
                    : PowerTransmissionLaserVisualState.Idle
                : PowerTransmissionLaserVisualState.Disabled
            : PowerTransmissionLaserVisualState.Unpowered;

        _appearance.SetData(uid, PowerDeviceVisuals.VisualState, state, appearance);
        _appearance.SetData(uid, PowerDeviceVisuals.Powered, component.LaserActive, appearance);
    }
}
