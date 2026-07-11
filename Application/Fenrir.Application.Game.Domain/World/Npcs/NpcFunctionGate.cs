using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Npcs;

/// <summary>
///     Whether a specific placed NPC number sits within proximity of the player (see
///     <see cref="NpcFunctionGate.CheckNpcProximity" />). Deliberately three-valued so a caller can tell
///     "the NPC is on this map but too far" (<see cref="Far" />) apart from "the NPC isn't placed on this
///     map at all" (<see cref="NpcNotInZone" />) -- the two failure shapes have different remediations for a
///     proximity-gated action.
/// </summary>
public enum NpcProximity
{
    /// <summary>No placement of the requested NPC number exists in this zone -- no proximity constraint applies.</summary>
    NpcNotInZone,

    /// <summary>At least one placement of the requested NPC number is within <see cref="NpcFunctionGate.ProximityRadius" />.</summary>
    Near,

    /// <summary>The NPC is placed in this zone, but every placement is beyond <see cref="NpcFunctionGate.ProximityRadius" />.</summary>
    Far
}

/// <summary>
///     Ports <c>ZONENPCINFO::CheckNPCFunction</c> (<c>Server/ts25zone/S07_MyGame07.cpp:230-257</c>): true when
///     ANY NPC placed in this zone, within <see cref="ProximityRadius" /> of the player, advertises this
///     numbered function (<c>nMenu[functionId] == 2</c>) -- not "is the player next to the specific NPC it named".
///     The NPC-id-keyed sibling <see cref="CheckNpcProximity" /> answers the second question instead -- "is the
///     player next to THIS specific NPC number" -- reusing the same squared-radius rule for the quest-anchor gate.
/// </summary>
public static class NpcFunctionGate
{
    /// <summary><c>GetDoubleXYZ(...) &lt; 10000.0f</c> is a squared-distance compare; real radius is sqrt(10000) = 100.</summary>
    public const float ProximityRadius = 100f;

    private const float ProximityRadiusSquared = ProximityRadius * ProximityRadius;

    /// <summary>Function id 1 -- <c>ProcessForLearnSkill1</c> (tSort 202), skill tree 1.</summary>
    public const int LearnSkillTree1 = 1;

    /// <summary>
    ///     Function id 4 -- NPC shop buy/sell (tSort 212/215/252, <c>ProcessForInventoryToNPCShop</c>/
    ///     <c>ProcessForNPCShopToInventory</c>).
    /// </summary>
    public const int NpcShop = 4;

    /// <summary>Function id 37 -- <c>ProcessForLearnSkill2</c> (tSort 233), tribe-4 skill tree 2.</summary>
    public const int LearnSkillTree2 = 37;

    /// <summary>
    ///     True when at least one NPC in <paramref name="zone" /> advertises <paramref name="functionId" /> and is
    ///     within <see cref="ProximityRadius" /> of the player. <paramref name="functionId" /> outside [0,100]
    ///     always returns false, matching the legacy's own bound.
    /// </summary>
    public static bool IsAvailable(ZoneDefinition zone, WorldDataCache worldData, int functionId, float posX,
        float posY, float posZ)
    {
        if (functionId is < 0 or > 100)
            return false;

        foreach (var spawn in zone.NpcSpawns)
        {
            // NpcSpawns is pre-filtered at cache-build time -- NpcId is never null here.
            if (!worldData.NpcsById.TryGetValue(spawn.NpcId!.Value, out var npc))
                continue;

            var offersFunction = false;
            foreach (var option in npc.MenuOptions)
                if (option.SlotIndex == functionId && option.OptionId == 2)
                {
                    offersFunction = true;
                    break;
                }

            if (!offersFunction)
                continue;

            var dx = spawn.PosX - posX;
            var dy = spawn.PosY - posY;
            var dz = spawn.PosZ - posZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     NPC-id-keyed proximity primitive: the per-NPC-coordinate variant of <see cref="IsAvailable" />. Applies
    ///     the same squared-distance-below-<c>10000</c> (radius <see cref="ProximityRadius" />, all three axes) rule
    ///     that <c>CheckNPCFunction</c> uses, but resolves the anchor by NPC NUMBER instead of by menu-function --
    ///     the resolution step legacy exposes via <c>ZONENPCINFO::ReturnZoneCoord</c>
    ///     (<c>Server/ts25zone/S07_MyGame07.cpp:209-228</c>). Where <c>ReturnZoneCoord</c> returns only the FIRST
    ///     placement's coordinate, this scans every placement of <paramref name="npcNumber" /> and reports
    ///     <see cref="NpcProximity.Near" /> if the player is within radius of ANY of them -- strictly more
    ///     permissive, so a multi-placed quest NPC never blocks a legitimately-adjacent player.
    /// </summary>
    public static NpcProximity CheckNpcProximity(ZoneDefinition zone, int npcNumber, float posX, float posY,
        float posZ)
    {
        var placed = false;

        foreach (var spawn in zone.NpcSpawns)
        {
            if (spawn.NpcId != npcNumber)
                continue;

            placed = true;

            var dx = spawn.PosX - posX;
            var dy = spawn.PosY - posY;
            var dz = spawn.PosZ - posZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return NpcProximity.Near;
        }

        return placed ? NpcProximity.Far : NpcProximity.NpcNotInZone;
    }
}
