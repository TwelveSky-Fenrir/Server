using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public sealed class WorldInfoBootResetVerifier(
    ZoneCenterSiegeState siege,
    Zone195NokSanState nokSan,
    PopupEventState popup,
    AllianceProposalCenterState allianceProposal,
    ILogger<WorldInfoBootResetVerifier> logger)
{

        public void Verify()
    {
        var violations = 0;

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var dtm = siege.GetZone038DtmValue(tribeId);
            if (dtm != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Zone038 DTM value for tribe {TribeId} is {Value} at boot, expected 0 (Server/ts25center/S07_MyGame01.cpp:131-134)",
                    tribeId, dtm);
            }

            var stonesHeld = nokSan.GetStonesHeld(tribeId);
            if (stonesHeld != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Nok-San stones-held count for tribe {TribeId} is {Value} at boot, expected 0 (Server/ts25center/S07_MyGame01.cpp:127-130)",
                    tribeId, stonesHeld);
            }
        }

        for (var slot = 0; slot < Zone195NokSanState.StoneSlotCount; slot++)
        {
            var owner = nokSan.GetOwner(slot);
            if (owner != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Nok-San stone slot {Slot} owner is {Owner} at boot, expected 0 (uncaptured) (Server/ts25center/S07_MyGame01.cpp:141-144)",
                    slot, owner);
            }
        }

        foreach (var type in Enum.GetValues<PopupEventType>())
        {
            if (popup.IsEnabled(type))
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: popup type {PopupType} is enabled at boot, expected disabled (Server/ts25center/S07_MyGame01.cpp:135-140)",
                    type);
            }
        }

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var possibleAlliance = allianceProposal.GetPossibleAllianceInfo(tribeId);
            if (possibleAlliance != AlliancePossibleInfo.Cleared)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: alliance-possibility cooldown marker for tribe {TribeId} is {Marker} at boot, expected cleared (Server/ts25center/S07_MyGame01.cpp:36-43)",
                    tribeId, possibleAlliance);
            }
        }

        for (var slot = 0; slot < AllianceProposalCenterState.SlotCount; slot++)
        {
            if (!allianceProposal.SlotIsEmpty(slot))
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: alliance-state slot {Slot} is not empty at boot (Server/ts25center/S07_MyGame01.cpp:44-47)",
                    slot);
            }
        }

        if (violations == 0)
            logger.LogInformation(
                "WorldInfo boot reset verified: Zone038 DTM, Nok-San stone state, popup-type flags, and alliance-possibility state all confirmed at their legacy compiled-in reset value (zero/disabled/empty) before the first zone tick or accepted connection");
    }
}
