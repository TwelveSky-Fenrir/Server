using Fenrir.Application.Game.Domain.Quests;

namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     The side-effect computation for an already-<see cref="TribeMigrationOutcome.Success" /> CZ_CHANGE_TO_TRIBE4_SEND
///     (op37) -- pure, I/O-free: given which branch fired, returns the new tribe and the new 5-slot quest
///     state, but performs no mutation or persistence itself. The caller (Services layer) is responsible for
///     applying both atomically via the dedicated stored procedure and mirroring them onto
///     <see cref="World.PlayerRuntimeState" />.
/// </summary>
/// <remarks>
///     Réf. "Fourth-tribe (Fujin) conversion and return" behavior contract's own Side effects section, citing
///     Server/ts25zone/S04_MyWork02.cpp:7739 (outbound quest-slot-0 reset), :7753-7756 (both branches' four
///     remaining slots), :7743 (return-allowance decrement, applied by the caller, not here), and :7745
///     (return branch's terminal-step slot-0 write, callee not independently re-verified this task -- see
///     <see cref="QuestCatalog.MaxStep" />'s own remarks). No item, money, stat point, level, equipment, or
///     skill assignment is touched by either branch -- only tribe membership and the 5 quest-progress slots.
/// </remarks>
public static class TribeMigrationConversion
{
    /// <param name="currentTribe">The character's tribe before this conversion.</param>
    /// <param name="previousTribe">
    ///     The character's persisted origin tribe (0-2) -- the return branch's target. Ignored on the outbound
    ///     branch.
    /// </param>
    /// <param name="questCatalog">Resolves the return branch's terminal/complete quest step for the restored tribe.</param>
    public static TribeMigrationResult Resolve(byte currentTribe, byte previousTribe, QuestCatalog questCatalog)
    {
        if (currentTribe != TribeMigrationGate.TribeFour)
            // Outbound: tribe becomes 3, the whole 5-slot quest state resets to idle (Fujin has no quest
            // chain of its own to resume mid-step).
            return new TribeMigrationResult(TribeMigrationGate.TribeFour, QuestProgress.None);

        // Return: tribe is restored to PreviousTribe; quest slot 0 is set to that tribe's OWN terminal step
        // (marking its chain already-complete, not reset to the beginning), the four remaining slots idle.
        var terminalStep = questCatalog.MaxStep(previousTribe);
        var restoredProgress = new QuestProgress(terminalStep, 0, 0, 0, 0);
        return new TribeMigrationResult(previousTribe, restoredProgress);
    }
}

/// <summary>
///     The resolved new tribe + quest state for an accepted conversion -- see <see cref="TribeMigrationConversion" />
///     .
/// </summary>
public readonly record struct TribeMigrationResult(byte NewTribe, QuestProgress NewQuestProgress);
