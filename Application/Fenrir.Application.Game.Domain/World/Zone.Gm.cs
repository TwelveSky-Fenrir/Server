using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Stats;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Elevated-tier (<c>GmCommandTier.Elevated</c>) GM-command mirrors that -- unlike every field on
///     <see cref="Tribes.TribeProgressZoneCommand" /> -- cannot be fully decided before reaching the tick that
///     applies them: the GM-EXP command's mode-0 branch (legacy PROCESS_DATA_SEND tSort 503) needs to read the
///     target character's own LIVE <see cref="PlayerRuntimeState.Experience" />/<see cref="PlayerRuntimeState.Level2" />
///     at the moment it applies, not at the moment <c>GmExpGrantService</c> posted it, to decide between its
///     three mutually exclusive branches (already-at-cap no-op, huge-magnitude "instant max level" shortcut,
///     ordinary deposit).
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Bounded capacity for <see cref="_gmExperienceInbox" /> -- also the basis for
    ///     <see cref="GmExperienceInboxDrainCapPerTick" />. Small on purpose: GM-EXP is an operator-only
    ///     command, never a per-player-action-rate channel like <see cref="_inventoryInbox" />.
    /// </summary>
    private const int GmExperienceInboxCapacity = 64;

    /// <summary>Per-tick drain cap for <see cref="_gmExperienceInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int GmExperienceInboxDrainCapPerTick = GmExperienceInboxCapacity / 2;

    /// <summary>
    ///     Legacy's own experience ceiling, <c>MAX_NUMBER_SIZE</c> -- NOT the wire field's/CLR <see cref="int" />
    ///     type ceiling. The GM-EXP command's mode-0 already-at-cap check, huge-magnitude-shortcut trigger, and
    ///     shortcut target are all compared against/derived from this exact constant in legacy
    ///     (Server/ts25zone/S04_MyWork04.cpp:972,974,977), and the shared crediting routine
    ///     <c>MyUtil::ProcessForExperience</c> hard-clamps <c>avt-&gt;aExp1</c> to the same constant
    ///     (Server/ts25zone/S07_MyGame03.cpp:177,190-193). <see cref="long" /> (not <see cref="int" />) because
    ///     <see cref="PlayerRuntimeState.Experience" /> itself is a <see cref="long" />.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/Protocol/DEFINE.h:365 (<c>#define MAX_NUMBER_SIZE 2000000000</c>) ;
    ///     Server/ts25zone/S04_MyWork04.cpp:972,974,977 (mode-0 entry guard, shortcut trigger, and shortcut
    ///     target) ; Server/ts25zone/S07_MyGame03.cpp:177,190-193 (<c>ProcessForExperience</c>'s own
    ///     LONGLONG-guarded hard clamp of <c>avt-&gt;aExp1</c>).
    /// </remarks>
    private const long GmMaxExperience = 2_000_000_000;

    private readonly Channel<GmSelfExperienceGrantZoneCommand> _gmExperienceInbox =
        Channel.CreateBounded<GmSelfExperienceGrantZoneCommand>(
            new BoundedChannelOptions(GmExperienceInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostGmSelfExperienceGrantCommand(in GmSelfExperienceGrantZoneCommand command)
    {
        return _gmExperienceInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" /> (<c>Zone.EconomyMirrors.cs</c>).
    ///     Must be called while holding <see cref="PlayerRuntimeState.EconomyActionLock" />, same as every other
    ///     Elevated/Admin-tier GM-command mirror that touches per-character progression state.
    /// </summary>
    public async Task<bool> PostGmSelfExperienceGrantCommandAndWaitAsync(GmSelfExperienceGrantZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostGmSelfExperienceGrantCommand(in withSignal))
            return false; // inbox full -- caller logs this; nothing to wait for.

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable (there is nothing else to persist for this command besides the in-memory
            // mirror itself, same posture as every other Post*CommandAndWaitAsync sibling); only the in-memory
            // mirror's timing is affected.
        }

        return true;
    }

    private void DrainGmExperienceCommands()
    {
        var processed = 0;
        while (processed < GmExperienceInboxDrainCapPerTick && _gmExperienceInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyGmSelfExperienceGrantCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                // Same containment posture as every other Drain*Commands method in this partial-class family: one
                // bad GM-EXP command must never take the whole tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} GM-experience command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= GmExperienceInboxDrainCapPerTick)
            LogDrainCapEngaged(_gmExperienceInbox.Reader, "gm-experience", GmExperienceInboxDrainCapPerTick);
    }

    /// <summary>
    ///     GM-EXP (legacy PROCESS_DATA_SEND tSort 503) dispatch on <see cref="GmSelfExperienceGrantZoneCommand.Mode" />
    ///     -- a no-op if the character already left this zone, or is mid cross-shard zone-transfer (legacy's own
    ///     <c>MyUtil::ProcessForExperience</c> outer ready-state/moving-zone/zero-amounts guard,
    ///     Server/ts25zone/S07_MyGame03.cpp:163-166 -- "isReady" is always true here by construction, same
    ///     reasoning <see cref="GrantMonsterKillExperience" />'s own remarks already give for its own call site).
    /// </summary>
    private void ApplyGmSelfExperienceGrantCommand(in GmSelfExperienceGrantZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.IsMovingZone)
            return;

        switch (command.Mode)
        {
            case 0:
                ApplyGmCharacterExperienceGrant(state, command.Magnitude);
                break;

            case 1:
                // Server/ts25zone/S07_MyGame03.cpp:318-321: the shared routine's own unconditional pet-experience
                // credit, reached with the character-tier amount fixed at zero for this mode -- so, contrary to
                // legacy's own inline comment mislabeling this mode "God tier / Rebirth," the second-tier
                // experience path (ProcessForExperience2, stubbed to a no-op outside a build variant Fenrir does
                // not target -- see the source behavior contract's own citation) is never reached here. Reuses
                // the exact same crediting core CreditPetGrowthFromMonsterKill/CreditPetGrowthFromPvpKill already
                // share (Zone.Combat.cs) rather than a bespoke path, since it is legacy's own single shared
                // entry point.
                if (command.Magnitude >= 1)
                    CreditPetGrowth(state, command.Magnitude);
                break;

            // Mode 3 ("mount activity experience") exists only in commented-out/dead legacy source and is never
            // live -- must not be modeled. Every other mode value is likewise silently ignored, matching the
            // source behavior contract's own Side effects/Edge cases.
        }
    }

    /// <summary>
    ///     Mode 0's own three mutually exclusive branches -- see the source behavior contract's own Side
    ///     effects for the full citation-backed rule this ports. The "raw magnitude deposited as-is" ordinary
    ///     branch's own upper-bound-only clamp (Server/ts25zone/S07_MyGame03.cpp:177-180) is applied locally
    ///     here, computed as a pre-clamped delta before calling into the shared
    ///     <see cref="ApplyCharacterExperienceGain" /> level-up cascade, rather than adding that clamp to
    ///     <see cref="ApplyCharacterExperienceGain" /> itself -- that method is also the monster-kill/PvP-kill
    ///     experience path, and no behavior contract covering THIS pass instructs widening that clamp to those
    ///     other two callers.
    /// </summary>
    private void ApplyGmCharacterExperienceGrant(PlayerRuntimeState state, int magnitude)
    {
        if (state.Experience >= GmMaxExperience)
            return; // already at the maximum representable value -- silent no-op, still reported as success.

        if (magnitude >= GmMaxExperience)
        {
            // "Instant max level" shortcut: deposit exactly enough to reach the first-tier level cap, then --
            // only if the character has not yet begun second-tier (God/Rebirth) progress, PlayerRuntimeState.Level2
            // -- deposit a further amount to reach GmMaxExperience exactly.
            var maxLevelExperience = worldData.LevelsByLevel.TryGetValue(LevelProgressionCalculator.MaxLevel,
                out var maxLevelRow)
                ? maxLevelRow.ExpRangeMin
                : GmMaxExperience;

            var toCap = maxLevelExperience - state.Experience;
            if (toCap > 0)
                ApplyCharacterExperienceGain(state, (int)Math.Min(toCap, int.MaxValue));

            if (state.Level2 == 0)
            {
                var toMax = GmMaxExperience - state.Experience;
                if (toMax > 0)
                    ApplyCharacterExperienceGain(state, (int)Math.Min(toMax, int.MaxValue));
            }

            return;
        }

        // Ordinary path: the raw requested magnitude (including negative -- not clamped at a lower bound, see
        // the source behavior contract's own Edge cases) deposited as-is, except clamped so the resulting total
        // never exceeds GmMaxExperience -- the one clamp the shared legacy routine itself applies.
        var remainingCapacity = GmMaxExperience - state.Experience;
        var clampedGain = magnitude < remainingCapacity ? magnitude : (int)remainingCapacity;
        ApplyCharacterExperienceGain(state, clampedGain);
    }
}
