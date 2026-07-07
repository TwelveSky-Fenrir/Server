-- Legacy tsSkill[][3] (Server/ts25zone/S04_MyWork03.cpp:123-164), the tribe-conversion skill-equivalence
-- table consumed by TChangeSkill (:167-238) when a character converts to a different base tribe via the
-- Book of Noble Dragon/Royal Serpent/Grand Tiger V2 (world.Items 99014/99015/99016 -- see
-- game.usp_Character_ApplyTribeConversion). GroupIndex is a Fenrir-only synthetic row id: the legacy table
-- has no id column of its own, just fixed array position (row = one skill's 3 tribe-specific ids). TribeId
-- 0/1/2 mirrors the legacy array's own 3 columns (0=Noble Dragon, 1=Royal Serpent, 2=Grand Tiger -- the
-- same convention world.StarterKitSkills already uses for PreviousTribe). Every group has all 3 tribes
-- populated in the legacy table (no partial rows), hence the NOT NULL SkillId with no sentinel.
CREATE TABLE world.TribeSkillEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    SkillId    INT     NOT NULL,
    CONSTRAINT PK_TribeSkillEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeSkillEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeSkillEquivalences_Tribe_Skill UNIQUE (TribeId, SkillId),
    CONSTRAINT FK_TribeSkillEquivalences_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
