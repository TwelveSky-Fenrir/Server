using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class AvatarActionService(ILogger<AvatarActionService> logger) : IAvatarActionService
{
    private const int StandUpActionSort = 30;

    public void PostAction(Zone zone, int characterId, in ActionInfo action, bool isResumeAction = false)
    {
        if (action.Sort == StandUpActionSort && zone.TryGetPlayer(characterId, out var state) &&
            state is { ReviveHackFlag: true })
        {
            logger.LogInformation(
                "Stand-up action rejected and character {CharacterId}'s session will be terminated: revive-hack flag still set (mProtect_ReviveHack)",
                characterId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (!zone.Post(ZoneCommand.Move(characterId, in action, isResumeAction)))
            logger.LogWarning(
                "Zone {MapId} inbox full: dropped Move for character {CharacterId}",
                zone.MapId, characterId);
    }
}
