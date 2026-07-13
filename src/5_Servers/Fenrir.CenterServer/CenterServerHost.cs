using System.Collections.Concurrent;
using System.Net;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.CenterServer;

/// <summary>
/// Cycle de vie du CenterServer : un serveur TCP <b>interne et passif</b> (fidèle à <c>ts25center</c> <c>:12003</c>)
/// qui accepte des liens serveur-à-serveur entrants (Zones, LoginServer) et n'ouvre <b>aucun</b> TCP sortant de
/// jeu. Boot <b>paresseux</b> : l'accept-loop est armée d'abord, la découverte des pairs est paresseuse, aucun
/// <c>connect()</c> bloquant au démarrage (Topologie <c>03_</c> §2.4). Réutilise la pile <c>Fenrir.Network</c>
/// (<see cref="FenrirTcpListener{TSession}"/> + <see cref="SocketConnection"/>) — socket + pipes + teardown
/// gracieux — plutôt que de réécrire une pile socket.
/// </summary>
/// <remarks>
/// <para><b>Périmètre de CE lot (substrat S2S minimal-viable) :</b> listener + cycle de vie de connexion + drain
/// gracieux à l'arrêt. Le dispatch S2S par opcode et le handshake authentifié sont des TODO explicites (voir
/// <see cref="RunLinkAsync"/>), bloqués en amont sur deux livrables d'autres domaines : (1) les paquets
/// <c>[FenrirPacket]</c> <c>FenrirServer.Center</c> + le <c>CenterOpcodeRegistry.Provider</c> +
/// <c>CenterFrameDispatcher</c> générés (wire-protocol/source-generator), et (2) un vérificateur de secret partagé
/// dans <c>Fenrir.Security</c>. Tant qu'ils n'existent pas, un lien accepté est tenu ouvert et drainé proprement,
/// sans interprétation de trame.</para>
/// <para><b>Cadre S2S = en-tête d'opcode 1 octet, sans length-prefix</b> (taille par opcode via registre généré) —
/// c'est pourquoi ce substrat ne passe pas par le <c>SessionLoop</c> client, dont le <c>FrameReader</c> est câblé
/// sur l'en-tête client 9 octets. Le lien n'applique pas le XOR client (clé à 0).</para>
/// </remarks>
internal sealed class CenterServerHost(ILogger<CenterServerHost> logger, IOptions<CenterServerOptions> options)
    : BackgroundService
{
    private readonly ConcurrentDictionary<Task, byte> _inFlightLinks = new();

    private FenrirTcpListener<CenterLinkSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        _listener = new FenrirTcpListener<CenterLinkSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            (sessionId, transport, remoteEndPoint) =>
                new CenterLinkSession(sessionId, transport, remoteEndPoint, logger),
            logger);

        logger.LogInformation(
            "Fenrir.CenterServer listening on :{Port} (internal S2S, passive; accepts Zone/Login links, opens no " +
            "outbound game TCP). S2S auth: {AuthState}.",
            opts.Port,
            string.IsNullOrEmpty(opts.SharedSecret)
                ? "NOT configured -- loopback-trust; authenticated handshake pending (TODO F4, hardens legacy flaw #8)"
                : "shared secret configured (verification handshake pending, TODO F4)");

        try
        {
            await _listener.AcceptLoopAsync(TrackInFlightLink, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        var outstanding = _inFlightLinks.Keys.ToArray();
        if (outstanding.Length == 0)
            return;

        try
        {
            await Task.WhenAll(outstanding).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "CenterServer shutdown proceeding with {Count} S2S link teardown(s) still in flight", outstanding.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "One or more S2S links faulted while tearing down during shutdown");
        }
    }

    private Task TrackInFlightLink(CenterLinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var task = RunLinkAsync(session, connection, ct);

        _inFlightLinks[task] = 0;
        _ = task.ContinueWith(t => _inFlightLinks.TryRemove(t, out _), TaskScheduler.Default);

        return task;
    }

    private async Task RunLinkAsync(CenterLinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var remoteIp = session.RemoteEndPoint?.Address.ToString();
        logger.LogInformation("S2S link {SessionId} accepted from {RemoteIp}", session.SessionId, remoteIp);

        var ioTask = connection.RunIoAsync(ct);

        try
        {
            // TODO(F4): authenticated handshake (shared secret via Fenrir.Security) then per-opcode S2S dispatch.
            //   The dispatch belongs here once Center [FenrirPacket]s + CenterOpcodeRegistry.Provider +
            //   CenterFrameDispatcher exist (wire-protocol/source-generator). It cannot reuse the client SessionLoop
            //   as-is: that path frames a 9-byte client header, whereas S2S frames a 1-byte opcode header.
            //   Until then, hold the link open and observe the peer's FIN / faults so shutdown drains cleanly.
            await AwaitLinkClosureAsync(session, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation("S2S link {SessionId} disconnected ({ExceptionType}: {Message})",
                    session.SessionId, ex.GetType().Name, ex.Message);
            else
                logger.LogError(ex, "S2S link {SessionId} ended abnormally due to an unhandled exception",
                    session.SessionId);
        }
        finally
        {
            // Unblocks SendLoopAsync still parked on the (empty) TX pipe so RunIoAsync can complete, mirroring
            // SessionLoop.RunConnectionAsync's own teardown ordering.
            connection.Abort();
            await ioTask.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            logger.LogInformation("S2S link {SessionId} from {RemoteIp} closed", session.SessionId, remoteIp);
        }
    }

    /// <summary>
    /// Substrat sans dispatch : lit le flux entrant uniquement pour détecter la fermeture (FIN du pair) ou une
    /// annulation, en n'examinant les octets que pour repartir en attente — aucune trame n'est consommée tant que
    /// le codec S2S Center n'est pas câblé (voir le TODO de <see cref="RunLinkAsync"/>). Remplacer ce corps par le
    /// décodage/dispatch S2S 1 octet dès que le registre/dispatcher Center généré est disponible.
    /// </summary>
    private static async Task AwaitLinkClosureAsync(CenterLinkSession session, CancellationToken ct)
    {
        var reader = session.Transport.Input;

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(ct).ConfigureAwait(false);

                if (result.IsCanceled)
                    break;

                // Nothing is framed/consumed yet: examine everything (park until more bytes arrive) but consume
                // nothing. A transport fault (peer reset) surfaces here as a SocketException/IOException and is left
                // to propagate to RunLinkAsync's classifier; only cancellation (shutdown) is a clean stop.
                reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
