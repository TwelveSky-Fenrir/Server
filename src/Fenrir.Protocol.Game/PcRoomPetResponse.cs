using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_PCROOM_PET_RECV Server/Header/Protocol/ZONE.h:1108-1116 ; mort en M33/LNW33 : orphelin total, les seules occurrences du nom dans tout Server/ sont les trois lignes de ZONE.h (struct, ZCP, ZCS).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PcRoomPet,
    ExpectedSize = 25)]
public readonly partial record struct PcRoomPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Xy { get; init; }

    public required int Value { get; init; }
}
