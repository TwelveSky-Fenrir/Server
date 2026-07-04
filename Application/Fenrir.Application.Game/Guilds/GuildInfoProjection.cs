using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Guilds;

namespace Fenrir.Application.Game.Guilds;

/// <summary>
///     Pure projection from the normalized game.Guilds/GuildMembers/GuildNotices rows onto the wire's
///     GUILD_INFO struct (ZC_GUILD_WORK_RECV, Core/Fenrir.Contracts/Packets/Shared/GuildInfo.cs -- 50
///     fixed member slots, contracts/06_guild_tribe.md). The legacy's own slot ASSIGNMENT (which of the
///     50 gMember array slots a member occupies) has no Fenrir equivalent -- normalized storage has no
///     slot index at all, only presence -- so slots here are filled in <see cref="GuildRosterRowDto.JoinedAtUtc" />
///     order (oldest member first); nothing in the wire contract or any client behavior documented in the
///     reports depends on WHICH of the 50 slots a name occupies, only on presence/role/call-name, so this
///     is a safe, documented inference, not a guess at gameplay-affecting behavior.
/// </summary>
public static class GuildInfoProjection
{
    private const int MaxMembers = 50;
    private const int MaxNotices = 4;

    /// <summary>
    ///     Zero-filled GUILD_INFO for every failure response (and the legacy's own tSort 1001 success quirk,
    ///     doc 10 §1 quirk 4) -- Fenrir's replacement for the legacy's uninitialized-stack-garbage leak on
    ///     those same responses.
    /// </summary>
    public static GuildInfo Empty()
    {
        return new GuildInfo
        {
            Name = "",
            Grade = 0,
            Master = "",
            SubMaster1 = "",
            SubMaster2 = "",
            MemberNames = NewEmptyStringArray(MaxMembers),
            MemberRoles = new int[MaxMembers],
            MemberCallNames = NewEmptyStringArray(MaxMembers),
            Notices = NewEmptyStringArray(MaxNotices),
            Point = 0,
            BuffType = 0,
            BuffState = 0,
            BuffTime = 0,
            ChangeLeader = 0
        };
    }

    /// <summary>Builds the full GUILD_INFO for a real guild from its already-loaded rows.</summary>
    public static GuildInfo Build(GuildSummaryDto guild, IReadOnlyList<GuildRosterRowDto> roster,
        IReadOnlyList<GuildNoticeRowDto> notices)
    {
        var memberNames = NewEmptyStringArray(MaxMembers);
        var memberRoles = new int[MaxMembers];
        var memberCallNames = NewEmptyStringArray(MaxMembers);
        var noticeTexts = NewEmptyStringArray(MaxNotices);

        var master = "";
        var subMaster1 = "";
        var subMaster2 = "";

        // Oldest-joined-first slot assignment -- see class remarks. Ordered defensively here (rather than
        // trusting the caller's own query ORDER BY) since slot 0 must be deterministic across callers.
        var ordered = roster.OrderBy(r => r.JoinedAtUtc).ToArray();
        for (var i = 0; i < ordered.Length && i < MaxMembers; i++)
        {
            var row = ordered[i];
            memberNames[i] = row.CharacterName;
            memberRoles[i] = row.Role;
            memberCallNames[i] = row.CallName;

            switch (row.Role)
            {
                case 2:
                    master = row.CharacterName;
                    break;
                case 1:
                    if (subMaster1.Length == 0)
                        subMaster1 = row.CharacterName;
                    else if (subMaster2.Length == 0)
                        subMaster2 = row.CharacterName;
                    break;
            }
        }

        foreach (var notice in notices)
            if (notice.NoticeIndex < MaxNotices)
                noticeTexts[notice.NoticeIndex] = notice.Text;

        return new GuildInfo
        {
            Name = guild.Name,
            Grade = guild.Grade,
            Master = master,
            SubMaster1 = subMaster1,
            SubMaster2 = subMaster2,
            MemberNames = memberNames,
            MemberRoles = memberRoles,
            MemberCallNames = memberCallNames,
            Notices = noticeTexts,
            Point = guild.Points,
            BuffType = guild.BuffType,
            BuffState = guild.BuffState,
            BuffTime = guild.BuffTime,
            ChangeLeader = 0
        };
    }

    private static string[] NewEmptyStringArray(int length)
    {
        var array = new string[length];
        Array.Fill(array, "");
        return array;
    }
}
