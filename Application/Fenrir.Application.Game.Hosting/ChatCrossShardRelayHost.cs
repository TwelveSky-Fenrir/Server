using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class ChatCrossShardRelayHost(
    ZoneRegistry zones,
    IChatCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ChatCrossShardRelayHost> logger) : BackgroundService, IChatCrossShardRelayQueue
{
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private static readonly ItemLinkInfo EmptyLink =
        new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    private readonly Channel<ChatCrossShardWhisperEntry> _outbox =
        Channel.CreateBounded<ChatCrossShardWhisperEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        public bool Enqueue(ChatCrossShardWhisperEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard whisper relay outbox full on shard {ShardId}; dropping one whisper from {SourceName} " +
            "to {TargetName} (character {TargetCharacterId} on shard {TargetShardId}) -- the sender already saw " +
            "the accepted acknowledgement, only this one cross-shard delivery is lost",
            options.Value.ShardId, entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId,
            entry.TargetShardId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ChatCrossShardRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cross-shard whisper relay poll failed for shard {ShardId}",
                    options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

        public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await FlushOutboundAsync(ct).ConfigureAwait(false);
        await DeliverInboundAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask FlushOutboundAsync(CancellationToken ct)
    {
        var reader = _outbox.Reader;
        var drained = 0;

        while (drained < MaxDrainedPerCycle && reader.TryRead(out var entry))
        {
            drained++;
            try
            {
                await relay.PublishAsync(entry, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to publish a cross-shard whisper from {SourceName} to {TargetName} (character " +
                    "{TargetCharacterId} on shard {TargetShardId}) from shard {ShardId}; this one whisper is lost",
                    entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId, entry.TargetShardId,
                    options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.ChatCrossShardRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                DeliverLocally(dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed whisper {RelayId} (from {SourceName} to {TargetName}) on " +
                    "shard {ShardId}",
                    dto.RelayId, dto.SourceAvatarName, dto.TargetAvatarName, shardId);
            }
    }

    private void DeliverLocally(ChatCrossShardWhisperDto dto)
    {
        if (!zones.TryGetPlayer(dto.TargetCharacterId, out var target) || target.IsMovingZone)
        {
            logger.LogDebug(
                "Relayed whisper {RelayId} from {SourceName} to character {TargetCharacterId} on shard {ShardId} " +
                "found no deliverable local target (departed or mid zone-transfer); dropped",
                dto.RelayId, dto.SourceAvatarName, dto.TargetCharacterId, options.Value.ShardId);
            return;
        }

        target.Session.Send(new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = dto.SourceAvatarName,
            Content = dto.Content,
            AuthType = dto.SenderAuthType,
            Link = EmptyLink
        });
    }
}
