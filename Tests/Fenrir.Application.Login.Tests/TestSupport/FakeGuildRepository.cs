using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeGuildRepository : IGuildRepository
{
    private readonly Dictionary<int, CharacterGuildMembershipDto> _membershipByCharacterId = new();

    public List<int> QueriedCharacterIds { get; } = [];

    public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
    {
        QueriedCharacterIds.Add(characterId);
        return ValueTask.FromResult(_membershipByCharacterId.GetValueOrDefault(characterId));
    }

    public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ImmutableArray<GuildSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ReadOnlyCollection<GuildRankingDetailDto>> GetRankingAsync(int count, CancellationToken ct)
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

    public static FakeGuildRepository Empty()
    {
        return new FakeGuildRepository();
    }

    public static FakeGuildRepository WithMembership(int characterId, CharacterGuildMembershipDto membership)
    {
        var repository = new FakeGuildRepository();
        repository._membershipByCharacterId[characterId] = membership;
        return repository;
    }
}
