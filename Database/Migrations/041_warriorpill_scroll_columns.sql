-- Lot 14 -- deux compteurs mutes en jeu, persistes par le legacy, jamais ecrits cote Fenrir --
-- WarriorPill / WarriorScroll.
--
-- CE QUE PERSISTE LE LEGACY, ET OU (CreateAvatarColumn, Server/Header/CSQLAvatar.cpp:556-756, hors de tout
-- #ifdef -- compte dans ReleaseM33 et ReleaseEU33 -- liste partagee SELECT et INSERT/UPDATE :769-812,
-- ecrite par le write-behind periodique de ts25playuser : Server/ts25playuser/S08_MyDB.cpp:99
-- GetAvatar(UPDATE, mDBTable[2]))
--   WarriorPill    aWarriorPill    STRUCT.h:515  CSQLAvatar.cpp:674
--                  decrement d'une unite par minute (Server/ts25zone/S07_MyGame04.cpp:1015-1019), consomme
--                  en jeu (EquipmentService.cs/UseInventoryItemService.cs cote Fenrir).
--   WarriorScroll  aWarriorScroll  STRUCT.h:509  CSQLAvatar.cpp:673 (colonne recyclee -- le commentaire
--                  d'origine //FIELD_AVATAR0( aDoubleKillExpTime2 ) reste sur la ligne mais la colonne est
--                  bien active, sans garde)
--                  credite a l'achat (Server/ts25zone/S04_MyWork03.cpp:5146-5149), decremente d'une unite
--                  par minute (S07_MyGame04.cpp:1020-1024), consomme en jeu (Zone.Combat.cs cote Fenrir :
--                  CP+1 et EXP x2 sur kill PvP, meme mecanique que S07_MyGame03.cpp:2505,2958).
--
-- L'ECART FENRIR : les deux sont deja mutes cote PlayerRuntimeState (TimedBuffCountdownSystem.cs TickGroupB,
-- meme fichier/meme motif que FightingGodForDestroy dans TickGroupA -- voir Migrations/034_ticket_counters_
-- and_fighting_god_writeback.sql) mais n'atteignaient aucune colonne, aucun TVP, aucun DTO -- perdus a la
-- deconnexion ET au changement de zone. Confirme par grep : aucune occurrence dans src/Fenrir.Data*,
-- Database/ avant ce script.
--
-- TROIS CHAMPS RETIRES DU LOT -- BLOQUES, PAS OUBLIES (Zone101Time / Zone126Time / Zone050Time2)
-- Le lot demande a l'origine incluait aussi Zone101Time/Zone126Time/Zone050Time2 (PlayerRuntimeState.
-- TimedBuffs.cs), avec pour citation legacy STRUCT.h:384/386/480 + CSQLAvatar.cpp:606/608/660 -- EXACTEMENT
-- les memes trois citations que celles deja utilisees par Migrations/034_ticket_counters_and_fighting_god_
-- writeback.sql (Lot 12) pour persister EliteDungeonTime/ScrollOfSeekersTime/IvyHallTicketTime. Ce script
-- documente explicitement (voir son propre en-tete, section PIEGE CONNU) que PlayerRuntimeState porte DEUX
-- proprietes runtime distinctes qui mutent le MEME compteur legacy -- l'une creditee par les tickets/
-- parchemins (Zone.EconomyMirrors.cs, deja persistee sous EliteDungeonTime/IvyHallTicketTime/
-- ScrollOfSeekersTime), l'autre decrementee chaque minute et creditee ailleurs (TimedBuffCountdownSystem.cs
-- TickPaidZones, HighLevelExperienceOutcomeApplier pour Zone101Time) -- et QUE CE N'EST PAS RESOLU,
-- deliberement, parce que fusionner les deux est un changement de LOGIQUE DE JEU (Domain/Simulation/
-- TimedBuffCountdownSystem.cs, Domain/World/Zone.EconomyMirrors.cs, Domain/Progression/
-- HighLevelExperienceOutcomeApplier.cs), hors du perimetre d'ecriture de ce lot (Fenrir.Data*/Database/ +
-- sites de sauvegarde).
-- Ajouter ICI trois colonnes de plus (Zone101Time/Zone126Time/Zone050Time2) ne fusionnerait rien : cela
-- ajouterait une TROISIEME representation persistee du meme compteur legacy a cote d'EliteDungeonTime/
-- ScrollOfSeekersTime/IvyHallTicketTime deja en place, sans jamais les faire converger -- exactement le
-- risque que l'en-tete de Migrations/034_ticket_counters... met en garde de ne pas creer. Ces trois champs
-- restent donc NON persistes par ce lot ; la fusion des deux proprietes runtime est un prealable qui
-- appartient a qui possede Domain/Simulation/TimedBuffCountdownSystem.cs et Domain/World/
-- Zone.EconomyMirrors.cs.
--
-- CE QUE CE SCRIPT FAIT, ET SEULEMENT CELA
-- Ajoute les deux colonnes reellement absentes a game.Characters. Operation de metadonnees pure sur une
-- colonne NOT NULL avec DEFAULT : lignes existantes preservees a 0 (aucun avatar existant n'a de solde de
-- pilule/parchemin de guerrier en cours -- meme valeur de depart que DropItemTime/EliteDungeonTime, un
-- compte a rebours qui part de zero tant que rien n'a ete achete/utilise).
--
-- CE QUE CE SCRIPT NE FAIT PAS -- ET POURQUOI (BLOQUE, PAS OUBLIE -- meme posture que Migrations/
-- 032_playtime_petbagdate_hsbreward_writeback.sql, 033_holystone_rankpoint_and_lootbox_pity_writeback.sql
-- et 034_animal_double_exp_and_combat_boost_columns.sql)
-- Il n'etend PAS game.tvp_CharacterProgress, ni usp_Character_PersistProgressBatch/usp_Character_
-- PersistFinalFlush, ni la projection RS0 de usp_Character_GetForWorldEntry. Au moment ou ce script est
-- ecrit, ces trois objets partagent le meme point de contention deja documente par les trois scripts
-- cites ci-dessus -- le mapper TVP/DTO genere par CaeriusNet lie POSITIONNELLEMENT -- et sont EN COURS DE
-- MODIFICATION CONCURRENTE : Fenrir.Data.Abstractions.Characters.CharacterProgressTvp/
-- CharacterWorldSnapshotDto portent DEJA, en queue, un champ TowerCpMilestoneCounter apparu pendant meme
-- la redaction de ce script, sans qu'aucun script SQL ne l'ait encore integre a la forme terminale de
-- Migrations/040_stellarcore_protect_charges_lodrounds_writeback.sql (qui ne porte elle-meme PAS encore
-- EliteDungeonTime.../PlayTime1/PlayTime3/HsbStoneRewardClaimed dans sa projection RS0 -- angle mort
-- independant, deja constate a l'ecriture de ce script, non corrige ici). Un DROP+CREATE ecrit ici, a
-- l'aveugle sur le type SQL exact que ce lot concurrent attribuera a TowerCpMilestoneCounter, ne ferait
-- qu'ajouter une candidate supplementaire a une course deja documentee (voir l'en-tete de Migrations/
-- 032_playtime_petbagdate_hsbreward_writeback.sql pour l'inventaire complet des lots concurrents et le
-- raisonnement complet sur pourquoi etendre a l'aveugle diverge plus surement que de differer).
-- Cote application, WarriorPill/WarriorScroll SONT DEJA cables integralement (contrairement aux lots
-- 032/033/034_animal_double_exp cites ci-dessus, qui devaient encore cabler leur cote C#) :
-- Fenrir.Data.Abstractions.Characters.CharacterProgressTvp/CharacterWorldSnapshotDto (deux nouveaux champs
-- en queue), PlayerEnterData (Domain/World/ZoneCommand.cs), EnterWorldService.cs (mapping
-- character.WarriorPill/WarriorScroll), Zone.PlayerLifecycle.cs (state.WarriorPill/WarriorScroll =
-- data.WarriorPill/WarriorScroll, ligne voisine de TowerCpMilestoneCounter) et ProgressWriteBehindHost.cs/
-- PositionWriteBehindHost.cs (les deux arguments nommes ajoutes a la construction de CharacterProgressTvp).
-- Seul le cote SQL (colonnes, TVP, procedures de write-behind, projection RS0) reste a cabler par le lot
-- qui fera la reconciliation finale de game.tvp_CharacterProgress.
--
-- CE QUI RESTE A FAIRE, POUR LE LOT QUI FERA LA RECONCILIATION FINALE DE game.tvp_CharacterProgress
-- Quand la course sera retombee (un seul DROP+CREATE qui declare enfin l'union complete de tous les lots,
-- miroir exact de la fin de CharacterProgressTvp.cs a ce moment-la), ajouter en QUEUE :
--   WarriorPill INT NOT NULL, WarriorScroll INT NOT NULL
-- et les deux clauses SET correspondantes (absolues, PlayerRuntimeState est proprietaire unique, meme
-- categorie que DropItemTime/EliteDungeonTime) dans usp_Character_PersistProgressBatch ET
-- usp_Character_PersistFinalFlush, et les deux colonnes en queue de la projection RS0 de
-- usp_Character_GetForWorldEntry (c.WarriorPill, c.WarriorScroll). Le cote application n'a plus rien a
-- faire : deja cable en integralite par ce lot (voir section precedente).
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql est deja journalise SHA-256 par Fenrir.Tools.DbMigrator sur toute base qui l'a
-- applique une fois -- le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change.
--
-- POURQUOI ALTER ADD ET NON AUTRE CHOSE
-- game.Characters porte de vraies donnees joueur : un ALTER additif avec DEFAULT sur une colonne NOT NULL
-- est une operation de metadonnees seule sur SQL Server, lignes existantes preservees a la valeur par
-- defaut. Aucun IHostedService n'est ajoute ni enregistre : ProgressWriteBehindHost/PositionWriteBehindHost
-- existants suffiront, seuls leurs appels TVP devront etre etendus par le lot de reconciliation finale
-- ci-dessus.

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'WarriorPill')
    ALTER TABLE game.Characters
        ADD WarriorPill INT NOT NULL
            CONSTRAINT DF_Characters_WarriorPill DEFAULT 0
            CONSTRAINT CK_Characters_WarriorPill CHECK (WarriorPill >= 0); -- aWarriorPill (Server/Header/CSQLAvatar.cpp:674), decrement par minute (Server/ts25zone/S07_MyGame04.cpp:1015-1019)
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'WarriorScroll')
    ALTER TABLE game.Characters
        ADD WarriorScroll INT NOT NULL
            CONSTRAINT DF_Characters_WarriorScroll DEFAULT 0
            CONSTRAINT CK_Characters_WarriorScroll CHECK (WarriorScroll >= 0); -- aWarriorScroll (Server/Header/CSQLAvatar.cpp:673), credit S04_MyWork03.cpp:5146-5149, decrement par minute S07_MyGame04.cpp:1020-1024
GO
