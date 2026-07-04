-- Singleton row for admin-tunable game-design settings that have NO legacy source value to stay iso to
-- (unlike most numeric constants in this codebase, which are byte-exact ports of a verified C++ constant
-- and therefore stay as C# consts -- see e.g. PartyRegistry.MaxLevelGap, GuildActionHandler.CreateGuildMoneyCost,
-- both verified against Server/ts25zone/S04_MyWork02.cpp and deliberately NOT here). Same singleton-row
-- pattern as game.WorldState.
--   ProxyShopDurationDays <- the offline/proxy shop's rental window (OpenShopStallHandler). The legacy's
--     aProxyShopDate rental-expiration field has no Fenrir column (OpenShopStallHandler's own remarks) --
--     this is a genuinely Fenrir-invented substitute, not a reproduced legacy value, so it is the one
--     candidate an admin can legitimately retune without breaking iso-behavior fidelity.
CREATE TABLE admin.GameSettings
(
    Id                    TINYINT NOT NULL CONSTRAINT DF_GameSettings_Id DEFAULT 1,
    ProxyShopDurationDays TINYINT NOT NULL CONSTRAINT DF_GameSettings_ProxyShopDurationDays DEFAULT 7,
    CONSTRAINT PK_GameSettings PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_GameSettings_Id CHECK (Id = 1),
    CONSTRAINT CK_GameSettings_ProxyShopDurationDays CHECK (ProxyShopDurationDays > 0)
);
