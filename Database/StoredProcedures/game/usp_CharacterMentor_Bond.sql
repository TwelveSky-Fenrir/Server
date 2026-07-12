-- Both sides update in one transaction: a fault between them must never leave one side bonded, the other not.
-- Preconditions (levels, tribe, not already bonded) are validated by the caller; this proc is the durable write only.
--
-- Deadlock-avoidance note: unlike usp_CharacterTrade_Execute (whose C#-side TradeLockHandler already acquires
-- both players' EconomyActionLock in ascending-CharacterId order before this kind of two-row update ever
-- reaches SQL, per that procedure's own header), no app-layer lock exists for mentor bonding -- so this
-- procedure closes the race itself: it always touches the row with the SMALLER CharacterId first, regardless
-- of which one is Master vs Student. Two concurrent bonds naming the same character pair in opposite roles
-- (e.g. Bond(A,B) racing Bond(B,A)) would otherwise lock game.Characters rows in opposite order and could
-- deadlock (error 1205) -- Microsoft's own guidance ("Deadlocks guide", "Analyze and prevent deadlocks in
-- Azure SQL Database") is to access rows in a consistent order across all transactions; fixing the order here
-- is strictly cheaper than adding a second lock-acquisition path on the C# side for this rare a race.
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
