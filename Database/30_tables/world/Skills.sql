-- Source: legacy SKILL_INFO (005_00003.IMG via SkillReader.ReadAll -- no runtime patch exists for this
-- file). One row per SkillRecord where Index != 0. The domain brief for this migration assumed "no
-- filtering, unlike items" for skills, but the real decoded data disproves that: 147 of the 300 raw
-- records are fully blank (empty name, every field 0) and every single one of them shares Index == 0
-- (verified: exactly one duplicate-index group exists across all 300 records, at index 0, and all of
-- its rows are blank -- no other index repeats). Since SkillId is a real, non-IDENTITY primary key that
-- another domain's FOREIGN KEY already depends on by exact name/type (per the cross-domain PK contract),
-- keeping 147 rows at SkillId = 0 isn't just noise, it's a PRIMARY KEY violation -- so this filter is
-- required, mirroring the same "index 0 = unused legacy slot" convention items already have.
-- Description[10] normalizes into world.SkillDescriptions (see that file): indices 7-9 are never
-- populated in any real row, and even 0-6 vary a lot per skill (0 to 7 lines, average ~3.9), so a block
-- of mostly-empty fixed columns would misrepresent how sparse it actually is.
-- GradeInfo[2] normalizes into world.SkillGrades: always exactly 2 rows per skill (grade 0/1), never
-- sparse -- one row per grade, not 22*2 columns crammed onto this row.
CREATE TABLE world.Skills
(
    SkillId         INT      NOT NULL,
    Name            NVARCHAR(25) NOT NULL,
    Type            TINYINT  NOT NULL,
    AttackType      TINYINT  NOT NULL,
    DataNumber2D    SMALLINT NOT NULL,
    TribeInfo1      TINYINT  NOT NULL,
    TribeInfo2      TINYINT  NOT NULL,
    LearnSkillPoint TINYINT  NOT NULL,
    MaxUpgradePoint TINYINT  NOT NULL,
    TotalHitNumber  TINYINT  NOT NULL,
    ValidRadius     SMALLINT NOT NULL,
    CONSTRAINT PK_Skills PRIMARY KEY CLUSTERED (SkillId)
);
