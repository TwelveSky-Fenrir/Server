using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     The displayed level sent to the target on a successful ask is <see cref="PlayerRuntimeState.CombinedLevel" />
///     (aLevel1+aLevel2), per the trade-ask displayed-level behavior contract -- a direct sum with no
///     additional offset/clamp at this site.
/// </summary>
public sealed class TradeInviteService(TradeRegistry trades) : ITradeInviteService
{
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so a busy asker naming a nonexistent/offline target gets the busy
    ///     reply, not "target not found". <see cref="TradeRegistry.IsBusy" /> is therefore checked ahead of
    ///     the by-name target lookup below; the same check inside <see cref="TradeRegistry.TryAsk" /> stays
    ///     in place for the actual registration.
    ///     <para>
    ///         Trade-ask displayed-level combined-level extension: Server/ts25zone/S04_MyWork02.cpp:8456-8517
    ///         (full CZ_TRADE_ASK_SEND handler) ; Server/ts25zone/S04_MyWork02.cpp:8514 (the exact outward
    ///         value -- asker's ordinary level plus high level, sent verbatim with no offset) ;
    ///         Server/ts25zone/S04_MyWork02.cpp:8478-8485 (the three designated inter-tribe zone numbers and
    ///         the tribe/alliance disconnect check, confirmed unaffected by this).
    ///     </para>
    /// </remarks>
    public TradeInviteResult Invite(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (trades.IsBusy(asker.CharacterId))
            return new TradeInviteResult(TradeInviteResultKind.AskerBusy);

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return new TradeInviteResult(TradeInviteResultKind.TargetNotFound);

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe)
            return new TradeInviteResult(TradeInviteResultKind.MustDisconnect);

        return trades.TryAsk(asker.CharacterId, target.CharacterId) switch
        {
            TradeAskOutcome.AskerBusy => new TradeInviteResult(TradeInviteResultKind.AskerBusy),
            TradeAskOutcome.TargetBusy => new TradeInviteResult(TradeInviteResultKind.TargetBusy),
            _ => new TradeInviteResult(TradeInviteResultKind.Sent, target.CharacterId, target.Name, asker.Name,
                asker.CombinedLevel)
        };
    }
}
