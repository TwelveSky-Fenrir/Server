-- Standalone per-character rune-socket load for world-entry hydration into
-- PlayerRuntimeState.RuneSystem/RuneSystemStat. Deliberately a SEPARATE read from
-- usp_Character_GetForWorldEntry (not merged into its 5-result-set bundle): runes are a distinct, lazily-added
-- subsystem, and keeping the hot, ordinal-mapped world-entry bundle untouched follows the same
-- append-past-the-read-range discipline CharacterWorldSnapshotDto documents for itself.
--
-- Returns one row per OCCUPIED socket (SocketIndex 0-3); absent sockets are hydrated to (RuneItemId=0,
-- RuneStat=0) = empty by the caller. Legacy in-memory shape: wAvatar.aRuneSystem[4]/aRuneSystemStat[4]
-- (Server/Header/Protocol/STRUCT.h:572-573).
CREATE PROCEDURE game.usp_Character_GetRunes @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT SocketIndex,
           RuneItemId,
           RuneStat
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId
    ORDER BY SocketIndex;
END;
