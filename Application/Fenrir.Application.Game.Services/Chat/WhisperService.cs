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

        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
            return new WhisperResolution(WhisperOutcome.TargetNotFound);

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
