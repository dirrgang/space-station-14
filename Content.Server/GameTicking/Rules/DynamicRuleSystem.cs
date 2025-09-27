using System.Collections.Generic;
using System.Diagnostics;
using Content.Server.Administration.Logs;
using Content.Server.RoundEnd;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed class DynamicRuleSystem : GameRuleSystem<DynamicRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DynamicDifficultySystem _difficulty = default!;

    protected override void Added(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.Budget = _random.Next(component.StartingBudgetMin, component.StartingBudgetMax);
        component.NextRuleTime = Timing.CurTime + _random.Next(component.MinRuleInterval, component.MaxRuleInterval);
        _difficulty.OnRuleAdded(uid);
    }

    protected override void Started(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.LastBudgetUpdate = Timing.CurTime;
        Execute((uid, component));
    }

    protected override void Ended(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var rule in component.Rules)
        {
            GameTicker.EndGameRule(rule);
        }

        _difficulty.OnRuleEnded(uid, component);
    }

    protected override void ActiveTick(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < component.NextRuleTime)
            return;

        if (_roundEnd.IsRoundEndRequested())
            return;

        Execute((uid, component));
    }

    private IEnumerable<EntProtoId> GetRuleSpawns(Entity<DynamicRuleComponent> entity)
    {
        _difficulty.ApplyBudgetGain(entity);
        var ctx = new EntityTableContext(new Dictionary<string, object>
        {
            { HasBudgetCondition.BudgetContextKey, entity.Comp.Budget },
        });

        return _entityTable.GetSpawns(entity.Comp.Table, ctx: ctx);
    }

    private void UpdateBudget(Entity<DynamicRuleComponent> entity)
    {
        _difficulty.ApplyBudgetGain(entity);
    }

    private List<EntityUid> Execute(Entity<DynamicRuleComponent> entity)
    {
        entity.Comp.NextRuleTime =
            Timing.CurTime + _random.Next(entity.Comp.MinRuleInterval, entity.Comp.MaxRuleInterval);

        var executedRules = new List<EntityUid>();

        foreach (var rule in GetRuleSpawns(entity))
        {
            var res = GameTicker.StartGameRule(rule, out var ruleUid);
            Debug.Assert(res);

            executedRules.Add(ruleUid);

            if (TryComp<DynamicRuleCostComponent>(ruleUid, out var cost))
            {
                entity.Comp.Budget -= cost.Cost;
                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(entity)} ran rule {ToPrettyString(ruleUid)} with cost {cost.Cost} on budget {entity.Comp.Budget}.");
            }
            else
            {
                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(entity)} ran rule {ToPrettyString(ruleUid)} which had no cost.");
            }
        }

        entity.Comp.Rules.AddRange(executedRules);
        return executedRules;
    }

    #region Command Methods

    public List<EntityUid> GetDynamicRules()
    {
        var rules = new List<EntityUid>();
        var query = EntityQueryEnumerator<DynamicRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            if (!GameTicker.IsGameRuleActive(uid, comp))
                continue;
            rules.Add(uid);
        }

        return rules;
    }

    public float? GetRuleBudget(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        UpdateBudget((entity.Owner, entity.Comp));
        return entity.Comp.Budget;
    }

    public float? AdjustBudget(Entity<DynamicRuleComponent?> entity, float amount)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        UpdateBudget((entity.Owner, entity.Comp));
        entity.Comp.Budget += amount;
        return entity.Comp.Budget;
    }

    public float? SetBudget(Entity<DynamicRuleComponent?> entity, float amount)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        entity.Comp.LastBudgetUpdate = Timing.CurTime;
        entity.Comp.Budget = amount;
        return entity.Comp.Budget;
    }

    public IEnumerable<EntProtoId> DryRun(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntProtoId>();

        return GetRuleSpawns((entity.Owner, entity.Comp));
    }

    public IEnumerable<EntityUid> ExecuteNow(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntityUid>();

        return Execute((entity.Owner, entity.Comp));
    }

    public IEnumerable<EntityUid> Rules(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntityUid>();

        return entity.Comp.Rules;
    }

    #endregion
}

