-- Additive V9 (Progression) migration for game.Characters -- separate script instead of editing an
-- already-applied one (the migrator journal, admin.SchemaVersions, forbids changing an applied script's
-- content; additive ALTER is the sanctioned corrective path, same convention Characters_commerce.sql/
-- Characters_progression.sql/Characters_social.sql all already document).
-- Legacy mapping: TeacherPoint <- wAvatar.aTeacherPoint (quest reward type 5, "GL_614_QUEST_TEACHER_POINT",
-- verified S04_MyWork02.cpp:7420-7423: `wAvatar.aTeacherPoint += tQUEST_INFO->qReward[index01][1];`).
-- CORRECTION (review finding, Phase C/V9): world.QuestRewards seed data has 36 real rows with
-- RewardType=5, several granting 100,000 points, that had nowhere to land before this column existed --
-- QuestStateMachine.Complete's own reward switch silently dropped this reward type entirely.
ALTER TABLE game.Characters
    ADD TeacherPoint INT NOT NULL CONSTRAINT DF_Characters_TeacherPoint DEFAULT 0;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_TeacherPoint CHECK (TeacherPoint >= 0);
GO
