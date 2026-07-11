using Fenrir.Data.Abstractions.Game;

namespace Fenrir.LoginServer.Tests.TestSupport;

internal sealed class FakeEventLogRepository : IEventLogRepository
{
    public ValueTask LogAsync(short eventCode, EventLogCategory category, int? actorAccountId,
        int? actorCharacterId, int? targetAccountId, int? targetCharacterId, short? shardId, long? deltaMoney,
        long? deltaBigMoney, int? itemId, int? quantity, byte? outcome, string? payload, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
