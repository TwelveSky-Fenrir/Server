using Fenrir.Core.Opcodes;
using Fenrir.Core.Wire;

namespace Fenrir.Security.RateLimiting;

public static class OpcodeRateLimiterPolicy
{
    private static readonly (int Capacity, double TokensPerSecond) Auth = (3, 1d / 5d);

    private static readonly (int Capacity, double TokensPerSecond) Movement = (10, 30d);

    private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (2, 1d / 5d);

    private static readonly (int Capacity, double TokensPerSecond) Attack = (8, 4d);

    private static readonly (int Capacity, double TokensPerSecond) Default = (5, 5d);


    public static (int Capacity, double TokensPerSecond) PolicyFor(FenrirServer server, byte opcode)
    {
        return (server, opcode) switch
        {
            (FenrirServer.Login, Opcodes.Login.Incoming.Loggedin) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.ZoneHandshake) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.EnterWorld) => Auth,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarAction) => Movement,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarActionResume) => Movement,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Heartbeat) => Heartbeat,


            (FenrirServer.Zone, Opcodes.Zone.Incoming.Attack) => Attack,

            _ => Default
        };
    }
}
