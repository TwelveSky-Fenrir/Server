-- Documents the THROW codes introduced by the phase A3 procedures (character persistence, guild writes,
-- cash, mutes), per admin.ErrorCatalog's "documentation as data" contract (architecture reference
-- §12.3). Guarded per-row for the same shared-table reason as 001_error_catalog_admin.sql.
IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50222)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50222, N'game', N'usp_Character_AdjustMoney: unknown character or insufficient money balance for this adjustment.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50230)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50230, N'game', N'usp_Guild_Create: guild name is already taken.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50231)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50231, N'game', N'usp_Guild_Create/usp_GuildMember_Add: character already belongs to a guild.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50232)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50232, N'game', N'usp_GuildMember_Add: guild is full (50 members, MAX_GUILD_AVATAR_NUM).');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50233)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50233, N'game', N'usp_GuildMember_SetRole/usp_Guild_SetMaster: character is not a member of this guild.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50234)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50234, N'game', N'usp_Guild_AdjustPoints: unknown guild or insufficient guild points for this adjustment.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50235)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50235, N'game', N'usp_Guild_Disband/SetMaster/SetBuff/SetLogo/SetGrade, usp_GuildMember_Add: guild not found.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50240)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50240, N'game', N'usp_Cash_Debit: insufficient cash balance for this debit.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50241)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50241, N'game', N'usp_Cash_Debit/usp_Cash_Credit: cash amount must be positive.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50306)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50306, N'admin', N'usp_Mute_Create: a mute must target at least one of @AccountId or @CharacterId.');
