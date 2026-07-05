using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Accounts;

// usp_Gift_GetPendingByAccount, oldest first -- CL_GIFT_INFO_SEND/CL_WANT_GIFT_SEND index into this order.
[GenerateDto]
public sealed partial record PendingGiftDto(int GiftId, int? ProductId, int Quantity, int Value, DateTime CreatedAtUtc);

/// <summary>One-row result of usp_Gift_ClaimIntoVault -- the account-vault slot the claimed item landed in.</summary>
[GenerateDto]
public sealed partial record GiftClaimResultDto(short SlotIndex);
