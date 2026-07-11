using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class SqlTribeBankTaxSweepGatewayTests
{
    [Fact]
    public async Task EmptyPayload_MakesNoRepositoryCall()
    {
        var repository = new FakeTribeBankSweepRepository();
        var gateway = new SqlTribeBankTaxSweepGateway(repository);

        await gateway.SweepAsync(101, TribeBankTaxSweepPayload.Empty, CancellationToken.None);

        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task NonEmptyPayload_ForwardsAllFourTribeAmounts()
    {
        var repository = new FakeTribeBankSweepRepository();
        var gateway = new SqlTribeBankTaxSweepGateway(repository);

        await gateway.SweepAsync(101, new TribeBankTaxSweepPayload(10, 0, 250, 9_999), CancellationToken.None);

        Assert.Equal(1, repository.CallCount);
        Assert.Equal((10L, 0L, 250L, 9_999L), repository.LastCall);
    }

    [Fact]
    public async Task PayloadWithASingleNonZeroTribe_IsNotTreatedAsEmpty()
    {
        var repository = new FakeTribeBankSweepRepository();
        var gateway = new SqlTribeBankTaxSweepGateway(repository);

        await gateway.SweepAsync(101, new TribeBankTaxSweepPayload(0, 0, 0, 5), CancellationToken.None);

        Assert.Equal(1, repository.CallCount);
        Assert.Equal((0L, 0L, 0L, 5L), repository.LastCall);
    }

    [Fact]
    public async Task RepositoryThrows_Propagates_SoTheFlushHostCanLogAndDropTheWindow()
    {
        var repository = new FakeTribeBankSweepRepository
        {
            Throw = new InvalidOperationException("simulated persistence failure")
        };
        var gateway = new SqlTribeBankTaxSweepGateway(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.SweepAsync(101, new TribeBankTaxSweepPayload(1, 2, 3, 4), CancellationToken.None));
    }

    private sealed class FakeTribeBankSweepRepository : ITribeBankSweepRepository
    {
        public int CallCount { get; private set; }
        public (long, long, long, long)? LastCall { get; private set; }
        public Exception? Throw { get; init; }

        public ValueTask ApplyTaxSweepAsync(long tribe0Amount, long tribe1Amount, long tribe2Amount,
            long tribe3Amount, CancellationToken ct)
        {
            CallCount++;
            if (Throw is { } ex)
                throw ex;

            LastCall = (tribe0Amount, tribe1Amount, tribe2Amount, tribe3Amount);
            return ValueTask.CompletedTask;
        }
    }
}
