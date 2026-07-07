-- Guarded per-row for the same shared-table reason as 001_error_catalog_admin.sql.
IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50222)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50222, N'game',
            N'usp_Character_AdjustMoney: unknown character or insufficient money balance for this adjustment.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50230)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50230, N'game', N'usp_Guild_Create: guild name is already taken.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50231)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50231, N'game', N'usp_Guild_Create/usp_GuildMember_Add: character already belongs to a guild.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50232)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50232, N'game', N'usp_GuildMember_Add: guild is full (50 members, MAX_GUILD_AVATAR_NUM).');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50233)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50233, N'game', N'usp_GuildMember_SetRole/usp_Guild_SetMaster: character is not a member of this guild.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50234)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50234, N'game', N'usp_Guild_AdjustPoints: unknown guild or insufficient guild points for this adjustment.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50235)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50235, N'game',
            N'usp_Guild_Disband/SetMaster/SetBuff/SetLogo/SetGrade, usp_GuildMember_Add: guild not found.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50240)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50240, N'game', N'usp_Cash_Debit: insufficient cash balance for this debit.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50241)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50241, N'game', N'usp_Cash_Debit/usp_Cash_Credit: cash amount must be positive.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50260)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50260, N'game', N'usp_CharacterItems_ReplaceTwoContainers: ContainerA and ContainerB must differ.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50261)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50261, N'game',
            N'usp_Character_AdjustMoney/usp_Character_AdjustMoneyAndReplaceContainer/...AndReplaceTwoContainers: adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50264)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50264, N'game',
            N'usp_Character_AdjustMoneyAndReplaceContainer: unknown character or insufficient money balance for this adjustment.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50265)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50265, N'game',
            N'usp_Character_AdjustMoneyAndReplaceTwoContainers: unknown character or insufficient money balance for this adjustment.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50266)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50266, N'game',
            N'usp_Character_AdjustMoneyAndReplaceTwoContainers: ContainerA and ContainerB must differ.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50306)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50306, N'admin', N'usp_Mute_Create: a mute must target at least one of @AccountId or @CharacterId.');
