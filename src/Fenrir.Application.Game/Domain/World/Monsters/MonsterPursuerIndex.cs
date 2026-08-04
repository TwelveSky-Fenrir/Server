using Fenrir.Application.Game.Domain.World.Runtime;

namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterPursuerIndex
{
    private readonly Dictionary<PursuitTarget, HashSet<int>> _pursuersByTarget = [];

    private readonly Dictionary<int, PursuitTarget> _targetByMonster = [];

    public void Track(MonsterEntity monster)
    {
        monster.PursuitStateChanged = Update;
        Update(monster);
    }

    public void Untrack(MonsterEntity monster)
    {
        monster.PursuitStateChanged = null;
        Remove(monster.ServerIndex);
    }

    public int CountOther(MonsterEntity subject, int characterId, RuntimeIncarnation incarnation)
    {
        var target = new PursuitTarget(characterId, incarnation);
        if (!_pursuersByTarget.TryGetValue(target, out var pursuers))
            return 0;

        return pursuers.Count - (pursuers.Contains(subject.ServerIndex) ? 1 : 0);
    }

    private void Update(MonsterEntity monster)
    {
        Remove(monster.ServerIndex);

        if (monster.AiState is not (MonsterAiState.Chase or MonsterAiState.AttackWindup) ||
            monster.TargetCharacterId is not { } characterId)
            return;

        var target = new PursuitTarget(characterId, monster.TargetIncarnation);
        if (!_pursuersByTarget.TryGetValue(target, out var pursuers))
            _pursuersByTarget.Add(target, pursuers = []);

        pursuers.Add(monster.ServerIndex);
        _targetByMonster.Add(monster.ServerIndex, target);
    }

    private void Remove(int monsterServerIndex)
    {
        if (!_targetByMonster.Remove(monsterServerIndex, out var target) ||
            !_pursuersByTarget.TryGetValue(target, out var pursuers))
            return;

        pursuers.Remove(monsterServerIndex);
        if (pursuers.Count == 0)
            _pursuersByTarget.Remove(target);
    }

    private readonly record struct PursuitTarget(int CharacterId, RuntimeIncarnation Incarnation);
}
