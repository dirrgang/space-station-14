// NOTE: This system runs on the server and piggybacks on DynamicRuleSystem updates.
using System;
using System.Collections.Generic;
using Content.Server.Antag.Components;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Server.Player;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Computes a dynamic difficulty score for the dynamic game rule budget so that threats can scale with crew strength.
/// </summary>
public sealed class DynamicDifficultySystem : EntitySystem
{
    private readonly ISawmill _sawmill = Logger.GetSawmill("dynamicdifficulty");

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeManager = default!;

    private bool _enabled;
    private bool _playerFactorEnabled;
    private bool _securityFactorEnabled;
    private bool _playtimeFactorEnabled;

    private float _baseRate;
    private float _minRate;
    private float _maxRate;
    private float _maxBudget;
    private float _playerWeight;
    private float _securityWeight;
    private float _playtimeScale;
    private float _costScale;

    // Departments flagged here count as defenders when the security factor is enabled.
    private readonly HashSet<string> _securityDepartments = new(StringComparer.OrdinalIgnoreCase);
    // Stores round-local state per dynamic rule entity (budget multiplier, reservations, etc.).
    private readonly Dictionary<EntityUid, DifficultyRuntime> _runtimes = new();

    public bool Enabled => _enabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.DynamicDifficultyEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyBaseRate, v => _baseRate = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyMinRate, v => _minRate = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyMaxRate, v => _maxRate = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyMaxBudget, v => _maxBudget = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyCostScale, v => _costScale = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyPlayerFactorEnabled, v => _playerFactorEnabled = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyPlayerWeight, v => _playerWeight = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultySecurityFactorEnabled, v => _securityFactorEnabled = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultySecurityWeight, v => _securityWeight = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultySecurityDepartments, OnSecurityDepartmentsChanged, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyPlaytimeFactorEnabled, v => _playtimeFactorEnabled = v, true);
        Subs.CVar(_cfg, CCVars.DynamicDifficultyPlaytimeScale, v => _playtimeScale = v, true);
    }

    // Called by DynamicRuleSystem when a dynamic preset is added so we can track it.
    public void OnRuleAdded(EntityUid uid)
    {
        GetRuntime(uid);
    }

    // Clean up bookkeeping and hand any reserved budget back once the rule ends.
    public void OnRuleEnded(EntityUid uid, DynamicRuleComponent component)
    {
        if (!_runtimes.TryGetValue(uid, out var runtime))
            return;

        if (runtime.Threats.Count > 0)
        {
            var refund = 0f;
            foreach (var entry in runtime.Threats.Values)
            {
                refund += entry.Cost;
            }

            component.Budget = MathF.Min(component.Budget + refund, _maxBudget);
        }

        _runtimes.Remove(uid);
    }

    // Applies budget accrual for a dynamic rule, optionally scaling it by live metrics.
    public void ApplyBudgetGain(Entity<DynamicRuleComponent> entity)
    {
        var delta = (float)(_timing.CurTime - entity.Comp.LastBudgetUpdate).TotalSeconds;
        if (delta <= 0f)
            return;

        entity.Comp.LastBudgetUpdate = _timing.CurTime;
        var runtime = GetRuntime(entity.Owner);

        // Fallback to vanilla behavior when the feature is disabled.
        // Reservations are ignored when difficulty control is disabled.
        if (!_enabled)
        {
            var gain = delta * entity.Comp.BudgetPerSecond;
            entity.Comp.Budget += gain;
            runtime.LastGain = gain;
            runtime.LastBudget = entity.Comp.Budget;
            return;
        }

        // Gather the current crew strength snapshot before computing the multiplier.
        var metrics = ComputeDefenseMetrics();
        runtime.Metrics = metrics;

        var multiplier = CalculateMultiplier(metrics, runtime.ManualBias);
        runtime.Multiplier = multiplier;

        var addition = delta * entity.Comp.BudgetPerSecond * multiplier;
        entity.Comp.Budget += addition;
        runtime.LastGain = addition;

        if (entity.Comp.Budget > _maxBudget)
            entity.Comp.Budget = _maxBudget;
        else if (entity.Comp.Budget < 0f)
            entity.Comp.Budget = 0f;

        runtime.LastBudget = entity.Comp.Budget;
        PruneThreats(entity, runtime);
    }

    // Called from AntagSelectionSystem to check whether we can afford another antagonist.
    public bool TryReserveBudget(Entity<AntagSelectionComponent> rule, AntagSelectionDefinition def, ICommonSession session, out float cost)
    {
        cost = 0f;

        if (!_enabled)
            return true;

        var userId = session.UserId;

        if (!TryGetController(rule.Owner, out var controller, out var dynamicRule))
        {
            _sawmill.Warning($"Received budget reservation request for {ToPrettyString(rule.Owner)} but no dynamic controller was found.");
            return true;
        }

        var dynamicEntity = (controller, dynamicRule);
        ApplyBudgetGain(dynamicEntity);
        var runtime = GetRuntime(controller);

        var activePlayers = runtime.Metrics.ActivePlayers;
        if (activePlayers <= 0)
            activePlayers = 1;

        cost = CalculateAntagCost(rule, def, session, activePlayers) * _costScale;

        if (dynamicRule.Budget < cost)
            return false;

        dynamicRule.Budget -= cost;
        runtime.LastBudget = dynamicRule.Budget;

        // Update or create the reservation for this user so we can refund it later.
        if (runtime.Threats.TryGetValue(userId, out var entry))
        {
            entry.Cost += cost;
            entry.ReservedAt = _timing.CurTime;
            entry.Label = GetThreatLabel(def, session);
            runtime.Threats[userId] = entry;
        }
        else
        {
            runtime.Threats[userId] = new ThreatEntry
            {
                UserId = userId,
                Cost = cost,
                ReservedAt = _timing.CurTime,
                Label = GetThreatLabel(def, session)
            };
        }

        return true;
    }

    // Once the mind exists, plug it into the reservation so we know when the antag actually leaves.
    public void BindAntagMind(EntityUid ruleUid, NetUserId userId, EntityUid mind)
    {
        if (!_enabled)
            return;

        if (!TryGetController(ruleUid, out var controller, out _))
            return;

        if (!_runtimes.TryGetValue(controller, out var runtime))
            return;

        if (!runtime.Threats.TryGetValue(userId, out var entry))
            return;

        entry.Mind = mind;
        runtime.Threats[userId] = entry;
    }

    // Used when antagonist setup fails so the reservation can be released.
    public void CancelReservation(EntityUid ruleUid, NetUserId userId)
    {
        if (!_enabled)
            return;

        if (!TryGetController(ruleUid, out var controller, out var dynamicRule))
        {
            _sawmill.Warning($"Attempted to cancel reservation on {ToPrettyString(ruleUid)} but no dynamic controller was found.");
            return;
        }

        if (!_runtimes.TryGetValue(controller, out var runtime))
        {
            _sawmill.Debug($"Attempted to cancel reservation for {ToPrettyString(controller)} but no runtime state exists.");
            return;
        }

        if (!runtime.Threats.TryGetValue(userId, out var entry))
        {
            _sawmill.Debug($"Attempted to cancel reservation for user {userId} on {ToPrettyString(controller)} but no reservation was recorded.");
            return;
        }

        runtime.Threats.Remove(userId);

        dynamicRule.Budget = MathF.Min(dynamicRule.Budget + entry.Cost, _maxBudget);
        runtime.LastBudget = dynamicRule.Budget;
        _sawmill.Info($"Refunded {entry.Cost:0.##} budget to {ToPrettyString(controller)} after failed antagonist setup for user {userId}.");
    }

    public DifficultySnapshot? GetSnapshot(EntityUid ruleUid, DynamicRuleComponent? component = null)
    {
        if (!TryGetController(ruleUid, out var controller, out var resolvedComponent))
            return null;

        if (!_runtimes.TryGetValue(controller, out var runtime))
            return null;

        if (component == null)
            component = resolvedComponent;

        var budget = component?.Budget ?? runtime.LastBudget;
        var threats = new List<ThreatSnapshot>(runtime.Threats.Count);
        foreach (var pair in runtime.Threats)
        {
            threats.Add(new ThreatSnapshot(pair.Value.Label, pair.Key, pair.Value.Cost));
        }

        return new DifficultySnapshot(
            _enabled,
            budget,
            runtime.Multiplier,
            runtime.ManualBias,
            runtime.Metrics.ActivePlayers,
            runtime.Metrics.AliveSecurity,
            runtime.Metrics.PlayerContribution,
            runtime.Metrics.SecurityContribution,
            threats);
    }

    public float AdjustBias(EntityUid ruleUid, float delta)
    {
        if (!TryGetController(ruleUid, out var controller, out _))
            return 0f;

        var runtime = GetRuntime(controller);
        runtime.ManualBias += delta;
        return runtime.ManualBias;
    }

    public float SetBias(EntityUid ruleUid, float value)
    {
        if (!TryGetController(ruleUid, out var controller, out _))
            return 0f;

        var runtime = GetRuntime(controller);
        runtime.ManualBias = value;
        return runtime.ManualBias;
    }

    private DifficultyRuntime GetRuntime(EntityUid uid)
    {
        if (!_runtimes.TryGetValue(uid, out var runtime))
        {
            runtime = new DifficultyRuntime();
            _runtimes[uid] = runtime;
        }

        return runtime;
    }

    // Refund budget for players who disconnected or lost their antagonist role.
    private void PruneThreats(Entity<DynamicRuleComponent> entity, DifficultyRuntime runtime)
    {
        if (runtime.Threats.Count == 0)
            return;

        var removals = new List<NetUserId>();

        foreach (var (user, entry) in runtime.Threats)
        {
            if (entry.Mind != null)
            {
                if (_roles.MindIsAntagonist(entry.Mind.Value))
                    continue;
            }
            else if (_playerManager.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame)
            {
                continue;
            }

            entity.Comp.Budget = MathF.Min(entity.Comp.Budget + entry.Cost, _maxBudget);
            runtime.LastBudget = entity.Comp.Budget;
            removals.Add(user);
        }

        foreach (var userId in removals)
        {
            runtime.Threats.Remove(userId);
        }
    }

    private DefenseMetrics ComputeDefenseMetrics()
    {
        var metrics = new DefenseMetrics();

        // Evaluate every connected player to get the current defensive weighting.
        foreach (var session in _playerManager.Sessions)
        {
            // Ignore players who are not currently active on-station.
            if (session.Status != SessionStatus.InGame)
                continue;

            // Skip observers or players who have not spawned in.
            if (session.AttachedEntity is not { } entity)
                continue;

            // Latejoin ghosts or critted bodies should not inflate the defender count.
            if (!IsEntityAlive(entity))
                continue;

            metrics.ActivePlayers++;

            var multiplier = 1f;
            // Scale contribution by playtime when requested.
            if (_playtimeFactorEnabled)
            {
                var span = _playTimeManager.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
                var hours = (float) span.TotalHours;
                if (hours > 0f)
                    multiplier += MathF.Log(1f + hours) * _playtimeScale;
            }

            // Aggregate the general player-contribution component.
            if (_playerFactorEnabled)
                metrics.PlayerContribution += _playerWeight * multiplier;

            // Security-aligned jobs get an additional configurable weighting.
            if (_securityFactorEnabled && TryGetSecurityContribution(session, multiplier, out var contribution))
            {
                metrics.SecurityContribution += contribution;
                metrics.AliveSecurity++;
            }
        }

        return metrics;
    }

    private bool TryGetSecurityContribution(ICommonSession session, float multiplier, out float contribution)
    {
        // Determine whether the session belongs to a security department.
        contribution = 0f;
        var mindId = session.GetMind();
        if (mindId == null)
            return false;

        if (!_jobs.MindTryGetJob(mindId.Value, out var job))
            return false;

        if (!_jobs.TryGetAllDepartments(job.ID, out var departments))
            return false;

        foreach (var department in departments)
        {
            if (_securityDepartments.Contains(department.ID))
            {
                contribution = _securityWeight * multiplier;
                return true;
            }
        }

        return false;
    }

    private static float CalculateMultiplier(in DefenseMetrics metrics, float manualBias, float baseRate, float minRate, float maxRate)
    {
        var result = baseRate + manualBias;
        result += metrics.PlayerContribution;
        result += metrics.SecurityContribution;
        return Math.Clamp(result, minRate, maxRate);
    }

    private float CalculateMultiplier(in DefenseMetrics metrics, float manualBias)
    {
        return CalculateMultiplier(metrics, manualBias, _baseRate, _minRate, _maxRate);
    }

    private static float EstimateAntagCount(AntagSelectionDefinition def, int activePlayers)
    {
        var min = MathF.Max(1f, def.Min);
        var max = def.Max <= 0 ? min : def.Max;
        float estimate = min;

        if (def.PlayerRatio > 0)
        {
            estimate = activePlayers / (float) def.PlayerRatio;
        }

        estimate = Math.Clamp(estimate, min, MathF.Max(min, max));
        return estimate;
    }

    private float CalculateAntagCost(Entity<AntagSelectionComponent> rule, AntagSelectionDefinition def, ICommonSession session, int activePlayers)
    {
        // Use the rule-level cost if present, otherwise fall back to definition heuristics.
        var baseCost = 0f;
        if (TryComp(rule.Owner, out DynamicRuleCostComponent? ruleCost))
            baseCost = ruleCost.Cost;

        if (baseCost <= 0f)
            baseCost = Math.Max(1, def.Min);

        var expected = EstimateAntagCount(def, activePlayers);
        if (expected <= 0f)
            expected = 1f;

        var perAntag = baseCost / expected;

        if (!_playtimeFactorEnabled)
            return perAntag;

        // Adjust cost for veteran antagonists when playtime scaling is enabled.
        var span = _playTimeManager.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
        var hours = (float) span.TotalHours;
        if (hours <= 0f)
            return perAntag;

        var multiplier = 1f + MathF.Log(1f + hours) * _playtimeScale;
        return perAntag * multiplier;
    }

    private bool IsEntityAlive(EntityUid entity)
    {
        if (!TryComp(entity, out MobStateComponent? mobState))
            return true;

        return mobState.CurrentState == MobState.Alive;
    }

    private void OnSecurityDepartmentsChanged(string value)
    {
        _securityDepartments.Clear();
        foreach (var part in value.Split(','))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                _securityDepartments.Add(trimmed);
        }

        // Always treat the core Security department as valid if the list is empty.
        if (_securityDepartments.Count == 0)
            _securityDepartments.Add("Security");
    }

    private bool TryGetController(EntityUid entity, out EntityUid controller, out DynamicRuleComponent component)
    {
        if (TryComp<DynamicRuleComponent>(entity, out var direct))
        {
            component = direct;
            controller = entity;
            return true;
        }

        if (!TryComp<DynamicDifficultyParticipantComponent>(entity, out var participant))
        {
            controller = default;
            component = default!;
            return false;
        }

        controller = participant.Controller;
        if (!TryComp<DynamicRuleComponent>(controller, out var owner))
        {
            component = default!;
            controller = default;
            return false;
        }

        component = owner;
        return true;
    }

    private sealed class DifficultyRuntime
    {
        public DefenseMetrics Metrics;
        public float Multiplier;
        public float ManualBias;
        public float LastGain;
        public float LastBudget;
        public readonly Dictionary<NetUserId, ThreatEntry> Threats = new();
    }

    private struct DefenseMetrics
    {
        public int ActivePlayers;
        public int AliveSecurity;
        public float PlayerContribution;
        public float SecurityContribution;
    }

    private sealed class ThreatEntry
    {
        public NetUserId UserId;
        public float Cost;
        public EntityUid? Mind;
        public string Label = string.Empty;
        public TimeSpan ReservedAt;
    }

    public readonly record struct DifficultySnapshot(
        bool Enabled,
        float Budget,
        float Multiplier,
        float ManualBias,
        int ActivePlayers,
        int AliveSecurity,
        float PlayerContribution,
        float SecurityContribution,
        IReadOnlyList<ThreatSnapshot> Threats);

    public readonly record struct ThreatSnapshot(string Label, NetUserId UserId, float Cost);

    private static string GetThreatLabel(AntagSelectionDefinition def, ICommonSession session)
    {
        if (def.MindRoles != null && def.MindRoles.Count > 0)
            return def.MindRoles[0];

        return session.Name;
    }
}




