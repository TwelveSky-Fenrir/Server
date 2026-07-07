using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IGuildRepository: only the single-guild lookup/buff-write surface used by the
///     handler test suites is exercised here; every other member is out of scope.
/// </summary>
internal sealed class FakeGuildRepository : IGuildRepository
{
    private readonly Dictionary<int, GuildSummaryDto> _guilds = new();
    private readonly Dictionary<int, List<GuildNoticeRowDto>> _notices = new();
    private readonly Dictionary<int, List<GuildRosterRowDto>> _rosters = new();

    private int _nextGuildId = 1;

    public bool ThrowOnSetBuff { get; set; }
    public bool ThrowOnCreateAndDebitMoney { get; set; }
    public bool ThrowOnUpgradeAndDebitMoney { get; set; }

    public (string Name, int MasterCharacterId, long DeltaMoney, int DeltaBigMoney)? LastCreateAndDebitMoney
    {
        get;
        private set;
    }

    public (int GuildId, int Grade, int CharacterId, long DeltaMoney, int DeltaBigMoney)? LastUpgradeAndDebitMoney
    {
        get;
        private set;
    }

    /// <summary>
    ///     GuildId/CharacterId DisbandAsync was last called with -- proves GuildActionService.DisbandGuildAsync
    ///     threads the acting master's characterId through (usp_Guild_Disband needs it to attribute the
    ///     guild-money audit row it now writes; see IGuildRepository.DisbandAsync's own doc comment).
    /// </summary>
    public (int GuildId, int CharacterId)? LastDisband { get; private set; }

    public (int GuildId, int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff)? LastSetBuff
    {
        get;
        private set;
    }

    /// <summary>Scripted return for <see cref="GetByCharacterAsync" /> -- null (the default) means "no guild".</summary>
    public CharacterGuildMembershipDto? MembershipToReturn { get; set; }

    public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
    {
        return ValueTask.FromResult(_guilds.GetValueOrDefault(guildId));
    }

    public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<GuildSummaryDto>(_guilds.Values.ToList()));
    }

    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct)
    {
        var top = _guilds.Values
            .OrderByDescending(g => g.Points)
            .ThenBy(g => g.GuildId)
            .Take(count)
            .Select(g => new GuildRankingRowDto(g.GuildId, g.Name, g.Points))
            .ToList();

        return ValueTask.FromResult(new ReadOnlyCollection<GuildRankingRowDto>(top));
    }

    public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
        CancellationToken ct)
    {
        if (ThrowOnSetBuff)
            throw new InvalidOperationException("Simulated SQL failure");

        LastSetBuff = (guildId, buffType, buffState, buffTime, buffTimeForDiff);

        if (_guilds.TryGetValue(guildId, out var guild))
            _guilds[guildId] = guild with
            {
                BuffType = buffType, BuffState = buffState, BuffTime = buffTime, BuffTimeForDiff = buffTimeForDiff
            };

        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(MembershipToReturn);
    }

    public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct)
    {
        var rows = _rosters.TryGetValue(guildId, out var existing) ? existing : [];
        return ValueTask.FromResult(new ReadOnlyCollection<GuildRosterRowDto>(rows));
    }

    public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct)
    {
        var rows = _notices.TryGetValue(guildId, out var existing) ? existing : [];
        return ValueTask.FromResult(new ReadOnlyCollection<GuildNoticeRowDto>(rows));
    }

    public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct)
    {
        if (ThrowOnCreateAndDebitMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastCreateAndDebitMoney = (name, masterCharacterId, deltaMoney, deltaBigMoney);

        var guildId = _nextGuildId++;
        _guilds[guildId] = new GuildSummaryDto(guildId, name, 1, masterCharacterId,
            0, 0, 0, 0, 0, 0,
            DateTime.UtcNow, 1);

        return ValueTask.FromResult(guildId);
    }

    public ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct)
    {
        LastDisband = (guildId, characterId);
        _guilds.Remove(guildId);

        return ValueTask.CompletedTask;
    }

    public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct)
    {
        if (_rosters.TryGetValue(guildId, out var rows))
        {
            var index = rows.FindIndex(r => r.CharacterId == characterId);
            if (index >= 0)
                rows[index] = rows[index] with { Role = role };
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct)
    {
        if (_rosters.TryGetValue(guildId, out var rows))
        {
            var index = rows.FindIndex(r => r.CharacterId == characterId);
            if (index >= 0)
                rows[index] = rows[index] with { CallName = callName };
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpgradeAndDebitMoneyAsync(int guildId, int grade, int characterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct)
    {
        if (ThrowOnUpgradeAndDebitMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastUpgradeAndDebitMoney = (guildId, grade, characterId, deltaMoney, deltaBigMoney);

        if (_guilds.TryGetValue(guildId, out var guild))
            _guilds[guildId] = guild with { Grade = grade };

        return ValueTask.CompletedTask;
    }

    public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct)
    {
        var list = _notices.TryGetValue(guildId, out var existing) ? existing : _notices[guildId] = [];
        var index = list.FindIndex(n => n.NoticeIndex == noticeIndex);
        var row = new GuildNoticeRowDto(guildId, noticeIndex, text, DateTime.UtcNow);

        if (index >= 0)
            list[index] = row;
        else
            list.Add(row);

        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustPointsAsync(int guildId, int delta, CancellationToken ct)
    {
        if (_guilds.TryGetValue(guildId, out var guild))
            _guilds[guildId] = guild with { Points = guild.Points + delta };

        return ValueTask.CompletedTask;
    }

    public void Seed(GuildSummaryDto guild)
    {
        _guilds[guild.GuildId] = guild;
    }

    public void SeedRoster(int guildId, params GuildRosterRowDto[] rows)
    {
        _rosters[guildId] = [..rows];
    }
}
