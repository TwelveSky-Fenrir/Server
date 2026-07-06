namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>Port of CHUGSOUNG_WAR_UI_ZoneIndex (S04_MyWork05.cpp:4946) -- zone number to one of the 12 tower slots.</summary>
public static class TowerZoneIndexTable
{
    /// <summary>-1 if <paramref name="zoneNumber" /> has no tower.</summary>
    public static int GetTowerIndex(int zoneNumber)
    {
        return zoneNumber switch
        {
            2 => 0,
            3 => 1,
            4 => 2,
            7 => 3,
            8 => 4,
            9 => 5,
            12 => 6,
            13 => 7,
            14 => 8,
            141 => 9,
            142 => 10,
            143 => 11,
            _ => -1
        };
    }

    /// <summary>
    ///     The tribe that permanently owns this zone's tower slot -- structural, derived purely from which
    ///     third of the twelve-slot table (S07_MyGame01.cpp:1339-1356) <paramref name="zoneNumber" />'s own
    ///     <see cref="GetTowerIndex" /> falls into: slots 0-2 belong to tribe 0, 3-5 to tribe 1, 6-8 to tribe 2,
    ///     9-11 to tribe 3 (S07_MyGame02.cpp:2119-2141's <c>towerTribe</c> resolution table). Null if
    ///     <paramref name="zoneNumber" /> hosts no recognized tower slot at all.
    /// </summary>
    /// <remarks>
    ///     Deliberately NOT <c>TowerWarState.GetControllingTribe</c> -- the legacy re-derives ownership from
    ///     the server-number table every time this is needed, never from the tower's own current-owner state,
    ///     which this schema doesn't even reassign once built (see <see cref="TowerFriendlyFireGate" />'s
    ///     remarks for why that distinction matters).
    /// </remarks>
    public static byte? GetOwningTribe(int zoneNumber)
    {
        var towerIndex = GetTowerIndex(zoneNumber);
        return towerIndex < 0 ? null : (byte)(towerIndex / 3);
    }
}
