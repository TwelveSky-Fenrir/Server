using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Services.ZoneTransfer;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Services;

// op22 CL_DEMAND_ZONE_SERVER_INFO_SEND -- Life/Mana floor-clamp sub-behavior of the per-login avatar
// realignment contract (Server/ts25login/S04_MyWork02.cpp:357-358, SetIntegerLow). Re-timed to op22 request
// time per ZoneTransferService's own <remarks>; see that file for why the zone/tribe realignment and
// item-purge sibling corrections are deliberately not covered here.
public class ZoneTransferServiceTests
{
    private const short HostedMapId = 42;
    private const int AccountId = 7;

    private static readonly CharacterSummaryDto Summary = new(501, 0, "Hero", 1, 0, 1, 1, 10);

    private static readonly ShardDirectoryEntryDto Shard =
        new(1, "10.0.0.1", 30000, 0, 100, 0f);

    [Fact]
    public async Task RequestZoneTransferAsync_LifeAndManaAlreadyAtOrAboveFloor_NeverWritesVitals()
    {
        var worldEntry = WorldEntryWith(850, 320);
        var characters = FakeCharacterRepository.With(Summary, worldEntry);
        var service = CreateService(characters);

        await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Null(characters.LastClampVitalsFloor);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_LifeBelowOne_ClampsLifeToOneAndBumpsFlushSequence()
    {
        var worldEntry = WorldEntryWith(0, 320);
        var characters = FakeCharacterRepository.With(Summary, worldEntry);
        var service = CreateService(characters);

        await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.NotNull(characters.LastClampVitalsFloor);
        Assert.Equal(worldEntry.CharacterId, characters.LastClampVitalsFloor!.CharacterId);
        Assert.Equal(1, characters.LastClampVitalsFloor.Life);
        Assert.Equal(320, characters.LastClampVitalsFloor.Mana);
        Assert.Equal(worldEntry.FlushSequence + 1, characters.LastClampVitalsFloor.FlushSequence);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_ManaBelowZero_ClampsManaToZero()
    {
        var worldEntry = WorldEntryWith(850, -40);
        var characters = FakeCharacterRepository.With(Summary, worldEntry);
        var service = CreateService(characters);

        await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.NotNull(characters.LastClampVitalsFloor);
        Assert.Equal(850, characters.LastClampVitalsFloor!.Life);
        Assert.Equal(0, characters.LastClampVitalsFloor.Mana);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_BothBelowFloor_ClampsBothIndependentlyInOneWrite()
    {
        var worldEntry = WorldEntryWith(-10, -10);
        var characters = FakeCharacterRepository.With(Summary, worldEntry);
        var service = CreateService(characters);

        await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.NotNull(characters.LastClampVitalsFloor);
        Assert.Equal(1, characters.LastClampVitalsFloor!.Life);
        Assert.Equal(0, characters.LastClampVitalsFloor.Mana);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_CharacterVanishedBeforeClamp_ReturnsCharacterNotFoundWithoutThrowing()
    {
        var characters = FakeCharacterRepository.With(Summary,
            WorldEntryWith(0, 0)); // FakeCharacterRepository.With always seeds one world entry
        var service = CreateService(characters);

        // A slot the account doesn't actually have a character in (per-slot occupancy check).
        var result = await service.RequestZoneTransferAsync(AccountId, 2, Guid.NewGuid(),
            0, CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.CharacterNotFound, result.Outcome);
        Assert.Null(characters.LastClampVitalsFloor);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_NoLiveShardHostsTheMap_ReturnsShardUnavailableAndMintsNoTicket()
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntryWith(850, 320));
        var otherShard = new ShardDirectoryEntryDto(2, "10.0.0.2", 30001, 0, 100, 0f);
        var (service, tickets, _, _) = CreateServiceWithDirectory(characters, [otherShard],
            new Dictionary<byte, short[]> { [2] = [HostedMapId + 1] });

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.ShardUnavailable, result.Outcome);
        Assert.Equal("", result.Ip);
        Assert.Equal(0, result.Port);
        Assert.Equal(0, result.Zone);
        Assert.Null(tickets.LastCreatedTicket);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_EmptyLiveShardDirectory_ReturnsShardUnavailableAndMintsNoTicket()
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntryWith(850, 320));
        var (service, tickets, _, _) = CreateServiceWithDirectory(characters, [],
            new Dictionary<byte, short[]>());

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.ShardUnavailable, result.Outcome);
        Assert.Null(tickets.LastCreatedTicket);
    }

    [Fact]
    public async Task RequestZoneTransferAsync_MultipleLiveShardsNoneHostTheMap_ReturnsShardUnavailable()
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntryWith(850, 320));
        var shard1 = new ShardDirectoryEntryDto(1, "10.0.0.1", 30000, 0, 100, 0f);
        var shard2 = new ShardDirectoryEntryDto(2, "10.0.0.2", 30001, 0, 100, 0f);
        var (service, tickets, _, _) = CreateServiceWithDirectory(characters, [shard1, shard2],
            new Dictionary<byte, short[]>
            {
                [1] = [HostedMapId + 1],
                [2] = [HostedMapId + 2]
            });

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.ShardUnavailable, result.Outcome);
        Assert.Null(tickets.LastCreatedTicket);
    }

    // gameserver-directory-heartbeat-liveness: the shard directory only proves a heartbeat within the last
    // ~17s, not that the shard is still alive right now -- a crashed shard would otherwise still get a
    // ticket minted for it. This is the dead-end-handoff fix: a failed TCP reachability probe on the
    // resolved shard must reject exactly like "no shard claims this map" (no ticket) and proactively evict
    // the dead shard's directory row instead of waiting for it to age out on its own.
    [Fact]
    public async Task
        RequestZoneTransferAsync_ResolvedShardFailsReachabilityProbe_ReturnsShardUnavailableMintsNoTicketAndEvictsTheShard()
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntryWith(850, 320));
        var (service, tickets, directory, reachability) = CreateServiceWithDirectory(characters, [Shard],
            new Dictionary<byte, short[]> { [Shard.ShardId] = [HostedMapId] });
        reachability.MarkUnreachable(Shard.Host, Shard.Port);

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.ShardUnavailable, result.Outcome);
        Assert.Equal("", result.Ip);
        Assert.Equal(0, result.Port);
        Assert.Equal(0, result.Zone);
        Assert.Null(tickets.LastCreatedTicket);
        Assert.Contains(Shard.ShardId, directory.MarkedUnreachableShardIds);
    }

    [Fact]
    public async Task
        RequestZoneTransferAsync_ResolvedShardPassesReachabilityProbe_MintsTicketAndNeverMarksItUnreachable()
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntryWith(850, 320));
        var (service, tickets, directory, _) = CreateServiceWithDirectory(characters, [Shard],
            new Dictionary<byte, short[]> { [Shard.ShardId] = [HostedMapId] });

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.Success, result.Outcome);
        Assert.NotNull(tickets.LastCreatedTicket);
        Assert.Empty(directory.MarkedUnreachableShardIds);
    }

    private static CharacterWorldEntryDto WorldEntryWith(int life, int mana)
    {
        return new CharacterWorldEntryDto(
            Summary.CharacterId, AccountId, Summary.Slot, Summary.Name, Summary.Tribe, Summary.Gender,
            Summary.HeadType, Summary.FaceType, Summary.Level, HostedMapId,
            0, 0, 0, 0,
            life, 1000, mana, 400, 99L);
    }

    private static ZoneTransferService CreateService(FakeCharacterRepository characters)
    {
        var directory = new FakeGameServerDirectoryRepository(Shard);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]> { [1] = [HostedMapId] });
        var tickets = new FakeSessionTicketRepository();
        var reachability = new FakeShardReachabilityProbe();
        var options = Options.Create(new LoginServerOptions());

        return new ZoneTransferService(characters, directory, shardMaps, tickets, reachability, options,
            NullLogger<ZoneTransferService>.Instance);
    }

    private static (ZoneTransferService Service, FakeSessionTicketRepository Tickets,
        FakeGameServerDirectoryRepository Directory, FakeShardReachabilityProbe Reachability)
        CreateServiceWithDirectory(
            FakeCharacterRepository characters, ShardDirectoryEntryDto[] shards,
            IReadOnlyDictionary<byte, short[]> hostedMapsByShard)
    {
        var directory = new FakeGameServerDirectoryRepository(shards);
        var shardMaps = new FakeShardMapAssignmentRepository(hostedMapsByShard);
        var tickets = new FakeSessionTicketRepository();
        var reachability = new FakeShardReachabilityProbe();
        var options = Options.Create(new LoginServerOptions());

        var service = new ZoneTransferService(characters, directory, shardMaps, tickets, reachability, options,
            NullLogger<ZoneTransferService>.Instance);
        return (service, tickets, directory, reachability);
    }
}
