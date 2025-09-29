using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Threat;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Threat;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly ThreatAssessmentSystem _threatAssessmentSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private void AddThreatDebugVerb(GetVerbsEvent<Verb> args, ICommonSession player)
    {
        if (!_adminManager.HasAdminFlag(player, AdminFlags.Debug))
            return;

        if (!TryComp(args.Target, out ThreatDebugComponent? debugComponent))
            return;

        var verb = new Verb
        {
            Text = Loc.GetString("threat-debug-verb-text"),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),
            Act = () => ExecuteThreatDebugVerb(args.User, args.Target, player, debugComponent),
            Impact = LogImpact.Low
        };

        args.Verbs.Add(verb);
    }

    private void ExecuteThreatDebugVerb(EntityUid user, EntityUid target, ICommonSession player, ThreatDebugComponent component)
    {
        ThreatReport report;
        bool success;

        if (component.UseAppraiserComponent)
        {
            var appraiser = component.AppraiserOverride ?? target;
            success = _threatAssessmentSystem.TryGetThreatReport(appraiser, target, out report, reuseOnCooldown: false, storeReport: false);
            if (!success)
            {
                _popup.PopupEntity(Loc.GetString("threat-debug-error-appraiser"), target, user);
                return;
            }
        }
        else
        {
            var appraiser = component.AppraiserOverride ?? target;
            success = _threatAssessmentSystem.TryDebugReport(component.Profile, target, out report, appraiser);
            if (!success)
            {
                _popup.PopupEntity(Loc.GetString("threat-debug-error-profile", ("profile", component.Profile)), target, user);
                return;
            }
        }

        var header = Loc.GetString("threat-debug-report-header",
            ("target", EntityManager.ToPrettyString(target)),
            ("profile", report.ProfileId),
            ("score", report.Score.ToString("0.##")));

        var sb = new StringBuilder();
        sb.AppendLine(header);

        foreach (var contribution in report.Contributions.OrderByDescending(c => Math.Abs(c.Value)))
        {
            var valueText = contribution.Value >= 0 ? $"+{contribution.Value:0.###}" : $"{contribution.Value:0.###}";
            var contextText = contribution.Context != ThreatSourceContext.None ? $" ({contribution.Context})" : string.Empty;
            var detailText = contribution.Detail != null ? $" [{contribution.Detail}]" : string.Empty;
            var sourceText = contribution.Source != null ? $" <{EntityManager.ToPrettyString(contribution.Source.Value)}>": string.Empty;

            sb.AppendLine($" - {contribution.Reason}{contextText}: {valueText}{detailText}{sourceText}");
        }

        _chatManager.DispatchServerMessage(player, sb.ToString());
        _popup.PopupEntity(Loc.GetString("threat-debug-popup-score", ("score", report.Score.ToString("0.##"))), target, user);
    }
}

