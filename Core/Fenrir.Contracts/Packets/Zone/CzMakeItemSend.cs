using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_MAKE_ITEM_SEND (CLIENT.h:278-289) — typedef SHARED with <see cref="CzMakeSkillSend" /> (30),
///     <see cref="CzMakePetSend" /> (88), <see cref="CzMakeItem2Send" /> (131): identical layout, distinct
///     C# contracts. 4-ingredient craft; <see cref="Sort" /> is an <c>MK_*</c> constant (make_item.cfg.h,
///     values differ per LNW33 build); unknown <c>tSort</c> → Quit. Response: ZC 32.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MakeItemSend, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzMakeItemSend : IIncomingPacket<CzMakeItemSend>
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
