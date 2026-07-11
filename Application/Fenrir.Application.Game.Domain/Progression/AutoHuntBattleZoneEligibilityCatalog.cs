namespace Fenrir.Application.Game.Domain.Progression;

public static class AutoHuntBattleZoneEligibilityCatalog
{

        public static bool IsBlocked(short mapId, int combinedLevel, int rebirthTier) => mapId switch
    {
        49 => combinedLevel is >= 10 and <= 89,
        51 => combinedLevel is >= 20 and <= 29,
        53 => combinedLevel is >= 30 and <= 39,
        120 => combinedLevel is >= 146 and <= 156,
        121 => combinedLevel is >= 150 and <= 153,
        122 => combinedLevel is >= 154 and <= 156,
        146 => combinedLevel is >= 90 and <= 112,
        147 => combinedLevel is >= 50 and <= 59,
        148 => combinedLevel is >= 60 and <= 69,
        149 => combinedLevel is >= 70 and <= 79,
        150 => combinedLevel is >= 80 and <= 89,
        151 => combinedLevel is >= 90 and <= 99,
        152 => combinedLevel is >= 100 and <= 105,
        153 => combinedLevel is >= 106 and <= 112,
        154 => combinedLevel is >= 1 and <= 157,
        155 => combinedLevel is >= 116 and <= 118,
        156 => combinedLevel is >= 119 and <= 121,
        157 => combinedLevel is >= 124 and <= 134,
        158 => combinedLevel is >= 125 and <= 127,
        159 => combinedLevel is >= 128 and <= 130,
        160 => combinedLevel is >= 135 and <= 145,
        161 => combinedLevel is >= 134 and <= 136,
        162 => combinedLevel is >= 137 and <= 139,
        163 => combinedLevel is >= 140 and <= 142,
        164 => combinedLevel is >= 145 and <= 151,
        295 => combinedLevel == 157 && rebirthTier < 7,
        296 => combinedLevel == 157 && rebirthTier >= 7,
        322 => combinedLevel == 157 && rebirthTier < 7,
        323 => combinedLevel == 157 && rebirthTier >= 7,
        319 or 320 or 321 => true,
        _ => false
    };
}
