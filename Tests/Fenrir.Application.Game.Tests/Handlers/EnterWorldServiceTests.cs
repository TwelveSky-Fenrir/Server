using System.Buffers;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.Handlers.Tribes;
using Fenrir.Application.Game.Tests.Progression;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Security;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Compression;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class EnterWorldServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 501;
    private static readonly IPEndPoint RemoteEndPoint = new(IPAddress.Parse("203.0.113.9"), 40000);

    [Fact]
    public async Task HandleAsync_CharacterHasAnActiveBan_AbortsBeforeFetchingTheCharacterBundle()
    {
        var service = CreateService(out var session, true);

        await service.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_RemoteIpIsFirewalled_AbortsBeforeFetchingTheCharacterBundle()
    {
        var service = CreateService(out var session, blockedIp: true);

        await service.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacter_PopulatesBuffsWorldStateAndPreviousTribe_InsteadOfZeros()
    {
        const short MapId = 7;
        const byte Tribe = 3;
        const byte PreviousTribe = 2;
        const float PosX = 111f, PosY = 5f, PosZ = 222f;

        var buffs = new List<CharacterBuffDto>
        {
            new(0, 42, 100),
            new(5, 7, 3)
        };
        var bundle = HappyPathBundle(MapId, Tribe, PreviousTribe, PosX, PosY, PosZ, buffs);

        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var guilds = new FakeGuildRepository();
        var guildRanking = new GuildRankingCache();
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.AddTribePoints(2, 777);

        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);
        var zoneCenterSiegeState = new ZoneCenterSiegeState();
        var tribeGuardCorridorState = new TribeGuardCorridorState();
        var towerWar = new TowerWarState();

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            guilds,
            guildRanking,
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            worldState,
            towerWar,
            zoneCenterSiegeState,
            tribeGuardCorridorState,
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);

        var expectedEnterWorld = new EnterWorldResponse
        {
            AvatarInfo = AvatarInfoFactory.CreateForCharacter(bundle.Character, bundle.Items,
                AvatarSocialSnapshot.Empty, bundle.Skills, bundle.Hotkeys),
            BuffInfo = ExpectedBuffInfo(buffs)
        };
        var expectedEnterWorldBytes = FrameWriter.WriteCompressedFrame(in expectedEnterWorld);

        var expectedWorldSnapshot = new WorldSnapshotResponse
        {
            WorldInfo = ZoneCenterSiegeProjection.Apply(
                WorldStateProjection.Apply(
                    GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top), worldState),
                zoneCenterSiegeState, tribeGuardCorridorState),
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        var expectedWorldSnapshotBytes = FrameWriter.WriteCompressedFrame(in expectedWorldSnapshot);

        var expectedTowerStatus = towerWar.BuildStatusSnapshot();
        var towerStatusFrameSize = FrameWriter.FrameSizeOf<TowerStatusResponse>();
        var expectedTowerStatusBytes = new byte[towerStatusFrameSize];
        FrameWriter.WriteFrame(in expectedTowerStatus, expectedTowerStatusBytes);

        var selfSpawnFrameSize = FrameWriter.FrameSizeOf<AvatarActionResponse>();

        var allSent = await ReadExactlyAsync(pipe,
            expectedEnterWorldBytes.Length + expectedWorldSnapshotBytes.Length + towerStatusFrameSize +
            selfSpawnFrameSize);

        var enterWorldActual = allSent.AsSpan(0, expectedEnterWorldBytes.Length).ToArray();
        Assert.Equal(expectedEnterWorldBytes, enterWorldActual);

        var worldSnapshotActual = allSent
            .AsSpan(expectedEnterWorldBytes.Length, expectedWorldSnapshotBytes.Length).ToArray();
        Assert.Equal(expectedWorldSnapshotBytes, worldSnapshotActual);

        var towerStatusActual = allSent
            .AsSpan(expectedEnterWorldBytes.Length + expectedWorldSnapshotBytes.Length, towerStatusFrameSize)
            .ToArray();
        Assert.Equal(expectedTowerStatusBytes, towerStatusActual);

        var selfSpawnActual = allSent
            .AsSpan(
                expectedEnterWorldBytes.Length + expectedWorldSnapshotBytes.Length + towerStatusFrameSize,
                selfSpawnFrameSize)
            .ToArray();
        var selfSpawnPayload = selfSpawnActual.AsSpan(1);
        Assert.True(ObjectForAvatar.TryRead(selfSpawnPayload.Slice(8, ObjectForAvatar.WireSize),
            out var selfSpawnData));
        Assert.Equal(PreviousTribe, selfSpawnData.PreviousTribe);
        Assert.Equal([PosX, PosY, PosZ], selfSpawnData.PetLocation);
        Assert.Equal(ExpectedEffectValueForView(buffs), selfSpawnData.EffectValueForView);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacterWithPersistedHeroRankPoints_SeedsThePlayerRuntimeStateMirror()
    {
        const short MapId = 9;
        const int PersistedHeroRankPoints = 250;

        var bundle = HappyPathBundle(MapId, 1, 1, 0f, 0f, 0f, []);
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);

        var heroRankings = new FakeHeroRankingRepository();
        heroRankings.Points[(CharacterId, 0)] = PersistedHeroRankPoints;

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            heroRankings,
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(zones.TryGet(MapId, out var zone));
        zone!.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(PersistedHeroRankPoints, state!.HeroRankPoints);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacterWithPersistedPetBagDate_SeedsThePlayerRuntimeStateMirror()
    {
        const short MapId = 13;
        const int PersistedPetBagDate = 20991231;

        var bundle = HappyPathBundle(MapId, 1, 1, 0f, 0f, 0f, [], petBagDate: PersistedPetBagDate);
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(zones.TryGet(MapId, out var zone));
        zone!.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(PersistedPetBagDate, state!.PetBagDate);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacterWithPersistedDropItemTime_SeedsThePlayerRuntimeStateMirror()
    {
        const short MapId = 15;
        const int PersistedDropItemTime = 42;

        var bundle = HappyPathBundle(MapId, 1, 1, 0f, 0f, 0f, [], dropItemTime: PersistedDropItemTime);
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(zones.TryGet(MapId, out var zone));
        zone!.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(PersistedDropItemTime, state!.DropItemTime);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacterWithPersistedWarPoint_SeedsThePlayerRuntimeStateMirror()
    {
        const short MapId = 17;
        const int PersistedWarPoint = 1234;

        var bundle = HappyPathBundle(MapId, 1, 1, 0f, 0f, 0f, [], warPoint: PersistedWarPoint);
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(zones.TryGet(MapId, out var zone));
        zone!.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(PersistedWarPoint, state!.WarPoint);
    }

    [Fact]
    public async Task HandleAsync_ReturningCharacterWithPersistedRunes_SeedsThePlayerRuntimeStateRuneArrays()
    {
        const short MapId = 11;

        var bundle = HappyPathBundle(MapId, 1, 1, 0f, 0f, 0f, []);
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([MapId]);

        var runes = new FakeRuneRepository
        {
            RowsToReturn =
            [
                new CharacterRuneSocketDto(0, 93514, 12345),
                new CharacterRuneSocketDto(2, 93516, 67890)
            ]
        };

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            runes,
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(zones.TryGet(MapId, out var zone));
        zone!.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.Equal(93514, state!.RuneSystem[0]);
        Assert.Equal(12345, state.RuneSystemStat[0]);
        Assert.Equal(0, state.RuneSystem[1]);
        Assert.Equal(0, state.RuneSystemStat[1]);
        Assert.Equal(93516, state.RuneSystem[2]);
        Assert.Equal(67890, state.RuneSystemStat[2]);
        Assert.Equal(0, state.RuneSystem[3]);
        Assert.Equal(0, state.RuneSystemStat[3]);
    }

    [Theory]
    [InlineData((byte)0, (byte)1)]
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)2, (byte)0)]
    [InlineData((byte)3, (byte)3)]
    [InlineData((byte)3, (byte)5)]
    [InlineData((byte)4, (byte)0)]
    [InlineData((byte)200, (byte)0)]
    public async Task HandleAsync_TribeAndPreviousTribeInternallyInconsistent_AbortsSession(byte tribe,
        byte previousTribe)
    {
        var bundle = HappyPathBundle(7, tribe, previousTribe, 0f, 0f, 0f, []);
        var service = CreateService(out var session, bundle: bundle);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData((byte)0, (byte)0)]
    [InlineData((byte)1, (byte)1)]
    [InlineData((byte)2, (byte)2)]
    [InlineData((byte)3, (byte)0)]
    [InlineData((byte)3, (byte)1)]
    [InlineData((byte)3, (byte)2)]
    public async Task HandleAsync_TribeAndPreviousTribeInternallyConsistent_DoesNotAbort(byte tribe,
        byte previousTribe)
    {
        var bundle = HappyPathBundle(7, tribe, previousTribe, 0f, 0f, 0f, []);
        var (service, session) = CreateWorkingService(bundle);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

        private static async Task<byte[]> ReadExactlyAsync(FakeDuplexPipe pipe, int totalLength)
    {
        var collected = new byte[totalLength];
        var offset = 0;

        while (offset < totalLength)
        {
            var result = await pipe.SessionToPeer.ReadAsync();
            var sliceLength = (int)Math.Min(result.Buffer.Length, totalLength - offset);
            var slice = result.Buffer.Slice(0, sliceLength);
            slice.ToArray().CopyTo(collected, offset);
            offset += sliceLength;
            pipe.SessionToPeer.AdvanceTo(slice.End);
        }

        return collected;
    }

    private static CharacterWorldEntryBundle HappyPathBundle(short mapId, byte tribe, byte previousTribe,
        float posX, float posY, float posZ, IReadOnlyList<CharacterBuffDto> buffs, int petBagDate = 0,
        int dropItemTime = 0, int warPoint = 0)
    {
        var character = new CharacterWorldSnapshotDto(
            CharacterId, AccountId, 0, "Hero", tribe, 0,
            1, 1, 10, mapId, posX, posY, posZ, 0f,
            30, 30, 21, 21, 1, 0, 0, 1,
            1, 1, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, dropItemTime, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            false, [], 0, 0, 0,
            0, 0, 0, 0, 0,
            previousTribe, 0, 0, 0, 0,
            0, PetBagDate: petBagDate, WarPoint: warPoint);

        return new CharacterWorldEntryBundle(
            character,
            new ReadOnlyCollection<CharacterItemSlotDto>([]),
            new ReadOnlyCollection<CharacterSkillDto>([]),
            new ReadOnlyCollection<CharacterHotkeyDto>([]),
            new ReadOnlyCollection<CharacterBuffDto>([..buffs]));
    }

        private static BuffInfo ExpectedBuffInfo(IReadOnlyList<CharacterBuffDto> buffs)
    {
        var buff = new int[70];
        foreach (var row in buffs)
        {
            buff[row.SlotIndex * 2] = row.Value;
            buff[row.SlotIndex * 2 + 1] = row.RemainingLegacyTicks;
        }

        return WorldStateTemplates.ZeroedBuffInfo with { Buff = buff };
    }

        private static int[] ExpectedEffectValueForView(IReadOnlyList<CharacterBuffDto> buffs)
    {
        var view = new int[35];
        foreach (var row in buffs)
            view[row.SlotIndex] = row.Value;

        return view;
    }

    private static EnterWorldService CreateService(out ZoneClientSession session, bool characterBanned = false,
        bool blockedIp = false, CharacterWorldEntryBundle? bundle = null)
    {
        var pipe = new FakeDuplexPipe();
        session = new ZoneClientSession(1, pipe, RemoteEndPoint);
        session.MarkTicketConsumed(AccountId, CharacterId);

        var options = ZoneTestKit.Options();
        var zones = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(),
            Array.Empty<ISimulationSystem>());
        zones.Initialize(bundle is null ? [] : [bundle.Character.MapId]);

        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(blockedIp),
            new FakeFirewallRuleRepository(),
            new FakeGmAllowlistRepository());

        return new EnterWorldService(
            new FakeCharacterRepository { WorldEntryBundleToReturn = bundle },
            ZoneTestKit.EmptyWorldData(),
            zones,
            new ThrowingMuteRepository(),
            new FakeBanRepository(characterBanned),
            firewall,
            new ThrowingGuildRepository(),
            new GuildRankingCache(),
            new FakeTribeRepository(),
            new ThrowingFriendRepository(),
            new ThrowingMentorRepository(),
            new ThrowingHeroRankingRepository(),
            new ThrowingRuneRepository(),
            new ThrowingCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(options),
            NullLogger<EnterWorldService>.Instance);
    }

        private static (EnterWorldService Service, ZoneClientSession Session) CreateWorkingService(
        CharacterWorldEntryBundle bundle)
    {
        var characters = new FakeCharacterRepository { WorldEntryBundleToReturn = bundle };
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([bundle.Character.MapId]);

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(),
            new ApplicationFirewall(new FakeBlockedIpRepository(), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeHeroRankingRepository(),
            new FakeRuneRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeCharacterLogoutStateRepository(),
            ZoneTestKit.CreateWorldState(),
            new TowerWarState(),
            new ZoneCenterSiegeState(),
            new TribeGuardCorridorState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        return (service, session);
    }

        private static string EncodeObfuscatedAccountId(int accountId)
    {
        var bytes = Encoding.Latin1.GetBytes("MG" + accountId);
        WireXor.ApplyUidXor(bytes);
        return Encoding.Latin1.GetString(bytes);
    }

    private static EnterWorldRequest ValidRequest(string? id = null)
    {
        return new EnterWorldRequest
        {
            Id = id ?? "irrelevant",
            AvatarName = "Hero",
            Action = new ActionInfo
            {
                Type = 0,
                Sort = 0,
                Frame = 0,
                Location = new float[3],
                TargetLocation = new float[3],
                Front = 0,
                TargetFront = 0,
                PetLocation = new float[3],
                PetTargetLocation = new float[3],
                PetFront = 0,
                PetSort = 0,
                TargetObjectSort = 0,
                TargetObjectIndex = 0,
                TargetObjectUniqueNumber = 0,
                SkillNumber = 0,
                SkillGradeNum1 = 0,
                SkillGradeNum2 = 0,
                SkillValue = 0
            }
        };
    }

    private sealed class ThrowingMuteRepository : IMuteRepository
    {
        public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
            CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }
    }

    private sealed class ThrowingGuildRepository : IGuildRepository
    {
        public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(
            int count, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(
            int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(
            int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
            int deltaBigMoney, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask UpgradeAndDebitMoneyAsync(int guildId, int grade, int characterId, long deltaMoney,
            int deltaBigMoney, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask AdjustPointsAsync(int guildId, int delta, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingFriendRepository : IFriendRepository
    {
        public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(
            int characterId, CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingMentorRepository : IMentorRepository
    {
        public ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingHeroRankingRepository : IHeroRankingRepository
    {
        public ValueTask<int?> GetPointsAsync(int characterId, byte periodKind, CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId,
            int? level, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId, int? level,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> RolloverIfDueAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingRuneRepository : IRuneRepository
    {
        public ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId,
            CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes,
            byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingCharacterShardLocationRepository : ICharacterShardLocationRepository
    {
        public ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
            CancellationToken ct)
        {
            throw new InvalidOperationException("Must not be reached once world-entry is already rejected.");
        }

        public ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<CharacterShardLocationDto?> FindByNameAsync(string avatarName, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<CharacterShardLocationDto?> FindByCharacterIdAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoOpMuteRepository : IMuteRepository
    {
        public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
        {
            return ValueTask.FromResult(false);
        }

        public ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
            CancellationToken ct)
        {
            return ValueTask.FromResult(ImmutableArray<int>.Empty);
        }
    }

    private sealed class EmptyFriendRepository : IFriendRepository
    {
        public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
            CancellationToken ct)
        {
            return ValueTask.FromResult(new ReadOnlyCollection<CharacterFriendDto>([]));
        }

        public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoMentorRepository : IMentorRepository
    {
        public ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct)
        {
            return ValueTask.FromResult<CharacterMentorDto?>(null);
        }

        public ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

        private sealed class RoleOnlyTribeRepository(byte role) : ITribeRepository
    {
        public ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
        {
            return ValueTask.FromResult(role);
        }

        public ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
