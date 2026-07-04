-- database/50_procedures/game/usp_CharacterMentor_ClearForCharacter.sql
-- Contract: CZ_TEACHER_END_SEND (opcode 63) -- clears BOTH TeacherCharacterId and StudentCharacterId on
-- the CALLING character's own row ONLY. Verified, preserved verbatim (contracts/05_social.md CZ_TEACHER_END_SEND
-- note): the legacy does NOT clean up the partner's opposite pointer here -- if A ends a bond where A is
-- the student, A's own aTeacher is cleared but A's former master's aStudent (still naming A) is left
-- dangling until the master separately calls their own END, or until the reciprocity check
-- (CZ_TEACHER_STATE_SEND/usp_CharacterMentor_GetForCharacter used from the OTHER side) reveals the break.
-- A documented legacy asymmetry, not a bug this proc "fixes".
-- Params:
--   @CharacterId INT
-- Result set: none.
-- Idempotent: yes (clearing an already-NULL pair is a no-op).
CREATE PROCEDURE game.usp_CharacterMentor_ClearForCharacter @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET TeacherCharacterId = NULL,
        StudentCharacterId = NULL
    WHERE CharacterId = @CharacterId;
END;
