namespace Fenrir.Application.Game.Domain.World.Loot;

public static class BossDropHelperResolver
{

        private static readonly int[] Tier1Animals = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];

        private static readonly int[] Tier2Animals = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

        private static readonly int[] Elixirs = [506, 507, 508, 509, 578, 579];

        public static int ResolveRandomTier1Animal(Random random)
    {
        return Tier1Animals[random.Next(Tier1Animals.Length)];
    }

        public static int ResolveRandomTier2Animal(Random random)
    {
        return Tier2Animals[random.Next(Tier2Animals.Length)];
    }

        public static int ResolveRandomElixir(Random random)
    {
        return Elixirs[random.Next(Elixirs.Length)];
    }
}
