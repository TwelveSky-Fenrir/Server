using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     Level-gap check uses <see cref="PlayerRuntimeState.CombinedLevel" /> (aLevel1+aLevel2) on both sides,
///     per the party-invite level-gate behavior contract.
/// </summary>
public sealed class PartyInviteService(PartyRegistry parties) : IPartyInviteService
{
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so an already-partied-and-not-leader or still-negotiating inviter
    ///     naming a nonexistent/offline target gets that outcome, not "target not found". These two
    ///     inviter-only checks mirror <see cref="PartyRegistry.TryInvite" />'s own already-verified internal
    ///     ordering (see that method's "Check order verified" remark) and are therefore run ahead of the
    ///     by-name target lookup below; <see cref="PartyRegistry.TryInvite" /> itself still performs the
    ///     full check sequence (including these two again) for the actual registration.
    ///     <para>
    ///         Party-invite level-gate combined-level extension: Server/ts25zone/S04_MyWork02.cpp:9608-9614
    ///         (combined-level computation for both sides, feeding <see cref="PartyRegistry.TryInvite" />'s own
    ///         already-generic <c>inviterCumulativeLevel</c>/<c>inviteeCumulativeLevel</c> parameters) ;
    ///         Server/ts25zone/S04_MyWork02.cpp:9603-9607 (the tribe/alliance disconnect check immediately
    ///         preceding, confirming check order is unaffected by this).
    ///     </para>
    /// </remarks>
    public PartyInviteResult Invite(Zone zone, PlayerRuntimeState inviter, string targetAvatarName)
    {
        if (parties.IsInParty(inviter.CharacterId) && !parties.IsLeader(inviter.CharacterId))
            return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
        if (parties.IsNegotiating(inviter.CharacterId))
            return new PartyInviteResult(PartyInviteResultKind.InviterBusy);

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return new PartyInviteResult(PartyInviteResultKind.TargetNotFound);

        var outcome = parties.TryInvite(inviter.CharacterId, inviter.CombinedLevel, inviter.Tribe,
            target.CharacterId, target.CombinedLevel, target.Tribe);

        return outcome switch
        {
            PartyInviteOutcome.InviterMustDisconnect => new PartyInviteResult(PartyInviteResultKind
                .InviterMustDisconnect),
            PartyInviteOutcome.InviterBusy => new PartyInviteResult(PartyInviteResultKind.InviterBusy),
            PartyInviteOutcome.TargetBusy => new PartyInviteResult(PartyInviteResultKind.TargetBusy),
            PartyInviteOutcome.TargetAlreadyPartied =>
                new PartyInviteResult(PartyInviteResultKind.TargetAlreadyPartied),
            _ => new PartyInviteResult(PartyInviteResultKind.Sent, target.CharacterId, target.Name, inviter.Name)
        };
    }
}
