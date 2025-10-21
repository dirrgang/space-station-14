using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking.Rules;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Round)]
public sealed class DynamicRuleCommand : ToolshedCommand
{
    private DynamicRuleSystem? _dynamicRuleSystem;
    private DynamicDifficultySystem? _difficultySystem;
    private readonly ISawmill _sawmill = Logger.GetSawmill("dynamicrule.cmd");

    [CommandImplementation("list")]
    public IEnumerable<EntityUid> List()
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.GetDynamicRules();
    }

    [CommandImplementation("get")]
    public EntityUid Get([CommandInvocationContext] IInvocationContext ctx)
    {
        return TryResolveRule(ctx, null, out var rule) ? rule : EntityUid.Invalid;
    }

    [CommandImplementation("budget")]
    public IEnumerable<float?> Budget([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return GetBudget(rule);
        }
    }

    [CommandImplementation("budget")]
    public float? Budget([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? GetBudget(rule) : null;
    }

    [CommandImplementation("adjust")]
    public IEnumerable<float?> Adjust([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input, float value)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return AdjustBudget(rule, value);
        }
    }

    [CommandImplementation("adjust")]
    public float? Adjust([CommandInvocationContext] IInvocationContext ctx, float value, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? AdjustBudget(rule, value) : null;
    }

    [CommandImplementation("set")]
    public IEnumerable<float?> Set([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input, float value)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return SetBudget(rule, value);
        }
    }

    [CommandImplementation("set")]
    public float? Set([CommandInvocationContext] IInvocationContext ctx, float value, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? SetBudget(rule, value) : null;
    }

    [CommandImplementation("dryrun")]
    public IEnumerable<IEnumerable<EntProtoId>> DryRun([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return DryRunInternal(rule);
        }
    }

    [CommandImplementation("dryrun")]
    public IEnumerable<EntProtoId> DryRun([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? DryRunInternal(rule) : Array.Empty<EntProtoId>();
    }

    [CommandImplementation("executenow")]
    public IEnumerable<IEnumerable<EntityUid>> ExecuteNow([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return ExecuteNowInternal(rule);
        }
    }

    [CommandImplementation("executenow")]
    public IEnumerable<EntityUid> ExecuteNow([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? ExecuteNowInternal(rule) : Array.Empty<EntityUid>();
    }

    [CommandImplementation("rules")]
    public IEnumerable<IEnumerable<EntityUid>> Rules([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return GetRules(rule);
        }
    }

    [CommandImplementation("rules")]
    public IEnumerable<EntityUid> Rules([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? GetRules(rule) : Array.Empty<EntityUid>();
    }

    [CommandImplementation("score")]
    public IEnumerable<string> Score([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            yield return GetScore(rule);
        }
    }

    [CommandImplementation("score")]
    public string Score([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? GetScore(rule) : "no dynamic rule";
    }

    [CommandImplementation("biasadjust")]
    public IEnumerable<float> BiasAdjust([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input, float delta)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            var value = AdjustBias(rule, delta);
            if (value != null)
                yield return value.Value;
        }
    }

    [CommandImplementation("biasadjust")]
    public float? BiasAdjust([CommandInvocationContext] IInvocationContext ctx, float delta, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? AdjustBias(rule, delta) : null;
    }

    [CommandImplementation("biasset")]
    public IEnumerable<float> BiasSet([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input, float value)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            var result = SetBias(rule, value);
            if (result != null)
                yield return result.Value;
        }
    }

    [CommandImplementation("biasset")]
    public float? BiasSet([CommandInvocationContext] IInvocationContext ctx, float value, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? SetBias(rule, value) : null;
    }

    [CommandImplementation("threats")]
    public IEnumerable<string> Threats([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            foreach (var line in GetThreatLines(rule))
                yield return line;
        }
    }

    [CommandImplementation("threats")]
    public IEnumerable<string> Threats([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        return TryResolveRule(ctx, target, out var rule) ? GetThreatLines(rule) : Array.Empty<string>();
    }

    [CommandImplementation("threatset")]
    public void ThreatSet(
        [CommandInvocationContext] IInvocationContext ctx,
        int count,
        float costPerThreat = 0f,
        string labelPrefix = "debug",
        EntityUid? target = null)
    {
        if (!TryResolveRule(ctx, target, out var rule))
            return;

        ApplyThreatSet(ctx, rule, count, costPerThreat, labelPrefix);
    }

    [CommandImplementation("threatset")]
    public void ThreatSet([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input, int count, float costPerThreat = 0f, string labelPrefix = "debug")
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            ApplyThreatSet(ctx, rule, count, costPerThreat, labelPrefix);
        }
    }

    [CommandImplementation("threatclear")]
    public void ThreatClear([CommandInvocationContext] IInvocationContext ctx, EntityUid? target = null)
    {
        if (!TryResolveRule(ctx, target, out var rule))
            return;

        ApplyThreatSet(ctx, rule, 0, 0f, "debug");
    }

    [CommandImplementation("threatclear")]
    public void ThreatClear([CommandInvocationContext] IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> input)
    {
        foreach (var candidate in input)
        {
            if (!TryResolveRule(ctx, candidate, out var rule))
                continue;

            ApplyThreatSet(ctx, rule, 0, 0f, "debug");
        }
    }

    private float? GetBudget(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.GetRuleBudget(rule);
    }

    private float? AdjustBudget(EntityUid rule, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.AdjustBudget(rule, value);
    }

    private float? SetBudget(EntityUid rule, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.SetBudget(rule, value);
    }

    private IEnumerable<EntProtoId> DryRunInternal(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.DryRun(rule);
    }

    private IEnumerable<EntityUid> ExecuteNowInternal(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.ExecuteNow(rule);
    }

    private IEnumerable<EntityUid> GetRules(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.Rules(rule);
    }

    private string GetScore(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        var comp = EntityManager.GetComponentOrNull<DynamicRuleComponent>(rule);
        if (comp == null)
        {
            _sawmill.Warning($"dynamicrule score: entity {rule} is missing DynamicRuleComponent.");
            return "no dynamic rule";
        }

        _dynamicRuleSystem.GetRuleBudget(rule);
        var snapshot = _difficultySystem.GetSnapshot(rule, comp);
        if (snapshot is null)
            return $"budget={comp.Budget:0.##} (difficulty disabled)";

        var value = snapshot.Value;
        var threats = value.Threats.Count == 0
            ? "none"
            : string.Join(", ", value.Threats.Select(t => $"{t.Label}:{t.Cost:0.##}{(t.IsDebug ? "*" : string.Empty)}"));

        return $"enabled={value.Enabled} budget={value.Budget:0.##} mult={value.Multiplier:0.##} bias={value.ManualBias:0.##} players={value.ActivePlayers} sec={value.AliveSecurity} threats=[{threats}]";
    }

    private float? AdjustBias(EntityUid rule, float delta)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        if (!EntityManager.HasComponent<DynamicRuleComponent>(rule))
        {
            _sawmill.Warning($"dynamicrule biasadjust: entity {rule} is missing DynamicRuleComponent.");
            return null;
        }

        _dynamicRuleSystem.GetRuleBudget(rule);
        return _difficultySystem.AdjustBias(rule, delta);
    }

    private float? SetBias(EntityUid rule, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        if (!EntityManager.HasComponent<DynamicRuleComponent>(rule))
        {
            _sawmill.Warning($"dynamicrule biasset: entity {rule} is missing DynamicRuleComponent.");
            return null;
        }

        _dynamicRuleSystem.GetRuleBudget(rule);
        return _difficultySystem.SetBias(rule, value);
    }

    private IEnumerable<string> GetThreatLines(EntityUid rule)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        _dynamicRuleSystem.GetRuleBudget(rule);
        var comp = EntityManager.GetComponentOrNull<DynamicRuleComponent>(rule);
        if (comp == null)
            yield break;

        var snapshot = _difficultySystem.GetSnapshot(rule, comp);
        if (snapshot is null)
            yield break;

        if (snapshot.Value.Threats.Count == 0)
        {
            yield return "no active threats";
            yield break;
        }

        foreach (var threat in snapshot.Value.Threats.OrderBy(t => t.Label))
        {
            var suffix = threat.IsDebug ? " (debug)" : string.Empty;
            yield return $"{threat.Label}: cost={threat.Cost:0.##}{suffix}";
        }
    }

    private void ApplyThreatSet(IInvocationContext ctx, EntityUid rule, int count, float costPerThreat, string labelPrefix)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        _dynamicRuleSystem.GetRuleBudget(rule);
        var result = _difficultySystem.SetDebugThreats(rule, count, costPerThreat, labelPrefix);
        ctx.WriteLine($"debug threats: +{result.Added}/-{result.Removed}, reserved={result.Reserved:0.##}, refunded={result.Refunded:0.##}, budget={result.Budget:0.##}");
    }

    private bool TryResolveRule(IInvocationContext ctx, EntityUid? candidate, out EntityUid rule)
    {
        rule = default;
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();

        if (candidate != null)
        {
            if (!EntityManager.EntityExists(candidate.Value))
            {
                ReportError(ctx, $"Entity {candidate.Value} does not exist.");
                return false;
            }

            if (!EntityManager.HasComponent<DynamicRuleComponent>(candidate.Value))
            {
                ReportError(ctx, $"{EntityManager.ToPrettyString(candidate.Value)} is not a dynamic rule.");
                return false;
            }

            var active = _dynamicRuleSystem.GetDynamicRules();
            if (!active.Contains(candidate.Value))
            {
                ReportError(ctx, $"{EntityManager.ToPrettyString(candidate.Value)} is not currently active.");
                return false;
            }

            rule = candidate.Value;
            return true;
        }

        var rules = _dynamicRuleSystem.GetDynamicRules();
        if (rules.Count == 0)
        {
            ReportError(ctx, "No dynamic rule is currently running.");
            return false;
        }

        if (rules.Count > 1)
        {
            var hint = string.Join(", ", rules.Select(uid => EntityManager.ToPrettyString(uid)));
            ReportError(ctx, $"Multiple dynamic rules are active ({hint}). Specify a target rule.");
            return false;
        }

        rule = rules[0];
        return true;
    }

    private void ReportError(IInvocationContext ctx, string message)
    {
        ctx.ReportError(new CommandMessageError(message));
    }

    private sealed class CommandMessageError : ConError
    {
        private readonly string _message;

        public CommandMessageError(string message)
        {
            _message = message;
        }

        public override FormattedMessage DescribeInner()
        {
            return FormattedMessage.FromUnformatted(_message);
        }
    }
}
