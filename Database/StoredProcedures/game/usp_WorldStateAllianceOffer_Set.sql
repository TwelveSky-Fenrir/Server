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
