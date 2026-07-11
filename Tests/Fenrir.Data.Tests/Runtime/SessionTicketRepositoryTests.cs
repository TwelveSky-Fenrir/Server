using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

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
    public async Task CreateAsync_ThenConsumeAsync_ReturnsTheStoredCharacterShardAndSessionToken()
    {
        const int accountId = 900_001;
        var sessionToken = Guid.NewGuid();

        await _repository.CreateAsync(accountId, 42, 3, 15, sessionToken, 0, CancellationToken.None);
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(42, consumed!.CharacterId);
        Assert.Equal((byte)3, consumed.ShardId);
        Assert.Equal(sessionToken, consumed.SessionToken);
        Assert.Equal((short)0, consumed.AccountGrade);
    }

    [Fact]
    public async Task CreateAsync_ThenConsumeAsync_RoundTripsANonZeroAccountGrade()
    {
        const int accountId = 900_006;

        await _repository.CreateAsync(accountId, 42, 3, 15, Guid.NewGuid(), 1, CancellationToken.None);
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal((short)1, consumed!.AccountGrade);
    }

    [Fact]
    public async Task ConsumeAsync_CalledASecondTimeForTheSameAccount_ReturnsNull()
    {
        const int accountId = 900_002;
        await _repository.CreateAsync(accountId, 7, 1, 15, Guid.NewGuid(), 0, CancellationToken.None);

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
        const int accountId = 900_004;
        await _repository.CreateAsync(accountId, 9, 2, 1, Guid.NewGuid(), 0, CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.Null(consumed);
    }

    [Fact]
    public async Task CreateAsync_CalledTwiceWithoutConsuming_TheSecondCallSupersedesTheFirst()
    {
        const int accountId = 900_005;
        var secondToken = Guid.NewGuid();
        await _repository.CreateAsync(accountId, 10, 1, 15, Guid.NewGuid(), 0, CancellationToken.None);
        await _repository.CreateAsync(accountId, 20, 2, 15, secondToken, 0, CancellationToken.None);

        var consumed = await _repository.ConsumeAsync(accountId, CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(20, consumed!.CharacterId);
        Assert.Equal((byte)2, consumed.ShardId);
        Assert.Equal(secondToken, consumed.SessionToken);
    }
}
