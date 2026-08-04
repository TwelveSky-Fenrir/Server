using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class ZoneCenterSiegeProjection
{
    public static WorldInfo Apply(WorldInfo template, ZoneCenterSiegeState siege, TribeGuardCorridorState tribeGuard)
    {
        var zone049State = new int[ZoneCenterSiegeState.Zone049Slots];
        var zone049StateTime = new int[ZoneCenterSiegeState.Zone049Slots];
        for (var slot = 0; slot < ZoneCenterSiegeState.Zone049Slots; slot++)
        {
            zone049State[slot] = siege.GetZone049State(slot);
            zone049StateTime[slot] = siege.GetZone049StateTime(slot);
        }

        var zone175 = new int[ZoneCenterSiegeState.Zone175Instances * ZoneCenterSiegeState.Zone175Slots];
        for (var instance = 0; instance < ZoneCenterSiegeState.Zone175Instances; instance++)
        for (var slot = 0; slot < ZoneCenterSiegeState.Zone175Slots; slot++)
            zone175[instance * ZoneCenterSiegeState.Zone175Slots + slot] = siege.GetZone175(instance, slot);

        var zone241 = new int[ZoneCenterSiegeState.Zone241Instances];
        for (var instance = 0; instance < ZoneCenterSiegeState.Zone241Instances; instance++)
            zone241[instance] = (int)siege.GetZone241(instance);

        var zone267 = new int[WorldStateService.TribeCount];
        var zone038Dtm = new int[WorldStateService.TribeCount];
        var experienceBonus = new float[WorldStateService.TribeCount];
        var itemDropBonus = new float[WorldStateService.TribeCount];
        var myoungItemDropBonus = new float[WorldStateService.TribeCount];
        var killOtherTribeBonus = new int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            zone267[tribeId] = siege.GetZone267(tribeId);
            zone038Dtm[tribeId] = siege.GetZone038DtmValue(tribeId);
            experienceBonus[tribeId] = siege.GetExperienceBonusRatio(tribeId);
            itemDropBonus[tribeId] = siege.GetItemDropBonusRatio(tribeId);
            myoungItemDropBonus[tribeId] = siege.GetMyoungItemDropBonusRatio(tribeId);
            killOtherTribeBonus[tribeId] = siege.GetKillOtherTribeBonus(tribeId);
        }

        var tribeGuardState = new int[TribeGuardCorridorState.TribeCount * TribeGuardCorridorState.SegmentCount];
        for (byte tribeId = 0; tribeId < TribeGuardCorridorState.TribeCount; tribeId++)
        for (byte segment = 0; segment < TribeGuardCorridorState.SegmentCount; segment++)
            tribeGuardState[tribeId * TribeGuardCorridorState.SegmentCount + segment] =
                tribeGuard.IsOpen(tribeId, segment) ? 0 : 1;

        return template with
        {
            Zone049TypeState = zone049State,
            Zone049TypeStateTime = zone049StateTime,
            Zone175TypeState = zone175,
            TribeGuardState = tribeGuardState,
            Zone038DTMValue = zone038Dtm,
            TribeGeneralExperienceUpRatioInfo = experienceBonus,
            TribeItemDropUpRatioInfo = itemDropBonus,
            TribeItemDropUpRatioForMyoungInfo = myoungItemDropBonus,
            TribeKillOtherTribeAddValueInfo = killOtherTribeBonus,
            Zone267TypeState = zone267,
            Zone241TypeState = zone241,
            ZoneFFATypeState = siege.Zone335,
            Zone194TypeState = siege.Zone194State
        };
    }
}
