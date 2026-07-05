namespace Fenrir.Application.Game.Domain.Social;

/// <summary>
///     Translates game.GuildMembers' DB role (0 member, 1 sub-master, 2 master) to/from the legacy wire's
///     aGuildRole encoding (0 master, 1 sub-master, 2 member) -- verified against actual write sites
///     (S04_MyWork02.cpp:10148, 10613-10664), not the DB migration's own comment, which is backwards
///     relative to the wire. Never compare a raw DB role byte against a wire constant directly -- always go
///     through this type.
/// </summary>
public static class GuildRoleCodec
{
    /// <summary>DB role (0/1/2) -> wire aGuildRole (2/1/0). Self-inverting: master/member swap, sub-master (1) unchanged.</summary>
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
    ///     DbRoleToWire is self-inverting, so this just delegates to it -- kept separate only so call sites read
    ///     correctly.
    /// </summary>
    public static byte WireRoleToDb(int wireRole)
    {
        return (byte)DbRoleToWire((byte)wireRole);
    }

    /// <summary>True when dbRole is the guild's master (DB role 2).</summary>
    public static bool IsMaster(byte dbRole)
    {
        return dbRole == 2;
    }

    /// <summary>True when dbRole is master or sub-master (DB role 1 or 2).</summary>
    public static bool IsMasterOrSubMaster(byte dbRole)
    {
        return dbRole is 1 or 2;
    }
}
