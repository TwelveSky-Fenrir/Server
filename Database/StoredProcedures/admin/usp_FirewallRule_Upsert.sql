-- Upsert semantics for the shared IP-flood-block write path (IpFloodGuard, Network.Dispatch.FloodProtection,
-- and any GM-ban tooling reusing IFirewallRuleRepository.BlockAsync). Unlike usp_FirewallRule_Add (insert-only,
-- THROWs 50303 on a pre-existing row -- appropriate for an explicit GM "add a new rule" action, where a
-- duplicate is an operator mistake worth surfacing), a second automated block against an already-blocked IP
-- must behave like legacy's own silent SetSqlBlockIP upsert (Server/Header/enter_count.cpp:59-63), not fail
-- loudly. Mirrors the same UPDATE-then-conditional-INSERT shape as auth.usp_AccountPin_Set (that procedure
-- has the identical race window described below -- not fixed here, out of this pass's admin-schema scope).
--
-- Deliberate exception to "don't reach for HOLDLOCK/UPDLOCK out of habit": under bare READ COMMITTED
-- (even with RCSI/OPTIMIZED_LOCKING), the UPDATE's row/key locks are released as soon as it completes since
-- there is no surrounding explicit transaction, leaving a window between "UPDATE saw 0 matching rows" and
-- "the INSERT executes" where two concurrent callers racing the SAME brand-new IpAddress (e.g. two
-- IpFloodGuard triggers for the same attacking IP arriving on different connections at once) can both take
-- the INSERT branch -- the loser then hits UQ_FirewallRules_IpAddress and surfaces a raw constraint-violation
-- error instead of the silent upsert this procedure's own contract promises. BEGIN TRANSACTION plus
-- UPDLOCK+HOLDLOCK on the UPDATE closes that window: HOLDLOCK holds the lock until COMMIT instead of
-- releasing it immediately, so a second concurrent caller for the same IpAddress blocks behind the first's
-- transaction and then sees the row the first one just wrote -- taking the UPDATE branch instead of racing
-- the INSERT. (Verified against Microsoft's own guidance for this exact "UPDATE probe + conditional INSERT"
-- pattern -- see this repo's fenrir-database-engineer session notes for the citation trail.)
CREATE PROCEDURE admin.usp_FirewallRule_Upsert @IpAddress VARCHAR(45),
                                               @RuleType TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE admin.FirewallRules WITH (UPDLOCK, HOLDLOCK)
    SET RuleType = @RuleType
    WHERE IpAddress = @IpAddress;

    IF
        @@ROWCOUNT = 0
        INSERT INTO admin.FirewallRules (IpAddress, RuleType)
        VALUES (@IpAddress, @RuleType);

    COMMIT TRANSACTION;
END;
