using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

// MAX_AVATAR_EFFECT_SORT_NUM resolves to 35 (not the generic 32): MG5ORIGIN is unconditional and
// KR is undefined in this build, so aBuff[35][2] flattens to 70 ints.
[FenrirWireType(280)]
public readonly partial record struct BuffInfo : IFenrirWireType<BuffInfo>
{
    [FixedArray(70)] public required int[] Buff { get; init; }
}
