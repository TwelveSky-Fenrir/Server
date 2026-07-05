using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.LoginGreeting,
    Obfuscation = WireObfuscationMode.XorPacketGlobal, ExpectedSize = 37)]
public readonly partial record struct LoginGreetingResponse : IOutgoingPacket
{
    // tPad0..tPad4: 5 ints (offsets 1..20), never written by the server (dead padding).
    [Reserved(20)] public required int RandomNumber { get; init; }
    public required int MaxPlayerNum { get; init; }
    public required int GagePlayerNum { get; init; }
    public required int PresentPlayerNum { get; init; }
}
