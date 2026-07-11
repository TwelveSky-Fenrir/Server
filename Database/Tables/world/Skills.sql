-- Legacy SKILL_INFO; one row per record where Index != 0 (147 of 300 raw records are blank filler rows all sharing Index == 0).
-- Description[10] normalizes into world.SkillDescriptions; GradeInfo[2] normalizes into world.SkillGrades (always exactly 2 rows/skill).
-- The CHECKs below mirror legacy's load-time field validation (MyShm::Skill_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:1277-1497), which rejects the whole 300-slot Load_Skill call on the first
-- offending record -- fatal to both ts25sharemem's and every ts25zone process's boot sequence. A relational
-- CHECK is the closest Fenrir equivalent of "this value can never be stored" (stricter in one respect:
-- enforced on every write, not just one boot-time pass). MaxUpgradePoint's floor of 1 is the highest-severity
-- one: SKILLSYSTEM::ReturnSkillValue (Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:186) divides by it
-- unconditionally with no zero-check at the point of division, so legacy's load-time >= 1 check was the sole
-- guard against a divide-by-zero there -- this floor is that guard's equivalent and a genuine new
-- restriction (the TINYINT column alone permits 0).
-- Application/Fenrir.Application.Game.Domain/Skills/SkillCatalog.cs already short-circuits to zero on a
-- non-positive MaxUpgradePoint at read time as defense in depth, but this constraint is what stops the
-- out-of-range value from ever being stored in the first place.
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
    CONSTRAINT PK_Skills PRIMARY KEY CLUSTERED (SkillId),
    CONSTRAINT CK_Skills_Type CHECK (Type BETWEEN 1 AND 4),
    CONSTRAINT CK_Skills_AttackType CHECK (AttackType BETWEEN 1 AND 5),
    CONSTRAINT CK_Skills_DataNumber2D CHECK (DataNumber2D BETWEEN 1 AND 10000),
    CONSTRAINT CK_Skills_TribeInfo1 CHECK (TribeInfo1 BETWEEN 1 AND 4),
    CONSTRAINT CK_Skills_TribeInfo2 CHECK (TribeInfo2 BETWEEN 1 AND 10),
    CONSTRAINT CK_Skills_LearnSkillPoint CHECK (LearnSkillPoint BETWEEN 1 AND 1000),
    CONSTRAINT CK_Skills_MaxUpgradePoint CHECK (MaxUpgradePoint BETWEEN 1 AND 1000), -- divide-by-zero guard, see header comment
    CONSTRAINT CK_Skills_TotalHitNumber CHECK (TotalHitNumber BETWEEN 0 AND 10),
    CONSTRAINT CK_Skills_ValidRadius CHECK (ValidRadius BETWEEN 0 AND 1000)
);
