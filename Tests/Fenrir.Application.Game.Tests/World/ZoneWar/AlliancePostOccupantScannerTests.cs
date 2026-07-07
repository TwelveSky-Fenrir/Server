using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class AlliancePostOccupantScannerTests
{
    private const short MapId = 37;

    private static readonly AlliancePostSite Site = new(MapId, Post0X: 100f, Post0Z: 100f, Post1X: 200f,
        Post1Z: 200f, Radius: 10f);

    private static Zone CreateZoneWithPlayer(int characterId, byte tribe, float posX, float posZ,
        byte tribeRole = 1, bool isDead = false, bool isMovingZone = false)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, MapId, tribe: tribe, posX: posX, posZ: posZ)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        state!.TribeRole = tribeRole;
        state.IsDead = isDead;
        state.IsMovingZone = isMovingZone;

        return zone;
    }

    [Fact]
    public void EmptyZone_BothPostsNull()
    {
        var zone = ZoneTestKit.CreateZone(MapId);

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
        Assert.Null(postTwo);
    }

    [Fact]
    public void TribeMasterInsidePost0Radius_ReturnsAsPostOne_PostTwoStaysNull()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X, posZ: Site.Post0Z);

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Equal(new AllianceCeremonyCandidate(1, 2), postOne);
        Assert.Null(postTwo);
    }

    [Fact]
    public void TribeMasterInsidePost1Radius_ReturnsAsPostTwo()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 3, posX: Site.Post1X, posZ: Site.Post1Z);

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
        Assert.Equal(new AllianceCeremonyCandidate(1, 3), postTwo);
    }

    [Fact]
    public void NonMasterTribeRole_NeverQualifies()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X, posZ: Site.Post0Z, tribeRole: 0);

        var (postOne, _) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
    }

    [Fact]
    public void SubMasterTribeRole_DoesNotQualify_OnlyExactMasterDoes()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X, posZ: Site.Post0Z, tribeRole: 2);

        var (postOne, _) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
    }

    [Fact]
    public void OutsideRadius_DoesNotQualify()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X + 1000f, posZ: Site.Post0Z);

        var (postOne, _) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
    }

    [Fact]
    public void Dead_DoesNotQualify()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X, posZ: Site.Post0Z, isDead: true);

        var (postOne, _) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
    }

    [Fact]
    public void MovingZone_DoesNotQualify()
    {
        var zone = CreateZoneWithPlayer(1, tribe: 2, posX: Site.Post0X, posZ: Site.Post0Z, isMovingZone: true);

        var (postOne, _) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Null(postOne);
    }

    [Fact]
    public void BothPostsSimultaneouslyOccupiedByDifferentMasters_BothResolve()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (sessionOne, _) = ZoneTestKit.CreateSession(1);
        var (sessionTwo, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(sessionOne, MapId, tribe: 0, posX: Site.Post0X, posZ: Site.Post0Z)));
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(sessionTwo, MapId, tribe: 1, posX: Site.Post1X, posZ: Site.Post1Z)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(1, out var stateOne));
        Assert.True(zone.TryGetPlayer(2, out var stateTwo));
        stateOne!.TribeRole = 1;
        stateTwo!.TribeRole = 1;

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, Site);

        Assert.Equal(new AllianceCeremonyCandidate(1, 0), postOne);
        Assert.Equal(new AllianceCeremonyCandidate(2, 1), postTwo);
    }
}
