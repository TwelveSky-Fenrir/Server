namespace Fenrir.Application.Game.Domain.World;

public static class PersonalDungeonBossTables
{

        public static int ResolveCatalogA(int rebirthTier)
    {
        return rebirthTier switch
        {
            0 or 1 => 725,
            2 => 728,
            3 => 729,
            4 => 730,
            5 => 731,
            6 => 730,
            7 => 736,
            8 => 737,
            9 => 738,
            10 => 748,
            11 => 749,
            _ => 750
        };
    }

        public static int ResolveCatalogD(int serverNumber)
    {
        return serverNumber switch
        {
            325 => 725,
            326 => 726,
            327 => 727,
            328 => 728,
            329 => 729,
            330 => 730,
            _ => 725
        };
    }

        public static int ResolveCatalogE(int rebirthTier)
    {
        return rebirthTier switch
        {
            0 or 1 => 725,
            2 => 726,
            3 => 727,
            4 => 728,
            5 => 729,
            6 => 730,
            7 => 736,
            8 => 737,
            9 => 738,
            10 => 748,
            11 => 749,
            _ => 750
        };
    }

        public static (float X, float Y, float Z) CatalogAAndESummonPosition => (1f, 21f, 0f);

        public static (float X, float Y, float Z) CatalogDSummonPosition => (0f, 21f, 0f);
}
