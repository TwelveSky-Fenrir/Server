namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeVoteElectionTally
{
    public static TribeVoteElectionCandidateDto? SelectWinner(
        IEnumerable<TribeVoteElectionCandidateDto> candidates, byte tribeId)
    {
        return candidates
            .Where(candidate => candidate.TribeId == tribeId && candidate.VotePoint > 0)
            .OrderByDescending(static candidate => candidate.VotePoint)
            .ThenBy(static candidate => candidate.SlotIndex)
            .FirstOrDefault();
    }
}
