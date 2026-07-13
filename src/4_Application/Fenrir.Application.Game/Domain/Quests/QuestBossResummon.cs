using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

/// <summary>
///     Pure eligibility rule for the personal "kill the captain" quest boss (qSort=5) re-summon, evaluated
///     once per valid avatar per tick.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:2193-2221 (AVATAR_OBJECT::SummonQuestBoss), appelé sans
///     condition à chaque tick depuis AVATAR_OBJECT::Update (S07_MyGame04.cpp:350). Quatre gardes, dans
///     l'ordre : (1) tri de quête = 5 ET état de progression = 2 (S07_MyGame04.cpp:2199) ; (2) ligne de quête
///     retrouvée pour (tribu, index de quête) — vérifiée par l'appelant qui possède le catalogue ; (3) carte
///     de spawn du boss = carte de ce processus de zone (S07_MyGame04.cpp:2208-2211) ; (4) distance euclidienne
///     3D réelle joueur → point de spawn fixe &lt;= 300.0 (S07_MyGame04.cpp:2212-2218, formule
///     Server/Header/mapcheck.h:12-15 GetLengthXYZ). L'état 2 = "quête capitaine acceptée, boss pas encore
///     tué" ; le seuil de complétion du kill est la constante codée en dur 1, pas un champ de la quête
///     (S07_MyGame04.cpp:1796-1805), déjà encodé dans <see cref="QuestStateMachine" /> case 5. Le boss apparaît
///     au point fixe de la quête (coordonnées entières castées en flottants), jamais à la position du joueur ;
///     son identifiant = 1re solution de la quête (aussi l'identifiant crédité au kill, quête[3]). La
///     déduplication globale par identifiant de monstre et les échecs silencieux (pool plein, monstre inconnu)
///     appartiennent au sous-système d'invocation, pas à cette règle.
/// </remarks>
public static class QuestBossResummon
{
    /// <summary>Seul le tri de quête "tuer le capitaine" (qSort=5) déclenche le re-summon.</summary>
    public const int TriggerQuestSort = 5;

    /// <summary>Rayon de portée autour du point de spawn fixe : re-summon si distance &lt;= 300.0, sinon rien.</summary>
    public const float SummonRadiusUnits = 300.0f;

    /// <summary>
    ///     Applique les gardes tri/état/carte/distance et, si toutes passent, retourne la requête d'invocation
    ///     (identifiant du monstre boss + point de spawn fixe). Retourne <c>null</c> — no-op silencieux — dès
    ///     qu'une garde échoue.
    /// </summary>
    /// <param name="questSort">Copie du tri de quête prise à l'acceptation (quête[2]).</param>
    /// <param name="presentState">
    ///     État de progression dérivé via <see cref="QuestStateMachine.ComputePresentState" /> ; l'état requis
    ///     est <see cref="QuestStateMachine.StateInProgress" /> (2).
    /// </param>
    /// <param name="quest">Ligne de quête courante déjà retrouvée par l'appelant pour (tribu, index de quête).</param>
    /// <param name="currentMapId">Numéro de carte de ce processus de zone.</param>
    public static QuestBossSummonRequest? Evaluate(int questSort, int presentState, QuestDefinition quest,
        short currentMapId, float avatarX, float avatarY, float avatarZ)
    {
        if (questSort != TriggerQuestSort || presentState != QuestStateMachine.StateInProgress)
            return null;

        var row = quest.Quest;
        if ((row.SummonZoneNumber ?? 0) != currentMapId)
            return null;

        float summonX = row.SummonPosX ?? 0;
        float summonY = row.SummonPosY ?? 0;
        float summonZ = row.SummonPosZ ?? 0;

        if (!IsWithinSummonRadius(avatarX, avatarY, avatarZ, summonX, summonY, summonZ))
            return null;

        return new QuestBossSummonRequest(row.Solution1 ?? 0, summonX, summonY, summonZ);
    }

    /// <summary>
    ///     Distance euclidienne 3D réelle (racine carrée de la somme des carrés des écarts X, Y, Z), comparée au
    ///     rayon fixe. À exactement 300.0 le re-summon procède ; au-dessus il s'abstient.
    /// </summary>
    public static bool IsWithinSummonRadius(float avatarX, float avatarY, float avatarZ,
        float summonX, float summonY, float summonZ)
    {
        var dx = avatarX - summonX;
        var dy = avatarY - summonY;
        var dz = avatarZ - summonZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= SummonRadiusUnits;
    }
}

/// <summary>
///     Requête d'invocation d'un boss de quête au point de spawn fixe de la quête. <see cref="MonsterId" /> est
///     la 1re solution de la quête (aussi l'identifiant crédité au kill).
/// </summary>
public readonly record struct QuestBossSummonRequest(int MonsterId, float PosX, float PosY, float PosZ);
