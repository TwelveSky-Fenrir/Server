-- database/50_procedures/game/usp_Tribe_SetMaster.sql
-- TRIBE_WORK tSort 55's tally write: appoints (or, with a null candidate, vacates) one tribe's Force
-- Leader. Unlike usp_Guild_SetMaster there is no membership-role table to flip -- ReturnTribeRole derives
-- directly off game.Tribes.MasterCharacterId (see usp_TribeRole_GetForCharacter).
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
