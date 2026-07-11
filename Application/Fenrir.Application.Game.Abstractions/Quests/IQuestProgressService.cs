using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Quests;

public readonly record struct QuestActionResult(bool Success);

public interface IQuestProgressService
{

        public ValueTask<QuestActionResult> AcceptAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

        public ValueTask<QuestActionResult> CompleteAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, int accountId, CancellationToken ct);

        public ValueTask<QuestActionResult> ReceiveAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

        public ValueTask<QuestActionResult> ExchangeAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);

        public ValueTask<QuestActionResult> AbandonAsync(QuestProgressRequest packet, PlayerRuntimeState state, Zone zone,
        int characterId, CancellationToken ct);
}
