namespace Fenrir.Application.Game.Social;

/// <summary>
///     Translates between game.GuildMembers' DB-side role enum (0 member, 1 sub-master, 2 master -- the
///     table's own header comment) and the legacy wire's <c>aGuildRole</c> encoding (0 master, 1
///     sub-master, 2 member -- VERIFIED against the actual write sites, not the DB comment: master gets
///     role 0 (<c>S04_MyWork02.cpp:10148</c> assigns a fresh member role 2; the "Guild Transfer Leader"
///     handler at l.10613-10664 demotes the outgoing master to 2 and promotes the incoming one to 0; the
///     guild-master-only gate throughout the same file is literally <c>aGuildRole != 0 =&gt; return</c>)).
///     This is a real, easy-to-miss inversion between the two layers -- the DB migration's own comment is
///     backwards relative to the wire, a documented lesson-learned catch from this pass (do not compare
///     the raw DB byte against a wire constant, or vice versa, without going through this type).
/// </summary>
public static class GuildRoleCodec
{
    /// <summary>DB role (0/1/2) -&gt; wire <c>aGuildRole</c> (2/1/0). Self-inverting: master/member swap, sub-master (1) is unchanged.</summary>
    public static int DbRoleToWire(byte dbRole)
    {
        return dbRole switch
        {
            0 => 2, // DB "member" -> wire "member"
            1 => 1, // sub-master -> sub-master
            2 => 0, // DB "master" -> wire "master"
            _ => 2
        };
    }

    /// <summary>
    ///     Wire <c>aGuildRole</c> (0/1/2) -&gt; DB role (2/1/0) -- GUILD_WORK tSort 9's own request encoding
    ///     (1=promote to sub-master, 2=demote to member, doc 10 §1) uses the SAME 0=master/1=sub-master/
    ///     2=member wire convention as AVATAR_INFO's own <c>aGuildRole</c>. <see cref="DbRoleToWire" /> is
    ///     mathematically self-inverting (a pure swap of 0/2, 1 fixed), so this delegates to it rather than
    ///     duplicating the same switch -- kept as its own named method purely so call sites read correctly
    ///     ("wire to DB" vs "DB to wire"), not because the underlying logic differs.
    /// </summary>
    public static byte WireRoleToDb(int wireRole)
    {
        return (byte)DbRoleToWire((byte)wireRole);
    }

    /// <summary>True when <paramref name="dbRole" /> is the guild's master (DB role 2) -- the legacy's <c>aGuildRole != 0</c> master-only gate, expressed against the DB encoding.</summary>
    public static bool IsMaster(byte dbRole)
    {
        return dbRole == 2;
    }

    /// <summary>True when <paramref name="dbRole" /> is master OR sub-master (DB role 1 or 2) -- the legacy's <c>aGuildRole != 0 &amp;&amp; != 1</c> management gate, expressed against the DB encoding.</summary>
    public static bool IsMasterOrSubMaster(byte dbRole)
    {
        return dbRole is 1 or 2;
    }
}
