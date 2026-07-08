using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Logging;

/// <summary>
///     Per-packet Debug entry logging for the movement/combat opcode family --
///     <see cref="Fenrir.Application.Game.Handlers.Handlers.AvatarActionHandler" /> (CZ_AVATAR_ACTION_SEND,
///     op15), <see cref="Fenrir.Application.Game.Handlers.Handlers.AvatarActionResumeHandler" />
///     (CZ_UPDATE_AVATAR_ACTION, op16), and <see cref="Fenrir.Application.Game.Handlers.Handlers.Combat.AttackHandler" />
///     (CZ_PROCESS_ATTACK_SEND, op18). These three fire on essentially every player movement/attack input --
///     as hot, per-connection, per-packet a path as anything <see cref="Fenrir.Network.Dispatch.Logging.PacketLog" />
///     already covers generically at the raw-frame level -- so entry logging here follows the exact same
///     <c>[LoggerMessage]</c> source-gen shape for the same reason: the compiler-generated <c>IsEnabled</c>
///     check must be paid for at (near) zero cost at the default Information log level, unlike a hand-written
///     <c>logger.LogDebug(...)</c> call, which boxes every value-type argument into a <c>params object?[]</c>
///     array before that check ever runs, on every single movement/attack packet, regardless of whether Debug
///     logging is enabled. See <see cref="Fenrir.Network.Dispatch.Logging.PacketLog" />'s own remarks for the
///     full rationale this mirrors; unlike that class, no method here needs an additional caller-side
///     <c>IsEnabled</c> gate of its own, since none of the arguments below cost anything beyond a field read
///     the caller already has in hand (no <see cref="System.Diagnostics.Stopwatch" /> capture or similar to
///     skip).
/// </summary>
internal static partial class MovementCombatLog
{
    /// <summary>
    ///     Every CZ_AVATAR_ACTION_SEND (op15, <paramref name="opcode" /> 15) or CZ_UPDATE_AVATAR_ACTION (op16,
    ///     <paramref name="opcode" /> 16) received, logged from the respective handler before it is forwarded
    ///     to <see cref="Fenrir.Application.Game.Abstractions.ZoneLifecycle.IAvatarActionService.PostAction" />.
    /// </summary>
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

    /// <summary>
    ///     Every CZ_PROCESS_ATTACK_SEND (op18) received, logged from <see cref="Fenrir.Application.Game.Handlers.Handlers.Combat.AttackHandler" />
    ///     before the case-range (anti-fuzzing) check runs.
    /// </summary>
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
