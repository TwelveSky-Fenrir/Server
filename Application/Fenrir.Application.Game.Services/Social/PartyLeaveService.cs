using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     No-op if not partied or is the leader (leader must use CZ_PARTY_BREAK_SEND). Deviation: dropping to 1
///     member auto-disbands here; legacy leaves a lone leader "partied" until an explicit Break.
/// </summary>
public sealed class PartyLeaveService(PartyRegistry parties, ILogger<PartyLeaveService> logger) : IPartyLeaveService
{
    public PartyLeaveResult Leave(int characterId)
    {
        if (!parties.TryLeave(characterId, out var membersBeforeLeave, out var disbanded))
        {
            logger.LogDebug(
                "Party leave ignored: character {CharacterId} is not a non-leader member of any party",
                characterId);
            return new PartyLeaveResult(false);
        }

        var remaining = disbanded ? [] : parties.GetMembers(membersBeforeLeave[0]);

        if (disbanded)
            logger.LogInformation(
                "Party disbanded: character {CharacterId} left, dropping the party below 2 members",
                characterId);
        else
            logger.LogInformation(
                "Party left: character {CharacterId} left, {RemainingCount} members remain",
                characterId, remaining.Count);

        return new PartyLeaveResult(true, membersBeforeLeave, disbanded, remaining);
    }
}
