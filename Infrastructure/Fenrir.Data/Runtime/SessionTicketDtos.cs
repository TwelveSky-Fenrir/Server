using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Runtime;

/// <summary>
///     Row shape of usp_SessionTicket_Consume's single result set. Ordinal mapping (CaeriusNet source generator)
///     mirrors the proc's SELECT column order exactly -- keep the two in lockstep if either changes.
/// </summary>
[GenerateDto]
public sealed partial record ConsumedTicketDto(int CharacterId, byte ShardId);
