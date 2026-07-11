using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WhisperService(
    ZoneRegistry zones,
    ICharacterShardLocationRepository characterShardLocations,
    IChatCrossShardRelayQueue chatRelay,
    IOptions<GameServerOptions> options)
    : IWhisperService
{
    public async ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        string content, int senderAuthType, CancellationToken cancellationToken)
    {
        if (string.Equals(sender.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            return new WhisperResolution(WhisperOutcome.SelfWhisper);

        if (zones.TryGetPlayerAndZoneByName(targetAvatarName, out var target, out var targetZone))
            return new WhisperResolution(WhisperOutcome.Delivered, target, targetZone);

        // Same-shard miss -- fall back to the cross-shard directory before giving up. This is a deliberate,
        // low-frequency player action (a whisper), not a per-tick/per-movement path, so an awaited DB call
        // here on the miss branch only is proportionate.
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
            return new WhisperResolution(WhisperOutcome.TargetNotFound);

        // The directory located the target alive on another shard. Enqueue the whisper onto the cross-shard
        // relay outbox (best-effort, fire-and-forget: ChatCrossShardRelayHost publishes it, the target's own
        // shard polls and delivers it if the target is still present -- a clean silent drop otherwise, matching
        // the op39 contract's since-departed-target parity). Return QueuedCrossShard so the handler acknowledges
        // the sender with Result=0 (accepted, target located, target's map number for display) exactly as the
        // legacy path does the instant its own point-in-time lookup succeeds.
        //
        // Ordering note: the legacy path emits the sender's Result=0 BEFORE the relay leaves; here the enqueue
        // happens inside this resolve call, before the handler sends Result=0. The reorder is unobservable in
        // Fenrir's SQL-mediated relay -- there is no delivery-acknowledgement path back to the sender, the poll
        // fans the row out on a separate cadence seconds later, and the outbox channel and the sender's send
        // channel are independent, so no party can observe which of the two happened first.
        //
        // The optional item-link attachment is not carried across the relay (see ChatCrossShardWhisperEntry's
        // own remarks) -- the cross-shard leg delivers a zeroed link; the same-shard path is unaffected.
        chatRelay.Enqueue(new ChatCrossShardWhisperEntry(
            options.Value.ShardId,
            sender.CharacterId,
            sender.Name,
            remote.ShardId,
            remote.CharacterId,
            targetAvatarName,
            content,
            (byte)senderAuthType));

        return new WhisperResolution(WhisperOutcome.QueuedCrossShard, OtherShardId: remote.ShardId,
            OtherMapId: remote.MapId);
    }
}
