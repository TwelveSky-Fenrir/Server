namespace Fenrir.Application.Game.Domain.Social;

public static class GuildRoleCodec
{
    public static int DbRoleToWire(byte dbRole)
    {
        return dbRole switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            _ => 2
        };
    }

    public static byte WireRoleToDb(int wireRole)
    {
        return (byte)DbRoleToWire((byte)wireRole);
    }

    public static bool IsMaster(byte dbRole)
    {
        return dbRole == 2;
    }

    public static bool IsMasterOrSubMaster(byte dbRole)
    {
        return dbRole is 1 or 2;
    }
}
