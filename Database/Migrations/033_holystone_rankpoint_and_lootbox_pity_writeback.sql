-- Lot 10 -- quatre compteurs mutes en jeu, persistes par le legacy, perdus par Fenrir : RankPoint (Holy
-- Stone Battle) et trois compteurs de pity de loot-box (cape, variante de cape, variante de monture).
--
-- CE QUE LE LEGACY PERSISTE, ET OU
--   * aRankPoint   Server/Header/Protocol/STRUCT.h:489 (int, AVATAR_INFO) ; FIELD_AVATAR0(aRankPoint),
--                  Server/Header/CSQLAvatar.cpp:645 ; colonne aRankPoint int(11) DEFAULT 0,
--                  Server/BuildEU33/DB/nxtserver.sql:150.
--                  ATTENTION A L'HOMONYME : USER_INFO porte aussi un aRankPoint (STRUCT.h:776, :867), un
--                  miroir de diffusion vers les clients, remis a 0 par ResetRank en meme temps que le champ
--                  persiste (Server/ts25zone/S07_MyGame03.cpp:8381) -- ce n'est PAS le champ d'etat. Cote
--                  Fenrir ce miroir correspond au champ RankPoint du paquet AvatarInfo (toujours emis a 0 a
--                  l'entree en monde, EnterWorldService.cs) : ce script n'y touche pas.
--   * gBox2249     Server/Header/Protocol/STRUCT.h:568 (int, AVATAR_INFO) ; FIELD_AVATAR0(gBox2249),
--                  CSQLAvatar.cpp:731 ; colonne gBox2249 int(11) DEFAULT -1, nxtserver.sql:187. Pity de la
--                  boite cape (case 2249, RandomCloak, Server/ts25zone/S04_MyWork03.cpp:886-891), plafond 100.
--   * gBox8114     STRUCT.h:569 ; FIELD_AVATAR0(gBox8114), CSQLAvatar.cpp:732 ; colonne DEFAULT -1,
--                  nxtserver.sql:188. Pity boite variante de cape (case 8114, S04_MyWork03.cpp:5921-5930),
--                  plafond 200.
--   * gBox8115     STRUCT.h:570 ; FIELD_AVATAR0(gBox8115), CSQLAvatar.cpp:733 ; colonne DEFAULT -1,
--                  nxtserver.sql:189. Pity boite variante de monture (case 8115, S04_MyWork03.cpp:5978-5987),
--                  plafond 200.
-- Les quatre FIELD_AVATAR0 sont hors de tout #ifdef : ils comptent dans ReleaseM33 comme dans ReleaseEU33.
-- DEFAULT 0 ici et non -1 comme le schema legacy : PlayerRuntimeState les porte deja en int C# initialise a
-- 0, et le compteur jumeau M15PetLuckyBoxPity (gBox8111, meme famille) a deja pose ce meme choix cote Fenrir
-- (Tables/game/Characters.sql, DF_Characters_M15PetLuckyBoxPity DEFAULT 0) -- une seule convention pour les
-- quatre compteurs de la meme famille, jamais -1 qui n'a aucun sens special cote C# (LootBoxRewardResolver
-- .PityStep incremente et remet a zero au plafond, aucune branche n'interroge -1).
--
-- CE QUI N'EST PAS DANS CE LOT
--   * RankPointDate  DEJA cable des deux sens (Migrations/010, TVP, DTO, ProgressWriteBehindHost,
--                    PositionWriteBehindHost, EnterWorldService.cs, Zone.PlayerLifecycle.cs) -- verifie et
--                    exclu du lot, rien a faire ici.
--   * RankBuffType   deja cable par Migrations/010 ; RankPoint suit exactement la MEME regle de remise a
--                    zero quotidienne (ResetRank, Server/ts25zone/S07_MyGame03.cpp:8283-8385, remet aRankPoint
--                    ET aRankBuffType ensemble des que aRankPointDate n'est plus la date du jour) -- voir
--                    EnterWorldService.cs, variable rankPoint calquee sur rankBuffType.
--
-- CE QUE CE SCRIPT NE FAIT PAS -- ET POURQUOI (BLOQUE, PAS OUBLIE)
-- Il n'etend PAS game.tvp_CharacterProgress, ni usp_Character_PersistProgressBatch/usp_Character_
-- PersistFinalFlush, ni la projection RS0 de usp_Character_GetForWorldEntry. game.tvp_CharacterProgress est
-- un type TABLE (aucun ALTER possible : chaque extension le DROP+RECREE en entier) et, au moment ou ce
-- script est ecrit, AU MOINS QUATRE AUTRES LOTS redeclarent ce meme type en parallele -- voir l'avertissement
-- documente dans Migrations/034_animal_double_exp_and_combat_boost_columns.sql, ecrit en observant la meme
-- course :
--   Migrations/012_autohunt_premium_pet_writeback_restore.sql        (restaure AutoTime/AutoTime2/BuffX2Time/
--                                                                      PremiumExpireUtc/PetGrowth/PetActivity)
--   Migrations/032_playtime_petbagdate_hsbreward_writeback.sql       (ajoute PlayTime1/PlayTime3/
--                                                                      HsbStoneRewardClaimed, restaure aussi
--                                                                      AutoTime/BuffX2Time + @Costumes)
--   Migrations/032_progress_writeback_reconciliation_and_item_value_counters.sql (ImproveItemValue/
--                                                                      AddItemValue/HighItemValue/
--                                                                      TaiyanKeyTimer, restaure @Costumes)
--   + le lot StellarCore/Protect*/LodRounds (Migrations/040_stellarcore_protect_charges_lodrounds_writeback.sql
--     au moment ou ce script est ecrit).
-- Fenrir.Data.Abstractions.Characters.CharacterProgressTvp (etat du disque au moment de l'ecriture) porte
-- deja les colonnes de TOUS les lots ci-dessus, RankPoint/CloakLuckyBoxPity/CloakVariantBoxPity/
-- MountVariantBoxPity compris, alors qu'AUCUNE migration actuellement sur disque ne recree le type/la
-- projection avec l'union complete. Ecrire ici un DROP+CREATE de plus ajouterait une candidate
-- supplementaire a la course, avec un risque reel de collision de numero de migration et de types SQL
-- devines a l'aveugle pour des colonnes appartenant a d'autres lots que je n'ai pas verifies. La regle du
-- depot est d'etendre l'existant plutot que de creer un chemin parallele qui finit par diverger -- diverger
-- ici serait d'ecrire une nouvelle forme concurrente du meme type sans visibilite sur les autres en vol.
-- Ce script se limite donc a la seule operation SURE et INDEPENDANTE : les quatre colonnes ALTER ADD sur
-- game.Characters, gardees par IF NOT EXISTS, qui ne collisionnent avec aucun autre lot puisque RankPoint/
-- CloakLuckyBoxPity/CloakVariantBoxPity/MountVariantBoxPity ne sont revendiquees par aucun autre.
--
-- CE QUI RESTE A FAIRE, POUR LE LOT QUI FERA LA RECONCILIATION FINALE DE game.tvp_CharacterProgress
-- Quand la course ci-dessus sera retombee, ajouter en QUEUE -- miroir exact de la fin de
-- CharacterProgressTvp.cs a ce moment-la -- les quatre colonnes de ce lot : RankPoint INT NOT NULL,
-- CloakLuckyBoxPity INT NOT NULL, CloakVariantBoxPity INT NOT NULL, MountVariantBoxPity INT NOT NULL, et les
-- quatre clauses SET correspondantes (absolues, PlayerRuntimeState est proprietaire unique, meme categorie
-- que DropItemTime) dans usp_Character_PersistProgressBatch ET usp_Character_PersistFinalFlush, et les
-- quatre colonnes en queue de la projection RS0 de usp_Character_GetForWorldEntry. Cote application,
-- PlayerEnterData (Domain/World/ZoneCommand.cs), EnterWorldService.cs et Zone.PlayerLifecycle.cs sont DEJA
-- cables pour les quatre (verifie sur le disque au moment de l'ecriture) -- rien n'y manque cote C#, seul le
-- DROP+CREATE terminal du TVP/des procedures/de la projection RS0 reste a faire, en un seul endroit, une
-- seule fois, par le lot qui cloturera la course.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql est deja liste dans _manifest.txt, donc journalise par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base qui l'a applique une fois. Le migrateur refuse de re-appliquer un
-- chemin journalise dont le contenu a change.
--
-- POURQUOI ALTER ADD ET NON DROP+CREATE
-- game.Characters porte de vraies donnees joueur : un ALTER additif avec DEFAULT sur une colonne NOT NULL
-- est une operation de metadonnees seule, lignes existantes preservees a la valeur par defaut.

-- Les quatre colonnes du lot. Gardes d'idempotence independantes : le script reste rejouable a la main sur
-- une base a l'etat inconnu. RankPoint sans plafond (aucune borne cote legacy ni cote MyFactor) ; les trois
-- compteurs de pity bornes a leur plafond respectif (LootBoxRewardResolver.PityStep + RewardTable.Roll,
-- src/Fenrir.Application.Game/Domain/World/Loot/CloakBoxRewardTable.cs:10,
-- CloakVariantBox8114RewardTable.cs:10, MountVariantBox8115RewardTable.cs:10).
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'RankPoint')
ALTER TABLE game.Characters
    ADD RankPoint INT NOT NULL
        CONSTRAINT DF_Characters_RankPoint DEFAULT 0
        CONSTRAINT CK_Characters_RankPoint CHECK (RankPoint >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CloakLuckyBoxPity')
ALTER TABLE game.Characters
    ADD CloakLuckyBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_CloakLuckyBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_CloakLuckyBoxPity CHECK (CloakLuckyBoxPity BETWEEN 0 AND 100);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CloakVariantBoxPity')
ALTER TABLE game.Characters
    ADD CloakVariantBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_CloakVariantBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_CloakVariantBoxPity CHECK (CloakVariantBoxPity BETWEEN 0 AND 200);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'MountVariantBoxPity')
ALTER TABLE game.Characters
    ADD MountVariantBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_MountVariantBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_MountVariantBoxPity CHECK (MountVariantBoxPity BETWEEN 0 AND 200);
GO
