-- Workstream D3 -- the DB half of Fenrir.Network.Dispatch.FloodProtection.FirewallAllowlistReconcileService's
-- legacy RemoveIPTick reconcile (Server/ts25firewall/main.cpp:682-698). See
-- Fenrir.Data.Security.FirewallRuleRepository.ReconcileAllowlistAsync's own remarks for the full citation
-- trail and for exactly why the other two legacy sub-steps (reseed 11 fixed infrastructure IPs; resync every
-- current account IP) are deliberately NOT reproduced here -- both need data (literal IP values; a
-- per-account "current IP" column) this project's schema does not have and its rules forbid guessing.
--
-- Legacy's own prune step (main.cpp:684) deletes an ALLOW-designated row unless its IP matches a currently
-- known member; Fenrir tracks no member-IP roster at all, so every allow-designated row is unconditionally
-- "stale" under this repository's model and is deleted unconditionally. RuleType values 0/2/4/5 (TCP_ALLOW/
-- ANY_ALLOW/TCP_ALLOW_CF/TCP_ALLOW_IPRANGE, Server/ts25firewall/firewall.h:20-27) are the allow designations;
-- 1/3 (TCP_BLOCK/ANY_BLOCK) are left untouched -- this procedure structurally cannot delete a block row.
CREATE PROCEDURE admin.usp_FirewallRule_ReconcileAllowlist
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM admin.FirewallRules
    WHERE RuleType IN (0, 2, 4, 5);
END;
