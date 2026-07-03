using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_AVATAR_EFFECT_VALUE_INFO (ZONE.h:406-415, 428-byte payload): <c>WCDEV3</c> off in EU33 so there is no
///     leading <c>tCompress</c>, and <c>MAX_AVATAR_EFFECT_SORT_NUM</c> resolves to 35 (MG5ORIGIN branch, DEFINE.h:
///     331-333) -&gt; 4 + 4 + 35*2*4 + 35*4 = 428. Buff/hotkey engine snapshot, unicast + AOI broadcast.
/// </summary>
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
