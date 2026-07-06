using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IEventLogRepository: records every single-row LogAsync call (append-only) so
///     LoginService test suites can assert an audit row was (or was not) written, without a real SQL Server
///     2025 instance.
/// </summary>
internal sealed class FakeEventLogRepository : IEventLogRepository
{
    public List<LoggedEvent> LoggedEvents { get; } = [];

    public ValueTask LogAsync(short eventCode, EventLogCategory category, int? actorAccountId,
        int? actorCharacterId, int? targetAccountId, int? targetCharacterId, short? shardId, long? deltaMoney,
        long? deltaBigMoney, int? itemId, int? quantity, byte? outcome, string? payload, CancellationToken ct)
    {
        LoggedEvents.Add(new LoggedEvent(eventCode, category, actorAccountId, actorCharacterId, targetAccountId,
            targetCharacterId, shardId, deltaMoney, deltaBigMoney, itemId, quantity, outcome, payload));
        return ValueTask.CompletedTask;
    }

    public ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public readonly record struct LoggedEvent(
        short EventCode,
        EventLogCategory Category,
        int? ActorAccountId,
        int? ActorCharacterId,
        int? TargetAccountId,
        int? TargetCharacterId,
        short? ShardId,
        long? DeltaMoney,
        long? DeltaBigMoney,
        int? ItemId,
        int? Quantity,
        byte? Outcome,
        string? Payload);
}
