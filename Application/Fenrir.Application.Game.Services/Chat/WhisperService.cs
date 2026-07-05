using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WhisperService(ZoneRegistry zones) : IWhisperService
{
    public WhisperResolution Resolve(PlayerRuntimeState sender, string targetAvatarName)
    {
        if (string.Equals(sender.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            return new WhisperResolution(WhisperOutcome.SelfWhisper, null, null);

        if (!zones.TryGetPlayerAndZoneByName(targetAvatarName, out var target, out var targetZone))
            return new WhisperResolution(WhisperOutcome.TargetNotFound, null, null);

        return new WhisperResolution(WhisperOutcome.Delivered, target, targetZone);
    }
}
