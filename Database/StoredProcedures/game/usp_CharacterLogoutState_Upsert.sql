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
-- Idempotent upsert: an UPDATE guarded on the PK, then an INSERT only when no row existed yet. Two concurrent
-- calls for the SAME @CharacterId (e.g. a duplicated flush or a rapid re-enter racing a still-in-flight
-- capture) can both see the UPDATE affect 0 rows and both attempt the INSERT -- the loser hits
-- PK_CharacterLogoutState. Under XACT_ABORT ON, that constraint violation happening inside this proc's own
-- explicit BEGIN TRANSACTION dooms the transaction (XACT_STATE() = -1, verified against Microsoft Learn's
-- TRY...CATCH/XACT_STATE docs) rather than merely failing the one statement, so the CATCH block cannot just
-- keep writing in the same transaction the way the no-explicit-transaction auth fixes do -- it must
-- ROLLBACK TRANSACTION first (the only operation an uncommittable transaction still permits besides a read),
-- then retry the UPDATE alone as a fresh autocommit statement: the row now exists (the winner's INSERT
-- committed), so the retried UPDATE deterministically applies this caller's own values as the final state --
-- consistent with this proc's existing last-write-wins upsert contract, and it never throws a new domain
-- error for what was always a benign race on a best-effort snapshot, never the world-spawn source.
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
        BEGIN
            BEGIN TRY
                INSERT INTO game.CharacterLogoutState (CharacterId, LastZone, PosX, PosY, PosZ, Life, Mana, CapturedAtUtc)
                VALUES (@CharacterId, @LastZone, @PosX, @PosY, @PosZ, @Life, @Mana, SYSUTCDATETIME());
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                UPDATE game.CharacterLogoutState
                SET LastZone      = @LastZone,
                    PosX          = @PosX,
                    PosY          = @PosY,
                    PosZ          = @PosZ,
                    Life          = @Life,
                    Mana          = @Mana,
                    CapturedAtUtc = SYSUTCDATETIME()
                WHERE CharacterId = @CharacterId;

                RETURN;
            END CATCH;
        END;

    COMMIT TRANSACTION;
END;
