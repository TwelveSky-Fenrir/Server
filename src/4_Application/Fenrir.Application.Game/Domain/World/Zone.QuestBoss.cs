using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int QuestBossPoolServerIndexBase = 1_006_000;

    private const int QuestBossPoolSize = 100;

    /// <summary>
    ///     Per-tick sweep of every avatar in this zone for the personal "kill the captain" quest boss (qSort=5)
    ///     re-summon (contract: évaluation une fois par avatar et par tick, sans cooldown propre — la
    ///     déduplication du pool tient lieu d'anti-spam). The cheap <see cref="QuestBossResummon.TriggerQuestSort" />
    ///     gate short-circuits every avatar not currently on a captain quest, so the catalog lookup and present-state
    ///     computation only run for the small subset actively hunting a captain.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : AVATAR_OBJECT::Update appelle SummonQuestBoss sans condition à chaque tick
    ///     (Server/ts25zone/S07_MyGame04.cpp:350). Ce balayage doit être invoqué depuis la boucle de tick
    ///     (Zone.Tick) — de préférence dans le bloc <c>legacyTicksElapsed &gt; 0</c> pour coller à la cadence
    ///     2 Hz TimeLogic de la simulation legacy plutôt qu'à la trame réseau 20 Hz ; la déduplication du pool
    ///     rend le placement idempotent quelle que soit la cadence. Runs on the tick thread (single writer), so
    ///     iterating <c>_players</c> and mutating the monster pool here is safe without locking.
    /// </remarks>
    internal void SummonPersonalQuestBossesForTick()
    {
        foreach (var (_, state) in _players)
            TrySummonPersonalQuestBoss(state);
    }

    private void TrySummonPersonalQuestBoss(PlayerRuntimeState state)
    {
        if (state.QuestSort != QuestBossResummon.TriggerQuestSort)
            return;

        var quest = _questCatalog.TryGet(state.Tribe, state.QuestStepPermanent);
        if (quest is null)
            return;

        var progress = new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
            state.QuestTargetPhase, state.QuestKillCounter);

        // present-state == 2 means "captain quest accepted, boss not yet killed". qSort=5's present-state
        // (QuestStateMachine case 5) never inspects inventory, so a no-op item probe is provably correct here
        // and keeps this per-avatar/per-tick path free of an equipment-container lookup.
        var presentState = QuestStateMachine.ComputePresentState(progress, state.Tribe, state.Level, _questCatalog,
            static _ => false);

        var request = QuestBossResummon.Evaluate(state.QuestSort, presentState, quest, MapId, state.PosX,
            state.PosY, state.PosZ);
        if (request is not { } summon)
            return;

        SummonPersonalQuestBoss(summon);
    }

    private void SummonPersonalQuestBoss(QuestBossSummonRequest request)
    {
        // Global dedup by monster id over the quest-boss pool: at most one live instance of this boss id at a
        // time (contract: "vérifier existence" flag). Any other eligible player near the same spawn re-summons
        // it once the current instance is killed. Order (dedup -> free slot -> catalog) follows the legacy
        // special-summon path (S10_MySummon.cpp:1261-1292); each pre-create guard is a silent no-op.
        if (QuestBossAlreadyLive(request.MonsterId))
            return;

        if (!TryFindFreeQuestBossSlot(out var serverIndex))
            return;

        if (!worldData.MonstersById.TryGetValue(request.MonsterId, out var definition))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            request.PosX, request.PosY, request.PosZ);

        SpawnMonster(monster);
    }

    private bool QuestBossAlreadyLive(int monsterId)
    {
        for (var i = 0; i < QuestBossPoolSize; i++)
        {
            var candidate = QuestBossPoolServerIndexBase + i;
            if (_monsters.TryGetValue(candidate, out var monster) && monster.Template.MonsterId == monsterId)
                return true;
        }

        return false;
    }

    private bool TryFindFreeQuestBossSlot(out int serverIndex)
    {
        for (var i = 0; i < QuestBossPoolSize; i++)
        {
            var candidate = QuestBossPoolServerIndexBase + i;
            if (!_monsters.ContainsKey(candidate))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }
}
