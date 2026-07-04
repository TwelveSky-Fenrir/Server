using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Accounts;

/// <summary>
///     One pending (Status=0) game.Gifts row -- ordinal contract of usp_Gift_GetPendingByAccount, oldest
///     first (delivery order -- the SAME order CL_GIFT_INFO_SEND's page index and CL_WANT_GIFT_SEND's
///     GiftInfoIndex both assume, login protocol report §4.21/§4.25).
/// </summary>
[GenerateDto]
public sealed partial record PendingGiftDto(int GiftId, int? ProductId, int Quantity, int Value, DateTime CreatedAtUtc);

/// <summary>One-row result of usp_Gift_ClaimIntoVault -- the account-vault slot the claimed item landed in.</summary>
[GenerateDto]
public sealed partial record GiftClaimResultDto(short SlotIndex);
