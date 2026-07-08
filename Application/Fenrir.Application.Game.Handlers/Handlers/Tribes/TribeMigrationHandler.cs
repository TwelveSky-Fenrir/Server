using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

/// <summary>
///     CZ_CHANGE_TO_TRIBE4_SEND (op37) -- the fourth-tribe (Fujin) conversion/return behavior. Thin: resolves
///     the session/zone/character state and delegates every precondition and side effect to
///     <see cref="ITribeMigrationService" />, then maps the resulting <see cref="TribeMigrationOutcome" /> onto
///     the legacy's own documented reply-vs-disconnect split -- see that enum's own remarks for why only three
///     rejection kinds reply at all and every other one silently disconnects instead.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7565-7758, per the "Fourth-tribe (Fujin) conversion and
///     return" legacy-behavior-translator contract. This opcode was compiled but unreachable in every shipped
///     legacy build -- its handler unconditionally disconnected the client as the very first statement
///     (Server/ts25zone/S04_MyWork02.cpp:7567-7568) -- and becomes reachable here for the first time.
/// </remarks>
public sealed class TribeMigrationHandler(
    ITribeMigrationService tribeMigrationService,
    ILogger<TribeMigrationHandler>? logger = null) : IAsyncPacketHandler<TribeMigrationRequest>
{
    public async ValueTask HandleAsync(TribeMigrationRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_CHANGE_TO_TRIBE4_SEND received (character {CharacterId})",
            session.SessionId, zoneSession.CharacterId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        TribeMigrationOutcome outcome;
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            outcome = await tribeMigrationService.ConvertAsync(zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (outcome)
        {
            case TribeMigrationOutcome.Success:
                session.Send(new TribeMigrationResponse { Result = 0 });
                return;
            case var repliesWithFailure when repliesWithFailure.RepliesWithFailure():
                session.Send(new TribeMigrationResponse { Result = 1 });
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
