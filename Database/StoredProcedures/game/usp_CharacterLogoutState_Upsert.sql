-- Capture (populate) one character's logout-info snapshot (game.CharacterLogoutState). Fenrir's stand-in for
-- the legacy UPDATE_LOGOUT_INFO capture macro (Server/Header/Protocol/DEFINE.h:750-757: element 0 = zone,
-- 1-3 = position, 4 = life, 5 = mana), which legacy re-stamped continuously through a live zone session
-- (on movement/teleport, on a ~2-minute periodic snapshot, on quit) and flushed to six persisted columns.
--
-- Fenrir calls this from EnterWorldService at successful world entry (CharacterLogoutStateRepository), re-
-- anchoring the snapshot to game.Characters' just-loaded, authoritative placement each session so the two can
-- never meaningfully diverge. The continuous in-session re-capture (movement/teleport/periodic/quit) is a
-- follow-up: it belongs in the write-behind flush / disconnect hook, not the enter-world path -- see the
-- repository interface's own remarks. No range validation on any value (matching legacy, which enforces none
-- at capture); the login redirect / tribe-consistency correction that consumes LastZone is a separate,
-- deferred read-side concern.
--
-- Idempotent upsert: an UPDATE guarded on the PK, then an INSERT only when no row existed yet. Two potential
-- writes -> XACT_ABORT ON + an explicit transaction so a THROW/runtime error can never leave a half-applied
-- row (a duplicate-key race between the UPDATE miss and the INSERT surfaces as a PK violation the caller
-- treats as a benign, retry-nothing failure -- this is a best-effort snapshot, never the world-spawn source).
CREATE PROCEDURE game.usp_CharacterLogoutState_Upsert @CharacterId INT,
                                                      @LastZone INT,
                                                      @PosX INT,
                                                      @PosY INT,
                                                      @PosZ INT,
                                                      @Life INT,
                                                      @Mana INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.CharacterLogoutState
    SET LastZone      = @LastZone,
        PosX          = @PosX,
        PosY          = @PosY,
        PosZ          = @PosZ,
        Life          = @Life,
        Mana          = @Mana,
        CapturedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        INSERT INTO game.CharacterLogoutState (CharacterId, LastZone, PosX, PosY, PosZ, Life, Mana, CapturedAtUtc)
        VALUES (@CharacterId, @LastZone, @PosX, @PosY, @PosZ, @Life, @Mana, SYSUTCDATETIME());

    COMMIT TRANSACTION;
END;
