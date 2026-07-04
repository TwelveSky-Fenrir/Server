using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Runtime;

// Ordinal-mapped: ctor order must match usp_SessionTicket_Consume's SELECT order.
[GenerateDto]
public sealed partial record ConsumedTicketDto(int CharacterId, byte ShardId);
