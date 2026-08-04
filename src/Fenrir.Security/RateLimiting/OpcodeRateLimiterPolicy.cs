using Fenrir.Core.Opcodes;
using Fenrir.Core.Wire;

namespace Fenrir.Security.RateLimiting;

public static class OpcodeRateLimiterPolicy
{
    private static readonly (int Capacity, double TokensPerSecond) LoginAttempt = (12, 0.5d);

    private static readonly (int Capacity, double TokensPerSecond) ZoneAuth = (3, 1d / 5d);

    private static readonly (int Capacity, double TokensPerSecond) Movement = (10, 30d);

    private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (10, 2d);

    private static readonly (int Capacity, double TokensPerSecond) Attack = (40, 20d);

    private static readonly (int Capacity, double TokensPerSecond) Interaction = (25, 12d);

    private static readonly (int Capacity, double TokensPerSecond) Default = (5, 5d);

    public static readonly (int Capacity, double TokensPerSecond) GmCommand = (3, 1d);


    public static (int Capacity, double TokensPerSecond) PolicyFor(FenrirServer server, byte opcode)
    {
        return (server, opcode) switch
        {
            (FenrirServer.Login, Opcodes.Login.Incoming.Loggedin) => LoginAttempt,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.ZoneHandshake) => ZoneAuth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.EnterWorld) => ZoneAuth,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarAction) => Movement,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarActionResume) => Movement,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Heartbeat) => Heartbeat,


            (FenrirServer.Zone, Opcodes.Zone.Incoming.Attack) => Attack,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.GenericAction) => Interaction,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.UseHotkeyItem) => Interaction,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.UseInventoryItem) => Interaction,

            _ => Default
        };
    }
}
