using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarEffectState,
    ExpectedSize = 429)]
public readonly partial record struct AvatarEffectStateResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }

    [FixedArray(70)] public required int[] EffectValue { get; init; }

    [FixedArray(35)] public required int[] EffectValueState { get; init; }
}
