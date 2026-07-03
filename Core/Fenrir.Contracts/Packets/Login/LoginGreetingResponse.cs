using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.LoginGreeting,
    Obfuscation = LegacyObfuscation.XorPacketGlobal, ExpectedSize = 37)]
public readonly partial record struct LoginGreetingResponse : IOutgoingPacket
{
    // tPad0..tPad4: 5 ints (offsets 1..20), never written by the server (dead padding).
    [Reserved(20)] public required int RandomNumber { get; init; }
    public required int MaxPlayerNum { get; init; }
    public required int GagePlayerNum { get; init; }
    public required int PresentPlayerNum { get; init; }
}
