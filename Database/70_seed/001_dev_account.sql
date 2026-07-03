-- DEV/TEST-ONLY fixture, not a production credential (docker-compose-style seed data, deliberately
-- public: password "FenrirDev123!"). Hash/salt precomputed with Fenrir.Domain.Security.PasswordHasher
-- (Argon2id, m=64 MiB, t=3, p=1) -- SQL itself cannot compute Argon2id. Idempotent: skipped if the
-- account already exists (matters if this script is ever re-run by hand outside the migrator's own
-- journal, e.g. against a restored backup).
IF
NOT EXISTS (SELECT 1 FROM auth.Accounts WHERE LoginName = N'devtest')
BEGIN
    DECLARE
@AccountId INT;

INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt)
VALUES (N'devtest',
        0x141E3EA297CE6045F49CD1D3474A9D966FD85009E9D03F8EB966F00924C01C68,
        0x4862A4BBC354FBA6E63B8E7D2A0C0CC1);

SET
@AccountId = CAST(SCOPE_IDENTITY() AS INT);

INSERT INTO game.Characters
(AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType, Level, MapId,
 PosX, PosY, PosZ, Heading, Life, MaxLife, Mana, MaxMana)
VALUES (@AccountId, 0, N'DevHero', 0, 0, 0, 0, 1, 1,
        0, 0, 0, 0, 100, 100, 50, 50);
END;
