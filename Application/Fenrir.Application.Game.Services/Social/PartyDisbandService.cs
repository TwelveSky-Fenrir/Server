using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>Leader-only, unconditional full disband.</summary>
public sealed class PartyDisbandService(PartyRegistry parties, ILogger<PartyDisbandService> logger)
    : IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId)
    {
        var members = parties.Disband(leaderId);

        if (members.Count == 0)
            logger.LogDebug("Party disband ignored: character {LeaderId} is not the leader of any party", leaderId);
        else
            logger.LogInformation("Party disbanded: leader {LeaderId} broke up a {MemberCount}-member party",
                leaderId, members.Count);

        return new PartyDisbandResult(members);
    }
}
