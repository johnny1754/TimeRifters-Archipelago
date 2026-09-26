using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeRiftersArchipelago
{
    public static class Logic
    {
        public const int ArenaCount = 15;
        public const int EpisodeCount = 3;

        public static int MilestonePercent(int count, int index) { return count < 4 || count > 20 || index < 0 || index >= count ? 0 : (100 * (index + 1) + count - 1) / count; }
        public static int ArenaLocationCount(int count) { return ArenaCount * count; }
        public static int CoreLocationCount(int count) { return ArenaLocationCount(count) + EpisodeCount; }
        public static int EscapeLocationStart(int count) { return CoreLocationCount(count); }
        public static int TitleLocationIndex(int count) { return EscapeLocationStart(count) + ArenaCount; }
        public static int LocationCount(int count, bool escapes) { return CoreLocationCount(count) + (escapes ? ArenaCount + 1 : 0); }
        public static bool MilestoneReached(bool normal, bool resetting, int arena, float fraction, int count, int milestone) { return normal && !resetting && arena >= 0 && arena < ArenaCount && count >= 4 && count <= 20 && milestone >= 0 && milestone < count && !Single.IsNaN(fraction) && !Single.IsInfinity(fraction) && fraction >= MilestonePercent(count, milestone) / 100f; }
        public static int ArenaCheckIndex(int arena, int milestone, int count) { return arena * count + milestone; }
        public static int EpisodeCheckIndex(int episode, int count) { return ArenaLocationCount(count) + episode; }
        public static int EscapeCheckIndex(int arena, int count) { return EscapeLocationStart(count) + arena; }
        public static int TitleCheckIndex(int count) { return TitleLocationIndex(count); }
        public static bool AddLocation(HashSet<int> checks, int index, int total) { return checks != null && index >= 0 && index < total && checks.Add(index); }
        public static int CountLocations(HashSet<int> checks, int start, int length) { int count = 0; if (checks == null) return 0; for (int i = start; i < start + length; i++) if (checks.Contains(i)) count++; return count; }

        public static bool ValidSession(string key) { if (key == null || key.Length != 64) return false; foreach (char c in key) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false; return true; }
        private static bool TryArenaOrder(string text, out int[] order) { order = null; string[] parts = text.Split(','); if (parts.Length != ArenaCount) return false; int mask = 0; int[] values = new int[ArenaCount]; for (int i = 0; i < ArenaCount; i++) { int value; if (!Int32.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value >= ArenaCount || (mask & (1 << value)) != 0) return false; values[i] = value; mask |= 1 << value; } order = values; return true; }
        private static bool TryChecks(string text, int total, out HashSet<int> checks) { checks = new HashSet<int>(); if (text.Length == 0) return true; foreach (string part in text.Split(',')) { int value; if (!Int32.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value >= total || !checks.Add(value)) return false; } return true; }

        public static bool ParseSnapshot(string[] lines, long now, out string key, out int items, out int echoes, out HashSet<int> checks, out bool escapes, out bool keys, out bool shuffle, out int[] order, out int requiredEchoes, out int goal, out bool deathLink, out int deathPercent, out int deathDuration, out int milestones)
        {
            key = null; items = echoes = requiredEchoes = goal = deathPercent = deathDuration = milestones = 0; checks = null; escapes = keys = shuffle = deathLink = false; order = null;
            long stamp; int e, k, s, d, total;
            if (lines.Length != 17 || lines[0] != "17" || !ValidSession(lines[1]) || !Int64.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out stamp) || stamp > now + 2 || now - stamp > 10 || !Int32.TryParse(lines[3], out items) || items < 0 || items > 127 || !Int32.TryParse(lines[4], out echoes) || echoes < 0 || !Int32.TryParse(lines[6], out e) || (e != 0 && e != 1) || !Int32.TryParse(lines[7], out k) || (k != 0 && k != 1) || !Int32.TryParse(lines[8], out s) || (s != 0 && s != 1) || !TryArenaOrder(lines[9], out order) || !Int32.TryParse(lines[10], out requiredEchoes) || requiredEchoes < 25 || requiredEchoes > 84 || !Int32.TryParse(lines[11], out goal) || (goal != 95 && goal != 99 && goal != 100 && goal != 101) || !Int32.TryParse(lines[12], out d) || (d != 0 && d != 1) || !Int32.TryParse(lines[13], out deathPercent) || deathPercent < 1 || deathPercent > 100 || !Int32.TryParse(lines[14], out deathDuration) || deathDuration < 30 || deathDuration > 120 || !Int32.TryParse(lines[15], out milestones) || milestones < 4 || milestones > 20 || !Int32.TryParse(lines[16], out total)) return false;
            escapes = e == 1; keys = k == 1; shuffle = s == 1; deathLink = d == 1;
            if (total != LocationCount(milestones, escapes) || echoes > total || !TryChecks(lines[5], total, out checks)) return false;
            key = lines[1]; return true;
        }
        public static bool WeaponAllowed(string weapon, int items) { if (weapon == "Flak") return (items & 1) != 0; if (weapon == "Lightning") return (items & 2) != 0; if (weapon == "AlienDisk") return (items & 4) != 0; if (weapon == "RocketLauncher") return (items & 8) != 0; if (weapon == "Rifle") return (items & 16) != 0; return true; }
        public static bool EpisodeAllowed(int episode, int items, bool keysRequired) { if (!keysRequired || episode == 0) return true; if (episode == 1) return (items & 32) != 0; if (episode == 2) return (items & 64) != 0; return false; }
    }
}
