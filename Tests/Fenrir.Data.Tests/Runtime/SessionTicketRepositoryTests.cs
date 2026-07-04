using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

/// <summary>
///     Exercises <see cref="SessionTicketRepository" /> against a real SQL Server 2025 instance running the
///     Database/ migrations (architecture reference §14.1: "chaque proc exécutée contre SQL 2025
///     conteneurisé"). runtime.SessionTickets is a natively compiled, memory-optimized table (ADR-0005) --
///     its create/consume/expire/supersede semantics live entirely in T-SQL, so there is no meaningful way
///     to cover this behavior without a real server.
/// </summary>
[Collection("SqlServer")]
public sealed class SessionTicketRepositoryTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ISessionTicketRepository _repository;

    public SessionTicketRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        _repository = new SessionTicketRepository(_provider.GetRequiredService<ICaeriusNetDbContext>());
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ThenConsumeAsync_ReturnsTheStoredCharacterAndShard()
    {
        const int accountId = 900_001;

        await _repository.CreateAsync(accountId, 42, 3, 15, CancellationToken.None);
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(42, consumed!.CharacterId);
        Assert.Equal((byte)3, consumed.ShardId);
    }

    [Fact]
    public async Task ConsumeAsync_CalledASecondTimeForTheSameAccount_ReturnsNull()
    {
        // Single-use ticket (ADR-0005): usp_SessionTicket_Consume's DELETE runs alongside the read no
        // matter what, so a replay for the same AccountId is the classic ticket-dupe attempt and must
        // find nothing the second time.
        const int accountId = 900_002;
        await _repository.CreateAsync(accountId, 7, 1, 15, CancellationToken.None);

        var first = await _repository.ConsumeAsync(accountId, CancellationToken.None);
        var second = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task ConsumeAsync_NoTicketWasEverCreatedForTheAccount_ReturnsNull()
    {
        const int accountId = 900_003;

        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.Null(consumed);
    }

    [Fact]
    public async Task ConsumeAsync_AfterTheTtlHasElapsed_ReturnsNull()
    {
        // 1 s TTL + ~1.3 s real delay: long enough to reliably cross ExpiresAtUtc, short enough to keep
        // this test fast. usp_SessionTicket_Consume still deletes the row but returns no result set once
        // SYSUTCDATETIME() has passed it.
        const int accountId = 900_004;
        await _repository.CreateAsync(accountId, 9, 2, 1, CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.Null(consumed);
    }

    [Fact]
    public async Task CreateAsync_CalledTwiceWithoutConsuming_TheSecondCallSupersedesTheFirst()
    {
        // DELETE-then-INSERT, never MERGE (architecture reference §12.3): a second login before the
        // previous ticket is consumed simply replaces it -- ConsumeAsync must only ever see the second one.
        const int accountId = 900_005;
        await _repository.CreateAsync(accountId, 10, 1, 15, CancellationToken.None);
        await _repository.CreateAsync(accountId, 20, 2, 15, CancellationToken.None);

        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(20, consumed!.CharacterId);
        Assert.Equal((byte)2, consumed.ShardId);
    }
}
