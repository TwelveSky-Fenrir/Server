using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WhisperService(ZoneRegistry zones, ICharacterShardLocationRepository characterShardLocations)
    : IWhisperService
{
    public async ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        CancellationToken cancellationToken)
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

        // The directory says the target is alive on some other shard, but there is no inter-shard message
        // relay/bus in this codebase yet (see this type's own remarks) -- reported honestly, not silently
        // collapsed into TargetNotFound, which would be a legacy-parity-adjacent lie about why delivery failed.
        return new WhisperResolution(WhisperOutcome.TargetOnAnotherShard, OtherShardId: remote.ShardId,
            OtherMapId: remote.MapId);
    }
}
