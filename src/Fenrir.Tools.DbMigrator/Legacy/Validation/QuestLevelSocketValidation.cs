using Fenrir.Tools.DbMigrator.Legacy.Readers;

namespace Fenrir.Tools.DbMigrator.Legacy.Validation;

internal static class QuestLevelSocketValidation
{
    public static void Run(string dataDir)
    {
        RunLevel(dataDir);
        RunSocket(dataDir);
        RunQuest(dataDir);
    }

    private static void RunLevel(string dataDir)
    {
        var levels = LevelReader.ReadAllRaw(dataDir);
        Console.WriteLine($"[Level] Parsed {levels.Count} records (expected 145).");

        var satisfied = 0;
        for (var n = 0; n < levels.Count - 1; n++)
            if (levels[n].RangeInfo[1] == levels[n + 1].RangeInfo[0] - 1)
                satisfied++;

        Console.WriteLine(
            $"[Level] Continuity invariant (RangeInfo[1] == next.RangeInfo[0] - 1) holds for {satisfied}/{levels.Count - 1} consecutive pairs.");
    }

    private static void RunSocket(string dataDir)
    {
        var sockets = SocketReader.ReadAllRaw(dataDir);
        Console.WriteLine($"[Socket] Parsed {sockets.Count} records (expected 2891).");

        for (var i = 0; i < Math.Min(3, sockets.Count); i++)
        {
            var s = sockets[i];
            Console.WriteLine(
                $"[Socket] record[{i}]: Type={s.Type}, Value01={s.Value01}, Value02={s.Value02}, Value03={s.Value03}, Value04={s.Value04}");
        }
    }

    private static void RunQuest(string dataDir)
    {
        var quests = QuestReader.ReadAllRaw(dataDir);
        Console.WriteLine($"[Quest] Parsed {quests.Count} records (expected 1000).");

        var first = quests[0];
        Console.WriteLine($"[Quest] record[0].Subject = \"{first.Subject}\"");
        Console.WriteLine($"[Quest] record[0].StartSpeech[0] = \"{first.StartSpeech[0]}\"");
        Console.WriteLine($"[Quest] record[0].StartSpeechColor[0] = {first.StartSpeechColor[0]}");
    }
}
