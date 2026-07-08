using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class AvatarActionService(ILogger<AvatarActionService> logger) : IAvatarActionService
{
    /// <summary>Action-sort value the legacy's "stand up from death" request rides -- S04_MyWork02.cpp:1313-1320.</summary>
    private const int StandUpActionSort = 30;

    public void PostAction(Zone zone, int characterId, in ActionInfo action, bool isResumeAction = false)
    {
        // mProtect_ReviveHack companion check (S04_MyWork02.cpp:1313-1320): a stand-up attempt while still
        // flagged from an unresolved death is kicked outright rather than silently denied. ReviveHackFlag is
        // only ever true while the character is dead, so no separate IsDead check is needed here.
        if (action.Sort == StandUpActionSort && zone.TryGetPlayer(characterId, out var state) &&
            state is { ReviveHackFlag: true })
        {
            // Anti-cheat gate, not routine chatter: an operator watching live logs should see this without
            // raising the default log level -- the client asked to stand up while still flagged from an
            // unresolved death, which mProtect_ReviveHack treats as a hack attempt, not a benign race.
            logger.LogInformation(
                "Stand-up action rejected and character {CharacterId}'s session will be terminated: revive-hack flag still set (mProtect_ReviveHack)",
                characterId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
            return;
        }

        zone.Post(ZoneCommand.Move(characterId, in action, isResumeAction));
    }
}
