using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

public static class QuestBossResummon
{
    public const int TriggerQuestSort = 5;

    public const float SummonRadiusUnits = 300.0f;

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

    public static bool IsWithinSummonRadius(float avatarX, float avatarY, float avatarZ,
        float summonX, float summonY, float summonZ)
    {
        var dx = avatarX - summonX;
        var dy = avatarY - summonY;
        var dz = avatarZ - summonZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= SummonRadiusUnits;
    }
}

public readonly record struct QuestBossSummonRequest(int MonsterId, float PosX, float PosY, float PosZ);
