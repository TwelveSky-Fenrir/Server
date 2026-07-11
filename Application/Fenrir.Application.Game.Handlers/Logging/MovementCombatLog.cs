using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Logging;

internal static partial class MovementCombatLog
{

        [LoggerMessage(
        EventId = 5001,
        EventName = "AvatarActionReceived",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: avatar action received for character {CharacterId}, op{Opcode} (type {ActionType} sort {ActionSort})")]
    public static partial void AvatarActionReceived(
        this ILogger logger,
        long sessionId,
        int characterId,
        byte opcode,
        int actionType,
        int actionSort);

        [LoggerMessage(
        EventId = 5002,
        EventName = "AttackReceived",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: attack received from character {CharacterId}, case {CaseValue} target server-index {TargetServerIndex}")]
    public static partial void AttackReceived(
        this ILogger logger,
        long sessionId,
        int characterId,
        int caseValue,
        int targetServerIndex);
}
