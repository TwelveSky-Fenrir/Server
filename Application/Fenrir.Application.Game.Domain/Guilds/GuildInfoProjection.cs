using Fenrir.Application.Game.Domain.Social;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>
///     Projects game.Guilds/GuildMembers/GuildNotices onto GUILD_INFO's 50 fixed slots. Normalized storage has no
///     slot index, so slots fill oldest-first by JoinedAtUtc.
/// </summary>
public static class GuildInfoProjection
{
    private const int MaxMembers = 50;
    private const int MaxNotices = 4;

    /// <summary>
    ///     Zero-filled GUILD_INFO for failure responses -- replaces the legacy's uninitialized-stack-garbage leak on
    ///     those responses.
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

        // Sorted here, not trusted from the caller's query -- slot 0 must be deterministic across callers.
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

        // Legacy parity (Server/ts25login/S08_MyDB.cpp:661-662): "is the buff active" is derived fresh at
        // read time as BuffTime > 0 AND BuffState != 0, never read as a pre-computed persisted flag --
        // GuildBuffDecay.Apply intentionally leaves BuffState set (sticky) once a guild has ever activated a
        // buff, even after its reserve is exhausted, so this projection is the one place that must compute
        // "active" instead of passing the column through. BuffType, by contrast, is copied unconditionally
        // (line 662), regardless of whether the derived active flag below is true.
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
