namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     Groups every GC-allocation-regression test class (anything asserting on
///     <see cref="GC.GetAllocatedBytesForCurrentThread" /> deltas across a tight <c>Zone.Tick</c>/broadcast
///     loop) into one shared, explicitly serialized xUnit collection.
/// </summary>
/// <remarks>
///     xUnit's default is one implicit parallel collection per test class -- fine for ordinary tests, but two
///     or more of these allocation-measurement loops actually running concurrently on different threads
///     contend for the same per-core <see cref="System.Buffers.ArrayPool{T}.Shared" /> buckets (each measures
///     a DIFFERENT fixed rented frame size -- <c>AttackResponse</c>, <c>GuildActionResponse</c>,
///     <c>MonsterReplicationResponse</c> -- so cross-talk between them can evict/miss the other's warmed-up
///     bucket), which then shows up as spurious allocation growth completely unrelated to the code any single
///     one of them is actually targeting. Confirmed empirically while adding a third class to this family: any
///     two of these classes pass fine pairwise, but running three concurrently made an unrelated class fail
///     intermittently. <c>DisableParallelization</c> makes every test in this collection run one at a time;
///     every OTHER collection in this project keeps running fully in parallel as before, so this has no effect
///     on overall suite wall-clock time beyond these few tests.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationRegressionCollection
{
    public const string Name = "Allocation regression tests (serialized)";
}
