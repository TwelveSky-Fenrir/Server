CREATE PROCEDURE game.usp_Tribe_SetMaster @TribeId TINYINT,
                                          @NewMasterCharacterId INT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @TribeId)
        THROW 50330, N'Tribe not found.', 1;

    IF
        @NewMasterCharacterId IS NOT NULL AND NOT EXISTS
            (SELECT 1 FROM game.Characters WHERE CharacterId = @NewMasterCharacterId AND Tribe = @TribeId)
        THROW 50331, N'Character is not a member of this tribe.', 1;

    UPDATE game.Tribes
    SET MasterCharacterId = @NewMasterCharacterId
    WHERE TribeId = @TribeId;
END;
