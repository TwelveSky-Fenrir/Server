using System.Collections.ObjectModel;
using System.Net;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.Handlers.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Security;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
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

    private static EnterWorldService CreateService(out ZoneClientSession session, bool characterBanned = false,
        bool blockedIp = false)
    {
        var pipe = new FakeDuplexPipe();
        session = new ZoneClientSession(1, pipe, RemoteEndPoint);
        session.MarkTicketConsumed(AccountId, CharacterId);

        var options = ZoneTestKit.Options();
        var zones = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(),
            Array.Empty<ISimulationSystem>());
        zones.Initialize([]);

        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(blockedIp),
            new FakeFirewallRuleRepository(),
            new FakeGmAllowlistRepository());

        return new EnterWorldService(
            new FakeCharacterRepository(),
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
            Options.Create(options),
            NullLogger<EnterWorldService>.Instance);
    }

    private static EnterWorldRequest ValidRequest()
    {
        return new EnterWorldRequest
        {
            // Never decoded: both new checks abort before ObfuscatedUidCodec ever looks at this field.
            Id = "irrelevant",
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
}
