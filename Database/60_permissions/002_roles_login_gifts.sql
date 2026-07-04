-- Additive grant for the login-side gift claim/list flow (CL_GIFT_INFO_SEND/CL_WANT_GIFT_SEND, "chantier
-- V8"): LoginServer's ClaimGiftHandler/GiftListHandler need object-level access to these three procs,
-- same narrow "everything else in game stays off-limits" posture 001_roles.sql's own header documents for
-- the character-select flow -- gifts are read/claimed at character select, before a world entry, so this
-- runs on LoginServer, not GameServer.
GRANT EXECUTE ON OBJECT::game.usp_Gift_GetPendingByAccount TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Gift_ClaimIntoVault TO fenrir_login_role;
GO
