namespace Fenrir.Application.Game.Social;

/// <summary>
///     Translates game.GuildMembers' DB role (0 member, 1 sub-master, 2 master) to/from the legacy
///     wire's <c>aGuildRole</c> encoding (0 master, 1 sub-master, 2 member) -- VERIFIED against actual
///     write sites (<c>S04_MyWork02.cpp:10148</c>, 10613-10664), not the DB migration's own comment, which
///     is backwards relative to the wire. Never compare a raw DB role byte against a wire constant
///     directly -- always go through this type.
/// </summary>
public static class GuildRoleCodec
{
    /// <summary>
    ///     DB role (0/1/2) -&gt; wire <c>aGuildRole</c> (2/1/0). Self-inverting: master/member swap, sub-master (1) is
    ///     unchanged.
    /// </summary>
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
    ///     Wire <c>aGuildRole</c> -&gt; DB role. GUILD_WORK tSort 9 uses the same wire convention as
    ///     AVATAR_INFO's <c>aGuildRole</c>, and <see cref="DbRoleToWire" /> is self-inverting, so this
    ///     just delegates to it -- kept as a separate named method only so call sites read correctly.
    /// </summary>
    public static byte WireRoleToDb(int wireRole)
    {
        return (byte)DbRoleToWire((byte)wireRole);
    }

    /// <summary>
    ///     True when <paramref name="dbRole" /> is the guild's master (DB role 2) -- the legacy's <c>aGuildRole != 0</c>
    ///     master-only gate, expressed against the DB encoding.
    /// </summary>
    public static bool IsMaster(byte dbRole)
    {
        return dbRole == 2;
    }

    /// <summary>
    ///     True when <paramref name="dbRole" /> is master OR sub-master (DB role 1 or 2) -- the legacy's
    ///     <c>aGuildRole != 0 &amp;&amp; != 1</c> management gate, expressed against the DB encoding.
    /// </summary>
    public static bool IsMasterOrSubMaster(byte dbRole)
    {
        return dbRole is 1 or 2;
    }
}
