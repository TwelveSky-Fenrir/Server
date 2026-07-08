using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// 428B payload: WCDEV3 off (no tCompress), MAX_AVATAR_EFFECT_SORT_NUM=35 -> 4+4+35*2*4+35*4.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarEffectState,
    ExpectedSize = 429)]
public readonly partial record struct AvatarEffectStateResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }

    /// <summary>
    ///     <c>int tEffectValue[35][2]</c>, flattened row-major: [buff i][0]=value, [buff i][1]=duration.
    /// </summary>
    [FixedArray(70)]
    public required int[] EffectValue { get; init; }

    [FixedArray(35)] public required int[] EffectValueState { get; init; }
}
