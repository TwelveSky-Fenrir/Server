CREATE PROCEDURE game.usp_CharacterMentor_Bond @MasterCharacterId INT,
                                               @StudentCharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    IF @MasterCharacterId < @StudentCharacterId
        BEGIN
            UPDATE game.Characters
            SET StudentCharacterId = @StudentCharacterId
            WHERE CharacterId = @MasterCharacterId;

            UPDATE game.Characters
            SET TeacherCharacterId = @MasterCharacterId
            WHERE CharacterId = @StudentCharacterId;
        END
    ELSE
        BEGIN
            UPDATE game.Characters
            SET TeacherCharacterId = @MasterCharacterId
            WHERE CharacterId = @StudentCharacterId;

            UPDATE game.Characters
            SET StudentCharacterId = @StudentCharacterId
            WHERE CharacterId = @MasterCharacterId;
        END;

    COMMIT TRANSACTION;
END;
