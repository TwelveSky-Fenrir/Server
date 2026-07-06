-- Upsert semantics for the shared IP-flood-block write path (IpFloodGuard, Network.Dispatch.FloodProtection,
-- and any GM-ban tooling reusing IFirewallRuleRepository.BlockAsync). Unlike usp_FirewallRule_Add (insert-only,
-- THROWs 50303 on a pre-existing row -- appropriate for an explicit GM "add a new rule" action, where a
-- duplicate is an operator mistake worth surfacing), a second automated block against an already-blocked IP
-- must behave like legacy's own silent SetSqlBlockIP upsert (Server/Header/enter_count.cpp:59-63), not fail
-- loudly. Mirrors the same UPDATE-then-conditional-INSERT shape as auth.usp_AccountPin_Set.
CREATE PROCEDURE admin.usp_FirewallRule_Upsert @IpAddress VARCHAR(45),
    @RuleType  TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE admin.FirewallRules
    SET RuleType = @RuleType
    WHERE IpAddress = @IpAddress;

    IF @@ROWCOUNT = 0
        INSERT INTO admin.FirewallRules (IpAddress, RuleType)
        VALUES (@IpAddress, @RuleType);
END;
