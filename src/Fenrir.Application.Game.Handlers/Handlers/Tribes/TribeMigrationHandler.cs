using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeMigrationHandler(ILogger<TribeMigrationHandler>? logger = null)
    : IAsyncPacketHandler<TribeMigrationRequest>
{
    public ValueTask HandleAsync(TribeMigrationRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        // Server/ts25zone/S04_MyWork02.cpp:7380-7384 -- legacy's first statement here is an unconditional Quit();
        // every branch below it in the shipped build is compiler-visible dead code.
        logger?.LogDebug(
            "Session {SessionId}: CZ_CHANGE_TO_TRIBE4_SEND received (character {CharacterId}) -- disconnecting unconditionally",
            session.SessionId, zoneSession.CharacterId);

        zoneSession.Abort(DisconnectReason.Faulted);
        return ValueTask.CompletedTask;
    }
}
