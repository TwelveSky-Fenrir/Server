-- database/50_procedures/game/usp_CharacterMentor_GetForCharacter.sql
-- Contract: this character's teacher/student bond (game.Characters.TeacherCharacterId/StudentCharacterId,
-- Characters_social.sql), resolved to both the raw ids (PlayerRuntimeState.TeacherCharacterId/
-- StudentCharacterId, for live CZ_TEACHER_END_SEND/CZ_TEACHER_STATE_SEND reciprocity checks) and live
-- names (AVATAR_INFO's Teacher/Student fields) in one round trip.
-- Params:
--   @CharacterId INT
-- Result set (RS0), always exactly 1 row:
--   1. TeacherCharacterId INT NULL
--   2. TeacherName        NVARCHAR(13) NULL (empty/absent if no teacher)
--   3. StudentCharacterId INT NULL
--   4. StudentName        NVARCHAR(13) NULL (empty/absent if no student)
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_CharacterMentor_GetForCharacter @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.TeacherCharacterId, TeacherName = teacher.Name,
           c.StudentCharacterId, StudentName = student.Name
    FROM game.Characters AS c
             LEFT JOIN game.Characters AS teacher ON teacher.CharacterId = c.TeacherCharacterId
             LEFT JOIN game.Characters AS student ON student.CharacterId = c.StudentCharacterId
    WHERE c.CharacterId = @CharacterId;
END;
