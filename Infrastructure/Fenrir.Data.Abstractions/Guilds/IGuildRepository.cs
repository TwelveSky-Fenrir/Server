using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Guilds;

public interface IGuildRepository
{
    public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct);

    // Polled every 30s by GuildBuffDecayHost -- ImmutableArray is the polled/hot-path terminal call shape,
    // not ReadOnlyCollection (request/response shape).
    public ValueTask<ImmutableArray<GuildSummaryDto>> GetAllAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct);

    /// <summary>
    ///     Cross-shard guild leaderboard read via <c>game.vw_GuildRanking</c>: same TOP(count)/GuildId-tiebreak
    ///     shape as <see cref="GetTopByPointsAsync" />, plus each guild's live <c>MemberCount</c> and a computed
    ///     <c>RankNo</c> (ties share a rank). Points is a hot, cluster-wide-shared counter written from whichever
    ///     shard's RvR kill processing fires, so this is always a live SQL read, never a per-shard cache.
    /// </summary>
    public ValueTask<ReadOnlyCollection<GuildRankingDetailDto>> GetRankingAsync(int count, CancellationToken ct);

    public ValueTask AdjustPointsAsync(int guildId, int delta, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct);

    public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct);

    public ValueTask<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct);

    public ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct);

    public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct);

    public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct);

    public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct);

    public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct);

    public ValueTask UpgradeAndDebitMoneyAsync(int guildId, int grade, int characterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct);

    public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
        CancellationToken ct);

    public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct);
}
