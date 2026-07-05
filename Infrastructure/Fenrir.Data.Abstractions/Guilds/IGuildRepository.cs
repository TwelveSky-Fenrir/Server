using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Guilds;

public interface IGuildRepository
{
    public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct);

    /// <summary>Every guild, master/buff/points included -- game.usp_Guild_GetAll (used by GuildBuffDecayHost's periodic scan).</summary>
    public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Top <paramref name="count" /> guilds by Points, highest first -- game.usp_Guild_GetTopByPoints.</summary>
    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct);

    public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct);

    public ValueTask DisbandAsync(int guildId, CancellationToken ct);

    public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct);

    public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct);

    public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct);

    public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct);

    public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct);

    public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
        CancellationToken ct);

    public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct);
}
