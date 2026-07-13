using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.LoginGreeting,
    Obfuscation = WireObfuscationMode.XorPacketGlobal, ExpectedSize = 37)]
public readonly partial record struct LoginGreetingResponse : IOutgoingPacket
{
    [Reserved(20)] public required int RandomNumber { get; init; }
    public required int MaxPlayerNum { get; init; }
    public required int GagePlayerNum { get; init; }
    public required int PresentPlayerNum { get; init; }
}
