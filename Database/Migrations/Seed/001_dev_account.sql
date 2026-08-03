IF
    NOT EXISTS (SELECT 1
                FROM auth.Accounts
                WHERE LoginName = N'dev')
    BEGIN

        INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt)
        VALUES (N'dev',
                0x0039F539E70BED20E756B69221924BFAB5E49824766C5B328EFBE7AB4A0221C4,
                0xD314C98F0131FD0067456788711684C5),
               (N'dev1',
                0x0039F539E70BED20E756B69221924BFAB5E49824766C5B328EFBE7AB4A0221C4,
                0xD314C98F0131FD0067456788711684C5);
    END;
