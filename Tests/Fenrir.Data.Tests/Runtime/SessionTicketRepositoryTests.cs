using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

// runtime.SessionTickets is a natively compiled, memory-optimized table (ADR-0005); its semantics live
// entirely in T-SQL, so this must run against a real server.
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
        // Single-use ticket: usp_SessionTicket_Consume's DELETE runs alongside the read, so a replay
        // must find nothing the second time.
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
        // usp_SessionTicket_Consume still deletes an expired row but returns no result set.
        const int accountId = 900_004;
        await _repository.CreateAsync(accountId, 9, 2, 1, CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.Null(consumed);
    }

    [Fact]
    public async Task CreateAsync_CalledTwiceWithoutConsuming_TheSecondCallSupersedesTheFirst()
    {
        // DELETE-then-INSERT, never MERGE: a second login before the ticket is consumed replaces it.
        const int accountId = 900_005;
        await _repository.CreateAsync(accountId, 10, 1, 15, CancellationToken.None);
        await _repository.CreateAsync(accountId, 20, 2, 15, CancellationToken.None);

        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(20, consumed!.CharacterId);
        Assert.Equal((byte)2, consumed.ShardId);
    }
}
