-- Server-side timer job (Login side, the one unsharded process in the topology), never on the client path.
-- Reports which rows were reaped and from which side (ServerKind) so the caller can log the two distinct
-- forced-timeout paths.
--
-- Interpreted, not native-compiled, unlike this feature's single-row hot-path procs: natively compiled
-- modules don't support the OUTPUT clause used here to capture the reaped rows. WITH (SNAPSHOT) makes the
-- required isolation level explicit regardless of the caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_AccountSession_ReapStale
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Reaped TABLE
        (
            AccountId  INT     NOT NULL,
            ServerKind TINYINT NOT NULL
        );

    DELETE
    FROM runtime.AccountSessions WITH (SNAPSHOT)
        OUTPUT deleted.AccountId, deleted.ServerKind INTO @Reaped (AccountId, ServerKind)
    WHERE LastRefreshedUtc <= DATEADD(MINUTE, -6, SYSUTCDATETIME());

    SELECT AccountId, ServerKind
    FROM @Reaped;
END;
