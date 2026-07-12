using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Services.ZoneTransfer;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Services;

public class ZoneTransferServiceTests
{
    private const short HostedMapId = 8;
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
    public async Task RequestZoneTransferAsync_RequestedSlotHoldsNoCharacter_ReturnsSlotEmptyWithoutThrowing()
    {
        var characters = FakeCharacterRepository.With(Summary,
            WorldEntryWith(0, 0));
        var service = CreateService(characters);

        var result = await service.RequestZoneTransferAsync(AccountId, 2, Guid.NewGuid(),
            0, CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.SlotEmpty, result.Outcome);
        Assert.Null(characters.LastClampVitalsFloor);
    }

    [Fact]
    public async Task
        RequestZoneTransferAsync_CharacterVanishedBetweenRosterAndWorldEntryReads_ReturnsCharacterNotFoundWithoutThrowing()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var service = CreateService(characters);

        var result = await service.RequestZoneTransferAsync(AccountId, Summary.Slot, Guid.NewGuid(),
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

    [Fact]
    public async Task
        RequestZoneTransferAsync_PersistedZoneNotOwnedByOwnTribe_RoutesToTheShardHostingTheHometownInstead()
    {
        const short HometownMapId = 1;
        var mismatchedSummary = new CharacterSummaryDto(501, 0, "Hero", 0, 0, 1, 1, 10);
        var worldEntry = new CharacterWorldEntryDto(
            mismatchedSummary.CharacterId, AccountId, mismatchedSummary.Slot, mismatchedSummary.Name,
            mismatchedSummary.Tribe, mismatchedSummary.Gender, mismatchedSummary.HeadType,
            mismatchedSummary.FaceType, mismatchedSummary.Level, HostedMapId, 0, 0, 0, 0,
            850, 1000, 320, 400, 99L);
        var characters = FakeCharacterRepository.With(mismatchedSummary, worldEntry);

        var hometownShard = new ShardDirectoryEntryDto(2, "10.0.0.2", 30002, 0, 100, 0f);
        var (service, tickets, _, _) = CreateServiceWithDirectory(characters, [Shard, hometownShard],
            new Dictionary<byte, short[]>
            {
                [Shard.ShardId] = [HostedMapId],
                [hometownShard.ShardId] = [HometownMapId]
            });

        var result = await service.RequestZoneTransferAsync(AccountId, mismatchedSummary.Slot, Guid.NewGuid(), 0,
            CancellationToken.None);

        Assert.Equal(ZoneTransferOutcome.Success, result.Outcome);
        Assert.Equal(HometownMapId, result.Zone);
        Assert.Equal(hometownShard.Host, result.Ip);
        Assert.Equal(hometownShard.Port, result.Port);
        Assert.NotNull(tickets.LastCreatedTicket);
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
