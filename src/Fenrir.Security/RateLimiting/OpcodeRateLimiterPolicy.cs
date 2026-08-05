using Fenrir.Core.Opcodes;
using Fenrir.Core.Wire;

namespace Fenrir.Security.RateLimiting;

public static class OpcodeRateLimiterPolicy
{
    private static readonly (int Capacity, double TokensPerSecond) LoginAttempt = (60, 5d);

    private static readonly (int Capacity, double TokensPerSecond) ZoneAuth = (30, 5d);

    private static readonly (int Capacity, double TokensPerSecond) AvatarActionUpdate = (512, 240d);

    private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (120, 30d);

    private static readonly (int Capacity, double TokensPerSecond) Attack = (512, 240d);

    private static readonly (int Capacity, double TokensPerSecond) Interaction = (240, 120d);

    private static readonly (int Capacity, double TokensPerSecond) Default = (240, 120d);

    public static readonly (int Capacity, double TokensPerSecond) GmCommand = (60, 20d);


    public static (int Capacity, double TokensPerSecond) PolicyFor(FenrirServer server, byte opcode)
    {
        return (server, opcode) switch
        {
            (FenrirServer.Login, Opcodes.Login.Incoming.Loggedin) => LoginAttempt,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.ZoneHandshake) => ZoneAuth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.EnterWorld) => ZoneAuth,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarAction) => AvatarActionUpdate,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarActionResume) => AvatarActionUpdate,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Heartbeat) => Heartbeat,


            (FenrirServer.Zone, Opcodes.Zone.Incoming.Attack) => Attack,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.GenericAction) => Interaction,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.UseHotkeyItem) => Interaction,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.UseInventoryItem) => Interaction,

            _ => Default
        };
    }
}
