using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class WorldInfoBootResetVerifierTests
{
    private static WorldInfoBootResetVerifier CreateVerifier(CapturingLogger<WorldInfoBootResetVerifier> logger,
        ZoneCenterSiegeState? siege = null, Zone195NokSanState? nokSan = null, PopupEventState? popup = null,
        AllianceProposalCenterState? allianceProposal = null)
    {
        return new WorldInfoBootResetVerifier(siege ?? new ZoneCenterSiegeState(), nokSan ?? new Zone195NokSanState(),
            popup ?? new PopupEventState(), allianceProposal ?? new AllianceProposalCenterState(), logger);
    }

    [Fact]
    public void Verify_FreshSingletons_LogsNoViolation_OnlyOneInfoEntry()
    {
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger);

        verifier.Verify();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    [Fact]
    public void Verify_DtmAlreadyNonZero_LogsOneErrorPerViolatingTribe()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone038DtmValue(2, 5);
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger, siege);

        verifier.Verify();

        var errors = logger.Entries.FindAll(e => e.Level == LogLevel.Error);
        Assert.Single(errors);
        Assert.Contains("Zone038 DTM", errors[0].Message);
        Assert.Contains("tribe 2", errors[0].Message);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public void Verify_NokSanStoneAlreadyCaptured_LogsOwnerAndStonesHeldViolations()
    {
        var nokSan = new Zone195NokSanState();
        nokSan.CommitCapture(0, 1);
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger, nokSan: nokSan);

        verifier.Verify();

        var errors = logger.Entries.FindAll(e => e.Level == LogLevel.Error);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Message.Contains("stone slot"));
        Assert.Contains(errors, e => e.Message.Contains("stones-held"));
    }

    [Fact]
    public void Verify_PopupTypeAlreadyEnabled_LogsOneErrorForThatType()
    {
        var popup = new PopupEventState();
        popup.SetEnabled(PopupEventType.MonsterPve, true);
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger, popup: popup);

        verifier.Verify();

        var errors = logger.Entries.FindAll(e => e.Level == LogLevel.Error);
        Assert.Single(errors);
        Assert.Contains("MonsterPve", errors[0].Message);
    }

    [Fact]
    public void Verify_AlliancePossibilityCooldownAlreadyArmed_LogsOneErrorForThatTribe()
    {
        var allianceProposal = new AllianceProposalCenterState();
        allianceProposal.SetPossibleAllianceCooldown(1, 20260101);
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger, allianceProposal: allianceProposal);

        verifier.Verify();

        var errors = logger.Entries.FindAll(e => e.Level == LogLevel.Error);
        Assert.Single(errors);
        Assert.Contains("alliance-possibility", errors[0].Message);
        Assert.Contains("tribe 1", errors[0].Message);
    }

    [Fact]
    public void Verify_AllianceStateSlotAlreadyOccupied_LogsOneErrorForThatSlot()
    {
        var allianceProposal = new AllianceProposalCenterState();
        allianceProposal.SetSlot(0, 1, 2);
        var logger = new CapturingLogger<WorldInfoBootResetVerifier>();
        var verifier = CreateVerifier(logger, allianceProposal: allianceProposal);

        verifier.Verify();

        var errors = logger.Entries.FindAll(e => e.Level == LogLevel.Error);
        Assert.Single(errors);
        Assert.Contains("alliance-state slot 0", errors[0].Message);
    }
}
