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
    CONSTRAINT CK_Skills_MaxUpgradePoint CHECK (MaxUpgradePoint BETWEEN 1 AND 1000), 
    CONSTRAINT CK_Skills_TotalHitNumber CHECK (TotalHitNumber BETWEEN 0 AND 10),
    CONSTRAINT CK_Skills_ValidRadius CHECK (ValidRadius BETWEEN 0 AND 1000)
);
