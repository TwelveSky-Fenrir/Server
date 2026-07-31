using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_PET_MOVE_RECV Server/Header/Protocol/ZONE.h:1405-1409 ; mort en M33/LNW33 : emetteur B_PET_MOVE_RECV compile sans USEND (S05_MyTransfer.cpp:1893-1898) et sans aucun appelant.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PetMove, ExpectedSize = 17)]
public readonly partial record struct PetMoveResponse : IOutgoingPacket
{
    [FixedArray(3)] public required float[] Location { get; init; }

    public required float Frame { get; init; }
}
