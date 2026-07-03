using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.LoginRecv,
    Obfuscation = LegacyObfuscation.XorPacketGlobal, ExpectedSize = 693)]
public readonly partial record struct LcLoginRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    // Id: "MG"+decimal(uUserIdx), pre-XORed over its strlen (USE_XOR_UID) before the packet-wide XOR
    // is applied on top -> double-XOR, see wire contract §3.3.
    [FixedString(255)] [LegacyUidField] public required string Id { get; init; }
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
