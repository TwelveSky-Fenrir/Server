using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

// Ordinal-mapped: ctor order must match usp_SessionTicket_Consume's SELECT order. SessionToken is appended
// last (Migrations/010_session_tickets_add_session_token.sql) -- it lets usp_AccountSession_TransitionToGame
// prove a Game-side world-entry claim is for the same login epoch that minted this ticket.
[GenerateDto]
public sealed partial record ConsumedTicketDto(int CharacterId, byte ShardId, Guid SessionToken);
