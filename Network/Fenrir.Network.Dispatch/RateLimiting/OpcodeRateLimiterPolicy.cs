using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

public static class OpcodeRateLimiterPolicy
{

        private static readonly (int Capacity, double TokensPerSecond) Auth = (3, 1d / 5d);

        private static readonly (int Capacity, double TokensPerSecond) Movement = (10, 30d);

        private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (2, 1d / 5d);

        private static readonly (int Capacity, double TokensPerSecond) Attack = (8, 4d);

        private static readonly (int Capacity, double TokensPerSecond) Default = (5, 5d);

    static OpcodeRateLimiterPolicy()
    {
        _ = new TokenBucket(Auth.Capacity, Auth.TokensPerSecond);
        _ = new TokenBucket(Movement.Capacity, Movement.TokensPerSecond);
        _ = new TokenBucket(Heartbeat.Capacity, Heartbeat.TokensPerSecond);
        _ = new TokenBucket(Attack.Capacity, Attack.TokensPerSecond);
        _ = new TokenBucket(Default.Capacity, Default.TokensPerSecond);
    }

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
