using System.Collections.ObjectModel;
using Fenrir.Application.Game.Hosting.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffDecayHostTests
{
    private static GuildBuffDecayHost CreateHost(IGuildRepository repository,
        FakeGuildBuffExpiryRelayQueue? relay = null,
        byte shardId = 1)
    {
        var options = ZoneTestKit.Options();
        options.ShardId = shardId;
        return new GuildBuffDecayHost(repository, ZoneTestKit.CreateRegistry(options),
            relay ?? new FakeGuildBuffExpiryRelayQueue(), Options.Create(options),
            NullLogger<GuildBuffDecayHost>.Instance);
    }

    [Fact]
    public async Task DecayOnceAsync_ExpiredGuild_PersistsOnlyTheFlooredBuffTime_LeavesTypeAndStateUntouched()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 1, 5, DateTime.UtcNow.AddMinutes(-10).Ticks));
        var host = CreateHost(repository);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.NotNull(repository.LastSetBuff);
        var (guildId, buffType, buffState, buffTime, _) = repository.LastSetBuff!.Value;
        Assert.Equal(1, guildId);
        Assert.Equal(2, buffType);
        Assert.Equal(1, buffState);
        Assert.Equal(0, buffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_NothingActive_NeverCallsSetBuff()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 0, 0, 0));
        var host = CreateHost(repository);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.Null(repository.LastSetBuff);
    }

    [Fact]
    public async Task DecayOnceAsync_NeverActivated_ButReserveAndCheckpointBothPresent_StillPersistsTheDecrementedTime()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 0, 60, DateTime.UtcNow.AddMinutes(53).AddSeconds(30).Ticks));
        var host = CreateHost(repository);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.NotNull(repository.LastSetBuff);
        var (_, _, buffState, buffTime, _) = repository.LastSetBuff!.Value;
        Assert.Equal(0, buffState);
        Assert.Equal(53, buffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_ActiveWithReserveStillRemaining_PersistsTheDecrementedTime()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 1, 60, DateTime.UtcNow.AddMinutes(53).AddSeconds(30).Ticks));
        var host = CreateHost(repository);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.NotNull(repository.LastSetBuff);
        var (_, _, buffState, buffTime, _) = repository.LastSetBuff!.Value;
        Assert.Equal(1, buffState);
        Assert.Equal(53, buffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_RepositoryScanThrows_DoesNotPropagate()
    {
        var host = CreateHost(new ThrowingGuildRepository());

        await host.DecayOnceAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DecayOnceAsync_ReserveReachesZero_EnqueuesOneClusterWideExpiryPush()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 1, 5, DateTime.UtcNow.AddMinutes(-10).Ticks));
        var relay = new FakeGuildBuffExpiryRelayQueue();
        var host = CreateHost(repository, relay, 3);

        await host.DecayOnceAsync(CancellationToken.None);

        var entry = Assert.Single(relay.Enqueued);
        Assert.Equal(3, entry.SourceShardId);
        Assert.Equal(1, entry.GuildId);
        Assert.Equal(0, entry.NewBuffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_ReserveStillPositive_NeverEnqueuesAnExpiryPush()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 1, 60, DateTime.UtcNow.AddMinutes(53).AddSeconds(30).Ticks));
        var relay = new FakeGuildBuffExpiryRelayQueue();
        var host = CreateHost(repository, relay);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task DecayOnceAsync_AlreadyExhausted_StructurallyNeverReselected_NeverReEnqueues()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, 1, 0, DateTime.UtcNow.AddMinutes(-10).Ticks));
        var relay = new FakeGuildBuffExpiryRelayQueue();
        var host = CreateHost(repository, relay);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.Null(repository.LastSetBuff);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task DecayOnceAsync_PersistFails_NeverEnqueuesAnExpiryPush()
    {
        var relay = new FakeGuildBuffExpiryRelayQueue();
        var host = CreateHost(new ThrowingSetBuffGuildRepository(), relay);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.Empty(relay.Enqueued);
    }

    private static GuildSummaryDto Guild(int guildId, int buffState, int buffTime, long buffTimeForDiff)
    {
        return new GuildSummaryDto(guildId, "Aesir", 1, null, 0,
            2, buffState, buffTime, buffTimeForDiff, 0,
            DateTime.UtcNow, 1);
    }

    private sealed class ThrowingGuildRepository : IGuildRepository
    {
        public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(
            CancellationToken ct)
        {
            throw new InvalidOperationException("Simulated SQL failure");
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

        private sealed class ThrowingSetBuffGuildRepository : IGuildRepository
    {
        public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct)
        {
            return ValueTask.FromResult(new ReadOnlyCollection<GuildSummaryDto>(
                [Guild(1, 1, 5, DateTime.UtcNow.AddMinutes(-10).Ticks)]));
        }

        public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct)
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
            throw new InvalidOperationException("Simulated SQL failure");
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
}
