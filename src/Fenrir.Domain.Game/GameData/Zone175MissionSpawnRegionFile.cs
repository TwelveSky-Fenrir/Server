namespace Fenrir.Domain.Game.GameData;

public static class Zone175MissionSpawnRegionFile
{
    public static bool TryGetStage(string sourceFileName, out int stage)
    {
        stage = sourceFileName switch
        {
            "Z019_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z019_SUMMONMONSTER_2.WREGION.csv" => 2,
            "Z019_SUMMONMONSTER_3.WREGION.csv" => 3,
            "Z019_SUMMONMONSTER_4.WREGION.csv" => 4,
            "Z019_SUMMONMONSTER_5.WREGION.csv" => 5,
            "Z175_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z178_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z178_SUMMONMONSTER_2.WREGION.csv" => 2,
            "Z182_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z182_SUMMONMONSTER_2.WREGION.csv" => 2,
            "Z182_SUMMONMONSTER_3.WREGION.csv" => 3,
            "Z186_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z186_SUMMONMONSTER_2.WREGION.csv" => 2,
            "Z186_SUMMONMONSTER_3.WREGION.csv" => 3,
            "Z186_SUMMONMONSTER_4.WREGION.csv" => 4,
            "Z190_SUMMONMONSTER_1.WREGION.csv" => 1,
            "Z190_SUMMONMONSTER_2.WREGION.csv" => 2,
            "Z190_SUMMONMONSTER_3.WREGION.csv" => 3,
            "Z190_SUMMONMONSTER_4.WREGION.csv" => 4,
            "Z190_SUMMONMONSTER_5.WREGION.csv" => 5,
            _ => 0
        };

        return stage != 0;
    }
}
