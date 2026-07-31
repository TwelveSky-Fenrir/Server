using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_SAMGUNDO_CENTER_INFO ZONE.h:1167-1172, Tribe demarre a l'offset 18 non aligne sous pack(1); mort en M33/LNW33: aucun emetteur, les 3 seules references sont dans ZONE.h.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SamgundoCenterInfo,
    ExpectedSize = 22)]
public readonly partial record struct SamgundoAnnouncementResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }

    public required int Tribe { get; init; }
}
