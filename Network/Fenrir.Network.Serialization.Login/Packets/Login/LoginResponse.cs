using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.Loggedin,
    Obfuscation = WireObfuscationMode.XorPacketGlobal, ExpectedSize = 693)]
public readonly partial record struct LoginResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(255)]
    [ObfuscatedUidField]
    public required string Id { get; init; }

    public required int UserSort { get; init; }
    public required int GoodFellow { get; init; }
    public required int LoginPlace { get; init; }
    public required int LoginPremium { get; init; }
    public required int SecondLoginSort { get; init; }
    [FixedString(5)] public required string MousePassword { get; init; }
    public required int SecretCardIndex01 { get; init; }
    public required int SecretCardIndex02 { get; init; }
    [FixedArray(50)] public required int[] GiftInfo { get; init; }
    [FixedString(200)] public required string ResultString { get; init; }
}
