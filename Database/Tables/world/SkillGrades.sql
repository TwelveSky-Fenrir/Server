-- Normalizes SKILL_INFO's gGradeInfo[2]: exactly 2 rows per skill (grade 0/1), never sparse.
-- The CHECKs below mirror legacy's load-time field validation (MyShm::Skill_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:1277-1497), same rationale as world.Skills's own CHECKs. Deliberately NOT
-- retyping any TINYINT column here even though several of these bounds (1000/10000) exceed TINYINT's
-- 0-255 storage ceiling: for those columns the floor is real new enforcement but the upper bound is
-- unreachable in practice beneath the column's own narrower ceiling. Widening those columns to SMALLINT
-- would be the fully faithful fix, but it ripples into Fenrir.Data.Abstractions/World/SkillDtos.cs
-- (currently `byte`-typed) and every Skills domain consumer of those DTOs -- a cross-cutting schema/DTO
-- decision, not something folded silently into this table's definition. The full-fidelity CHECK bound is
-- kept anyway so no follow-up constraint change is needed if/when those columns are ever widened. Flag for
-- fenrir-solution-architect if a real skill ever needs one of these fields above 255.
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
    CONSTRAINT CK_SkillGrades_GradeIndex CHECK (GradeIndex BETWEEN 0 AND 1),
    CONSTRAINT CK_SkillGrades_ManaUse CHECK (ManaUse BETWEEN 0 AND 10000),
    CONSTRAINT CK_SkillGrades_RecoverInfo1 CHECK (RecoverInfo1 BETWEEN 0 AND 10000),
    CONSTRAINT CK_SkillGrades_RecoverInfo2 CHECK (RecoverInfo2 BETWEEN 0 AND 10000),
    CONSTRAINT CK_SkillGrades_StunAttack CHECK (StunAttack BETWEEN 0 AND 100),
    CONSTRAINT CK_SkillGrades_StunDefense CHECK (StunDefense BETWEEN 0 AND 100),
    CONSTRAINT CK_SkillGrades_FastRunSpeed CHECK (FastRunSpeed BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackInfo1 CHECK (AttackInfo1 BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackInfo2 CHECK (AttackInfo2 BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackInfo3 CHECK (AttackInfo3 BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_RunTime CHECK (RunTime BETWEEN 0 AND 10000),
    CONSTRAINT CK_SkillGrades_ChargingDamageUp CHECK (ChargingDamageUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackPowerUp CHECK (AttackPowerUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_DefensePowerUp CHECK (DefensePowerUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackSuccessUp CHECK (AttackSuccessUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackBlockUp CHECK (AttackBlockUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_ElementAttackUp CHECK (ElementAttackUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_ElementDefenseUp CHECK (ElementDefenseUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_AttackSpeedUp CHECK (AttackSpeedUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_RunSpeedUp CHECK (RunSpeedUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_ShieldLifeUp CHECK (ShieldLifeUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_LuckUp CHECK (LuckUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_CriticalUp CHECK (CriticalUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_ReturnSuccessUp CHECK (ReturnSuccessUp BETWEEN 0 AND 10000),
    CONSTRAINT CK_SkillGrades_StunDefenseUp CHECK (StunDefenseUp BETWEEN 0 AND 1000),
    CONSTRAINT CK_SkillGrades_DestroySuccessUp CHECK (DestroySuccessUp BETWEEN 0 AND 10000)
);
