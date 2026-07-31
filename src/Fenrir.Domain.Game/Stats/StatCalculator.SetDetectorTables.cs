using System.Collections.Frozen;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static readonly int[][] Set01Combinations = BuildConcrete(
        [87000, 87001],
        [87021, 87022],
        [87042, 87043],
        [87500, 87501],
        [87521, 87522],
        [87542, 87543]);

    private static readonly int[][] Set02Combinations = BuildCombinations(
        ([87002, 87003, 87004], [87005]),
        ([87023, 87024, 87025], [87026]),
        ([87044, 87045, 87046], [87047]),
        ([87502, 87503, 87504], [87505]),
        ([87523, 87524, 87525], [87526]),
        ([87544, 87545, 87546], [87547]));

    private static readonly int[][] Set03Combinations = BuildConcrete(
        [87006, 87007, 87008],
        [87027, 87028, 87029],
        [87048, 87049, 87050],
        [87506, 87507, 87508],
        [87527, 87528, 87529],
        [87548, 87549, 87550]);

    private static readonly int[][] Set04Combinations = BuildCombinations(
        ([87009, 87010, 87011], [87012]),
        ([87030, 87031, 87032], [87033]),
        ([87051, 87052, 87053], [87054]),
        ([87509, 87510, 87511], [87512]),
        ([87530, 87531, 87532], [87533]),
        ([87551, 87552, 87553], [87554]));

    private static readonly int[][] Set05Combinations = BuildCombinations(
        ([87013, 87014, 87015, 86816], [87016, 87017, 87018, 87019, 87020]),
        ([87034, 87035, 87036, 86817], [87037, 87038, 87039, 87040, 87041]),
        ([87055, 87056, 87057, 86818], [87058, 87059, 87060, 87061, 87062]),
        ([87013, 87014, 87015, 86816], [87516, 87517, 87518, 87519, 87520]),
        ([87534, 87535, 87536, 86817], [87537, 87538, 87539, 87540, 87541]),
        ([87555, 87556, 87557, 86818], [87558, 87559, 87560, 87561, 87562]));

    private static readonly int[][] Set06Combinations = BuildConcrete(
        [87063, 87064],
        [87085, 87086],
        [87107, 87108]);

    private static readonly int[][] Set07Combinations = BuildCombinations(
        ([87065, 87066, 87067], [87068]),
        ([87087, 87088, 87089], [87090]),
        ([87109, 87110, 87111], [87112]));

    private static readonly int[][] Set08Combinations = BuildConcrete(
        [87069, 87070],
        [87091, 87092],
        [87113, 87114]);

    private static readonly int[][] Set09Combinations = BuildCombinations(
        ([87071, 87072, 87073], [87074, 87075, 87076]),
        ([87093, 87094, 87095], [87096, 87097, 87098]),
        ([87115, 87116, 87117], [87118, 87119, 87120]));

    private static readonly int[][] Set19Combinations = BuildCombinations(
        ([87200, 87201, 87202], [87203, 87204, 87205]),
        ([87222, 87223, 87224], [87225, 87226, 87227]),
        ([87244, 87245, 87246], [87247, 87248, 87249]));

    private static readonly int[][] Set10Combinations = BuildCombinations(
        ([87077, 87078, 87079], [87080, 87081, 87082, 87083, 87084]),
        ([87099, 87100, 87101], [87102, 87103, 87104, 87105, 87106]),
        ([87121, 87122, 87123], [87124, 87125, 87126, 87127, 87128]));

    private static readonly int[][] Set21Combinations = BuildCombinations(
        ([88001, 88002, 88003], [88004, 88005, 88006, 88007, 88008]),
        ([88009, 88010, 88011], [88012, 88013, 88014, 88015, 88016]),
        ([88017, 88018, 88019], [88020, 88021, 88022, 88023, 88024]));

    private static readonly int[][] Set22Combinations = BuildCombinations(
        ([88025, 88026, 88027], [88028, 88029, 88030, 88031, 88032]),
        ([88033, 88034, 88035], [88036, 88037, 88038, 88039, 88040]),
        ([88041, 88042, 88043], [88044, 88045, 88046, 88047, 88048]));

    private static readonly int[][] Set15Combinations = BuildConcrete(
        [89515, 89516, 89517, 89518, 89519, 89520],
        [89536, 89537, 89538, 89539, 89540, 89541],
        [89557, 89558, 89559, 89560, 89561, 89562]);

    private static readonly FrozenSet<int> GodRingAmuletIds = new[]
    {
        88007, 88015, 88023, 88031, 88039, 88047,
        88008, 88016, 88024, 88032, 88040, 88048
    }.ToFrozenSet();

    private static readonly FrozenSet<int> LegendaryPieceIds =
        Enumerable.Range(87206, 8)
            .Concat(Enumerable.Range(87228, 8))
            .Concat(Enumerable.Range(87250, 8))
            .ToFrozenSet();

    private static int[][] BuildCombinations(params (int[] Weapons, int[] Fixed)[] groups)
    {
        var combinations = new List<int[]>(groups.Length * 4);
        foreach (var (weapons, fixedPieces) in groups)
        foreach (var weapon in weapons)
        {
            var combination = new int[fixedPieces.Length + 1];
            combination[0] = weapon;
            Array.Copy(fixedPieces, 0, combination, 1, fixedPieces.Length);
            Array.Sort(combination);
            combinations.Add(combination);
        }

        return [.. combinations];
    }

    private static int[][] BuildConcrete(params int[][] tuples)
    {
        var combinations = new int[tuples.Length][];
        for (var i = 0; i < tuples.Length; i++)
            combinations[i] = SortedCopy(tuples[i]);
        return combinations;
    }

    private static int[] SortedCopy(int[] source)
    {
        var copy = (int[])source.Clone();
        Array.Sort(copy);
        return copy;
    }
}
