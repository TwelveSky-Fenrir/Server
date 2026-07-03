using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op25 CL_GIFT_INFO_SEND — the 10-page gift list (login protocol report §4.25). The legacy reloads
///     <c>uGiftInfo</c> from MemberInfo and answers <c>LC_DEMAND_GIFT_RECV(0, [10][2])</c> with
///     <c>[i][0]=uGiftInfo[i], [i][1]=0</c>; in EU33 that array is only ever fed by the web-API gift path, whose
///     port is deferred to chantier V8 — so the byte-exact answer for every current account is Result=0 with an
///     all-zero matrix, which is what this handler pins. No SQL ⇒ inline. Replace the static reply with the real
///     gift storage read when V8 lands.
/// </summary>
public sealed class ClGiftInfoSendHandler : IInlinePacketHandler<ClGiftInfoSend>
{
    // Safe to share across sessions: Write() only ever reads the array, and nothing else can reach this instance.
    private static readonly LcDemandGiftRecv NoGifts = new() { Result = 0, GiftItem = new int[20] };

    public void Handle(in ClGiftInfoSend packet, IPacketSession session)
    {
        session.Send(NoGifts);
    }
}
