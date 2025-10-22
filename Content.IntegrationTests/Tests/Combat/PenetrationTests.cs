using System.Numerics;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Combat;

[TestFixture]
public sealed class PenetrationTests : ContentIntegrationTest
{
    [Test]
    public async Task ProjectileDeflectsWhenArmorExceedsAp()
    {
        await using var pair = await StartServerDummyTicker();
        var server = pair.Server;
        var entManager = server.EntMan;

        await server.WaitPostAsync(() =>
        {
            var cfg = server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(CCVars.CombatPenetrationPrepassEnabled, true);
        });

        await server.WaitIdleAsync();

        EntityUid projectile = default;
        ProjectileHitEvent hitEvent = default;
        FixedPoint2 bluntDamage = FixedPoint2.Zero;
        FixedPoint2 piercingDamage = FixedPoint2.Zero;
        bool projectileSpent = false;
        bool penetrationHandled = false;

        await server.WaitPostAsync(() =>
        {
            var mapId = server.MapManager.CreateMap();
            var coords = new MapCoordinates(Vector2.Zero, mapId);

            var target = entManager.SpawnEntity("CEExampleTarget", coords);
            projectile = entManager.SpawnEntity("CEExampleAPSlugRound", coords);

            var projectileComponent = entManager.GetComponent<ProjectileComponent>(projectile);
            hitEvent = new ProjectileHitEvent(new DamageSpecifier(projectileComponent.Damage), target, null);
            entManager.EventBus.RaiseLocalEvent(projectile, ref hitEvent);

            projectileSpent = entManager.GetComponent<ProjectileComponent>(projectile).ProjectileSpent;
            penetrationHandled = hitEvent.PenetrationHandled;

            hitEvent.Damage.DamageDict.TryGetValue("Blunt", out bluntDamage);
            hitEvent.Damage.DamageDict.TryGetValue("Piercing", out piercingDamage);
        });

        Assert.That(projectileSpent, Is.True, "Projectile should lodge after deflection.");
        Assert.That(penetrationHandled, Is.True, "Penetration flag should be set on the hit event.");
        Assert.That(bluntDamage.Float(), Is.GreaterThan(0f), "Deflection should convert sharp damage into blunt.");
        Assert.That(piercingDamage, Is.EqualTo(FixedPoint2.Zero), "Piercing damage should be zeroed on deflection.");
    }
}
