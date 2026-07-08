using Fenrir.Application.Game.Handlers.Handlers.Combat;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Tests.Handlers.Combat;

// Movement/combat entry logging (Fenrir.Application.Game.Handlers.Logging.MovementCombatLog.AttackReceived):
// AttackHandler must log every CZ_PROCESS_ATTACK_SEND (op18) it receives, with the real session id, character
// id, mCase, and target server-index carried by the packet -- not a bare "handler called" marker -- before the
// anti-fuzzing case-range check even runs.
public class AttackHandlerLoggingTests
{
    private static AttackForProtocol Attack(int mCase, int targetServerIndex)
    {
        return new AttackForProtocol
        {
            Case = mCase,
            ServerIndex1 = 7,
            UniqueNumber1 = 7,
            ServerIndex2 = targetServerIndex,
            UniqueNumber2 = unchecked((uint)targetServerIndex),
            SenderLocation = [0, 0, 0],
            AttackActionValue1 = 0,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    [Fact]
    public void Handle_ValidCase_LogsAttackReceivedWithRealFieldValues()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(55);
        session.MarkTicketConsumed(1, 7);
        session.CurrentZone = zone;
        var logger = new CapturingLogger<AttackHandler>();
        var handler = new AttackHandler(new AttackService(), logger);

        handler.Handle(new AttackRequest { AttackInfo = Attack(3, 42) }, session);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Debug);
        Assert.Contains("55", entry.Message); // session id
        Assert.Contains("7", entry.Message); // attacking character id
        Assert.Contains("case 3", entry.Message); // real mCase value, not a placeholder
        Assert.Contains("target server-index 42", entry.Message); // real target, not a placeholder
    }

    [Fact]
    public void Handle_CaseOutOfRange_StillLogsAttackReceived_BeforeRejecting()
    {
        // The entry log fires unconditionally before the anti-fuzzing case-range gate, so even a rejected
        // attack (case outside 1-6) must still have produced the "attack received" line -- logging must never
        // silently skip the malformed/malicious path, which is exactly the traffic an operator most wants
        // visible.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(56);
        session.MarkTicketConsumed(1, 8);
        session.CurrentZone = zone;
        var logger = new CapturingLogger<AttackHandler>();
        var handler = new AttackHandler(new AttackService(), logger);

        handler.Handle(new AttackRequest { AttackInfo = Attack(99, 1) }, session);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("case 99"));
        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}
