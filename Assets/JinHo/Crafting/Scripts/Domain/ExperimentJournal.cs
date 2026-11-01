using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Blacksmith
{
    [Serializable] public sealed class ExperimentRecord
    {
        public string attemptId, outcome, signature;
        public long startedUtcTicks, lastUtcTicks;
        public int repetitions = 1, fuelSpent;
        public Station station;
        public ToolKind tool;
        public List<Stack> inputs = new(), outputs = new(), returned = new();
        public List<string> actions = new();
    }

    // Observes committed attempts only. Never queries recipe requirements or changes inventory/mastery.
    public static class ExperimentJournal
    {
        public static void Ensure(SaveData data)
        {
            data.experiments ??= new List<ExperimentRecord>();
            data.experiments.RemoveAll(x => x == null);
            if (data.experiments.Count > 200) data.experiments.RemoveRange(200, data.experiments.Count - 200);
        }
        public static void Safe(Action action)
        {
            try { action(); } catch (Exception e) { Debug.LogWarning("실험 기록을 갱신하지 못했습니다. 제작 처리는 유지됩니다. " + e.GetType().Name); }
        }
        public static void Begin(SaveData data, Station station, ToolKind tool, IEnumerable<Stack> inputs, int fuel)
        {
            Recover(data);
            data.pendingExperiment = new ExperimentRecord { attemptId = Guid.NewGuid().ToString("N"),
                startedUtcTicks = DateTime.UtcNow.Ticks, lastUtcTicks = DateTime.UtcNow.Ticks,
                station = station, tool = tool, fuelSpent = fuel, outcome = "진행 중",
                inputs = inputs.Select(x => x.Copy(x.count)).ToList() };
        }
        public static void Action(SaveData data, string operation)
        {
            if (data.pendingExperiment != null) data.pendingExperiment.actions.Add(operation);
        }
        static string Stacks(IEnumerable<Stack> stacks) => string.Join(";", stacks.GroupBy(x => x.Key).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key + "=" + x.Sum(s => (long)s.count).ToString(CultureInfo.InvariantCulture)));
        public static void Complete(SaveData data, string outcome, IEnumerable<Stack> outputs, IEnumerable<Stack> returned)
        {
            var entry = data.pendingExperiment;
            if (entry == null || string.IsNullOrEmpty(entry.attemptId)) return;
            entry.outcome = outcome; entry.lastUtcTicks = DateTime.UtcNow.Ticks;
            entry.outputs = outputs.Where(x => x != null && x.count > 0).Select(x => x.Copy(x.count)).ToList();
            entry.returned = returned.Select(x => x.Copy(x.count)).ToList();
            entry.signature = (int)entry.station + "|" + (entry.station == Station.Tools ? (int)entry.tool : -1) + "|" + Stacks(entry.inputs) + "|" +
                string.Join(";", entry.actions) + "|" + outcome + "|" + Stacks(entry.outputs) + "|" + Stacks(entry.returned) + "|" + entry.fuelSpent;
            Ensure(data);
            var same = data.experiments.FirstOrDefault(x => x.signature == entry.signature);
            if (same != null)
            {
                same.repetitions = same.repetitions < int.MaxValue ? same.repetitions + 1 : int.MaxValue;
                same.lastUtcTicks = entry.lastUtcTicks; data.experiments.Remove(same); entry = same;
            }
            data.experiments.Insert(0, entry);
            if (data.experiments.Count > 200) data.experiments.RemoveRange(200, data.experiments.Count - 200);
            data.pendingExperiment = null;
        }
        // Existing durable snapshots return uncommitted ingredients; an unknown outcome is not a failed recipe.
        public static void Recover(SaveData data)
        {
            Safe(() => { Ensure(data);
                // JsonUtility may synthesize an empty reference object for a field absent in legacy JSON.
                if (!string.IsNullOrEmpty(data.pendingExperiment?.attemptId))
                    Complete(data, "미완료 · 저장된 결과 없음", Array.Empty<Stack>(), Array.Empty<Stack>());
                else data.pendingExperiment = null;
            });
        }
    }
}
