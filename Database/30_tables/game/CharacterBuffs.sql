-- Persists the active buff table at disconnect (legacy aBufSort/aBufEffectValue/aBufEffectTime arrays):
-- one row per occupied buff slot, row-absence = empty slot.
-- [CORRIGÉ-REVUE, décision D8] Ce n'est PAS "réappliqué au prochain world-entry" en général -- le legacy
-- vide inconditionnellement uBuffInfo à CHAQUE login frais (ts25playuser/S07_MyGame01.cpp:939,
-- RegisterUserForLogin: `ZeroMemory(&z->uBuffInfo, sizeof(BUFF_INFO))`), avant toute entrée en zone. Le
-- flux Login->CharSelect->world-entry (via LoginServer/handover) DOIT donc ignorer ces lignes et démarrer
-- avec un état de buff vide, exactement comme le C++ -- les exposer quand même dans
-- usp_Character_GetForWorldEntry.RS4 sans ce garde-fou reproduirait un comportement que le legacy n'a
-- jamais eu (buffs qui survivent une reconnexion complète), en violation de la politique iso-comportement.
-- Le transfert de carte EN COURS DE SESSION (ZoneTransfer) ne passe JAMAIS par cette table non plus :
-- l'état vit dans PlayerRuntimeState et voyage directement dans la commande Enter (in-memory, D5) --
-- cette table n'a donc, à ce jour, AUCUN consommateur légitime au world-entry ; elle existe pour un futur
-- scénario de reprise après redémarrage du process (hors périmètre actuel), à réévaluer par Phase C's buff
-- engine avant de câbler la moindre lecture de RS4 sur le chemin normal d'entrée en monde.
-- Slot count is 35: MAX_AVATAR_EFFECT_SORT_NUM (DEFINE.h:323-339) is a build-config switch, BUT MG5ORIGIN
-- itself is defined UNCONDITIONALLY at DEFINE.h:18 (a bare #define, never gated by M33/any other macro) and
-- KR is never defined anywhere in the tree -- so every build, ReleaseEU33 included, resolves to the
-- MG5ORIGIN/!KR branch = 35, not the generic #else's 32. (A prior pass here mis-traced this #ifdef chain
-- and "corrected" this to 32 -- reverted after cross-checking against Docs/protocol/M1_Legacy_Wire_Contract.md,
-- which sealed this exact figure against a real compilation.)
-- RemainingLegacyTicks is the remaining duration in LEGACY TICKS of 500 ms (D4: LegacyTick = 500 ms is
-- the single conversion constant; buffs are decremented per tick by the simulation, report 05 --
-- persisting ticks verbatim avoids any ms<->tick conversion drift at the storage boundary; the x10
-- error class flagged by review 08 cannot exist if nothing ever converts).
-- SlotIndex is the legacy buff slot (0-34); Value is the slot's effect value int stored verbatim (its
-- per-buff semantics belong to the buff engine, Phase C).
CREATE TABLE game.CharacterBuffs
(
    CharacterId          INT     NOT NULL,
    SlotIndex            TINYINT NOT NULL,
    Value                INT     NOT NULL CONSTRAINT DF_CharacterBuffs_Value DEFAULT 0,
    RemainingLegacyTicks INT     NOT NULL CONSTRAINT DF_CharacterBuffs_RemainingLegacyTicks DEFAULT 0,
    CONSTRAINT PK_CharacterBuffs PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterBuffs_SlotIndex CHECK (SlotIndex <= 34), -- 35 legacy buff slots (MG5ORIGIN)
    CONSTRAINT CK_CharacterBuffs_RemainingLegacyTicks CHECK (RemainingLegacyTicks >= 0),
    CONSTRAINT FK_CharacterBuffs_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
