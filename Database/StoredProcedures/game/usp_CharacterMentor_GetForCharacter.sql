CREATE PROCEDURE game.usp_CharacterMentor_GetForCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT c.TeacherCharacterId,
       TeacherName = teacher.Name,
       c.StudentCharacterId,
       StudentName = student.Name
FROM game.Characters AS c
         LEFT JOIN game.Characters AS teacher ON teacher.CharacterId = c.TeacherCharacterId
         LEFT JOIN game.Characters AS student ON student.CharacterId = c.StudentCharacterId
WHERE c.CharacterId = @CharacterId;
END;
