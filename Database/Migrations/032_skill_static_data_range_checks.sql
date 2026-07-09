-- Skill static-data load-time field validation (legacy SKILLSYSTEM::Init / MyShm::Load_Skill /
-- MyShm::Skill_CheckValidElement) -- fenrir-database-engineer behavior contract. Closes the confirmed gap:
-- neither world.Skills nor world.SkillGrades has ever had a CHECK constraint on any of the legacy-bounded
-- numeric fields below (Database/Tables/world/Skills.sql, Database/Tables/world/SkillGrades.sql -- both
-- already applied/journaled, so this ships as a new corrective migration rather than an in-place edit of
-- either, same pattern as Migrations/030_levels_rangeinfo3_check_constraint.sql and
-- Migrations/031_quest_step_and_reward_amount_range_checks.sql).
--
-- Legacy never lets an out-of-range skill record reach the shared-memory skill table at all:
-- MyShm::Skill_CheckValidElement (Server/Header/S15_MyShare.cpp:1277-1497) rejects the whole 300-slot
-- Load_Skill call on the first offending record (Server/Header/S15_MyShare.cpp:515-564), which is fatal to
-- both ts25sharemem's boot sequence (Server/ts25sharemem/main.cpp:38-72) and every ts25zone process's fixed
-- startup data-load sequence (Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:8-15,
-- Server/ts25zone/GameSystem/GameSystem_00_Load.cpp:3-36, Server/ts25zone/S01_MainApplication.cpp:265-283).
-- A relational CHECK constraint is the closest available Fenrir equivalent of "this value can never be
-- stored" -- stricter than legacy in one respect (enforced on every write, not just at one boot-time pass)
-- but matching legacy's intent that these fields are bounded.
--
-- MaxUpgradePoint is the highest-severity field here: SKILLSYSTEM::ReturnSkillValue
-- (Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:186) divides by it unconditionally with no zero-check
-- at the point of division, so legacy's load-time >= 1 check was the sole guard against a divide-by-zero
-- there. This constraint's floor of 1 is that guard's Fenrir equivalent and is a genuine, newly-enforced
-- restriction (world.Skills.MaxUpgradePoint's TINYINT column type alone permits 0).
-- Application/Fenrir.Application.Game.Domain/Skills/SkillCatalog.cs already short-circuits to zero on a
-- non-positive MaxUpgradePoint at read time as defense in depth, but nothing prevented an out-of-range value
-- from ever being stored until this constraint.
--
-- Deliberately NOT retyping any column in this script. Several world.SkillGrades columns are TINYINT
-- (storage ceiling 255) where the legacy ceiling below is 1000 or 10000 -- for those columns this
-- constraint's floor (where the legacy floor is 1, not 0: only world.Skills.LearnSkillPoint and
-- MaxUpgradePoint) is real new enforcement, but the upper bound is unreachable in practice beneath the
-- column's own narrower ceiling. Widening those columns to SMALLINT would be the fully faithful fix, but it
-- ripples into Fenrir.Data.Abstractions/World/SkillDtos.cs (currently `byte`-typed to match) and every
-- Application/Fenrir.Application.Game.Domain/Skills/* consumer of those DTOs -- a cross-cutting schema/DTO
-- decision outside a single database migration's scope, not something this script silently widens. Adding
-- the full-fidelity CHECK now (rather than a CHECK narrowed to match today's TINYINT ceiling) means no
-- second migration is needed if/when those columns are ever widened. Flag for fenrir-solution-architect if
-- a real skill ever needs one of these fields above 255.
--
-- Existing seed data (Migrations/Seed/world/070_skills.sql, 072_skill_grades.sql) checked against every
-- bound below before adding these as validating (WITH CHECK, the ALTER TABLE default) constraints: all 293
-- seeded skill rows and 586 seeded grade rows fall comfortably inside every new bound (e.g. MaxUpgradePoint
-- ranges 10-120 and is never 0; ManaUse tops out at 1817 of a 0-10000 ceiling; StunAttack/StunDefense top
-- out at 50 of a 0-100 ceiling) -- this migration cannot fail against the shipped dataset.
--
-- Deliberately NOT replicated here (per the contract's Edge cases section -- structural properties of the
-- legacy fixed-size C-string/array-of-structs storage with no direct analogue once represented relationally):
-- SkillId's 1-300 range and its must-equal-own-slot-position rule (SkillId is already PRIMARY KEY / NOT NULL
-- on world.Skills), and the Name/Description terminator-within-fixed-buffer checks (Name is bounded
-- NVARCHAR(25); each SkillDescriptions row is independently bounded NVARCHAR(51) with LineIndex 0-9 via the
-- existing CK_SkillDescriptions_LineIndex; SkillGrades already enforces "at most one grade-0 and one grade-1
-- row per skill" via its composite PK plus the existing CK_SkillGrades_GradeIndex).
ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_Type CHECK (Type BETWEEN 1 AND 4);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_AttackType CHECK (AttackType BETWEEN 1 AND 5);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_DataNumber2D CHECK (DataNumber2D BETWEEN 1 AND 10000);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_TribeInfo1 CHECK (TribeInfo1 BETWEEN 1 AND 4);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_TribeInfo2 CHECK (TribeInfo2 BETWEEN 1 AND 10);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_LearnSkillPoint CHECK (LearnSkillPoint BETWEEN 1 AND 1000);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_MaxUpgradePoint CHECK (MaxUpgradePoint BETWEEN 1 AND 1000);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_TotalHitNumber CHECK (TotalHitNumber BETWEEN 0 AND 10);
GO

ALTER TABLE world.Skills
    ADD CONSTRAINT CK_Skills_ValidRadius CHECK (ValidRadius BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ManaUse CHECK (ManaUse BETWEEN 0 AND 10000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_RecoverInfo1 CHECK (RecoverInfo1 BETWEEN 0 AND 10000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_RecoverInfo2 CHECK (RecoverInfo2 BETWEEN 0 AND 10000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_StunAttack CHECK (StunAttack BETWEEN 0 AND 100);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_StunDefense CHECK (StunDefense BETWEEN 0 AND 100);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_FastRunSpeed CHECK (FastRunSpeed BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackInfo1 CHECK (AttackInfo1 BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackInfo2 CHECK (AttackInfo2 BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackInfo3 CHECK (AttackInfo3 BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_RunTime CHECK (RunTime BETWEEN 0 AND 10000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ChargingDamageUp CHECK (ChargingDamageUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackPowerUp CHECK (AttackPowerUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_DefensePowerUp CHECK (DefensePowerUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackSuccessUp CHECK (AttackSuccessUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackBlockUp CHECK (AttackBlockUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ElementAttackUp CHECK (ElementAttackUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ElementDefenseUp CHECK (ElementDefenseUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_AttackSpeedUp CHECK (AttackSpeedUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_RunSpeedUp CHECK (RunSpeedUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ShieldLifeUp CHECK (ShieldLifeUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_LuckUp CHECK (LuckUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_CriticalUp CHECK (CriticalUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_ReturnSuccessUp CHECK (ReturnSuccessUp BETWEEN 0 AND 10000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_StunDefenseUp CHECK (StunDefenseUp BETWEEN 0 AND 1000);
GO

ALTER TABLE world.SkillGrades
    ADD CONSTRAINT CK_SkillGrades_DestroySuccessUp CHECK (DestroySuccessUp BETWEEN 0 AND 10000);
GO
