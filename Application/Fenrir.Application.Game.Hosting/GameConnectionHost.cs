using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>Owns the zone listen socket; the GameServer-side twin of <c>Fenrir.LoginServer.LoginConnectionHost</c>.</summary>
public sealed class GameConnectionHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zones,
    IFrameDispatcher dispatcher,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    IWriteBehindFlusher writeBehindFlusher,
    IAccountSessionRepository accountSessions,
    IEventLogRepository eventLog,
    IpFloodGuard ipFloodGuard,
    TribeQuotaRegistry tribeQuota,
    ILogger<GameConnectionHost> logger) : BackgroundService
{
    /// <summary>
    ///     game.EventLog.EventCode for a character leaving a live zone (Category=Session, "Logout") -- see
    ///     Fenrir.Application.Login.Services.Login.LoginService's LoginSucceededEventCode remarks for the full
    ///     four-code cross-reference within this category.
    /// </summary>
    private const short LogoutEventCode = 4;

    private FenrirTcpListener<ZoneClientSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener<ZoneClientSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            static (sessionId, transport, remoteEndPoint) =>
                new ZoneClientSession(sessionId, transport, remoteEndPoint));

        logger.LogInformation("GameServer listening on port {Port} (shard {ShardId}, maps [{Maps}])", opts.Port,
            opts.ShardId, string.Join(", ", zones.Zones.Select(z => z.MapId).Order()));

        try
        {
            await _listener.AcceptLoopAsync(OnAcceptedAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task OnAcceptedAsync(ZoneClientSession zoneSession, SocketConnection connection, CancellationToken ct)
    {
        registry.Register(zoneSession);

        // Captured once: RemoteEndPoint is fixed at accept time (SocketConnection's own remark), and both the
        // acquire and the matching release below must key on the exact same string.
        var remoteIp = zoneSession.RemoteEndPoint?.Address.ToString();

        try
        {
            // Trigger A (contract): the concurrent-connection gauge must already be incremented and read back
            // before any connect-acknowledgement is sent (Server/ts25zone/S03_MyUser.cpp:460-474's early-return
            // ordering) -- so this runs before Greet, not after.
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
                return; // IP just got persistently blocked and every session sharing it (this one included) aborted

            Greet(zoneSession, connection);

            await Task.WhenAll(
                connection.RunIOAsync(ct),
                SessionLoop.RunAsync(zoneSession, dispatcher, rateLimiter, ipFloodGuard, ct)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Zone session {SessionId} ended", zoneSession.SessionId);
        }
        finally
        {
            if (remoteIp is not null)
                ipFloodGuard.ReleaseConnection(remoteIp);

            // A session that never completed world registration has no CurrentZone -- nothing to remove, nothing to flush.
            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                // A dropped Leave (full inbox) can't be retried here, and would otherwise leave a permanent phantom
                // in the zone's player list -- log loudly rather than silently discard.
                if (!zone.Post(ZoneCommand.Leave(characterId)))
                    logger.LogError(
                        "Zone {MapId} inbox full: dropped Leave for character {CharacterId} on disconnect -- character remains a phantom in the zone until its next Move/handoff",
                        zone.MapId, characterId);

                // Immediate flush: the periodic 5 s/512-entity flush would otherwise leave this player's last position unpersisted.
                writeBehindFlusher.RequestImmediateFlush();

                await LogLogoutAsync(zoneSession.AccountId, characterId, zone.MapId).ConfigureAwait(false);
            }

            if (zoneSession.AccountId is { } accountId)
                await TearDownAccountSessionAsync(accountId, zoneSession.AccountSessionToken).ConfigureAwait(false);

            // Unconditional and idempotent: frees this connection's counted tribe-quota slot regardless of how
            // far it got (never temp-registered at all, stalled before avatar-selection, or fully in-world) --
            // same "disconnect always clears the flag" posture as legacy's own S03_MyUser.cpp:340-411.
            tribeQuota.Release(zoneSession.SessionId);

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Best-effort cross-process cleanup of this account's <c>runtime.AccountSessions</c> row -- must never
    ///     throw out of a connection-teardown path. <see cref="IAccountSessionRepository.ClearIfOwnerAsync" />
    ///     idempotently no-ops if the row already moved on (e.g. a newer login already replaced it).
    /// </summary>
    private async ValueTask TearDownAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        try
        {
            await accountSessions.MarkTearingDownAsync(accountId, CancellationToken.None).ConfigureAwait(false);
            await accountSessions
                .ClearIfOwnerAsync(accountId, AccountSessionServerKind.Game, options.Value.ShardId,
                    sessionToken ?? default, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to tear down runtime.AccountSessions row for account {AccountId}",
                accountId);
        }
    }

    /// <summary>
    ///     Best-effort game.EventLog audit row for a character leaving a live zone (Category=Session, "Logout")
    ///     -- must never throw out of a connection-teardown path, same posture as
    ///     <see cref="TearDownAccountSessionAsync" /> above. Fired only when the session actually reached
    ///     <see cref="ZoneClientSession.CurrentZone" /> (see this method's caller: an authenticated ticket that
    ///     never got past zone registration has nothing meaningful to log here, same reasoning as the
    ///     Leave-command/immediate-flush guard right above it).
    /// </summary>
    private async ValueTask LogLogoutAsync(int? accountId, int characterId, short mapId)
    {
        try
        {
            await eventLog.LogAsync(LogoutEventCode, EventLogCategory.Session, accountId, characterId, null, null,
                options.Value.ShardId, null, null, null, null, 1, $"MapId={mapId}", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write game.EventLog row for logout (character {CharacterId})",
                characterId);
        }
    }

    /// <summary>Unlike Login's op 0, <c>ZC_CONNECT_OK_RECV</c> carries no packet-level XOR, only the stream cipher it seeds.</summary>
    private static void Greet(ZoneClientSession session, SocketConnection connection)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new ZoneGreetingResponse { RandomNumber = randomNumber });
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
