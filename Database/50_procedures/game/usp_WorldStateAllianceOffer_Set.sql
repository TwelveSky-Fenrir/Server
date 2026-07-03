-- database/50_procedures/game/usp_WorldStateAllianceOffer_Set.sql
-- Contract: upsert one (offering tribe, candidate tribe) alliance-offer row.
-- Params:
--   @FromTribeId TINYINT -- game.Tribes.TribeId, the offering tribe
--   @ToTribeId   TINYINT -- game.Tribes.TribeId, the candidate tribe (must differ from @FromTribeId --
--                           CK_WorldStateAllianceOffers_NotSelf)
--   @IsAccepted  BIT
-- Result set: none.
-- Idempotent: yes. DELETE-then-INSERT, never MERGE (architecture reference §12.3), matching
-- game.usp_GuildNotice_Set's own idiom: the DELETE is a no-op the first time a given pair is ever set.
CREATE PROCEDURE game.usp_WorldStateAllianceOffer_Set @FromTribeId TINYINT,
    @ToTribeId   TINYINT,
    @IsAccepted  BIT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.WorldStateAllianceOffers
WHERE FromTribeId = @FromTribeId
  AND ToTribeId = @ToTribeId;

INSERT INTO game.WorldStateAllianceOffers (FromTribeId, ToTribeId, IsAccepted)
VALUES (@FromTribeId, @ToTribeId, @IsAccepted);
END;
