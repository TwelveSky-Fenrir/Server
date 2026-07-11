namespace Fenrir.Application.Game.Tests.TestSupport;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationRegressionCollection
{
    public const string Name = "Allocation regression tests (serialized)";
}
