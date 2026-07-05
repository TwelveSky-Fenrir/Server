using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Quests;

/// <summary>Outcome of a single CZ_PROCESS_QUEST_SEND action: whether the handler should abort or echo back.</summary>
public readonly record struct QuestActionResult(bool Success);

/// <summary>
///     The 5-action quest state machine (<see cref="QuestStateMachine" />) business logic behind
///     CZ_PROCESS_QUEST_SEND (opcode 36). Every rejection is reported as a failed <see cref="QuestActionResult" />;
///     there is no clean <c>tResult</c> failure path for this opcode.
/// </summary>
/// <remarks>
///     Reward money and any touched container are persisted synchronously in the same transaction as the
///     quest-state row. CP/XP/TeacherPoint rewards are write-behind, mirrored via <see cref="QuestZoneCommand" />
///     after the SQL commit and awaited before the caller's economy-action lock releases -- both the read and the
///     mirror must happen while holding the lock to avoid a duplication race.
/// </remarks>
public interface IQuestProgressService
{
    /// <summary>tSort 1, "mission issuance".</summary>
    public ValueTask<QuestActionResult> AcceptAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

    /// <summary>tSort 2, "mission completed".</summary>
    public ValueTask<QuestActionResult> CompleteAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

    /// <summary>
    ///     tSort 3, "mission receive" -- does not mutate quest state at all, only deposits an item; reuses
    ///     the plain InventoryZoneCommand channel rather than QuestZoneCommand.
    /// </summary>
    public ValueTask<QuestActionResult> ReceiveAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

    /// <summary>tSort 4, "mission exchange".</summary>
    public ValueTask<QuestActionResult> ExchangeAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

    /// <summary>tSort 5, "mission abandonment" -- no container touched.</summary>
    public ValueTask<QuestActionResult> AbandonAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);
}
