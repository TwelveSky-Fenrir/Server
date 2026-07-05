using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     No-op if not partied or is the leader (leader must use CZ_PARTY_BREAK_SEND). Deviation: dropping to 1
///     member auto-disbands here; legacy leaves a lone leader "partied" until an explicit Break.
/// </summary>
public sealed class PartyLeaveService(PartyRegistry parties) : IPartyLeaveService
{
    public PartyLeaveResult Leave(int characterId)
    {
        if (!parties.TryLeave(characterId, out var membersBeforeLeave, out var disbanded))
            return new PartyLeaveResult(false);

        var remaining = disbanded ? [] : parties.GetMembers(membersBeforeLeave[0]);

        return new PartyLeaveResult(true, membersBeforeLeave, disbanded, remaining);
    }
}
