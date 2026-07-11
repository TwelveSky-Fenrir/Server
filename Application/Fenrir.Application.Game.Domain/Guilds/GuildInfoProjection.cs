using Fenrir.Application.Game.Domain.Social;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildInfoProjection
{
    private const int MaxMembers = 50;
    private const int MaxNotices = 4;

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

        var ordered = roster.OrderBy(r => r.JoinedAtUtc).ToArray();
        for (var i = 0; i < ordered.Length && i < MaxMembers; i++)
        {
            var row = ordered[i];
            memberNames[i] = row.CharacterName;
            memberRoles[i] = GuildRoleCodec.DbRoleToWire(row.Role);
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

        var buffActive = guild.BuffTime > 0 && guild.BuffState != 0;

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
            BuffState = buffActive ? 1 : 0,
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
