-- database/50_procedures/game/usp_WorldStateAllianceOffer_Set.sql
-- Idempotent upsert via DELETE-then-INSERT (never MERGE, per architecture reference §12.3).
--
-- Hardening fix (2026-07-12 pass): the DELETE and INSERT used to run as two independent autocommit
-- statements (no explicit transaction), which is a plain compliance gap against this schema's own
-- "any procedure with more than one write statement wraps them in BEGIN TRANSACTION/COMMIT TRANSACTION" rule
-- -- a crash between the two would leave the offer deleted with nothing re-inserted. Now wrapped in one
-- transaction, same shape as every other multi-statement writer in this schema. This also closes a narrow
-- concurrent-race window for two racing Set calls on the exact same (FromTribeId, ToTribeId) pair (a losing
-- caller now fails cleanly on PK_WorldStateAllianceOffers instead of a case where one caller's DELETE could
-- commit independently of its own INSERT); alliance offers are a single-tribe-leader manual action, so this
-- is defense-in-depth rather than a hot concurrent path.
CREATE PROCEDURE game.usp_WorldStateAllianceOffer_Set @FromTribeId TINYINT,
                                                      @ToTribeId TINYINT,
                                                      @IsAccepted BIT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE
    FROM game.WorldStateAllianceOffers
    WHERE FromTribeId = @FromTribeId
      AND ToTribeId = @ToTribeId;

    INSERT INTO game.WorldStateAllianceOffers (FromTribeId, ToTribeId, IsAccepted)
    VALUES (@FromTribeId, @ToTribeId, @IsAccepted);

    COMMIT TRANSACTION;
END;
