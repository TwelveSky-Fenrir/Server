-- database/50_procedures/game/usp_WorldStateAllianceOffer_Set.sql
-- Idempotent upsert via DELETE-then-INSERT (never MERGE, per architecture reference §12.3).
CREATE PROCEDURE game.usp_WorldStateAllianceOffer_Set @FromTribeId TINYINT,
                                                      @ToTribeId TINYINT,
                                                      @IsAccepted BIT
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
