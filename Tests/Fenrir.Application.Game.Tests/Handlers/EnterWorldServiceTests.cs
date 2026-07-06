using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.Handlers.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Security;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Compression;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

// op12 ZC_REGISTER_AVATAR_RECV -- cluster C02: a GM-banned character (admin.Bans) and a firewalled IP must both
// abort world-entry before any other repository is ever touched. Both fixtures below reuse the plain
// FakeCharacterRepository, whose GetWorldEntryBundleAsync throws NotSupportedException by its own documented
// scope -- if either new check failed to short-circuit, the service would blow up with that exception instead
// of aborting cleanly, so a clean Faulted abort is itself proof the character bundle was never fetched.
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

    // Covers the bridge-enterworld-spawn/bridge-stats-equipment contracts' EnterWorldService gaps: BuffInfo/
    // EffectValueForView must reflect a returning character's persisted buffs (not the all-zero template),
    // WorldInfo must reflect the live WorldStateService RvR snapshot (not the all-zero template), and the
    // self-spawn ObjectForAvatar's PreviousTribe/top-level PetLocation must reflect the persisted character
    // (not a flat 0/zeroed array).
    [Fact]
    public async Task HandleAsync_ReturningCharacter_PopulatesBuffsWorldStateAndPreviousTribe_InsteadOfZeros()
    {
        const short MapId = 7;
        // Tribe 3 (fourth faction) with PreviousTribe 2 is the one legitimate case the two fields differ
        // (Server/ts25zone/S04_MyWork02.cpp:880-901) -- deliberately chosen so this fixture both stays
        // internally consistent under EnterWorldService's own Tribe/PreviousTribe gate and still proves
        // PreviousTribe isn't synthesized as Tribe.
        const byte Tribe = 3;
        const byte PreviousTribe = 2;
        const float PosX = 111f, PosY = 5f, PosZ = 222f;

        var buffs = new List<CharacterBuffDto>
        {
            new(SlotIndex: 0, Value: 42, RemainingLegacyTicks: 100),
            new(SlotIndex: 5, Value: 7, RemainingLegacyTicks: 3)
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

        var service = new EnterWorldService(
            characters,
            worldData,
            zones,
            new NoOpMuteRepository(),
            new FakeBanRepository(false),
            new ApplicationFirewall(new FakeBlockedIpRepository(false), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            guilds,
            guildRanking,
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeCharacterShardLocationRepository(),
            worldState,
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);

        // 1) EnterWorldResponse: BuffInfo must reflect the persisted buffs, not the all-zero template.
        var expectedEnterWorld = new EnterWorldResponse
        {
            AvatarInfo = AvatarInfoFactory.CreateForCharacter(bundle.Character, bundle.Items,
                AvatarSocialSnapshot.Empty),
            BuffInfo = ExpectedBuffInfo(buffs)
        };
        var enterWorldActual = await PacketAssert.ReadSentBytesAsync(pipe);
        Assert.Equal(ZoneMessageFactory.Encode(in expectedEnterWorld), enterWorldActual);

        // 2) WorldSnapshotResponse: WorldInfo must reflect the live WorldStateService snapshot, not zeros.
        var expectedWorldSnapshot = new WorldSnapshotResponse
        {
            WorldInfo = WorldStateProjection.Apply(
                GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top), worldState),
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        var worldSnapshotActual = await PacketAssert.ReadSentBytesAsync(pipe);
        Assert.Equal(ZoneMessageFactory.Encode(in expectedWorldSnapshot), worldSnapshotActual);

        // 3) Self-spawn AvatarActionResponse: PreviousTribe/EffectValueForView/top-level PetLocation must
        // reflect the persisted character -- decoded via ObjectForAvatar's own TryRead rather than
        // reproducing all ~50 sibling fields by hand (Data starts 8 bytes into the 1-byte-opcode-prefixed,
        // uncompressed payload: ServerIndex(int) + UniqueNumber(uint) precede it).
        var selfSpawnActual = await PacketAssert.ReadSentBytesAsync(pipe);
        var selfSpawnPayload = selfSpawnActual.AsSpan(1);
        Assert.True(ObjectForAvatar.TryRead(selfSpawnPayload.Slice(8, ObjectForAvatar.WireSize), out var selfSpawnData));
        Assert.Equal(PreviousTribe, selfSpawnData.PreviousTribe);
        Assert.Equal([PosX, PosY, PosZ], selfSpawnData.PetLocation);
        Assert.Equal(ExpectedEffectValueForView(buffs), selfSpawnData.EffectValueForView);
    }

    // Covers the bridge-tribe-validation contract's Behavior C (Server/ts25zone/S04_MyWork02.cpp:880-901): the
    // just-loaded character record's own Tribe/PreviousTribe must be internally consistent, or the session
    // ends outright with no response -- this validates the server's own data against itself, never anything
    // the client claims.
    [Theory]
    [InlineData((byte)0, (byte)1)] // main-faction Tribe, PreviousTribe != Tribe
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)2, (byte)0)]
    [InlineData((byte)3, (byte)3)] // fourth faction, PreviousTribe outside {0,1,2}
    [InlineData((byte)3, (byte)5)]
    [InlineData((byte)4, (byte)0)] // Tribe itself outside 0-3
    [InlineData((byte)200, (byte)0)]
    public async Task HandleAsync_TribeAndPreviousTribeInternallyInconsistent_AbortsSession(byte tribe,
        byte previousTribe)
    {
        var bundle = HappyPathBundle(mapId: 7, tribe, previousTribe, posX: 0f, posY: 0f, posZ: 0f, buffs: []);
        var service = CreateService(out var session, bundle: bundle);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData((byte)0, (byte)0)] // main-faction Tribe, PreviousTribe == Tribe
    [InlineData((byte)1, (byte)1)]
    [InlineData((byte)2, (byte)2)]
    [InlineData((byte)3, (byte)0)] // fourth faction, PreviousTribe one of the three original tribes
    [InlineData((byte)3, (byte)1)]
    [InlineData((byte)3, (byte)2)]
    public async Task HandleAsync_TribeAndPreviousTribeInternallyConsistent_DoesNotAbort(byte tribe,
        byte previousTribe)
    {
        var bundle = HappyPathBundle(mapId: 7, tribe, previousTribe, posX: 0f, posY: 0f, posZ: 0f, buffs: []);
        var (service, session) = CreateWorkingService(bundle);

        await service.HandleAsync(ValidRequest(EncodeObfuscatedAccountId(AccountId)), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    private static CharacterWorldEntryBundle HappyPathBundle(short mapId, byte tribe, byte previousTribe,
        float posX, float posY, float posZ, IReadOnlyList<CharacterBuffDto> buffs)
    {
        var character = new CharacterWorldSnapshotDto(
            CharacterId: CharacterId, AccountId: AccountId, Slot: 0, Name: "Hero", Tribe: tribe, Gender: 0,
            HeadType: 1, FaceType: 1, Level: 10, MapId: mapId, PosX: posX, PosY: posY, PosZ: posZ, Heading: 0f,
            Life: 30, MaxLife: 30, Mana: 21, MaxMana: 21, FlushSequence: 1, Experience: 0, Level2: 0, StatVit: 1,
            StatStr: 1, StatInt: 1, StatDex: 1, StatPoints: 0, SkillPoints: 0, Money: 0, BigMoney: 0, StoreMoney: 0,
            BigStoreMoney: 0, RebirthCount: 0, Title: 0, Halo: 0, ContributionPoints: 0, EatLifePotion: 0,
            EatManaPotion: 0, EatStrPotion: 0, EatDexPotion: 0, EatElePotion: 0, ProtectForDeath: 0,
            ProtectForDestroy: 0, DoubleExpTime1: 0, DoubleExpTime2: 0, DropItemTime: 0, InventoryDate: 0,
            StoreDate: 0, QuestStepPermanent: 0, QuestActiveId: 0, QuestSort: 0, QuestTargetPhase: 0,
            QuestKillCounter: 0, JoinWar: 0, MissionKillOtherTribe: 0, MissionKillMonster: 0, MissionPlayTime: 0,
            AutoHuntEnabled: false, AutoHuntConfig: [], AutoLifeRatio: 0, AutoManaRatio: 0, PetGrowth: 0,
            PetActivity: 0, TeacherPoint: 0, AutoBuffTime: 0, PremiumExpireUtc: 0, Exp2: 0,
            PreviousTribe: previousTribe, MountItemId: 0, MountExpActivity: 0, MountPower: 0, MountSlotIndex: 0,
            MountTime: 0);

        return new CharacterWorldEntryBundle(
            character,
            new ReadOnlyCollection<CharacterItemSlotDto>([]),
            new ReadOnlyCollection<CharacterSkillDto>([]),
            new ReadOnlyCollection<CharacterHotkeyDto>([]),
            new ReadOnlyCollection<CharacterBuffDto>([..buffs]));
    }

    /// <summary>Mirrors EnterWorldService's own BuildBuffInfo (private) for this test's own small, hand-picked fixture.</summary>
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

    /// <summary>Mirrors EnterWorldService's own BuildEffectValueForView (private) for this test's own fixture.</summary>
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
            new ThrowingCharacterShardLocationRepository(),
            ZoneTestKit.CreateWorldState(),
            Options.Create(options),
            NullLogger<EnterWorldService>.Instance);
    }

    /// <summary>
    ///     Unlike <see cref="CreateService" /> (whose Throwing* fakes exist specifically to prove an abort
    ///     happens before those repositories are ever touched), this builds a fully working service so a
    ///     Tribe/PreviousTribe-consistent request can run to completion without an unrelated fake throwing.
    /// </summary>
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
            new FakeBanRepository(false),
            new ApplicationFirewall(new FakeBlockedIpRepository(false), new FakeFirewallRuleRepository(),
                new FakeGmAllowlistRepository()),
            new FakeGuildRepository(),
            new GuildRankingCache(),
            new RoleOnlyTribeRepository(0),
            new EmptyFriendRepository(),
            new NoMentorRepository(),
            new FakeCharacterShardLocationRepository(),
            ZoneTestKit.CreateWorldState(),
            Options.Create(ZoneTestKit.Options()),
            NullLogger<EnterWorldService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        return (service, session);
    }

    /// <summary>Mirrors ObfuscatedUidCodec.TryDecodeAccountId's encoding half: Latin1("MG"+id), then USE_XOR_UID.</summary>
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
            // Never decoded by the two abort tests below: both abort before ObfuscatedUidCodec ever looks at
            // this field. A happy-path test that reaches that check must pass a real encoded id instead.
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

    /// <summary>Only <see cref="GetRoleForCharacterAsync" /> is exercised by EnterWorldService; every other member is out of scope.</summary>
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
