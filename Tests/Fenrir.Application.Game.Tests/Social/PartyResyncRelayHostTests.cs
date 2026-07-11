using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyResyncRelayHostTests
{
    private const byte ShardId = 3;

    [Fact]
    public async Task PollOnceAsync_QueuedEntry_IsPublishedToTheRepository()
    {
        var relay = new FakePartyResyncRelayRepository();
        var host = CreateHost(relay, []);

        var entry = new PartyResyncRelayEntry((byte)PartyResyncRelaySort.Request, ShardId, 100, "LeaderName",
            "LeaderName");
        Assert.True(host.Enqueue(entry));

        await host.PollOnceAsync(CancellationToken.None);

        var published = Assert.Single(relay.Published);
        Assert.Equal(entry, published);
    }

    [Fact]
    public async Task PollOnceAsync_DeliveredRow_IsRoutedToTheRegisteredHandler()
    {
        var relay = new FakePartyResyncRelayRepository();
        var handler = new FakePartyResyncRelayHandler();
        var host = CreateHost(relay, [handler]);

        var dto = new PartyResyncRelayDto(55, (byte)PartyResyncRelaySort.Request, 7, 100, "LeaderName", "MemberName");
        relay.NextPoll = [dto];

        await host.PollOnceAsync(CancellationToken.None);

        var handled = Assert.Single(handler.Handled);
        Assert.Equal(55, handled.RelayId);
        Assert.Equal("LeaderName", handled.PartyName);
    }

    [Fact]
    public async Task PollOnceAsync_PartyBreakRow_IsRoutedWithItsOwnSort()
    {
        var relay = new FakePartyResyncRelayRepository();
        var handler = new FakePartyResyncRelayHandler();
        var host = CreateHost(relay, [handler]);

        var dto = new PartyResyncRelayDto(9, (byte)PartyResyncRelaySort.PartyBreak, 7, 200, "GoneParty", "Straggler");
        relay.NextPoll = [dto];

        await host.PollOnceAsync(CancellationToken.None);

        var handled = Assert.Single(handler.Handled);
        Assert.Equal((byte)PartyResyncRelaySort.PartyBreak, handled.Sort);
    }

    [Fact]
    public async Task PollOnceAsync_RowWithNoRegisteredHandler_IsDroppedWithoutThrowing()
    {
        var relay = new FakePartyResyncRelayRepository();
        var host = CreateHost(relay, []);

        relay.NextPoll =
        [
            new PartyResyncRelayDto(1, (byte)PartyResyncRelaySort.Request, 7, 100, "LeaderName", "MemberName")
        ];

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task PollOnceAsync_PublishFailure_IsIsolated_DoesNotThrow()
    {
        var relay = new FakePartyResyncRelayRepository { ThrowOnPublish = new InvalidOperationException("boom") };
        var host = CreateHost(relay, []);

        host.Enqueue(new PartyResyncRelayEntry((byte)PartyResyncRelaySort.Request, ShardId, 1, "P", "A"));

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task PollOnceAsync_InboundHandlerThrows_IsIsolatedPerRow_DoesNotThrow()
    {
        var relay = new FakePartyResyncRelayRepository();
        var host = CreateHost(relay, [new ThrowingPartyResyncRelayHandler()]);

        relay.NextPoll =
        [
            new PartyResyncRelayDto(1, (byte)PartyResyncRelaySort.Request, 7, 100, "LeaderName", "MemberName")
        ];

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    private static PartyResyncRelayHost CreateHost(FakePartyResyncRelayRepository relay,
        IEnumerable<IPartyResyncRelayHandler> handlers)
    {
        return new PartyResyncRelayHost(handlers, relay,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<PartyResyncRelayHost>.Instance);
    }

    private sealed class ThrowingPartyResyncRelayHandler : IPartyResyncRelayHandler
    {
        public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
        {
            throw new InvalidOperationException("handler boom");
        }
    }
}
