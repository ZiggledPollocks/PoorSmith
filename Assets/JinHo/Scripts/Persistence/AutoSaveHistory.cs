// [코드 지도] AutoSaveHistory: 저장 순서별 자동 저장본의 생성, 최신본 조회와 최대 개수 유지를 처리한다.
// 주요 함수: BeginNewGame, Save, List
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Persistence/AutoSaveHistory.cs.md

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>Eight most recent autosave snapshots, ordered by file sequence rather than game day.</summary>
public static class AutoSaveHistory
{
    public const string PrimaryName = "smithing-loop-v1.json";
    const string FolderName = "smithing-loop-history";
    public sealed class Entry
    {
        public string Path;
        public long Sequence;
        public int Day;
        public DateTime SavedAt;
    }

    public static List<Entry> List(string root)
    {
        string folder = System.IO.Path.Combine(root, FolderName);
        var result = new List<Entry>();
        if (!Directory.Exists(folder)) return result;
        foreach (string path in Directory.GetFiles(folder, "auto-*-day-*.json"))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string[] parts = name.Split('-');
            if (parts.Length != 4 || parts[0] != "auto" || parts[2] != "day" ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence) ||
                !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int day) ||
                sequence < 1 || day < 1) continue;
            result.Add(new Entry { Path = path, Sequence = sequence, Day = day,
                SavedAt = File.GetLastWriteTime(path) });
        }
        return result.OrderByDescending(e => e.Sequence).ToList();
    }

    // 핵심 분기: day < 1 판정.
    // 다음 연결: AutoSaveHistory.List(string) 호출.
    public static void Save(string root, string json, int day)
    {
        if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
        List<Entry> before = List(root);
        long sequence = before.Count == 0 ? 1 : checked(before[0].Sequence + 1);
        string folder = System.IO.Path.Combine(root, FolderName);
        Directory.CreateDirectory(folder);
        string snapshot = System.IO.Path.Combine(folder,
            $"auto-{sequence:D16}-day-{day:D6}.json");
        try
        {
            new FileTextStore(snapshot).Write(json);
            new FileTextStore(System.IO.Path.Combine(root, PrimaryName)).Write(json);
        }
        catch
        {
            if (File.Exists(snapshot)) File.Delete(snapshot);
            throw;
        }

        // Prune only after both writes succeed. A failed prune leaves a recoverable extra entry.
        var retained = List(root).Take(8).Select(e => e.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Entry entry in List(root))
            if (!retained.Contains(entry.Path))
                try { File.Delete(entry.Path); } catch (IOException) { }
    }

    public sealed class ResetTransaction : IDisposable
    {
        readonly string root;
        readonly string staging;
        bool complete;
        internal ResetTransaction(string root, string staging) { this.root = root; this.staging = staging; }
        public void Commit()
        {
            complete = true;
            try { Directory.Delete(staging, true); } catch (IOException) { /* inactive backup */ }
        }
        public void Dispose()
        {
            if (complete) return;
            foreach (string source in Directory.GetFileSystemEntries(staging))
            {
                string target = System.IO.Path.Combine(root, System.IO.Path.GetFileName(source));
                if (Directory.Exists(source)) Directory.Move(source, target);
                else File.Move(source, target);
            }
            Directory.Delete(staging);
        }
    }

    // 핵심 분기: Directory.Exists(source) 판정.
    // 다음 연결: AutoSaveHistory.ResetTransaction.Dispose() 호출.
    public static ResetTransaction BeginNewGame(string root)
    {
        Directory.CreateDirectory(root);
        string staging = System.IO.Path.Combine(root, "new-game-pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var transaction = new ResetTransaction(root, staging);
        try
        {
            var names = new List<string> { PrimaryName, PrimaryName + ".bak", FolderName };
            for (int slot = 1; slot <= 4; slot++)
            {
                names.Add($"smithing-loop-slot-{slot}.json");
                names.Add($"smithing-loop-slot-{slot}.json.bak");
            }
            foreach (string name in names)
            {
                string source = System.IO.Path.Combine(root, name);
                string target = System.IO.Path.Combine(staging, name);
                if (Directory.Exists(source)) Directory.Move(source, target);
                else if (File.Exists(source)) File.Move(source, target);
            }
            return transaction;
        }
        catch
        {
            transaction.Dispose();
            throw;
        }
    }
}
