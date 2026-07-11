namespace Fenrir.Application.Game.Domain.World.Loot;

public static class LootRandomSource
{

        public static int RandomNumber(Random random)
    {
        var r1 = random.Next(0, 1000);
        var r2 = random.Next(0, 1000);
        return (1 + r1) * (1 + r2);
    }
}
