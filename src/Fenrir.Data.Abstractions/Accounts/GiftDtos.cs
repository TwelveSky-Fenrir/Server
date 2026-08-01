using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Accounts;

[GenerateDto]
public sealed partial record PendingGiftDto(int GiftId, int? ProductId, int Quantity, int Value, DateTime CreatedAtUtc);
