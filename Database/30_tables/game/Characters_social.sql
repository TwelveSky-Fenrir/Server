-- Additive social migration for game.Characters (Server Logic chapter, Phase C/V6 Social), same
-- rationale as Characters_progression.sql: the migrator journal (admin.SchemaVersions) forbids changing
-- an applied script's content, so a NEW script is the sanctioned corrective path (_manifest.txt header).
-- Legacy mapping (wAvatar/AVATAR_INFO, STRUCT.h):
--   TeacherCharacterId <- aTeacher[MAX_AVATAR_NAME_LENGTH] (name of this character's MASTER, if any).
--   StudentCharacterId <- aStudent[MAX_AVATAR_NAME_LENGTH] (name of this character's STUDENT, if any).
-- Both legacy fields are a single VARCHAR name each (not an array), so -- unlike GuildMembers/
-- TribeSubMasters (>1 slot -> child table) -- these are genuinely scalar and belong as columns, same
-- shape as Guilds.MasterCharacterId. Nullable: "no teacher"/"no student" is the default, common case.
-- Real FK (not a name) for the same reason every other *CharacterId column in this schema is: a name is
-- not a stable reference (renames/deletes must not silently orphan the relationship).
-- Deliberately NOT enforcing "at most one student globally" or "mentor chains" here -- CZ_TEACHER_ASK_SEND's
-- own preconditions (report ServerDocs/30_Fenrir_ServerLogic/contracts/05_social.md: emitter must have
-- neither a teacher nor a student already, target must be lower level, same tribe) are app-side gates
-- (Fenrir.Application.Game.Social.Mentor.MentorRegistry), exactly like every other social precondition in
-- this lot (duel/trade/friend) is app-side, not a DB CHECK.
ALTER TABLE game.Characters
    ADD TeacherCharacterId INT NULL,
        StudentCharacterId INT NULL,
        CONSTRAINT FK_Characters_TeacherCharacter FOREIGN KEY (TeacherCharacterId) REFERENCES game.Characters (CharacterId),
        CONSTRAINT FK_Characters_StudentCharacter FOREIGN KEY (StudentCharacterId) REFERENCES game.Characters (CharacterId),
        CONSTRAINT CK_Characters_TeacherNotSelf CHECK (TeacherCharacterId IS NULL OR TeacherCharacterId <> CharacterId),
        CONSTRAINT CK_Characters_StudentNotSelf CHECK (StudentCharacterId IS NULL OR StudentCharacterId <> CharacterId);
GO
