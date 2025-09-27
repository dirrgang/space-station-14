using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables the dynamic difficulty controller that adjusts the dynamic game rule budget based on live round metrics.
    /// </summary>
    [CVarControl(AdminFlags.Server | AdminFlags.Round)]
    public static readonly CVarDef<bool> DynamicDifficultyEnabled =
        CVarDef.Create("dynamic.difficulty.enabled", false, CVar.ARCHIVE | CVar.SERVERONLY);

    /// <summary>
    /// Base multiplier applied to the dynamic rule budget accrual when the difficulty controller is enabled.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyBaseRate =
        CVarDef.Create("dynamic.difficulty.base_rate", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum multiplier that the difficulty controller may apply to budget accrual.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyMinRate =
        CVarDef.Create("dynamic.difficulty.min_rate", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// Maximum multiplier that the difficulty controller may apply to budget accrual.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyMaxRate =
        CVarDef.Create("dynamic.difficulty.max_rate", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Cap for the dynamic rule budget when using the difficulty controller.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyMaxBudget =
        CVarDef.Create("dynamic.difficulty.max_budget", 750f, CVar.SERVERONLY);

    /// <summary>
    /// Global scalar applied to antagonist cost reservations computed by the difficulty controller.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyCostScale =
        CVarDef.Create("dynamic.difficulty.cost_scale", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Toggles the player-count contribution to the difficulty multiplier.
    /// </summary>
    public static readonly CVarDef<bool> DynamicDifficultyPlayerFactorEnabled =
        CVarDef.Create("dynamic.difficulty.player_factor", true, CVar.SERVERONLY);

    /// <summary>
    /// Weight added to the difficulty multiplier per alive, in-game player when the player-count factor is enabled.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyPlayerWeight =
        CVarDef.Create("dynamic.difficulty.player_weight", 0.02f, CVar.SERVERONLY);

    /// <summary>
    /// Toggles the security staffing contribution to the difficulty multiplier.
    /// </summary>
    public static readonly CVarDef<bool> DynamicDifficultySecurityFactorEnabled =
        CVarDef.Create("dynamic.difficulty.security_factor", true, CVar.SERVERONLY);

    /// <summary>
    /// Base weight added to the difficulty multiplier per alive security-aligned player.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultySecurityWeight =
        CVarDef.Create("dynamic.difficulty.security_weight", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// Comma separated list of department prototype IDs that are considered security-aligned for difficulty calculations.
    /// </summary>
    public static readonly CVarDef<string> DynamicDifficultySecurityDepartments =
        CVarDef.Create("dynamic.difficulty.security_departments", "Security,Command", CVar.SERVERONLY);

    /// <summary>
    /// Toggles playtime scaling on defender and antagonist weights.
    /// </summary>
    public static readonly CVarDef<bool> DynamicDifficultyPlaytimeFactorEnabled =
        CVarDef.Create("dynamic.difficulty.playtime_factor", true, CVar.SERVERONLY);

    /// <summary>
    /// Multiplier applied to log-scaled hours of playtime when computing weight adjustments.
    /// </summary>
    public static readonly CVarDef<float> DynamicDifficultyPlaytimeScale =
        CVarDef.Create("dynamic.difficulty.playtime_scale", 0.25f, CVar.SERVERONLY);
}