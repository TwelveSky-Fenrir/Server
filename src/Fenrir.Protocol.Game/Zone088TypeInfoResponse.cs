using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_88_TYPE_INFO ZONE.h:1161-1165; mort en M33/LNW33: aucun emetteur, les 3 seules references sont dans ZONE.h.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Zone088TypeInfo,
    ExpectedSize = 9)]
public readonly partial record struct Zone088TypeInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
