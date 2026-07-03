using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_MAKE_PET_SEND (CLIENT.h:289) — same typedef as <see cref="CraftItemRequest" /> (29). Pet
///     crafting/fusion, 6 recipes <c>CMAKE_PET_1..6</c>. Response: <c>ZC_MAKE_PET_RECV</c> (op 105, 29
///     bytes, outside this lot) + <c>MakeNotice</c> announcement on success.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CraftPet, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftPetRequest : IIncomingPacket<CraftPetRequest>
{
    public required int Sort { get; init; }

    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Page3 { get; init; }

    public required int Index3 { get; init; }

    public required int Page4 { get; init; }

    public required int Index4 { get; init; }
}
