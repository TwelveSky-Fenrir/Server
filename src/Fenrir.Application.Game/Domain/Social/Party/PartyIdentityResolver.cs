namespace Fenrir.Application.Game.Domain.Social.Party;

public static class PartyIdentityResolver
{
    public static string ResolveCurrentPartyName(IReadOnlyList<int> partyMembers, int characterId, string ownName,
        Func<int, string?> tryResolveMemberName)
    {
        if (partyMembers is not { Count: > 0 })
            return string.Empty;

        var leaderId = partyMembers[0];
        if (leaderId == characterId)
            return ownName;

        return tryResolveMemberName(leaderId) is { Length: > 0 } leaderName ? leaderName : ownName;
    }

    public static string ResolveCurrentPartyName(PartyRegistry partyRegistry, int characterId, string ownName,
        Func<int, string?> tryResolveMemberName)
    {
        var roster = partyRegistry.GetRoster(characterId);
        if (roster.Count == 0)
            return string.Empty;

        var leader = roster[0];
        if (leader.CharacterId == characterId)
            return ownName;

        if (leader.Name is { Length: > 0 })
            return leader.Name;

        return tryResolveMemberName(leader.CharacterId) is { Length: > 0 } leaderName ? leaderName : ownName;
    }
}
