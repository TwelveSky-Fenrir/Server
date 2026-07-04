-- database/50_procedures/game/usp_CharacterMentor_Bond.sql
-- Contract: CZ_TEACHER_START_SEND (opcode 62) -- establishes a teacher/student bond atomically on BOTH
-- characters' rows in one transaction (the master's StudentCharacterId AND the student's
-- TeacherCharacterId must never be updated as two independent statements -- a fault between them would
-- leave one side bonded and the other not, an inconsistent pair no read path expects). MentorRegistry
-- has already verified every precondition (levels, tribe, neither side already bonded) before calling
-- this -- this proc is the durable write, not the gate.
-- Params:
--   @MasterCharacterId  INT
--   @StudentCharacterId INT
-- Result set: none.
-- Idempotent: yes -- re-bonding the same pair leaves the same end state.
CREATE PROCEDURE game.usp_CharacterMentor_Bond @MasterCharacterId  INT,
    @StudentCharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

UPDATE game.Characters
SET StudentCharacterId = @StudentCharacterId
WHERE CharacterId = @MasterCharacterId;

UPDATE game.Characters
SET TeacherCharacterId = @MasterCharacterId
WHERE CharacterId = @StudentCharacterId;

COMMIT TRANSACTION;
END;
