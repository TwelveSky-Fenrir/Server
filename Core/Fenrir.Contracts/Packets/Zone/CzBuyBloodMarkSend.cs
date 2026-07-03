using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_BUY_BLOOD_MARK_SEND (CLIENT.h:361-367) — purchase a blood-mark catalog item. <c>BloodIndex</c>
///     must be within <c>[0, mBloodShop.aBloodNum)</c>. Registered under <c>USE_BLOOD</c> (on in EU33).
///     Reply: ZC_BUY_BLOOD_MARK_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyBloodMarkSend, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzBuyBloodMarkSend : IIncomingPacket<CzBuyBloodMarkSend>
{
    public required int BloodIndex { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
