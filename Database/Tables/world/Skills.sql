-- Legacy SKILL_INFO; one row per record where Index != 0 (147 of 300 raw records are blank filler rows all sharing Index == 0).
-- Description[10] normalizes into world.SkillDescriptions; GradeInfo[2] normalizes into world.SkillGrades (always exactly 2 rows/skill).
CREATE TABLE world.Skills
(
    SkillId         INT          NOT NULL,
    Name            NVARCHAR(25) NOT NULL,
    Type            TINYINT      NOT NULL,
    AttackType      TINYINT      NOT NULL,
    DataNumber2D    SMALLINT     NOT NULL,
    TribeInfo1      TINYINT      NOT NULL,
    TribeInfo2      TINYINT      NOT NULL,
    LearnSkillPoint TINYINT      NOT NULL,
    MaxUpgradePoint TINYINT      NOT NULL,
    TotalHitNumber  TINYINT      NOT NULL,
    ValidRadius     SMALLINT     NOT NULL,
    CONSTRAINT PK_Skills PRIMARY KEY CLUSTERED (SkillId)
);
