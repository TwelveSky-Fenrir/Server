using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob
///     for tSort 700, the unlabeled GM pet-experience-grant command -- no dedicated wire opcode, multiplexed
///     inside the shared envelope, same posture as <see cref="GmCreateItemPayload" />/<see cref="GmBlockAvatarPayload" />.
///     Both fields are write-only from the server's own perspective: neither PetId nor PetExperience is ever
///     read as client input by the legacy handler (PetId is unconditionally overwritten with the "no pet"
///     sentinel before the equipped-pet check even runs; PetExperience is never read at all). This type is
///     therefore only ever used to WRITE the echoed response fields into <see cref="GenericActionResponse" />
///     .Data, never to decode an inbound request.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:2062-2083 (case 700 body: privilege gate, unconditional
///     result-code assignment, unconditional PetId overwrite to the "no pet" sentinel, the equipped-pet-slot/
///     catalog-sort-code eligibility check, the conditional PetExperience write) ; Server/Header/Protocol/
///     STRUCT.h:1296-1300 (request/response payload shape -- PetId then PetExperience, both confirmed
///     write-only from the server's side for this command).
///     <para>
///         The "no pet" sentinel's exact legacy numeric value is now confirmed as -1:
///         Server/ts25zone/S04_MyWork04.cpp:2071 (<c>r-&gt;tPetID = -1;</c>, executed unconditionally right
///         after the tResult=0 assignment, before the equipped-pet-slot check) and :2078
///         (<c>r-&gt;tPetID = tITEM_INFO-&gt;iIndex;</c>, only reached once an eligible equipped pet is found).
///         GM_PETEXP.tPetID is a signed int (STRUCT.h:1296-1300), so -1 is representable on the wire and
///         distinct from 0. <see cref="Fenrir.Application.Game.Services.Gm.GmPetExperienceGrantService" />
///         uses -1, matching this confirmed value -- deliberately diverging from this codebase's own
///         otherwise-established "0 = no pet equipped" convention
///         (<see cref="Fenrir.Application.Game.Domain.Pets.PetSlots.ResolveEquippedPetItemId" />,
///         <see cref="Fenrir.Application.Game.Domain.Pets.PetGrowthCalculator.Compute" />'s own
///         <c>petItemId == 0</c> guard) because this specific field is a distinct, independently confirmed
///         wire sentinel, not a shared internal representation.
///     </para>
/// </remarks>
[FenrirWireType(8)]
public readonly partial record struct GmPetExperienceGrantPayload : IFenrirWireType<GmPetExperienceGrantPayload>
{
    /// <summary>
    ///     Echoed back: the "no pet" sentinel if no eligible pet was equipped, or the equipped item's own
    ///     catalog item id if one was (regardless of whether a growth grant actually happened for it).
    /// </summary>
    public required int PetId { get; init; }

    /// <summary>
    ///     Only meaningfully set when an eligible pet was found and a nonzero grant was actually computed;
    ///     0 otherwise (this codebase never leaves it holding a stale/uninitialized buffer value the way the
    ///     legacy implementation's un-zeroed local does).
    /// </summary>
    public required int PetExperience { get; init; }
}
