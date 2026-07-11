using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct AntiCampingGuardPoint(float X, float Y, float Z);

public sealed record AntiCampingMapGuardPoints(
    ImmutableArray<AntiCampingGuardPoint> HolyStoneSymbolPoints,
    AntiCampingGuardPoint? TowerPoint)
{
    public static readonly AntiCampingMapGuardPoints Empty = new([], null);
}

public sealed class AntiCampingGuardPointCatalog(IReadOnlyDictionary<short, AntiCampingMapGuardPoints> pointsByMapId)
{
    public static readonly ImmutableArray<short> GuardedMapIds = [2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143];

    public static readonly AntiCampingGuardPointCatalog Empty =
        new(ImmutableDictionary<short, AntiCampingMapGuardPoints>.Empty);

    private static readonly ImmutableDictionary<short, ImmutableArray<AntiCampingGuardPoint>>
        HolyStoneSymbolPointTable =
            new Dictionary<short, ImmutableArray<AntiCampingGuardPoint>>
            {
                [2] = [new AntiCampingGuardPoint(-1810f, -1f, 3155f)],
                [3] =
                [
                    new AntiCampingGuardPoint(-6760f, 0f, 1187f),
                    new AntiCampingGuardPoint(-7780f, 0f, 400f),
                    new AntiCampingGuardPoint(-6864f, 0f, 2761f)
                ],
                [4] = [new AntiCampingGuardPoint(7839f, 461f, 6520f)],
                [7] = [new AntiCampingGuardPoint(-831f, 10f, -3392f)],
                [8] =
                [
                    new AntiCampingGuardPoint(4410f, 28f, 4666f),
                    new AntiCampingGuardPoint(5493f, 38f, 4174f),
                    new AntiCampingGuardPoint(5545f, 41f, 6452f)
                ],
                [9] = [new AntiCampingGuardPoint(-2438f, -590f, 6697f)],
                [12] = [new AntiCampingGuardPoint(-4045f, 0f, 1648f)],
                [13] =
                [
                    new AntiCampingGuardPoint(-7610f, 0f, 5763f),
                    new AntiCampingGuardPoint(-6684f, 0f, 5319f),
                    new AntiCampingGuardPoint(-5397f, 0f, 5819f)
                ],
                [14] = [new AntiCampingGuardPoint(7174f, 336f, 6191f)],
                [141] = [new AntiCampingGuardPoint(-1132f, 0f, 3486f)],
                [142] =
                [
                    new AntiCampingGuardPoint(-2505f, 0f, 7201f),
                    new AntiCampingGuardPoint(-2063f, 1f, 6846f),
                    new AntiCampingGuardPoint(-2948f, 8f, 6105f)
                ],
                [143] = [new AntiCampingGuardPoint(-38f, 0f, 4432f)]
            }.ToImmutableDictionary();

    public static readonly AntiCampingGuardPointCatalog Default = BuildDefault();

    private static AntiCampingGuardPointCatalog BuildDefault()
    {
        var builder = ImmutableDictionary.CreateBuilder<short, AntiCampingMapGuardPoints>();

        foreach (var mapId in GuardedMapIds)
        {
            var symbolPoints = HolyStoneSymbolPointTable[mapId];
            AntiCampingGuardPoint? towerPoint =
                TowerGuardianCatalog.TryGetGuardianLocation(mapId, out var x, out var y, out var z)
                    ? new AntiCampingGuardPoint(x, y, z)
                    : null;

            builder[mapId] = new AntiCampingMapGuardPoints(symbolPoints, towerPoint);
        }

        return new AntiCampingGuardPointCatalog(builder.ToImmutable());
    }

    public bool IsGuardedMap(short mapId)
    {
        return GuardedMapIds.Contains(mapId);
    }

    public AntiCampingMapGuardPoints GetPoints(short mapId)
    {
        return pointsByMapId.TryGetValue(mapId, out var points) ? points : AntiCampingMapGuardPoints.Empty;
    }
}
