using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — claim a gift page into the shared chest (login protocol report §4.21, V1 path:
///     GIFT_V2 is off in EU33). Bounds violation (index outside 0..9) reproduces the legacy Quit(); a claim on an
///     empty page replies <c>Result=1</c> ("no gift") — and in EU33 every page IS empty on this wire:
///     <c>uGiftInfo</c> is only ever populated by the web-API path (<c>mUseWebApi</c>), whose port is deferred to
///     chantier V8 together with the account chest transfer (codes 0/2/101). Until then this static
///     <c>Result=1</c> IS the exact legacy answer for the whole EU33 population. No SQL ⇒ inline.
/// </summary>
public sealed class ClWantGiftSendHandler : IInlinePacketHandler<ClWantGiftSend>
{
    private const int MaxGiftPageIndex = 9; // MAX_GIFT_ITEM_PAGE_NUM - 1

    public void Handle(in ClWantGiftSend packet, IPacketSession session)
    {
        if (packet.GiftInfoIndex is < 0 or > MaxGiftPageIndex)
        {
            ((LoginClientSession)session).Abort(DisconnectReason.Malformed);
            return;
        }

        session.Send(new LcWantGiftRecv { Result = 1 });
    }
}
