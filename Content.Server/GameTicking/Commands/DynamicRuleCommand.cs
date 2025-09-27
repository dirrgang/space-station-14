using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking.Rules;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server.GameTicking.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Round)]
public sealed class DynamicRuleCommand : ToolshedCommand
{
    private DynamicRuleSystem? _dynamicRuleSystem;
    private DynamicDifficultySystem? _difficultySystem;
    // Shared logger so admins get feedback if a command cannot target a dynamic rule.
    private readonly ISawmill _sawmill = Logger.GetSawmill("dynamicrule.cmd");

    [CommandImplementation("list")]
    public IEnumerable<EntityUid> List()
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.GetDynamicRules();
    }

    [CommandImplementation("get")]
    public EntityUid Get()
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.GetDynamicRules().FirstOrDefault();
    }

    [CommandImplementation("budget")]
    public IEnumerable<float?> Budget([PipedArgument] IEnumerable<EntityUid> input)
        => input.Select(Budget);

    [CommandImplementation("budget")]
    public float? Budget([PipedArgument] EntityUid input)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.GetRuleBudget(input);
    }

    [CommandImplementation("adjust")]
    public IEnumerable<float?> Adjust([PipedArgument] IEnumerable<EntityUid> input, float value)
        => input.Select(i => Adjust(i, value));

    [CommandImplementation("adjust")]
    public float? Adjust([PipedArgument] EntityUid input, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.AdjustBudget(input, value);
    }

    [CommandImplementation("set")]
    public IEnumerable<float?> Set([PipedArgument] IEnumerable<EntityUid> input, float value)
        => input.Select(i => Set(i, value));

    [CommandImplementation("set")]
    public float? Set([PipedArgument] EntityUid input, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.SetBudget(input, value);
    }

    [CommandImplementation("dryrun")]
    public IEnumerable<IEnumerable<EntProtoId>> DryRun([PipedArgument] IEnumerable<EntityUid> input)
        => input.Select(DryRun);

    [CommandImplementation("dryrun")]
    public IEnumerable<EntProtoId> DryRun([PipedArgument] EntityUid input)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.DryRun(input);
    }

    [CommandImplementation("executenow")]
    public IEnumerable<IEnumerable<EntityUid>> ExecuteNow([PipedArgument] IEnumerable<EntityUid> input)
        => input.Select(ExecuteNow);

    [CommandImplementation("executenow")]
    public IEnumerable<EntityUid> ExecuteNow([PipedArgument] EntityUid input)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.ExecuteNow(input);
    }

    [CommandImplementation("rules")]
    public IEnumerable<IEnumerable<EntityUid>> Rules([PipedArgument] IEnumerable<EntityUid> input)
        => input.Select(Rules);

    [CommandImplementation("rules")]
    public IEnumerable<EntityUid> Rules([PipedArgument] EntityUid input)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        return _dynamicRuleSystem.Rules(input);
    }

    // Provide a per-entity summary of the live difficulty state (budget, multiplier, reservations).
    [CommandImplementation("score")]
    public IEnumerable<string> Score([PipedArgument] IEnumerable<EntityUid> input)
        => input.Select(Score);

    [CommandImplementation("score")]
    public string Score([PipedArgument] EntityUid input)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        var comp = EntityManager.GetComponentOrNull<DynamicRuleComponent>(input);
        if (comp == null)
        {
            _sawmill.Warning($"dynamicrule score: entity {input} is missing DynamicRuleComponent.");
            return "no dynamic rule";
        }
        _dynamicRuleSystem.GetRuleBudget(input);
        var snapshot = _difficultySystem.GetSnapshot(input, comp);
        if (snapshot is null)
            return $"budget={comp.Budget:0.##} (difficulty disabled)";

        var value = snapshot.Value;
        var threats = value.Threats.Count == 0
            ? "none"
            : string.Join(", ", value.Threats.Select(t => $"{t.Label}:{t.Cost:0.##}"));

        return $"enabled={value.Enabled} budget={value.Budget:0.##} mult={value.Multiplier:0.##} bias={value.ManualBias:0.##} players={value.ActivePlayers} sec={value.AliveSecurity} threats=[{threats}]";
    }

    // Nudge the manual bias up or down for every matching dynamic rule.
    [CommandImplementation("biasadjust")]
    public IEnumerable<float> BiasAdjust([PipedArgument] IEnumerable<EntityUid> input, float delta)
        => input.Select(id => BiasAdjust(id, delta));

    [CommandImplementation("biasadjust")]
    public float BiasAdjust([PipedArgument] EntityUid input, float delta)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        if (!EntityManager.HasComponent<DynamicRuleComponent>(input))
        {
            _sawmill.Warning($"dynamicrule biasadjust: entity {input} is missing DynamicRuleComponent.");
            return 0f;
        }
        _dynamicRuleSystem.GetRuleBudget(input);
        return _difficultySystem.AdjustBias(input, delta);
    }

    // Set the manual bias to an explicit value for a batch of entities.
    [CommandImplementation("biasset")]
    public IEnumerable<float> BiasSet([PipedArgument] IEnumerable<EntityUid> input, float value)
        => input.Select(id => BiasSet(id, value));

    [CommandImplementation("biasset")]
    public float BiasSet([PipedArgument] EntityUid input, float value)
    {
        _dynamicRuleSystem ??= GetSys<DynamicRuleSystem>();
        _difficultySystem ??= GetSys<DynamicDifficultySystem>();

        if (!EntityManager.HasComponent<DynamicRuleComponent>(input))
        {
            _sawmill.Warning($"dynamicrule biasset: entity {input} is missing DynamicRuleComponent.");
            return 0f;
        }
        _dynamicRuleSystem.GetRuleBudget(input);
        return _difficultySystem.SetBias(input, value);
    }
}


