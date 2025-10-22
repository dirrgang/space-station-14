using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Enables the server-authoritative penetration pre-pass for physical damage. When false the legacy coefficient-based
    ///     pipeline is used exclusively.
    /// </summary>
    public static readonly CVarDef<bool> CombatPenetrationPrepassEnabled =
        CVarDef.Create("combat.penetration_prepass.enabled", false, CVar.SERVER);
}
