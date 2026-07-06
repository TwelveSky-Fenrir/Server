-- Cluster-wide "concurrent player" tally for the login-time server-full quota gate: every row counts
-- regardless of ServerKind/ShardId (Login-side rows are accounts registered but not yet moved into a zone,
-- Game-side rows are players already in a zone) -- see runtime.AccountSessions' own header. Fenrir's stand-in
-- for the legacy session-broker process (ts25playuser's zero-argument ReturnPresentUserNum).
--
-- Interpreted, not native-compiled, like usp_AccountSession_ReapStale/RefreshAndGetKicked: WITH (SNAPSHOT)
-- makes the required isolation level explicit regardless of the caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_AccountSession_GetActiveCount
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*)
    FROM runtime.AccountSessions WITH (SNAPSHOT);
END;
