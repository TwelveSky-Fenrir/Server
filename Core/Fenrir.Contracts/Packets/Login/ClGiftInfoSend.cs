using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_GIFT_INFO_SEND (CLIENT.h l.117-119, header-only): ask for the 10-page gift list; answered by
///     LC_DEMAND_GIFT_RECV (login protocol report §4.25). Same second-login gate as ops 17/18/19/21/22.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.GiftInfoSend,
    ExpectedSize = 9,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClGiftInfoSend : IIncomingPacket<ClGiftInfoSend>
{
}
