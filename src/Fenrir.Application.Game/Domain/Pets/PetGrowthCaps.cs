namespace Fenrir.Application.Game.Domain.Pets;

public static class PetGrowthCaps
{
    public static readonly IReadOnlyList<int> Values =
    [
        40_000_000, 80_000_000, 160_000_000, 320_000_000,
        80_000_000, 160_000_000, 320_000_000, 640_000_000
    ];
}
