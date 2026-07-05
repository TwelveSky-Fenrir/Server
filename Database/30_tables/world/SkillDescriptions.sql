-- Normalizes SKILL_INFO's sDescription[10][51]: one row per non-empty line (0-7 lines/skill, variable), unlike ITEM_INFO's fixed 3-line description kept as plain columns on world.Items.
CREATE TABLE world.SkillDescriptions
(
    SkillId   INT     NOT NULL,
    LineIndex TINYINT NOT NULL,
    Text      NVARCHAR(51) NOT NULL,
    CONSTRAINT PK_SkillDescriptions PRIMARY KEY CLUSTERED (SkillId, LineIndex),
    CONSTRAINT FK_SkillDescriptions_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_SkillDescriptions_LineIndex CHECK (LineIndex BETWEEN 0 AND 9)
);
