-- Migrations/045_accountvault_bigmoney_column.sql
-- Adds the account-scoped "Save/vault BigMoney" pool (legacy masterinfo.uSaveMoney's BigMoney-family
-- counterpart -- there is no cited legacy field name distinct from the general vault money split this
-- schema's own Money/Money2 pair already models; only ONE new counter is added here, not a Money2-style
-- second mirror, since CZ_PROCESS_DATA_SEND tSort 242/245 only ever needs one) backing the Inventory
-- BigMoney <-> Save BigMoney transfer (tSort 242 deposit/245 withdraw). See
-- Migrations/046_accountvault_bigmoney_transfer_proc.sql for the procedure this column supports (also a
-- migration, not StoredProcedures/, for the same before/after-migrations ordering reason Migrations/041/042
-- already document).
ALTER TABLE game.AccountVault
    ADD BigMoney INT NOT NULL
        CONSTRAINT DF_AccountVault_BigMoney DEFAULT 0
        CONSTRAINT CK_AccountVault_BigMoney CHECK (BigMoney >= 0);
