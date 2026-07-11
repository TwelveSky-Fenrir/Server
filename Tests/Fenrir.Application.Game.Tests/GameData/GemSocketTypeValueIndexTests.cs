using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.GameData;

/// <summary>
///     Workstream B13-socket-prerequisites -- <see cref="WorldDataCache.GemSocketsByTypeAndValue" />, the
///     (Type,Value02)-keyed index <see cref="WorldDataCacheBuilder" /> builds alongside
///     <see cref="WorldDataCache.GemSocketsById" /> from the same validated <c>world.GemSockets</c> rows, so
///     <c>StatCalculator.SumGemSocketContribution</c> can look a gem up by (gem type, gem value) instead of by
///     GemSocketId.
/// </summary>
public class GemSocketTypeValueIndexTests
{
    [Fact]
    public void Build_IndexesGemSockets_ByTypeAndValue02_NotByGemSocketId()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(GemSocketId: 42, Type: 2, Value01: 0, Value02: 50, Value03: 100, Value04: 7)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        // Present in both indexes, but under different keys.
        Assert.True(cache.GemSocketsById.ContainsKey(42));
        Assert.False(cache.GemSocketsByTypeAndValue.ContainsKey(42));

        var key = StatCalculator.GemSocketTypeValueKey(2, 50);
        var row = Assert.Single(cache.GemSocketsByTypeAndValue.Values);
        Assert.Equal(42, row.GemSocketId);
        Assert.Same(cache.GemSocketsById[42], cache.GemSocketsByTypeAndValue[key]);
    }

    [Fact]
    public void Build_OnDuplicateTypeAndValue02_FirstGemSocketIdWins()
    {
        // Two rows sharing the same (Type,Value02) key -- GemSocketId 3 must win over GemSocketId 9, matching
        // legacy's own first-match linear scan (GSOCKET::Search, GameSystem_08_Socket.cpp:22-32), regardless of
        // which order they appear in the source list.
        var rows = WorldDataTestRows.MinimalRows() with
        {
            GemSockets =
            [
                new GemSocketRowDto(9, 5, 0, 20, 999, 999),
                new GemSocketRowDto(3, 5, 0, 20, 111, 222)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        var key = StatCalculator.GemSocketTypeValueKey(5, 20);
        var row = cache.GemSocketsByTypeAndValue[key];
        Assert.Equal(3, row.GemSocketId);
        Assert.Equal(111, row.Value03);
    }

    [Fact]
    public void Build_DifferentValue02_DoNotCollide()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            GemSockets =
            [
                new GemSocketRowDto(1, 7, 0, 10, 100, 0),
                new GemSocketRowDto(2, 7, 0, 11, 200, 0)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(2, cache.GemSocketsByTypeAndValue.Count);
        Assert.Equal(100, cache.GemSocketsByTypeAndValue[StatCalculator.GemSocketTypeValueKey(7, 10)].Value03);
        Assert.Equal(200, cache.GemSocketsByTypeAndValue[StatCalculator.GemSocketTypeValueKey(7, 11)].Value03);
    }

    [Fact]
    public void Build_KeyFormula_MatchesStatCalculatorGemSocketTypeValueKey_AcrossValidRange()
    {
        // GameData cannot reference the Stats project, so WorldDataCacheBuilder duplicates
        // StatCalculator.GemSocketTypeValueKey's formula -- this test is the regression guard that the two
        // stay byte-for-byte identical. Types 2-29 (the "mid" band) is the one legacy band whose Value02 range
        // (1-100) spans the formula's full plausible byte domain without also having to satisfy a narrower
        // per-band Value02 ceiling (Type=1 caps at 33, Types 39-46 cap at 10).
        var rows = new List<GemSocketRowDto>();
        var gemSocketId = 1;
        for (var type = 2; type <= 29; type++)
        for (var value02 = 1; value02 <= 100; value02++)
            rows.Add(new GemSocketRowDto(gemSocketId++, type, 0, value02, 1, 0));

        var worldRows = WorldDataTestRows.MinimalRows() with { GemSockets = rows };
        var (cache, _) = WorldDataCacheBuilder.Build(worldRows);

        Assert.Equal(rows.Count, cache.GemSocketsByTypeAndValue.Count);
        foreach (var row in rows)
        {
            var expectedKey = StatCalculator.GemSocketTypeValueKey((byte)row.Type, (byte)row.Value02);
            Assert.True(cache.GemSocketsByTypeAndValue.TryGetValue(expectedKey, out var found));
            Assert.Equal(row.GemSocketId, found.GemSocketId);
        }
    }
}
