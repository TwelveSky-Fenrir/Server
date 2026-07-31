using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_REFINE_ITEM_RECV ZONE.h:1154-1159; mort en M33/LNW33: les 4 appelants sont sous #ifdef USE_REFINE, undef DEFINE.h:106.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SmeltItem,
    ExpectedSize = 13)]
public readonly partial record struct SmeltItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Cost { get; init; }
    public required int Value { get; init; }
}
