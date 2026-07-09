using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Projects <see cref="ZoneCenterSiegeState" /> and <see cref="TribeGuardCorridorState" /> onto WorldInfo's own
///     fields -- same "overlay onto an otherwise-zeroed template" shape as
///     <see cref="Guilds.GuildRankingProjection" />/<see cref="WorldStateProjection" />. This covers exactly the
///     WorldInfo field groups that already have a real, process-wide backing model (Zone049/Zone175/Zone267/
///     Zone241/Zone335 siege-event state, the per-tribe Zone038 DTM value, the three tribe bonus-ratio arrays plus
///     the kill-other-tribe bonus, and the tribe-guard corridor passability table) but that neither sibling
///     projection merges onto the outbound packet -- both those classes' own remarks explicitly flag this as an
///     open gap (see the "WORLD_INFO field-group backing state" behavior contract's Outputs section).
///     <para>
///         Every other WorldInfo field is passed through unchanged: no backing state exists yet for the
///         remaining numbered zone-siege machines, alliance/vote bookkeeping, guild battle, four-guild,
///         Tribe4Quest, NokSanStone, WaterRainHeaven, PopUp, TribeMasterCallAbility, or any field this task's
///         contract found has no cited sort-code binding at all -- those stay on whatever the caller's own
///         template already carries.
///     </para>
///     <para>
///         Some of the values surfaced here are themselves known placeholders one layer down, not a new
///         approximation introduced by this class: <see cref="ZoneCenterSiegeState.SetZone175" />/
///         <see cref="ZoneCenterSiegeState.SetZone267" />'s own remarks note the stored value is the raw
///         legacy event code, not the byte-exact legacy state constant, and Zone241/the four bonus-ratio
///         fields have no confirmed write path yet (<see cref="ZoneCenterBroadcastIngestor" />'s GAP 1/GAP 2) so
///         they currently always read back as their type's default until a future contract supplies the missing
///         binding -- this projection exists so that binding starts reaching the wire the moment it lands,
///         without a second follow-up change here.
///     </para>
/// </summary>
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
                tribeGuard.IsOpen(tribeId, segment) ? 1 : 0;

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
            ZoneFFATypeState = siege.Zone335
        };
    }
}
