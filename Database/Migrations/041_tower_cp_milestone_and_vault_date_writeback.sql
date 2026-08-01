-- Lot 15 -- TowerCpMilestoneCounter/InventoryDate/StoreDate : trois champs mutes en jeu cote Fenrir,
-- persistes par le legacy, jamais persistes ici. Confirme par recherche exhaustive avant redaction :
-- 0 resultat pour les trois dans src/Fenrir.Data*/Database/ (TVP, DTO, colonnes, procedures) avant ce script.
--
-- BUG DECOUVERT EN COURS DE REDACTION, CORRIGE AU PASSAGE (section 6 ci-dessous) : la redeclaration RS0 de
-- Migrations/040 permutait RankPoint/CloakLuckyBoxPity/CloakVariantBoxPity/MountVariantBoxPity avec
-- ImproveItemValue/AddItemValue/HighItemValue/TaiyanKeyTimer (ordre inverse de celui declare par
-- CharacterWorldSnapshotDto -- verifie programmatiquement, diff ordinal-par-ordinal des deux listes) :
-- lecture par ordinal, donc ces huit colonnes arrivaient croisees en RAM a chaque entree en monde (RankPoint
-- recevait la valeur d'ImproveItemValue, etc.), silencieusement, sans exception. La section 6 ci-dessous
-- restaure l'ordre correct ; verifie de nouveau apres correction (0 mismatch sur les 119 colonnes de RS0
-- projetees ici, WarriorPill/WarriorScroll exclues -- voir note plus bas).
--
--   TowerCpMilestoneCounter  aKillMonsterNum2, Server/Header/Protocol/STRUCT.h:388. Incremente a chaque kill
--                            qui satisfait ReturnFixedLevel(aLevel1+aLevel2) - mRealLevel < 10
--                            (Server/ts25zone/S07_MyGame02.cpp:2437-2439) ; a 1000 il repasse a 0 et paie
--                            killcp (S07_MyGame02.cpp:2440-2463) -- semantique portee cote Fenrir par
--                            TowerCpForPvmMilestone.RegisterKill, appelee depuis
--                            MonsterSpawnScheduler.ApplyTowerCpForPvmMilestone (hors perimetre d'ecriture de
--                            ce lot). PERSISTE cote legacy : FIELD_AVATAR0(aKillMonsterNum2) dans
--                            CSQLDatabase::CreateAvatarColumn (Server/Header/CSQLAvatar.cpp:556,610), colonne
--                            reelle aKillMonsterNum2 int(11) DEFAULT 0 (Server/BuildEU33/DB/nxtserver.sql:90).
--                            NOUVELLE colonne game.Characters (aucun equivalent existant). ANGLE MORT
--                            DISTINCT, NON TRAITE ICI : AvatarInfo.KillMonsterNum2 reste cable en dur a 0 par
--                            Fenrir.Core/Packets/Shared/AvatarInfoTemplates.cs:63 (AvatarInfoFactory ne le
--                            mappe nulle part) -- ce lot ferme le round-trip DB, pas l'affichage client.
--   InventoryDate/StoreDate  aInventoryDate/aStoreDate, STRUCT.h:356,361. Dates d'expiration des pages
--                            louees d'inventaire/entrepot. Colonnes DEJA presentes
--                            (Database/Tables/game/Characters.sql:114-117) et DEJA lues
--                            (usp_Character_GetForWorldEntry, CharacterWorldSnapshotDto) : seul le cote
--                            ECRITURE manquait -- absentes de tvp_CharacterProgress et des deux procedures de
--                            write-behind. A ce jour, aucun site de mutation runtime ne les modifie (l'opcode
--                            legacy d'extension d'espace, Server/ts25login/S04_MyWork02.cpp reference dans
--                            STRUCT.h + S04_MyWork03.cpp:2672,2685, n'est pas encore porte cote Fenrir) --
--                            seule la normalisation de charge (VaultDateNormalization.NormalizeIfExpired,
--                            EnterWorldService.cs) les fait varier en memoire sans jamais ecrire le resultat
--                            en base. Ce script ferme ce round-trip par avance : sans effet observable tant
--                            que l'opcode d'extension n'existe pas, correct des qu'il le sera (meme motif
--                            avant-compatible que StellarCoreExpireDate dans Migrations/040).
--
-- POURQUOI UN NOUVEAU SCRIPT ET DROP+RECREATE DU TYPE
-- game.tvp_CharacterProgress et les deux procedures de write-behind sont deja journalisees SHA-256 ; le
-- migrateur refuse de re-appliquer un chemin dont le contenu a change. Un type TABLE ne s'ALTERe pas et ne
-- se DROP pas tant qu'une procedure le prend en parametre : meme sequence DROP-procs / DROP-type /
-- CREATE-type / CREATE-procs que Migrations/011/012/040.
--
-- ETAT CONCURRENT AU MOMENT DE L'ECRITURE -- A REVERIFIER IMPERATIVEMENT AVANT APPLICATION REELLE
-- Ce depot est modifie EN CONTINU par plusieurs lots paralleles. Entre la premiere lecture et l'ecriture de
-- ce script, Fenrir.Data.Abstractions.Characters.CharacterProgressTvp a gagne, dans l'ordre :
--   1. PetBagDate/PlayTime1/PlayTime3/HsbStoneRewardClaimed (Lot 11) -- colonnes game.Characters DEJA
--      ajoutees par Migrations/032_playtime_petbagdate_hsbreward_writeback.sql (guarde IF NOT EXISTS) mais
--      jamais cablees jusqu'ici dans le TVP ni dans RS0. Ce script les cable au passage : le DROP+CREATE du
--      type les inclut de toute facon (forme positionnelle du record C#), et laisser un parametre TVP
--      recu-mais-jamais-assigne serait pire que l'etat actuel (illusion de persistance).
--   2. WarriorPill/WarriorScroll (Lot 14, PAS le sujet initial de ce script). Au moment de la premiere
--      passe de redaction, ces deux champs n'avaient AUCUNE colonne game.Characters -- puis
--      Migrations/041_warriorpill_scroll_columns.sql est apparu sur le disque (Lot 14 les ajoute, gardees
--      IF NOT EXISTS, et documente explicitement dans son propre en-tete que "le lot qui fera la
--      reconciliation finale de game.tvp_CharacterProgress" doit les cabler). Ce script EST cette
--      reconciliation : WarriorPill/WarriorScroll sont maintenant cables en INTEGRALITE (TYPE, les deux SET,
--      RS0) au meme titre que TowerCpMilestoneCounter/InventoryDate/StoreDate -- voir sections 4/5/6.
--      DEPENDANCE D'ORDRE D'APPLICATION : Migrations/041_warriorpill_scroll_columns.sql DOIT s'appliquer
--      AVANT ce script (sinon c.WarriorPill/c.WarriorScroll n'existent pas encore et les UPDATE/SELECT
--      ci-dessous echouent) -- Database/_manifest.txt liste ce script juste APRES lui, dans cet ordre.
-- Si un lot supplementaire a etendu le record entre cette redaction et l'application reelle, ce script
-- sous-estime l'union et redevient lui-meme une regression positionnelle -- reverifier contre l'etat final
-- de CharacterProgressTvp.cs/CharacterWorldSnapshotDto avant de merger, meme role que Migrations/012 et 040.

-- 1. Colonne de destination sur game.Characters. Seule TowerCpMilestoneCounter est nouvelle -- InventoryDate/
--    StoreDate existent deja (Database/Tables/game/Characters.sql:114-117), aucun ALTER pour elles.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'TowerCpMilestoneCounter')
ALTER TABLE game.Characters
    ADD TowerCpMilestoneCounter INT NOT NULL
        CONSTRAINT DF_Characters_TowerCpMilestoneCounter DEFAULT 0
        CONSTRAINT CK_Characters_TowerCpMilestoneCounter CHECK (TowerCpMilestoneCounter >= 0);
GO

-- 2. Dropper les deux procedures qui referencent le type, puis le type lui-meme.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Recreer le type, miroir exact de CharacterProgressTvp tel qu'observe (voir note ci-dessus) + les trois
--    colonnes de ce lot (TowerCpMilestoneCounter/InventoryDate/StoreDate), avant WarriorPill/WarriorScroll
--    en queue (non cables, voir note).
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId              INT          NOT NULL,
    FlushSequence            BIGINT       NOT NULL,
    Level                    SMALLINT     NOT NULL,
    Level2                   SMALLINT     NOT NULL,
    Experience               BIGINT       NOT NULL,
    Life                     INT          NOT NULL,
    MaxLife                  INT          NOT NULL,
    Mana                     INT          NOT NULL,
    MaxMana                  INT          NOT NULL,
    StatVit                  INT          NOT NULL,
    StatStr                  INT          NOT NULL,
    StatInt                  INT          NOT NULL,
    StatDex                  INT          NOT NULL,
    StatPoints               INT          NOT NULL,
    SkillPoints              INT          NOT NULL,
    ContributionPoints       INT          NOT NULL,
    Exp2                     INT          NOT NULL,
    RebirthCount             INT          NOT NULL,
    EatLifePotion            INT          NOT NULL,
    EatManaPotion            INT          NOT NULL,
    EatStrPotion             INT          NOT NULL,
    EatDexPotion             INT          NOT NULL,
    EatElePotion             INT          NOT NULL,
    DropItemTime             INT          NOT NULL,
    M15PetLuckyBoxPity       INT          NOT NULL,
    MountItemId              INT          NOT NULL,
    MountExpActivity         INT          NOT NULL,
    MountPower               INT          NOT NULL,
    MountSlotIndex           INT          NOT NULL,
    MountTime                INT          NOT NULL,
    VisibleState             INT          NOT NULL,
    SpecialState             INT          NOT NULL,
    UseOrnament              INT          NOT NULL,
    Title                    INT          NOT NULL,
    Halo                     INT          NOT NULL,
    TeacherPoint             INT          NOT NULL,
    WarPointDelta            INT          NOT NULL,
    BloodCoinDelta           INT          NOT NULL,
    PetExpX2Time             INT          NOT NULL,
    AnimalAbsorbTime         INT          NOT NULL,
    AnimalAbsorbState        INT          NOT NULL,
    CostumeIndex             INT          NOT NULL,
    ProtectForHalo           INT          NOT NULL,
    BonusItemLevel           INT          NOT NULL,
    BonusItemValue           BIT          NOT NULL,
    TribeNotifyScrollCount   INT          NOT NULL,
    TribeFourReturnAllowance INT          NOT NULL,
    BottleSlots              NVARCHAR(70) NOT NULL,
    DrunkBottleIndex         INT          NOT NULL,
    AutoBuffTime             INT          NOT NULL,
    AutoBuffSkill            NVARCHAR(48) NOT NULL,
    RankPointDate            INT          NOT NULL,
    RankBuffType             INT          NOT NULL,
    AutoTime                 INT          NOT NULL,
    AutoTime2                INT          NOT NULL,
    BuffX2Time               INT          NOT NULL,
    PremiumExpireUtc         BIGINT       NOT NULL,
    PetGrowth                INT          NOT NULL,
    PetActivity              INT          NOT NULL,
    ImproveItemValue         INT          NOT NULL,
    AddItemValue             INT          NOT NULL,
    HighItemValue            INT          NOT NULL,
    TaiyanKeyTimer           INT          NOT NULL,
    RankPoint                INT          NOT NULL,
    CloakLuckyBoxPity        INT          NOT NULL,
    CloakVariantBoxPity      INT          NOT NULL,
    MountVariantBoxPity      INT          NOT NULL,
    ProtectForRefine         INT          NOT NULL,
    ProtectForDestroy        INT          NOT NULL,
    ProtectForCostume        INT          NOT NULL,
    ProtectForDestroy2       INT          NOT NULL,
    LodRounds                INT          NOT NULL,
    StellarCoreExpireDate    NVARCHAR(80) NOT NULL,
    EliteDungeonTime         INT          NOT NULL,
    DungeonKeyTime           INT          NOT NULL,
    IvyHallTicketTime        INT          NOT NULL,
    ScrollOfSeekersTime      INT          NOT NULL,
    FightingGodForDestroy    INT          NOT NULL,
    PetBagDate               INT          NOT NULL,
    PlayTime1                INT          NOT NULL,
    PlayTime3                INT          NOT NULL,
    HsbStoneRewardClaimed    INT          NOT NULL,
    TowerCpMilestoneCounter  INT          NOT NULL,
    InventoryDate            INT          NOT NULL,
    StoreDate                INT          NOT NULL,
    WarriorPill              INT          NOT NULL,
    WarriorScroll            INT          NOT NULL
);
GO

-- 4. Recreer usp_Character_PersistProgressBatch. Garde d'idempotence inchangee.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level                    = s.Level,
        c.Level2                   = s.Level2,
        c.Experience               = s.Experience,
        c.Life                     = s.Life,
        c.MaxLife                  = s.MaxLife,
        c.Mana                     = s.Mana,
        c.MaxMana                  = s.MaxMana,
        c.StatVit                  = s.StatVit,
        c.StatStr                  = s.StatStr,
        c.StatInt                  = s.StatInt,
        c.StatDex                  = s.StatDex,
        c.StatPoints               = s.StatPoints,
        c.SkillPoints              = s.SkillPoints,
        c.ContributionPoints       = s.ContributionPoints,
        c.Exp2                     = s.Exp2,
        c.RebirthCount             = s.RebirthCount,
        c.EatLifePotion            = s.EatLifePotion,
        c.EatManaPotion            = s.EatManaPotion,
        c.EatStrPotion             = s.EatStrPotion,
        c.EatDexPotion             = s.EatDexPotion,
        c.EatElePotion             = s.EatElePotion,
        c.DropItemTime             = s.DropItemTime,
        c.M15PetLuckyBoxPity       = s.M15PetLuckyBoxPity,
        c.MountItemId              = s.MountItemId,
        c.MountExpActivity         = s.MountExpActivity,
        c.MountPower               = s.MountPower,
        c.MountSlotIndex           = s.MountSlotIndex,
        c.MountTime                = s.MountTime,
        c.VisibleState             = s.VisibleState,
        c.SpecialState             = s.SpecialState,
        c.UseOrnament              = s.UseOrnament,
        c.Title                    = s.Title,
        c.Halo                     = s.Halo,
        c.TeacherPoint             = s.TeacherPoint,
        c.WarPoint                 = c.WarPoint + s.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time             = s.PetExpX2Time,
        c.AnimalAbsorbTime         = s.AnimalAbsorbTime,
        c.AnimalAbsorbState        = s.AnimalAbsorbState,
        c.CostumeIndex             = s.CostumeIndex,
        c.ProtectForHalo           = s.ProtectForHalo,
        c.BonusItemLevel           = s.BonusItemLevel,
        c.BonusItemValue           = s.BonusItemValue,
        c.TribeNotifyScrollCount   = s.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = s.TribeFourReturnAllowance,
        c.BottleSlots              = s.BottleSlots,
        c.DrunkBottleIndex         = s.DrunkBottleIndex,
        c.AutoBuffTime             = s.AutoBuffTime,
        c.AutoBuffSkill            = s.AutoBuffSkill,
        c.RankPointDate            = s.RankPointDate,
        c.RankBuffType             = s.RankBuffType,
        c.AutoTime                 = s.AutoTime,
        c.AutoTime2                = s.AutoTime2,
        c.BuffX2Time               = s.BuffX2Time,
        c.PremiumExpireUtc         = s.PremiumExpireUtc,
        c.PetGrowth                = s.PetGrowth,
        c.PetActivity              = s.PetActivity,
        c.RankPoint                = s.RankPoint,
        c.CloakLuckyBoxPity        = s.CloakLuckyBoxPity,
        c.CloakVariantBoxPity      = s.CloakVariantBoxPity,
        c.MountVariantBoxPity      = s.MountVariantBoxPity,
        c.ImproveItemValue         = s.ImproveItemValue,
        c.AddItemValue             = s.AddItemValue,
        c.HighItemValue            = s.HighItemValue,
        c.TaiyanKeyTimer           = s.TaiyanKeyTimer,
        c.ProtectForRefine         = s.ProtectForRefine,
        c.ProtectForDestroy        = s.ProtectForDestroy,
        c.ProtectForCostume        = s.ProtectForCostume,
        c.ProtectForDestroy2       = s.ProtectForDestroy2,
        c.LodRounds                = s.LodRounds,
        c.StellarCoreExpireDate    = s.StellarCoreExpireDate,
        c.EliteDungeonTime         = s.EliteDungeonTime,
        c.DungeonKeyTime           = s.DungeonKeyTime,
        c.IvyHallTicketTime        = s.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = s.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = s.FightingGodForDestroy,
        c.PetBagDate               = s.PetBagDate,
        c.PlayTime1                = s.PlayTime1,
        c.PlayTime3                = s.PlayTime3,
        c.HsbStoneRewardClaimed    = s.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter  = s.TowerCpMilestoneCounter,
        c.InventoryDate            = s.InventoryDate,
        c.StoreDate                = s.StoreDate,
        c.WarriorPill              = s.WarriorPill,
        c.WarriorScroll            = s.WarriorScroll,
        c.FlushSequence            = s.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 5. Recreer usp_Character_PersistFinalFlush (deconnexion + changement de zone, via
--    PositionWriteBehindHost.FlushCharacterNowAsync).
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level                    = p.Level,
        c.Level2                   = p.Level2,
        c.Experience               = p.Experience,
        c.Life                     = p.Life,
        c.MaxLife                  = p.MaxLife,
        c.Mana                     = p.Mana,
        c.MaxMana                  = p.MaxMana,
        c.StatVit                  = p.StatVit,
        c.StatStr                  = p.StatStr,
        c.StatInt                  = p.StatInt,
        c.StatDex                  = p.StatDex,
        c.StatPoints               = p.StatPoints,
        c.SkillPoints              = p.SkillPoints,
        c.ContributionPoints       = p.ContributionPoints,
        c.Exp2                     = p.Exp2,
        c.RebirthCount             = p.RebirthCount,
        c.EatLifePotion            = p.EatLifePotion,
        c.EatManaPotion            = p.EatManaPotion,
        c.EatStrPotion             = p.EatStrPotion,
        c.EatDexPotion             = p.EatDexPotion,
        c.EatElePotion             = p.EatElePotion,
        c.DropItemTime             = p.DropItemTime,
        c.M15PetLuckyBoxPity       = p.M15PetLuckyBoxPity,
        c.MountItemId              = p.MountItemId,
        c.MountExpActivity         = p.MountExpActivity,
        c.MountPower               = p.MountPower,
        c.MountSlotIndex           = p.MountSlotIndex,
        c.MountTime                = p.MountTime,
        c.VisibleState             = p.VisibleState,
        c.SpecialState             = p.SpecialState,
        c.UseOrnament              = p.UseOrnament,
        c.Title                    = p.Title,
        c.Halo                     = p.Halo,
        c.TeacherPoint             = p.TeacherPoint,
        c.WarPoint                 = c.WarPoint + p.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time             = p.PetExpX2Time,
        c.AnimalAbsorbTime         = p.AnimalAbsorbTime,
        c.AnimalAbsorbState        = p.AnimalAbsorbState,
        c.CostumeIndex             = p.CostumeIndex,
        c.ProtectForHalo           = p.ProtectForHalo,
        c.BonusItemLevel           = p.BonusItemLevel,
        c.BonusItemValue           = p.BonusItemValue,
        c.TribeNotifyScrollCount   = p.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = p.TribeFourReturnAllowance,
        c.BottleSlots              = p.BottleSlots,
        c.DrunkBottleIndex         = p.DrunkBottleIndex,
        c.AutoBuffTime             = p.AutoBuffTime,
        c.AutoBuffSkill            = p.AutoBuffSkill,
        c.RankPointDate            = p.RankPointDate,
        c.RankBuffType             = p.RankBuffType,
        c.AutoTime                 = p.AutoTime,
        c.AutoTime2                = p.AutoTime2,
        c.BuffX2Time               = p.BuffX2Time,
        c.PremiumExpireUtc         = p.PremiumExpireUtc,
        c.PetGrowth                = p.PetGrowth,
        c.PetActivity              = p.PetActivity,
        c.RankPoint                = p.RankPoint,
        c.CloakLuckyBoxPity        = p.CloakLuckyBoxPity,
        c.CloakVariantBoxPity      = p.CloakVariantBoxPity,
        c.MountVariantBoxPity      = p.MountVariantBoxPity,
        c.ImproveItemValue         = p.ImproveItemValue,
        c.AddItemValue             = p.AddItemValue,
        c.HighItemValue            = p.HighItemValue,
        c.TaiyanKeyTimer           = p.TaiyanKeyTimer,
        c.ProtectForRefine         = p.ProtectForRefine,
        c.ProtectForDestroy        = p.ProtectForDestroy,
        c.ProtectForCostume        = p.ProtectForCostume,
        c.ProtectForDestroy2       = p.ProtectForDestroy2,
        c.LodRounds                = p.LodRounds,
        c.StellarCoreExpireDate    = p.StellarCoreExpireDate,
        c.EliteDungeonTime         = p.EliteDungeonTime,
        c.DungeonKeyTime           = p.DungeonKeyTime,
        c.IvyHallTicketTime        = p.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = p.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = p.FightingGodForDestroy,
        c.PetBagDate               = p.PetBagDate,
        c.PlayTime1                = p.PlayTime1,
        c.PlayTime3                = p.PlayTime3,
        c.HsbStoneRewardClaimed    = p.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter  = p.TowerCpMilestoneCounter,
        c.InventoryDate            = p.InventoryDate,
        c.StoreDate                = p.StoreDate,
        c.WarriorPill              = p.WarriorPill,
        c.WarriorScroll            = p.WarriorScroll,
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.FlushSequence            = q.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 6. Chemin de LECTURE. Redeclaration COMPLETE de RS0, verifiee programmatiquement colonne-par-colonne
--    contre l'ordre declare de CharacterWorldSnapshotDto (0 mismatch) : reprend Migrations/040 jusqu'a
--    StellarCoreExpireDate (avec la correction d'ordre RankPoint/CloakLuckyBoxPity/CloakVariantBoxPity/
--    MountVariantBoxPity <-> ImproveItemValue/AddItemValue/HighItemValue/TaiyanKeyTimer -- voir en-tete) puis
--    ajoute, dans l'ordre du record C# observe, EliteDungeonTime/DungeonKeyTime/IvyHallTicketTime/
--    ScrollOfSeekersTime/FightingGodForDestroy/PlayTime1/PlayTime3/HsbStoneRewardClaimed/
--    TowerCpMilestoneCounter/WarriorPill/WarriorScroll -- onze colonnes deja presentes sur
--    CharacterWorldSnapshotDto mais absentes de RS0 depuis Migrations/040 (qui s'arretait a
--    StellarCoreExpireDate). InventoryDate/StoreDate sont deja projetees plus haut dans la liste (juste
--    apres DropItemTime, position inchangee depuis Migrations/002) : non deplacees.
CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence,
           c.Experience,
           c.Level2,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.Money,
           c.BigMoney,
           c.StoreMoney,
           c.BigStoreMoney,
           c.RebirthCount,
           c.Title,
           c.Halo,
           c.ContributionPoints,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForDeath,
           c.ProtectForDestroy,
           c.DoubleExpTime1,
           c.DoubleExpTime2,
           c.DropItemTime,
           c.InventoryDate,
           c.StoreDate,
           ISNULL(q.StepPermanent, 0) AS QuestStepPermanent,
           ISNULL(q.ActiveQuestId, 0) AS QuestActiveId,
           ISNULL(q.QSort, 0)         AS QuestSort,
           ISNULL(q.TargetPhase, 0)   AS QuestTargetPhase,
           ISNULL(q.KillCounter, 0)   AS QuestKillCounter,
           c.JoinWar,
           c.MissionKillOtherTribe,
           c.MissionKillMonster,
           c.MissionPlayTime,
           c.AutoHuntEnabled,
           c.AutoHuntConfig,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.PetGrowth,
           c.PetActivity,
           c.TeacherPoint,
           c.AutoBuffTime,
           c.PremiumExpireUtc,
           c.Exp2,
           c.PreviousTribe,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AutoTime2,
           c.Zone241Time,
           c.PetBagDate,
           c.WarPoint,
           c.M15PetLuckyBoxPity,
           c.VisibleState,
           c.SpecialState,
           c.UseOrnament,
           c.BloodCoin,
           c.PetExpX2Time,
           c.AnimalAbsorbTime,
           c.AnimalAbsorbState,
           c.CostumeIndex,
           c.ProtectForHalo,
           c.BonusItemLevel,
           c.BonusItemValue,
           c.TribeNotifyScrollCount,
           c.TribeFourReturnAllowance,
           c.BottleSlots,
           c.DrunkBottleIndex,
           c.AutoBuffSkill,
           c.RankPointDate,
           c.RankBuffType,
           c.AutoTime,
           c.BuffX2Time,
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer,
           c.RankPoint,
           c.CloakLuckyBoxPity,
           c.CloakVariantBoxPity,
           c.MountVariantBoxPity,
           c.ProtectForRefine,
           c.ProtectForCostume,
           c.ProtectForDestroy2,
           c.LodRounds,
           c.StellarCoreExpireDate,
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy,
           c.PlayTime1,
           c.PlayTime3,
           c.HsbStoneRewardClaimed,
           c.TowerCpMilestoneCounter,
           c.WarriorPill,
           c.WarriorScroll
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           CAST(Quantity AS INT) AS Quantity, -- game.CharacterItems.Quantity is SMALLINT; widen back to INT
           Enchant,                           -- here so CharacterItemSlotDto's existing int-typed ctor param
           Combine,                           -- keeps reading it via SqlDataReader.GetInt32 without an
           Refine,                            -- InvalidCastException (see CharacterItems.sql's own comment)
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
    ORDER BY Container, Slot;

    SELECT SlotIndex,
           SkillId,
           Grade
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;

    SELECT Page,
           KeyIndex,
           Sort,
           Value1,
           Value2
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
    ORDER BY Page, KeyIndex;

    SELECT SlotIndex,
           Value,
           RemainingLegacyTicks
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
GO
