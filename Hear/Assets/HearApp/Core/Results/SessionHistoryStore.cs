using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HearApp.Core.Results
{
    /// <summary>Local session history for the Results screen. PlayerPrefs+JSON, matching this
    /// project's existing settings persistence (GameFlowController) rather than adding a new
    /// storage mechanism - there is no backend/account system to sync against yet.</summary>
    public static class SessionHistoryStore
    {
        private const string PrefsKey = "Results.SessionHistory.v1";

        // Generous cap so PlayerPrefs (a flat string blob) never grows unbounded; oldest entries
        // drop first. Well beyond the ~10-session baseline window the Results screen cares about.
        private const int MaxStoredEntries = 200;

        [Serializable]
        private sealed class Container
        {
            public List<SessionHistoryEntry> Entries = new();
        }

        public static IReadOnlyList<SessionHistoryEntry> LoadAll()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return Array.Empty<SessionHistoryEntry>();
            try
            {
                var container = JsonUtility.FromJson<Container>(json);
                return (IReadOnlyList<SessionHistoryEntry>)container?.Entries ?? Array.Empty<SessionHistoryEntry>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionHistoryStore] Could not parse stored history, ignoring: {e}");
                return Array.Empty<SessionHistoryEntry>();
            }
        }

        public static void Append(SessionHistoryEntry entry)
        {
            var entries = new List<SessionHistoryEntry>(LoadAll()) { entry };
            if (entries.Count > MaxStoredEntries)
                entries.RemoveRange(0, entries.Count - MaxStoredEntries);
            Save(entries);
        }

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private static void Save(List<SessionHistoryEntry> entries)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new Container { Entries = entries }));
            PlayerPrefs.Save();
        }

        // ---- Derived queries used by the Results screen ----

        /// <summary>Most recent `count` entries, newest first.</summary>
        public static IReadOnlyList<SessionHistoryEntry> Recent(int count)
        {
            var all = LoadAll();
            return all.Skip(Math.Max(0, all.Count - count)).Reverse().ToList();
        }

        public static int ReliableCount(IReadOnlyList<SessionHistoryEntry> entries) => entries.Count(e => e.Reliable);

        /// <summary>Average hearing-age estimate across the most recent reliable sessions (falls
        /// back to all sessions if none are reliable yet) - this is "the overall hearing age".</summary>
        public static float OverallHearingAge(IReadOnlyList<SessionHistoryEntry> entries, int window = 10)
        {
            var pool = entries.Where(e => e.Reliable).ToList();
            if (pool.Count == 0) pool = entries.ToList();
            if (pool.Count == 0) return 0f;
            var recentPool = pool.Skip(Math.Max(0, pool.Count - window)).ToList();
            return recentPool.Average(e => e.HearingAgeEstimate);
        }
    }
}
