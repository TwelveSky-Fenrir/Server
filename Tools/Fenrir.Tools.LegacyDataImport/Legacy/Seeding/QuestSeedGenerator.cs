using System.Globalization;
using System.Text;
using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

/// <summary>
///     Row counts produced by <see cref="QuestSeedGenerator.Generate" />, reported back so the caller can
///     confirm real production numbers (in particular how much the QuestSpeeches sparse-normalization saved versus
///     the theoretical 1000 x 10 x 15 = 150,000 max).
/// </summary>
public sealed record QuestSeedStats(
    int TotalRawRecords,
    int PaddingRecordsExcluded,
    int QuestRowCount,
    int QuestRewardRowCount,
    int QuestSpeechRowCount);

/// <summary>
///     Generates the idempotent world.Quests / world.QuestRewards / world.QuestSpeeches seed script from the
///     real 005_00006.IMG data. Not runtime code -- invoked once (by a throwaway scratch console project, per
///     the workflow's "do not touch Program.cs" rule) to produce the checked-in
///     Database/70_seed/world_quests.sql file.
/// </summary>
public static class QuestSeedGenerator
{
    // Legacy qReward[slot][0] discriminator, confirmed from the actual game logic (NOT a guess): the switch in
    // ts25zone/S04_MyWork02.cpp (~line 7404) and the bounds check in Header/S15_MyShare.cpp (~line 2046,
    // "qReward[i][0] < 1 || > 6") together prove qReward is a (RewardType, Value) tagged union, not an
    // (itemId, quantity) pair as might naively be assumed from the field name.
    private const int RewardTypeNone = 1; // case 1: break -- a deliberate no-op/"empty slot" sentinel
    private const int RewardTypeMoney = 2;
    private const int RewardTypeKillOtherTribeCount = 3;
    private const int RewardTypeExperience = 4;
    private const int RewardTypeTeacherPoint = 5;

    private const int RewardTypeItem = 6; // Value is an ItemId; ts25zone/S07_MyGame04.cpp's
    // ReturnItemQuantityForQuestReward proves the granted quantity is derived at grant-time from the item's own
    // iSort (equipment => 0, everything else => 1) and is never itself stored in qReward.

    // SpeechKind mapping -- our own invented convention (QUEST_INFO has no such discriminator), documented here
    // and in the table's SQL comment since both must agree.
    private const byte SpeechStart = 0;
    private const byte SpeechHurry = 1;
    private const byte SpeechProcess1 = 2;
    private const byte SpeechProcess2 = 3;
    private const byte SpeechProcess3 = 4;
    private const byte SpeechProcess4 = 5;
    private const byte SpeechProcess5 = 6;
    private const byte SpeechSuccess = 7;
    private const byte SpeechFailure = 8;
    private const byte SpeechCall = 9;

    private const int MaxRowsPerInsert = 500;

    /// <summary>
    ///     Writes three sibling files into <paramref name="outputDir" />: 050_quests.sql,
    ///     051_quest_rewards.sql, 052_quest_speeches.sql (one seed file per table, matching the convention already
    ///     established by the other world.* domains' seed files under Database/70_seed/world/).
    /// </summary>
    public static QuestSeedStats Generate(string dataDir, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var all = QuestReader.ReadAll(dataDir);
        // Sentinel: QUEST_INFO array slots 689-1000 are unused padding -- empty qSubject AND all-zero
        // qStartNPCNumber/qEndNPCNumber/qNextIndex/reward-type/speech content (confirmed by direct inspection
        // of the real decoded data while designing this schema). Index itself is never 0 here (legacy array
        // is 1-based, unlike some other legacy tables), so emptiness of Subject is the real "is this slot
        // used" signal for this table.
        var quests = all.Where(q => !string.IsNullOrEmpty(q.Subject)).ToList();
        var includedIds = new HashSet<int>(quests.Select(q => q.Index));

        var questRows = new List<string>(quests.Count);
        var rewardRows = new List<string>(quests.Count * 3);
        var speechRows = new List<string>(quests.Count * 10 * 15);

        foreach (var q in quests)
        {
            questRows.Add(BuildQuestRow(q, includedIds));

            for (var slot = 0; slot < 3; slot++)
            {
                var type = q.Reward[slot][0];
                if (type == RewardTypeNone) continue; // empty slot, see RewardTypeNone comment above
                var value = q.Reward[slot][1];
                var itemId = type == RewardTypeItem ? value.ToString(CultureInfo.InvariantCulture) : "NULL";
                var amount = type == RewardTypeItem ? "NULL" : value.ToString(CultureInfo.InvariantCulture);
                rewardRows.Add($"({q.Index}, {slot}, {type}, {itemId}, {amount})");
            }

            AppendSpeechRows(speechRows, q.Index, SpeechStart, q.StartSpeech, q.StartSpeechColor);
            AppendSpeechRows(speechRows, q.Index, SpeechHurry, q.HurrySpeech, q.HurrySpeechColor);
            AppendSpeechRows(speechRows, q.Index, SpeechProcess1, q.ProcessSpeech1, q.ProcessSpeech1Color);
            AppendSpeechRows(speechRows, q.Index, SpeechProcess2, q.ProcessSpeech2, q.ProcessSpeech2Color);
            AppendSpeechRows(speechRows, q.Index, SpeechProcess3, q.ProcessSpeech3, q.ProcessSpeech3Color);
            AppendSpeechRows(speechRows, q.Index, SpeechProcess4, q.ProcessSpeech4, q.ProcessSpeech4Color);
            AppendSpeechRows(speechRows, q.Index, SpeechProcess5, q.ProcessSpeech5, q.ProcessSpeech5Color);
            AppendSpeechRows(speechRows, q.Index, SpeechSuccess, q.SuccessSpeech, q.SuccessSpeechColor);
            AppendSpeechRows(speechRows, q.Index, SpeechFailure, q.FailureSpeech, q.FailureSpeechColor);
            AppendSpeechRows(speechRows, q.Index, SpeechCall, q.CallSpeech, q.CallSpeechColor);
        }

        var questsSb = new StringBuilder();
        questsSb.AppendLine("-- Generated by Fenrir.Tools.LegacyDataImport.Legacy.Seeding.QuestSeedGenerator from");
        questsSb.AppendLine("-- 005_00006.IMG (real legacy DATA directory) -- do not hand-edit, regenerate instead.");
        questsSb.AppendLine($"-- Row count at generation time: world.Quests={quests.Count} (of {all.Count} raw " +
                            $"array slots, {all.Count - quests.Count} unused-padding slots excluded).");
        questsSb.AppendLine();
        WriteGuardedInsertBlock(questsSb, "world.Quests",
            "QuestId, Subject, Category, Step, Level, Type, Sort, " +
            "SummonZoneNumber, SummonPosX, SummonPosY, SummonPosZ, " +
            "StartNPCNumber, KeyNpcNumber1, KeyNpcNumber2, KeyNpcNumber3, KeyNpcNumber4, KeyNpcNumber5, EndNPCNumber, " +
            "Solution1, Solution2, Solution3, Solution4, NextIndex",
            questRows,
            int.MaxValue); // single statement (688 rows, well under the 1000 cap) -- see 050_quests.sql header WHY
        File.WriteAllText(Path.Combine(outputDir, "050_quests.sql"), questsSb.ToString());

        var rewardsSb = new StringBuilder();
        rewardsSb.AppendLine("-- Generated by Fenrir.Tools.LegacyDataImport.Legacy.Seeding.QuestSeedGenerator from");
        rewardsSb.AppendLine("-- 005_00006.IMG (real legacy DATA directory) -- do not hand-edit, regenerate instead.");
        rewardsSb.AppendLine($"-- Row count at generation time: world.QuestRewards={rewardRows.Count} (of a " +
                             $"theoretical {quests.Count * 3} = {quests.Count} quests x 3 slots max; the rest " +
                             "are RewardType=1 \"none\" sentinel slots, intentionally not stored).");
        rewardsSb.AppendLine();
        WriteGuardedInsertBlock(rewardsSb, "world.QuestRewards", "QuestId, SlotIndex, RewardType, ItemId, Amount",
            rewardRows, MaxRowsPerInsert);
        File.WriteAllText(Path.Combine(outputDir, "051_quest_rewards.sql"), rewardsSb.ToString());

        var speechesSb = new StringBuilder();
        speechesSb.AppendLine("-- Generated by Fenrir.Tools.LegacyDataImport.Legacy.Seeding.QuestSeedGenerator from");
        speechesSb.AppendLine("-- 005_00006.IMG (real legacy DATA directory) -- do not hand-edit, regenerate instead.");
        speechesSb.AppendLine($"-- Row count at generation time: world.QuestSpeeches={speechRows.Count} (theoretical " +
                              "max 1000 quest slots x 10 speech blocks x 15 lines = 150,000; even restricted to " +
                              $"just the {quests.Count} real quests it would be {quests.Count * 10 * 15} -- sparse " +
                              "per-line normalization is what actually keeps this table small).");
        speechesSb.AppendLine();
        WriteGuardedInsertBlock(speechesSb, "world.QuestSpeeches", "QuestId, SpeechKind, LineIndex, Text, Color",
            speechRows, MaxRowsPerInsert);
        File.WriteAllText(Path.Combine(outputDir, "052_quest_speeches.sql"), speechesSb.ToString());

        return new QuestSeedStats(all.Count, all.Count - quests.Count, quests.Count, rewardRows.Count,
            speechRows.Count);
    }

    private static string BuildQuestRow(QuestRecord q, HashSet<int> includedIds)
    {
        // qNextIndex==0 means "end of chain" (3 quests). One quest (589) points at 689, an excluded padding
        // slot -- functionally the same "end of chain" outcome, so both normalize to NULL (never a literal 0,
        // per the FK-nullability convention: 0 is not a valid QuestId).
        var nextIndex = q.NextIndex == 0 || !includedIds.Contains(q.NextIndex)
            ? "NULL"
            : q.NextIndex.ToString(CultureInfo.InvariantCulture);

        // qSummonInfo is only ever populated for Sort=5 ("Kill the Captain" boss quests) -- confirmed by direct
        // inspection (127 of 688 quests, all Sort=5). [0] is a zone number (SummonQuestBoss compares it against
        // mSERVER_INFO.mServerNumber, bounds-checked 0..200 in S15_MyShare.cpp -- fits world.Zones); [1..3] are
        // signed X/Y/Z world coordinates (cast to float in SummonQuestBoss) for where the boss is summoned.
        var summonZone = q.SummonInfo[0] == 0 ? "NULL" : q.SummonInfo[0].ToString(CultureInfo.InvariantCulture);
        var summonX = q.SummonInfo[1] == 0 ? "NULL" : q.SummonInfo[1].ToString(CultureInfo.InvariantCulture);
        var summonY = q.SummonInfo[2] == 0 ? "NULL" : q.SummonInfo[2].ToString(CultureInfo.InvariantCulture);
        var summonZ = q.SummonInfo[3] == 0 ? "NULL" : q.SummonInfo[3].ToString(CultureInfo.InvariantCulture);

        var keyNpc = q.KeyNPCNumber.Select(NullIfZero).ToArray();
        var solution = q.Solution.Select(NullIfZero).ToArray();

        return "(" + string.Join(", ", new[]
        {
            q.Index.ToString(CultureInfo.InvariantCulture),
            SqlString(q.Subject),
            q.Category.ToString(CultureInfo.InvariantCulture),
            q.Step.ToString(CultureInfo.InvariantCulture),
            q.Level.ToString(CultureInfo.InvariantCulture),
            q.Type.ToString(CultureInfo.InvariantCulture),
            q.Sort.ToString(CultureInfo.InvariantCulture),
            summonZone, summonX, summonY, summonZ,
            q.StartNPCNumber.ToString(CultureInfo.InvariantCulture),
            keyNpc[0], keyNpc[1], keyNpc[2], keyNpc[3], keyNpc[4],
            q.EndNPCNumber.ToString(CultureInfo.InvariantCulture),
            solution[0], solution[1], solution[2], solution[3],
            nextIndex
        }) + ")";
    }

    private static void AppendSpeechRows(List<string> rows, int questId, byte speechKind, IReadOnlyList<string> lines,
        IReadOnlyList<int> colors)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            rows.Add($"({questId}, {speechKind}, {i}, {SqlString(lines[i])}, {colors[i]})");
        }
    }

    private static string NullIfZero(int value)
    {
        return value == 0 ? "NULL" : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string SqlString(string value)
    {
        return "N'" + value.Replace("'", "''") + "'";
    }

    private static void WriteGuardedInsertBlock(StringBuilder sb, string table, string columns, List<string> rows,
        int maxRowsPerInsert)
    {
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM {table})");
        sb.AppendLine("BEGIN");
        for (var offset = 0; offset < rows.Count; offset += maxRowsPerInsert)
        {
            var chunk = rows.Skip(offset).Take(maxRowsPerInsert).ToList();
            sb.AppendLine($"    INSERT INTO {table} ({columns}) VALUES");
            for (var i = 0; i < chunk.Count; i++)
            {
                var suffix = i == chunk.Count - 1 ? ";" : ",";
                sb.AppendLine($"    {chunk[i]}{suffix}");
            }
        }

        sb.AppendLine("END;");
        sb.AppendLine();
    }
}
