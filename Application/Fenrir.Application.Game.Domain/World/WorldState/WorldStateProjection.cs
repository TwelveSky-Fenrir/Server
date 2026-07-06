using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Projects <see cref="WorldStateService" />'s live RvR snapshot onto WorldInfo's own fields -- same "overlay
///     onto an otherwise-zeroed template" shape as <see cref="Guilds.GuildRankingProjection" />, covering the slice
///     <see cref="WorldStateService" />'s own remarks document as backed by a real table today (Zone038 ownership,
///     the tribe-symbol-battle window, per-tribe symbol ownership, the neutral monster symbol, and per-tribe
///     points). Every other WorldInfo field is passed through unchanged: no backing state exists yet for the
///     numbered zone-siege machines, guild battle, four-guild, alliance-offer flattening, or tribe gate/vote-close
///     fields -- see <see cref="WorldStateService" />'s own class remarks for that boundary.
/// </summary>
public static class WorldStateProjection
{
    public static WorldInfo Apply(WorldInfo template, WorldStateService worldState)
    {
        var world = worldState.World;
        var tribes = worldState.GetAllTribes();

        var tribeSymbol = new int[WorldStateService.TribeCount];
        var tribePoint = new int[WorldStateService.TribeCount];

        foreach (var tribe in tribes)
        {
            if (tribe.TribeId >= WorldStateService.TribeCount)
                continue;

            tribeSymbol[tribe.TribeId] = tribe.HasSymbol ? 1 : 0;
            tribePoint[tribe.TribeId] = tribe.Points;
        }

        return template with
        {
            // WorldRvrState uses null as "no winner/no symbol yet" (0 is itself a valid TribeId); the wire
            // template has no such sentinel, so null collapses to 0 here, same as an unresolved Zone038.
            Zone038WinTribe = world.Zone038WinTribe ?? 0,
            Zone038WinTribeTime = world.Zone038WinTribeTime ?? 0,
            TribeSymbolBattle = world.TribeSymbolBattle ? 1 : 0,
            TribeSymbol = tribeSymbol,
            MonsterSymbol = world.MonsterSymbol ?? 0,
            MonsterSymbolEndTime = world.MonsterSymbolEndTime ?? 0,
            TribePoint = tribePoint
        };
    }
}
