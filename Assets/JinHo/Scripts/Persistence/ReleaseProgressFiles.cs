using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Validated progress migration/import. Original files are never silently reset.</summary>
public static class ReleaseProgressFiles
{
    public const int CurrentVersion = 2;
    public sealed class Candidate
    {
        public string path, json, fingerprint, error;
        public SmithingLoop.Progress progress;
        public long sequence;
        public DateTime savedUtc;
        public bool Valid => progress != null && error == null;
        public string Summary => Valid ? $"{progress.smith.day}일 · {progress.campaign?.playSeconds / 60:0}분 · {savedUtc.ToLocalTime():MM/dd HH:mm} · {(progress.campaign?.inTown == false ? "채집 필드" : "마을")}" : error;
    }
    public static bool Decode(string json, out SmithingLoop.Progress progress, out string error)
    {
        progress = null; error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\""))
                throw new InvalidDataException("저장 형식을 확인할 수 없습니다.");
            var value = JsonUtility.FromJson<SmithingLoop.Progress>(json);
            if (value == null || value.version < 1 || value.version > CurrentVersion)
                throw new InvalidDataException(value?.version > CurrentVersion ? "현재 게임보다 새로운 버전의 저장본입니다. 게임을 업데이트해 주세요." : "지원하지 않는 저장 형식입니다.");
            if (!SmithingLoop.IsValidSave(json)) throw new InvalidDataException("저장 내용이 손상되었거나 필요한 아이템 데이터가 없습니다.");
            // Version 1 is the actual shipped unified schema. Version 2 adds metadata only.
            value.version = CurrentVersion;
            progress = value;
            return true;
        }
        catch (Exception e) { error = e is InvalidDataException ? e.Message : "저장본을 읽을 수 없습니다. 원본은 보존했습니다."; return false; }
    }
    public static Candidate Read(string path, long sequence = 0)
    {
        var c = new Candidate { path = path, sequence = sequence };
        try
        {
            c.json = File.ReadAllText(path, Encoding.UTF8);
            c.fingerprint = Fingerprint(c.json);
            Decode(c.json, out c.progress, out c.error);
            if (c.Valid)
            {
                c.sequence = Math.Max(sequence, c.progress.saveSequence);
                if (!DateTime.TryParse(c.progress.savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out c.savedUtc))
                    c.savedUtc = File.GetLastWriteTimeUtc(path); // Display only for legacy files.
            }
        }
        catch { c.error = "저장 파일을 읽을 수 없습니다. 원본은 보존했습니다."; }
        return c;
    }
    public static string Fingerprint(string json)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
    }
    public static List<Candidate> Current(string root)
    {
        var all = AutoSaveHistory.List(root).Select(e => Read(e.Path, e.Sequence)).ToList();
        string primary = Path.Combine(root, AutoSaveHistory.PrimaryName);
        if (File.Exists(primary))
        {
            var c = Read(primary);
            if (c.Valid && c.sequence == 0 && all.Any(x => x.Valid && x.fingerprint == c.fingerprint))
                c.sequence = all.First(x => x.Valid && x.fingerprint == c.fingerprint).sequence;
            all.Add(c);
        }
        // Sequence wins over day: loading a past day and saving creates the newest entry.
        return all.OrderByDescending(c => c.sequence).ThenByDescending(c => c.progress?.campaign?.playSeconds ?? 0)
            .ThenByDescending(c => c.progress?.savedUtc ?? "", StringComparer.Ordinal)
            .GroupBy(c => c.fingerprint ?? c.path).Select(g => g.First()).Take(8).ToList();
    }
    public static List<Candidate> Legacy(string persistentRoot)
    {
        var roots = new List<string>();
        string builds = Path.Combine(persistentRoot, "Builds");
        if (Directory.Exists(builds)) roots.AddRange(Directory.GetDirectories(builds));
        roots.Add(persistentRoot); // Before build-GUID isolation was introduced.
        var candidates = new List<Candidate>();
        foreach (string root in roots)
            try
            {
                candidates.AddRange(Current(root));
                for(int slot=1;slot<=4;slot++)
                {
                    string path=Path.Combine(root,$"smithing-loop-slot-{slot}.json");
                    if(File.Exists(path))candidates.Add(Read(path));
                }
            } catch (IOException) { }
        // Separate build timelines cannot be ordered by their local sequence alone.
        return candidates.GroupBy(c => c.fingerprint??c.path).Select(g => g.First())
            .OrderByDescending(c => c.Valid).ThenByDescending(c => c.progress?.smith.day??0).ThenByDescending(c => c.progress?.campaign?.playSeconds ?? 0)
            .ThenByDescending(c => c.sequence).ToList();
    }
    public static SmithingLoop.Progress ReadMigrated(string path)
    {
        Candidate c = Read(path);
        if (!c.Valid) throw new InvalidDataException(c.error);
        PreserveOriginal(c, GameSavePaths.Root);
        return c.progress;
    }
    static void PreserveOriginal(Candidate c, string root)
    {
        if (JsonUtility.FromJson<SmithingLoop.Progress>(c.json).version >= CurrentVersion) return;
        string preserved = Path.Combine(root, "Migrations", "v1-" + c.fingerprint + ".json");
        if (!File.Exists(preserved)) new FileTextStore(preserved).Write(c.json);
    }
    // Validate and preserve BEFORE replacement; keep primary plus backup unchanged on failure.
    public static void Install(Candidate c, string root, bool preventRepeat=false)
    {
        Candidate fresh = Read(c.path, c.sequence);
        if (!fresh.Valid) throw new InvalidDataException(fresh.error);
        PreserveOriginal(fresh, root);
        string target = Path.Combine(root, AutoSaveHistory.PrimaryName);
        if(File.Exists(target))
        {
            var installed=Read(target);
            if(preventRepeat&&installed.Valid && installed.progress.importSourceHash==fresh.fingerprint)return;
        }
        if(preventRepeat)fresh.progress.importSourceHash=fresh.fingerprint;
        string json = JsonUtility.ToJson(fresh.progress, true);
        if (File.Exists(target) && Fingerprint(File.ReadAllText(target)) == Fingerprint(json)) return;
        if (File.Exists(target))
        {
            string old = File.ReadAllText(target);
            string archive = Path.Combine(root, "Recovery", Fingerprint(old) + ".json");
            if (!File.Exists(archive)) new FileTextStore(archive).Write(old);
        }
        AutoSaveHistory.Save(root, json, fresh.progress.smith.day, n =>
        {
            Stamp(fresh.progress, n);
            return JsonUtility.ToJson(fresh.progress, true);
        });
    }
    public static void Stamp(SmithingLoop.Progress p, long sequence)
    {
        p.version = CurrentVersion; p.saveSequence = sequence;
        p.savedUtc = DateTime.UtcNow.ToString("O");
        p.gameVersion = Application.version; p.buildId = Application.buildGUID;
    }
    public static string Destination(SmithingLoop.Progress p)
    {
        if (p?.campaign?.story?.enabled == true && !p.campaign.story.tutorialCompleted)
            return FieldSceneTravel.TownScenePath;
        return p?.campaign?.sceneName == Path.GetFileNameWithoutExtension(FieldSceneTravel.FieldScenePath)
            ? FieldSceneTravel.FieldScenePath : FieldSceneTravel.TownScenePath;
    }
}
