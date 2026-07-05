using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.Login,
    Obfuscation = WireObfuscationMode.XorPacketGlobal, ExpectedSize = 693)]
public readonly partial record struct LoginResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    // Pre-XORed (USE_XOR_UID) before the packet-wide XOR is applied on top -> double-XOR.
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
