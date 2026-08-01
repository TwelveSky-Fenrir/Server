-- Lot 8 : six compteurs/tableaux mutes en jeu cote Fenrir, jamais persistes, que le legacy persiste tous
-- sans #ifdef dans CreateAvatarColumn (Server/Header/CSQLAvatar.cpp:556-756), donc ecrits par le
-- write-behind ts25playuser (Server/ts25playuser/S08_MyDB.cpp:99) des lors qu'une zone les modifie.
--
--   ProtectForRefine   aProtectForRefine/aProtectForSmelt, CSQLAvatar.cpp:596. Stock de charges credite a
--                      l'usage d'un consommable (Server/ts25zone/S04_MyWork03.cpp:1545-1546), consomme au
--                      raffinage (S04_MyWork02.cpp:13306,13429-13432).
--   ProtectForDestroy  aProtectForDestroy, CSQLAvatar.cpp:593. Colonne et lecture RS0 DEJA presentes
--                      (game.Characters.ProtectForDestroy, usp_Character_GetForWorldEntry) mais jamais
--                      ecrite : absente de tvp_CharacterProgress et des deux procedures de write-behind.
--                      Ce script comble uniquement le cote ecriture ; aucun ALTER TABLE necessaire pour elle.
--   ProtectForCostume  aProtectForCostume, CSQLAvatar.cpp:599, enregistree SANS garde USE_COSTUME. Creditee
--                      par consommable (S04_MyWork03.cpp:2429-2432), consommee avec notification
--                      S202_PROTECT_COSTUME (S04_MyWork02.cpp:2642-2648).
--   ProtectForDestroy2 aProtectForDestroy2, CSQLAvatar.cpp:594. Second palier de charges, distinct de
--                      ProtectForDestroy (credit S04_MyWork03.cpp:2451-2452, consommation
--                      S104PROTECT_ITEM2 S04_MyWork02.cpp:2941-2945).
--   LodRounds          aZone241Time, CSQLAvatar.cpp:635 (alias historique aChallengeNum). ATTENTION DE
--                      LECTURE : Fenrir porte DEUX proprietes PlayerRuntimeState distinctes pour ce meme
--                      champ legacy -- Zone241Time (deja persiste par ce script AVANT ce lot, via
--                      game.Characters.Zone241Time / usp_Character_AdjustZone241Time, alimente par la
--                      mission quotidienne et le peage de zone 241) et LodRounds (alimente par le seul
--                      ticket 1434 "Life or Death", mort sous M33/LNW33 -- #ifdef WUSE_ITEM_1434 n'existe
--                      que sous #ifdef __REBIRTH__, Server/Header/use_inventory.h:73-82). Les deux
--                      mutent aujourd'hui INDEPENDAMMENT en memoire sans jamais se resynchroniser. Ce
--                      script persiste LodRounds tel que nomme cote Fenrir (nouvelle colonne dediee,
--                      extension du meme tvp_CharacterProgress -- pas un chemin d'ecriture parallele) parce
--                      que c'est le champ demande et que le producteur mort n'est "pas une raison de le
--                      laisser ephemere" ; mais la duplication avec Zone241Time reste un angle mort distinct
--                      a reconcilier hors de ce lot (unifier les deux proprietes PlayerRuntimeState),
--                      changement qui touche Zone.EconomyMirrors.cs/UseInventoryItemService.cs/
--                      PlayerRuntimeState.Consumables.cs, hors perimetre d'ecriture de ce lot.
--   StellarCoreExpireDate aStellarCoreExpireDate[MAX_AVATAR_STELLAR_NUM=10], CSQLAvatar.cpp:722, chaque
--                      slot CREATE_INT(...,8,EFIX_ANIMAL_POWER) (CSQLAvatar.cpp:524-527) : hors bornes
--                      0..99999999 => ECRASE A 0, pas de modulo (CSQLAvatar.cpp:39-42). Encode ici comme
--                      UNE colonne texte a largeur fixe (10*8=80 caracteres), meme convention que
--                      BottleSlots/AutoBuffSkill (StellarCoreExpireDateCodec, meme fichier que
--                      BottleSlotsCodec/AutoBuffSkillCodec). Producteurs vivants sous M33 (USE_STELLAR_CORE
--                      allume) : equipement (Server/ts25zone/S04_MyWork03.cpp:1455), retrait
--                      (S04_MyWork02.cpp:15283-15298). NOTE DE PORTEE : le tableau soeur des ITEMS
--                      equipes (aStellarCore, PlayerRuntimeState.StellarCoreWardrobe) reste, lui,
--                      NON PERSISTE -- hors de ce lot, qui ne nomme que StellarCoreExpireDate. Persister
--                      les dates d'expiration sans le tableau d'items est sans danger (le seul lecteur
--                      actuel est l'affichage client AvatarInfo, pas de logique serveur qui suppose
--                      l'occupation d'un slot) et avant-compatible : une fois StellarCoreWardrobe porte a
--                      son tour (meme motif que game.CharacterCostumes/tvp_CharacterCostumeSlot), les dates
--                      deja en base deviennent immediatement correctes sans script de rattrapage.
--
-- POURQUOI UN NOUVEAU SCRIPT ET DROP+RECREATE DU TYPE
-- game.Characters, tvp_CharacterProgress et les deux procedures de write-behind sont deja journalisees
-- SHA-256 (fichiers de base + Migrations/002...033) ; le migrateur refuse de re-appliquer un chemin dont le
-- contenu a change. Un type TABLE ne s'ALTERe pas et ne se DROP pas tant qu'une procedure le prend en
-- parametre : meme sequence DROP-procs / DROP-type / CREATE-type / CREATE-procs que Migrations/011/012.
--
-- ETAT CONCURRENT AU MOMENT DE L'ECRITURE -- A REVERIFIER IMPERATIVEMENT AVANT APPLICATION REELLE
-- Ce depot est modifie EN CONTINU pendant la preparation de ce script par plusieurs lots paralleles qui
-- etendent eux aussi tvp_CharacterProgress/usp_Character_PersistProgressBatch/usp_Character_PersistFinalFlush/
-- usp_Character_GetForWorldEntry : au moins RankPoint/CloakLuckyBoxPity/CloakVariantBoxPity/
-- MountVariantBoxPity, ImproveItemValue/AddItemValue/HighItemValue/TaiyanKeyTimer, et
-- EliteDungeonTime/DungeonKeyTime/IvyHallTicketTime/ScrollOfSeekersTime/FightingGodForDestroy ont ete vus
-- apparaitre dans Fenrir.Data.Abstractions.Characters.CharacterProgressTvp (et dans les deux hotes de
-- write-behind) au cours meme de la redaction de ce script ; le numero Migrations/032 a ete repris et
-- renomme plusieurs fois en quelques minutes. CE SCRIPT RECREE LE TYPE COMME L'UNION OBSERVEE AU DERNIER
-- INSTANT VERIFIE (voir CharacterProgressTvp.cs / CharacterWorldSnapshotDto au moment de l'ecriture) + les
-- six colonnes de ce lot en queue -- CE N'EST PAS UNE GARANTIE que c'est la forme FINALE. Il doit
-- s'appliquer apres tout script qui ajoute a game.Characters les colonnes des lots cites ci-dessus
-- (numerote 040, au-dela de tout numero vu a l'ecriture) ET avant d'etre considere fiable, la personne qui
-- merge doit reverifier ce DROP+CREATE contre l'etat definitif de CharacterProgressTvp.cs une fois tous les
-- lots paralleles integres -- exactement le role qu'a joue Migrations/012 vis-a-vis de 010/011. Si un lot
-- supplementaire est arrive entre-temps, ce script sous-estimera l'union et redeviendra lui-meme une
-- regression au sens de Migrations/012 ; ne pas le traiter comme definitif sans cette reverification.
--
-- usp_Character_GetForWorldEntry utilise CREATE OR ALTER (pas de type TABLE en parametre) : la
-- redeclaration ci-dessous est la projection RS0 COMPLETE et correcte independamment de l'etat intermediaire
-- du cluster 03x au moment ou ce script s'execute reellement.

-- 1. Colonnes de destination sur game.Characters. ProtectForDestroy existe deja (voir base
--    Tables/game/Characters.sql:103-105) : pas d'ALTER pour elle, seulement le cablage TVP/procedures
--    ci-dessous. Chaque ADD est garde par sys.columns.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'ProtectForRefine')
    ALTER TABLE game.Characters
        ADD ProtectForRefine INT NOT NULL
            CONSTRAINT DF_Characters_ProtectForRefine DEFAULT 0
            CONSTRAINT CK_Characters_ProtectForRefine CHECK (ProtectForRefine >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'ProtectForCostume')
    ALTER TABLE game.Characters
        ADD ProtectForCostume INT NOT NULL
            CONSTRAINT DF_Characters_ProtectForCostume DEFAULT 0
            CONSTRAINT CK_Characters_ProtectForCostume CHECK (ProtectForCostume >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'ProtectForDestroy2')
    ALTER TABLE game.Characters
        ADD ProtectForDestroy2 INT NOT NULL
            CONSTRAINT DF_Characters_ProtectForDestroy2 DEFAULT 0
            CONSTRAINT CK_Characters_ProtectForDestroy2 CHECK (ProtectForDestroy2 >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'LodRounds')
    ALTER TABLE game.Characters
        ADD LodRounds INT NOT NULL
            CONSTRAINT DF_Characters_LodRounds DEFAULT 0
            CONSTRAINT CK_Characters_LodRounds CHECK (LodRounds >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'StellarCoreExpireDate')
    ALTER TABLE game.Characters
        ADD StellarCoreExpireDate NVARCHAR(80) NOT NULL
            CONSTRAINT DF_Characters_StellarCoreExpireDate DEFAULT N''
            CONSTRAINT CK_Characters_StellarCoreExpireDate CHECK (LEN(StellarCoreExpireDate) IN (0, 80));
GO

-- 2. Dropper les deux procedures qui referencent le type, puis le type lui-meme.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Recreer le type, miroir exact de CharacterProgressTvp tel qu'observe (voir note ci-dessus) + les six
--    colonnes de ce lot en queue.
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId              INT           NOT NULL,
    FlushSequence            BIGINT        NOT NULL,
    Level                    SMALLINT      NOT NULL,
    Level2                   SMALLINT      NOT NULL,
    Experience               BIGINT        NOT NULL,
    Life                     INT           NOT NULL,
    MaxLife                  INT           NOT NULL,
    Mana                     INT           NOT NULL,
    MaxMana                  INT           NOT NULL,
    StatVit                  INT           NOT NULL,
    StatStr                  INT           NOT NULL,
    StatInt                  INT           NOT NULL,
    StatDex                  INT           NOT NULL,
    StatPoints               INT           NOT NULL,
    SkillPoints               INT          NOT NULL,
    ContributionPoints       INT           NOT NULL,
    Exp2                     INT           NOT NULL,
    RebirthCount             INT           NOT NULL,
    EatLifePotion            INT           NOT NULL,
    EatManaPotion            INT           NOT NULL,
    EatStrPotion             INT           NOT NULL,
    EatDexPotion             INT           NOT NULL,
    EatElePotion             INT           NOT NULL,
    DropItemTime             INT           NOT NULL,
    M15PetLuckyBoxPity       INT           NOT NULL,
    MountItemId              INT           NOT NULL,
    MountExpActivity         INT           NOT NULL,
    MountPower               INT           NOT NULL,
    MountSlotIndex           INT           NOT NULL,
    MountTime                INT           NOT NULL,
    VisibleState             INT           NOT NULL,
    SpecialState              INT          NOT NULL,
    UseOrnament               INT          NOT NULL,
    Title                     INT          NOT NULL,
    Halo                      INT          NOT NULL,
    TeacherPoint              INT          NOT NULL,
    WarPointDelta             INT          NOT NULL,
    BloodCoinDelta            INT          NOT NULL,
    PetExpX2Time              INT          NOT NULL,
    AnimalAbsorbTime          INT          NOT NULL,
    AnimalAbsorbState         INT          NOT NULL,
    CostumeIndex              INT          NOT NULL,
    ProtectForHalo            INT          NOT NULL,
    BonusItemLevel            INT          NOT NULL,
    BonusItemValue            BIT          NOT NULL,
    TribeNotifyScrollCount    INT          NOT NULL,
    TribeFourReturnAllowance  INT          NOT NULL,
    BottleSlots               NVARCHAR(70) NOT NULL,
    DrunkBottleIndex          INT          NOT NULL,
    AutoBuffTime               INT         NOT NULL,
    AutoBuffSkill              NVARCHAR(48) NOT NULL,
    RankPointDate              INT         NOT NULL,
    RankBuffType                INT        NOT NULL,
    AutoTime                    INT        NOT NULL,
    AutoTime2                   INT        NOT NULL,
    BuffX2Time                  INT        NOT NULL,
    PremiumExpireUtc             BIGINT    NOT NULL,
    PetGrowth                    INT       NOT NULL,
    PetActivity                  INT       NOT NULL,
    RankPoint                    INT       NOT NULL,
    CloakLuckyBoxPity            INT       NOT NULL,
    CloakVariantBoxPity          INT       NOT NULL,
    MountVariantBoxPity          INT       NOT NULL,
    ImproveItemValue             INT       NOT NULL,
    AddItemValue                 INT       NOT NULL,
    HighItemValue                INT       NOT NULL,
    TaiyanKeyTimer                INT      NOT NULL,
    ProtectForRefine               INT     NOT NULL,
    ProtectForDestroy               INT    NOT NULL,
    ProtectForCostume                INT   NOT NULL,
    ProtectForDestroy2                INT  NOT NULL,
    LodRounds                          INT NOT NULL,
    StellarCoreExpireDate       NVARCHAR(80) NOT NULL,
    EliteDungeonTime                   INT NOT NULL,
    DungeonKeyTime                     INT NOT NULL,
    IvyHallTicketTime                  INT NOT NULL,
    ScrollOfSeekersTime                INT NOT NULL,
    FightingGodForDestroy              INT NOT NULL
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

-- 6. Chemin de LECTURE. Redeclaration COMPLETE et correcte de RS0, independante de l'etat intermediaire du
--    cluster 03x (restaure AutoTime/BuffX2Time si un script concurrent ne l'a pas deja fait -- ordinaux fixes
--    par CharacterWorldSnapshotDto, generateur CaeriusNet, lecture par position) et ajoute en queue
--    ProtectForRefine/ProtectForCostume/ProtectForDestroy2/LodRounds/StellarCoreExpireDate. ProtectForDestroy
--    est deja projetee plus haut dans la liste (juste apres ProtectForDeath) : non deplacee.
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
           c.RankPoint,
           c.CloakLuckyBoxPity,
           c.CloakVariantBoxPity,
           c.MountVariantBoxPity,
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer,
           c.ProtectForRefine,
           c.ProtectForCostume,
           c.ProtectForDestroy2,
           c.LodRounds,
           c.StellarCoreExpireDate
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
