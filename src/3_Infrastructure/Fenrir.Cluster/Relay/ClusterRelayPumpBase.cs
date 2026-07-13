using System.Threading.Channels;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Cluster.Relay;

/// <summary>
/// Pompe générique d'un relais DB-outbox cross-shard, extraite du patron commun aux 7 relais
/// (Chat/Party/Social/GuildTribe/RvrSiege/ProxyShop/GuildBuff). Deux boucles indépendantes tournent dans
/// <see cref="ExecuteAsync"/> :
/// <list type="bullet">
/// <item>La boucle <b>sortante</b>, dirigée par événement (aucun minuteur) : <see cref="Enqueue"/> écrit dans un
/// canal borné non bloquant, et un lecteur unique le draine vers
/// <see cref="IClusterRelayBackend{TEntry,TDto}.PublishAsync"/> dès qu'une entrée arrive.</item>
/// <item>La boucle <b>entrante</b>, sondage périodique (<see cref="PeriodicTimer"/> à l'intervalle du topic) :
/// lit <see cref="IClusterRelayBackend{TEntry,TDto}.PollAsync"/> puis remet chaque ligne au traitement local
/// via <see cref="DeliverAsync"/>.</item>
/// </list>
/// Chaque publication et chaque livraison passe par <see cref="CrossShardRelayRetry"/> (3 tentatives, backoff
/// 50 ms puis 200 ms) avant que l'exception finale ne remonte au bloc log-and-continue par ligne. La faute d'une
/// ligne n'interrompt jamais le traitement des suivantes. Le curseur de lecture est avancé côté proc
/// <c>*_Poll</c> et l'idempotence est portée par le <c>CorrelationId</c> côté proc <c>*_Publish</c> — inchangés.
/// Une sous-classe ne fournit que : ses paramètres de topic (capacité/intervalle/rétention/shard, passés au
/// constructeur), sa livraison spécifique (<see cref="DeliverAsync"/>, qui touche le monde de zone côté
/// application) et ses textes/catégories de log (les cinq crochets <c>On*</c>, chacun via son propre
/// <c>ILogger&lt;THost&gt;</c> pour préserver la catégorie de log par topic). Ce type ne référence jamais la
/// couche application : la livraison concrète reste côté <c>Fenrir.Application.Game.Hosting</c>.
/// </summary>
/// <typeparam name="TEntry">Charge utile sortante mise en file par le producteur.</typeparam>
/// <typeparam name="TDto">Ligne entrante lue depuis l'outbox pour livraison locale.</typeparam>
public abstract class ClusterRelayPumpBase<TEntry, TDto> : BackgroundService
{
    private readonly IClusterRelayBackend<TEntry, TDto> _backend;
    private readonly byte _shardId;
    private readonly TimeSpan _pollInterval;
    private readonly int _retentionSeconds;
    private readonly Channel<TEntry> _outbox;

    protected ClusterRelayPumpBase(
        IClusterRelayBackend<TEntry, TDto> backend,
        byte shardId,
        int capacity,
        TimeSpan pollInterval,
        int retentionSeconds)
    {
        _backend = backend;
        _shardId = shardId;
        _pollInterval = pollInterval;
        _retentionSeconds = retentionSeconds;
        _outbox = Channel.CreateBounded<TEntry>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Point d'entrée du producteur (implémente l'<c>I*RelayQueue</c> de la sous-classe). Non bloquant : écrit
    /// dans le canal borné et rend la main immédiatement. Sur canal plein, <see cref="OnOutboxFull"/> journalise
    /// la perte de ce seul événement cross-shard et renvoie <c>false</c>.
    /// </summary>
    public bool Enqueue(TEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        OnOutboxFull(entry);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboundLoop = RunOutboundFlushLoopAsync(stoppingToken);
        var inboundLoop = RunInboundDeliveryLoopAsync(stoppingToken);
        await Task.WhenAll(outboundLoop, inboundLoop).ConfigureAwait(false);
    }

    private async Task RunOutboundFlushLoopAsync(CancellationToken stoppingToken)
    {
        var reader = _outbox.Reader;

        while (await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            try
            {
                await FlushOutboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnOutboundFlushFailed(ex);
            }
    }

    private async Task RunInboundDeliveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);

        do
        {
            try
            {
                await DeliverInboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnInboundDeliveryFailed(ex);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Effectue un cycle complet (flush sortant + livraison entrante) de manière déterministe, sans minuteur —
    /// utilisé par les tests d'hôte pour piloter le relais un tour à la fois.
    /// </summary>
    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await FlushOutboundAsync(ct).ConfigureAwait(false);
        await DeliverInboundAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask FlushOutboundAsync(CancellationToken ct)
    {
        var reader = _outbox.Reader;

        while (reader.TryRead(out var entry))
            try
            {
                await CrossShardRelayRetry.RunAsync(() => _backend.PublishAsync(entry, ct), ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnPublishFailed(entry, ex);
            }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var incoming = await _backend.PollAsync(_shardId, _retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await CrossShardRelayRetry.RunAsync(() => DeliverAsync(dto, ct), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDeliveryFailed(dto, ex);
            }
    }

    /// <summary>Applique localement une ligne entrante (broadcast de zone, resync de groupe, etc.).</summary>
    protected abstract ValueTask DeliverAsync(TDto dto, CancellationToken ct);

    /// <summary>Journalise l'abandon d'une entrée sur canal sortant plein (texte spécifique au topic).</summary>
    protected abstract void OnOutboxFull(TEntry entry);

    /// <summary>Journalise l'échec du flush sortant au niveau de la boucle (texte spécifique au topic).</summary>
    protected abstract void OnOutboundFlushFailed(Exception ex);

    /// <summary>Journalise l'échec de la livraison entrante au niveau de la boucle (texte spécifique au topic).</summary>
    protected abstract void OnInboundDeliveryFailed(Exception ex);

    /// <summary>Journalise l'échec de publication d'une entrée après épuisement des tentatives.</summary>
    protected abstract void OnPublishFailed(TEntry entry, Exception ex);

    /// <summary>Journalise l'échec de livraison locale d'une ligne après épuisement des tentatives.</summary>
    protected abstract void OnDeliveryFailed(TDto dto, Exception ex);
}
