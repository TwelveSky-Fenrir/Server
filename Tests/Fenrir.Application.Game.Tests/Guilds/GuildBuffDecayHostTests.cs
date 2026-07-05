using Fenrir.Application.Game.Hosting.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffDecayHostTests
{
    [Fact]
    public async Task DecayOnceAsync_ExpiredGuild_PersistsTheDeactivatedRow()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, buffState: 1, buffTime: 5, buffTimeForDiff: DateTime.UtcNow.AddMinutes(-10).Ticks));
        var host = new GuildBuffDecayHost(repository, NullLogger<GuildBuffDecayHost>.Instance);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.NotNull(repository.LastSetBuff);
        var (guildId, buffType, buffState, buffTime, _) = repository.LastSetBuff!.Value;
        Assert.Equal(1, guildId);
        Assert.Equal(0, buffType);
        Assert.Equal(0, buffState);
        Assert.Equal(0, buffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_NothingActive_NeverCallsSetBuff()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, buffState: 0, buffTime: 0, buffTimeForDiff: 0));
        var host = new GuildBuffDecayHost(repository, NullLogger<GuildBuffDecayHost>.Instance);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.Null(repository.LastSetBuff);
    }

    [Fact]
    public async Task DecayOnceAsync_ActiveWithReserveStillRemaining_PersistsTheDecrementedTime()
    {
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, buffState: 1, buffTime: 60, buffTimeForDiff: DateTime.UtcNow.AddMinutes(-7).Ticks));
        var host = new GuildBuffDecayHost(repository, NullLogger<GuildBuffDecayHost>.Instance);

        await host.DecayOnceAsync(CancellationToken.None);

        Assert.NotNull(repository.LastSetBuff);
        var (_, _, buffState, buffTime, _) = repository.LastSetBuff!.Value;
        Assert.Equal(1, buffState);
        Assert.Equal(53, buffTime);
    }

    [Fact]
    public async Task DecayOnceAsync_RepositoryScanThrows_DoesNotPropagate()
    {
        var host = new GuildBuffDecayHost(new ThrowingGuildRepository(), NullLogger<GuildBuffDecayHost>.Instance);

        await host.DecayOnceAsync(CancellationToken.None); // must not throw
    }

    private static GuildSummaryDto Guild(int guildId, int buffState, int buffTime, long buffTimeForDiff)
    {
        return new GuildSummaryDto(guildId, "Aesir", Grade: 1, MasterCharacterId: null, Points: 0,
            BuffType: 2, BuffState: buffState, BuffTime: buffTime, BuffTimeForDiff: buffTimeForDiff, Logo: 0,
            CreatedAtUtc: DateTime.UtcNow, MemberCount: 1);
    }

    private sealed class ThrowingGuildRepository : IGuildRepository
    {
        public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask<System.Collections.ObjectModel.ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(
            CancellationToken ct) => throw new InvalidOperationException("Simulated SQL failure");

        public ValueTask<System.Collections.ObjectModel.ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(
            int count, CancellationToken ct) => throw new NotSupportedException();

        public ValueTask<System.Collections.ObjectModel.ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(
            int guildId, CancellationToken ct) => throw new NotSupportedException();

        public ValueTask<System.Collections.ObjectModel.ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(
            int guildId, CancellationToken ct) => throw new NotSupportedException();

        public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask DisbandAsync(int guildId, CancellationToken ct) => throw new NotSupportedException();

        public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
            CancellationToken ct) => throw new NotSupportedException();

        public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
