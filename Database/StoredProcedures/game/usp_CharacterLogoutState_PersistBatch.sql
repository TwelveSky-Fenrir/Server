CREATE PROCEDURE game.usp_CharacterLogoutState_PersistBatch @Snapshots game.tvp_CharacterLogoutState READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE ls
    SET ls.LastZone      = s.LastZone,
        ls.PosX          = s.PosX,
        ls.PosY          = s.PosY,
        ls.PosZ          = s.PosZ,
        ls.Life          = s.Life,
        ls.Mana          = s.Mana,
        ls.FlushSequence = s.FlushSequence,
        ls.CapturedAtUtc = SYSUTCDATETIME()
    FROM game.CharacterLogoutState AS ls
             JOIN @Snapshots AS s ON s.CharacterId = ls.CharacterId
    WHERE s.FlushSequence > ls.FlushSequence;

    INSERT INTO game.CharacterLogoutState (CharacterId, LastZone, PosX, PosY, PosZ, Life, Mana, FlushSequence,
                                           CapturedAtUtc)
    SELECT s.CharacterId,
           s.LastZone,
           s.PosX,
           s.PosY,
           s.PosZ,
           s.Life,
           s.Mana,
           s.FlushSequence,
           SYSUTCDATETIME()
    FROM @Snapshots AS s
             LEFT JOIN game.CharacterLogoutState AS ls ON ls.CharacterId = s.CharacterId
    WHERE ls.CharacterId IS NULL;

    COMMIT TRANSACTION;
END;
