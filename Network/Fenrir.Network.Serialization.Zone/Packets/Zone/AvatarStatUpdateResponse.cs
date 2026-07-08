using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// USE_PREMIUM_LONGTIME is on, so Value2 is present (12-byte payload, not 8).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStatUpdate, ExpectedSize = 13)]
public readonly partial record struct AvatarStatUpdateResponse : IOutgoingPacket
{
    /// <summary>Stat code (S010CHARACTER_HP, S011CHARACTER_MP, S019MONEY, S014PET_EXP, S904UPDATE_HERO_POINT, ...).</summary>
    public required int Sort { get; init; }

    public required int Value { get; init; }

    /// <summary>Present due to <c>USE_PREMIUM_LONGTIME</c>; 0 in most emissions.</summary>
    public required int Value2 { get; init; }
}
