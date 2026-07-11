using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterBossSpawnMachineTests
{
    private const int Base = MonsterBossSpawnMachine.DefaultBossSlotBase;

    private static MonsterBossSummonCandidate Candidate(int monsterId, float x = 10f, float y = 20f, float z = 30f)
    {
        return new MonsterBossSummonCandidate(monsterId, x, y, z, 5f, 1);
    }

    private static MonsterBossSpawnZoneState NewState(FakeBossSpawnSink sink, params MonsterBossSummonCandidate[] pool)
    {
        return new MonsterBossSpawnZoneState
        {
            Candidates = [.. pool],
            SlotBase = Base,
            Random = new Random(12345),
            Sink = sink
        };
    }

    [Fact]
    public void Reload_FillsFiveFreeSlots_AndAdvancesToCheck()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, MonsterBossSpawnMachine.InvocationIntervalLegacyTicks);

        Assert.Equal(5, sink.Spawns.Count);
        Assert.Equal(new[] { Base, Base + 1, Base + 2, Base + 3, Base + 4 },
            sink.Spawns.ConvertAll(s => s.ServerIndex));
        Assert.Equal(MonsterBossSummonState.Check, state.State);
        Assert.Equal(MonsterBossSpawnMachine.InvocationIntervalLegacyTicks, state.AnchorTick);
    }

    [Fact]
    public void Reload_AllFiveSpawns_ReuseTheSameCandidateRecord()
    {
        var sink = new FakeBossSpawnSink(777);
        var candidate = Candidate(777, 111f, 222f, 333f);
        var state = NewState(sink, candidate);

        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Equal(5, sink.Spawns.Count);
        Assert.All(sink.Spawns, s => Assert.Equal(candidate, s.Candidate));
    }

    [Fact]
    public void Reload_UnresolvableId_DoesNotSpawnOrAdvance_ThenRetriesNextInvocation()
    {
        var sink = new FakeBossSpawnSink();
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Empty(sink.Spawns);
        Assert.Equal(MonsterBossSummonState.Reload, state.State);

        sink.AddResolvable(500);
        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Equal(5, sink.Spawns.Count);
        Assert.Equal(MonsterBossSummonState.Check, state.State);
    }

    [Fact]
    public void Reload_WithFewerThanFiveFreeSlots_SpawnsOnlyThoseFree()
    {
        var sink = new FakeBossSpawnSink(500);
        for (var i = 0; i < MonsterBossSpawnMachine.BossSlotWindowSize - 3; i++)
            sink.Occupy(Base + i);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Equal(3, sink.Spawns.Count);
        Assert.Equal(new[] { Base + 17, Base + 18, Base + 19 }, sink.Spawns.ConvertAll(s => s.ServerIndex));
        Assert.Equal(MonsterBossSummonState.Check, state.State);
    }

    [Fact]
    public void Reload_WithNoFreeSlots_SpawnsNothing_ButStillAdvancesToCheck()
    {
        var sink = new FakeBossSpawnSink(500);
        for (var i = 0; i < MonsterBossSpawnMachine.BossSlotWindowSize; i++)
            sink.Occupy(Base + i);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Empty(sink.Spawns);
        Assert.Equal(MonsterBossSummonState.Check, state.State);
    }

    [Fact]
    public void Check_UnconditionallyAdvancesToWait_AndSetsTheAnchorWaitReads()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);
        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Equal(MonsterBossSummonState.Wait, state.State);
        Assert.Equal(40, state.AnchorTick);
    }

    [Fact]
    public void Wait_HoldsUntilExactlyThreeHours_ThenResetsToReload()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);
        MonsterBossSpawnMachine.Advance(state, 20);

        MonsterBossSpawnMachine.Advance(state, (int)MonsterBossSpawnMachine.WaitDurationLegacyTicks - 1);
        Assert.Equal(MonsterBossSummonState.Wait, state.State);

        var spawnsBefore = sink.Spawns.Count;
        MonsterBossSpawnMachine.Advance(state, 20);
        Assert.Equal(MonsterBossSummonState.Reload, state.State);
        Assert.Equal(spawnsBefore, sink.Spawns.Count);
    }

    [Fact]
    public void EmptyPool_IsInert_NeverSpawns_AndNeverLeavesReload()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink);

        for (var i = 0; i < 10; i++)
            MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Empty(sink.Spawns);
        Assert.Equal(MonsterBossSummonState.Reload, state.State);
    }

    [Fact]
    public void InvocationGate_DoesNotAdvanceBeforeTwentyTicks_ThenFiresExactlyOnce()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, MonsterBossSpawnMachine.InvocationIntervalLegacyTicks - 1);
        Assert.Empty(sink.Spawns);
        Assert.Equal(MonsterBossSummonState.Reload, state.State);

        MonsterBossSpawnMachine.Advance(state, 1);
        Assert.Equal(5, sink.Spawns.Count);
        Assert.Equal(MonsterBossSummonState.Check, state.State);
    }

    [Fact]
    public void Accumulation_ASecondReload_FillsTheRemainingFreeSlots_SkippingOccupiedOnes()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, 20);
        Assert.Equal(5, sink.Spawns.Count);

        state.State = MonsterBossSummonState.Reload;
        MonsterBossSpawnMachine.Advance(state, 20);

        Assert.Equal(10, sink.Spawns.Count);
        Assert.Equal(new[] { Base + 5, Base + 6, Base + 7, Base + 8, Base + 9 },
            sink.Spawns.GetRange(5, 5).ConvertAll(s => s.ServerIndex));
    }

    [Fact]
    public void NegativeElapsed_ContributesNothing_NeverRewinds()
    {
        var sink = new FakeBossSpawnSink(500);
        var state = NewState(sink, Candidate(500));

        MonsterBossSpawnMachine.Advance(state, -100);

        Assert.Equal(0, state.ElapsedLegacyTicks);
        Assert.Empty(sink.Spawns);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    public void NormalizeBossRows_AppliesTheBossOnlyEvenCountRule(int inputCount, int expectedCount)
    {
        var rows = new List<MonsterBossSummonCandidate>();
        for (var i = 0; i < inputCount; i++)
            rows.Add(Candidate(500 + i));

        var normalized = MonsterBossSummonCatalog.NormalizeBossRows(rows);

        Assert.Equal(expectedCount, normalized.Length);
        for (var i = 0; i < expectedCount; i++)
            Assert.Equal(rows[i], normalized[i]);
    }

    private sealed class FakeBossSpawnSink : IMonsterBossSpawnSink
    {
        private readonly HashSet<int> _occupied = [];
        private readonly HashSet<int> _resolvable;

        public FakeBossSpawnSink(params int[] resolvableMonsterIds)
        {
            _resolvable = [.. resolvableMonsterIds];
        }

        public List<(int ServerIndex, MonsterBossSummonCandidate Candidate)> Spawns { get; } = [];

        public bool TryResolveCandidate(int monsterId)
        {
            return _resolvable.Contains(monsterId);
        }

        public bool IsSlotFree(int serverIndex)
        {
            return !_occupied.Contains(serverIndex);
        }

        public void SpawnBoss(int serverIndex, in MonsterBossSummonCandidate candidate)
        {
            _occupied.Add(serverIndex);
            Spawns.Add((serverIndex, candidate));
        }

        public void AddResolvable(int monsterId)
        {
            _resolvable.Add(monsterId);
        }

        public void Occupy(int serverIndex)
        {
            _occupied.Add(serverIndex);
        }
    }
}
