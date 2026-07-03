-- Normalizes SKILL_INFO's sDescription[10][51] (Header/Protocol/STRUCT.h:123-146): one row per
-- non-empty description line, not 10 mostly-empty NVARCHAR columns. Real data check before committing
-- to this shape: description-line indices 7-9 are populated in 0 of 153 real skills, and even indices
-- 0-6 vary from 0 to 7 populated lines per skill (average ~3.9) -- genuinely sparse/variable-length,
-- unlike ITEM_INFO's always-3-line description (kept as plain columns on world.Items).
CREATE TABLE world.SkillDescriptions
(
    SkillId   INT     NOT NULL,
    LineIndex TINYINT NOT NULL,
    Text      NVARCHAR(51) NOT NULL,
    CONSTRAINT PK_SkillDescriptions PRIMARY KEY CLUSTERED (SkillId, LineIndex),
    CONSTRAINT FK_SkillDescriptions_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_SkillDescriptions_LineIndex CHECK (LineIndex BETWEEN 0 AND 9)
);
