namespace Fenrir.Data.Abstractions.Security;

/// <summary>
///     admin.Bans.Reason / usp_Ban_Create's @Reason is TINYINT (0-255, Database/Tables/admin/Bans.sql:10,
///     Database/StoredProcedures/admin/usp_Ban_Create.sql:7). Legacy's GM-BLOCK reason/sort code is the literal
///     integer 603 (Server/ts25zone/S04_MyWork04.cpp:1511), which does not fit in that range -- this enum is
///     Fenrir's own in-range mapping for ban-issuing causes, not a literal import of any legacy numeric value.
///     Extend this enum as more ban-issuing paths are added; never renumber an existing member (durably stored).
/// </summary>
public enum BanReason : byte
{
    /// <summary>GM-issued manual block/ban of an online avatar (legacy case 519 "[GM]-BLOCK").</summary>
    GmManualBlock = 1
}
