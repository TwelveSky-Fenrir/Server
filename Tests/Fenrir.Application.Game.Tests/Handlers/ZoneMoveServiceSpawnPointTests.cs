using System.Collections.Frozen;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Covers <see cref="ZoneMoveService" />'s arrival-position resolution: the target zone's
///     <c>ZoneSpawnPoints</c>/<c>DefaultSpawn*</c> data, never the client, is what places a transferring
///     character. Closes the coverage gap <see cref="ZoneMoveServiceTests" /> explicitly disclaims.
/// </summary>
public class ZoneMoveServiceSpawnPointTests
{
    private const int CharacterId = 10;
    private const short SourceMapId = 2;
    private const short TargetMapId = 50;

    private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone, Zone TargetZone)
        CreateService(ZoneDefinition targetDefinition)
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [TargetMapId] = targetDefinition
        }.ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([SourceMapId, TargetMapId]);

        var worldState = ZoneTestKit.CreateWorldState();
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[SourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId,
            ZoneTestKit.EnterData(session, SourceMapId, posX: 10f, posY: 0f, posZ: 10f)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        return (service, session, sourceZone, zones[TargetMapId]);
    }

    private static ZoneMoveRequest Request(short presentZone, short targetZone, int sort = 4)
    {
        return new ZoneMoveRequest { Sort = sort, ZoneNumber = targetZone, PresentZoneNumber = presentZone };
    }

    [Fact]
    public async Task TargetZoneHasMatchingSpawnPointForSourceZone_CharacterArrivesAtThatSpawnPoint()
    {
        var targetDefinition = new ZoneDefinition(
            new ZoneRowDto(TargetMapId, 0f, 0f, 0f),
            [],
            [new ZoneSpawnPointRowDto(TargetMapId, 0, SourceMapId, 111f, 22f, 333f)],
            [],
            []);
        var (service, session, sourceZone, targetZone) = CreateService(targetDefinition);

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);
        sourceZone.Tick(TimeSpan.FromMilliseconds(50)); // drains the posted Leave, hands off Enter to targetZone
        targetZone.Tick(TimeSpan.FromMilliseconds(50)); // drains the handed-off Enter

        Assert.False(sourceZone.TryGetPlayer(CharacterId, out _));
        Assert.True(targetZone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(111f, state!.PosX);
        Assert.Equal(22f, state.PosY);
        Assert.Equal(333f, state.PosZ);
    }

    [Fact]
    public async Task TargetZoneHasNoMatchingSpawnPoint_CharacterArrivesAtDefaultSpawn()
    {
        var targetDefinition = new ZoneDefinition(
            new ZoneRowDto(TargetMapId, 55f, 5f, 66f),
            [],
            [new ZoneSpawnPointRowDto(TargetMapId, 0, (short)(SourceMapId + 1), 111f, 22f, 333f)],
            [],
            []);
        var (service, session, sourceZone, targetZone) = CreateService(targetDefinition);

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        targetZone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(targetZone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(55f, state!.PosX);
        Assert.Equal(5f, state.PosY);
        Assert.Equal(66f, state.PosZ);
    }

    [Fact]
    public async Task TargetZoneHasNoSpawnPointsAtAll_CharacterArrivesAtDefaultSpawn()
    {
        var targetDefinition = new ZoneDefinition(
            new ZoneRowDto(TargetMapId, 1f, 2f, 3f), [], [], [], []);
        var (service, session, sourceZone, targetZone) = CreateService(targetDefinition);

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        targetZone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(targetZone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(1f, state!.PosX);
        Assert.Equal(2f, state.PosY);
        Assert.Equal(3f, state.PosZ);
    }
}
