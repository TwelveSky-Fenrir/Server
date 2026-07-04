-- Normalizes SKILL_INFO's gGradeInfo[2]: exactly 2 rows per skill (grade 0/1), never sparse.
CREATE TABLE world.SkillGrades
(
    SkillId          INT      NOT NULL,
    GradeIndex       TINYINT  NOT NULL,
    ManaUse          SMALLINT NOT NULL,
    RecoverInfo1     TINYINT  NOT NULL,
    RecoverInfo2     TINYINT  NOT NULL,
    StunAttack       TINYINT  NOT NULL,
    StunDefense      TINYINT  NOT NULL,
    FastRunSpeed     SMALLINT NOT NULL,
    AttackInfo1      SMALLINT NOT NULL,
    AttackInfo2      TINYINT  NOT NULL,
    AttackInfo3      TINYINT  NOT NULL, -- always 0 in current data; reserved slot in AttackInfo[3], kept for fidelity
    RunTime          SMALLINT NOT NULL,
    ChargingDamageUp TINYINT  NOT NULL,
    AttackPowerUp    TINYINT  NOT NULL,
    DefensePowerUp   TINYINT  NOT NULL,
    AttackSuccessUp  TINYINT  NOT NULL,
    AttackBlockUp    TINYINT  NOT NULL,
    ElementAttackUp  TINYINT  NOT NULL,
    ElementDefenseUp TINYINT  NOT NULL, -- always 0 in current data; reserved, kept for fidelity
    AttackSpeedUp    TINYINT  NOT NULL,
    RunSpeedUp       TINYINT  NOT NULL,
    ShieldLifeUp     TINYINT  NOT NULL,
    LuckUp           TINYINT  NOT NULL,
    CriticalUp       TINYINT  NOT NULL,
    ReturnSuccessUp  TINYINT  NOT NULL,
    StunDefenseUp    TINYINT  NOT NULL,
    DestroySuccessUp TINYINT  NOT NULL,
    CONSTRAINT PK_SkillGrades PRIMARY KEY CLUSTERED (SkillId, GradeIndex),
    CONSTRAINT FK_SkillGrades_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_SkillGrades_GradeIndex CHECK (GradeIndex BETWEEN 0 AND 1)
);
