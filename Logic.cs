using System;
using System.Globalization;

namespace TimeRiftersArchipelago
{
    public static class Logic
    {
        public const int ArenaCount = 15;
        public const int MilestonesPerArena = 4;
        public const int ArenaLocationCount = ArenaCount * MilestonesPerArena;
        public const int EpisodeCount = 3;
        public const int CoreLocationCount = ArenaLocationCount + EpisodeCount;
        public const int EscapeLocationStart = CoreLocationCount;
        public const int TitleLocationIndex = EscapeLocationStart + ArenaCount;
        public const int LocationCount = TitleLocationIndex + 1;
        public const int WeaponItemCount = 5;
        public const int HighCheckMask = 32767; // Bits 64 through 78 are used by bonus checks.
        private const decimal ULongRange = 18446744073709551616m; // 2^64

        public static int MilestoneMask(bool normalPlay, bool resetting, int arenaIndex, float fraction)
        {
            if (!normalPlay || resetting || arenaIndex < 0 || arenaIndex >= ArenaCount
                || Single.IsNaN(fraction) || Single.IsInfinity(fraction) || fraction < 0 || fraction > 1) return 0;
            int result = 0;
            if (fraction >= .25f) result |= 1;
            if (fraction >= .50f) result |= 2;
            if (fraction >= .75f) result |= 4;
            if (fraction >= 1f) result |= 8;
            return result;
        }

        public static bool AddCheck(ref ulong low, ref int high, int arenaIndex, int milestoneIndex)
        {
            int index = arenaIndex * MilestonesPerArena + milestoneIndex;
            return AddLocation(ref low, ref high, index);
        }

        public static bool AddEpisodeCheck(ref ulong low, ref int high, int episodeIndex)
        {
            return AddLocation(ref low, ref high, ArenaLocationCount + episodeIndex);
        }

        public static bool AddEscapeCheck(ref ulong low, ref int high, int arenaIndex)
        {
            return AddLocation(ref low, ref high, EscapeLocationStart + arenaIndex);
        }

        public static bool AddTitleCheck(ref ulong low, ref int high)
        {
            return AddLocation(ref low, ref high, TitleLocationIndex);
        }

        private static bool AddLocation(ref ulong low, ref int high, int index)
        {
            if (index < 0 || index >= LocationCount) return false;
            if (index < 64)
            {
                ulong bit = 1UL << index;
                if ((low & bit) != 0) return false;
                low |= bit;
                return true;
            }
            int bitHigh = 1 << (index - 64);
            if ((high & bitHigh) != 0) return false;
            high |= bitHigh;
            return true;
        }

        public static int CountChecks(ulong low, int high)
        {
            int count = 0;
            while (low != 0) { count += (int)(low & 1UL); low >>= 1; }
            while (high != 0) { count += high & 1; high >>= 1; }
            return count;
        }

        public static int CountLocations(ulong low, int high, int start, int length)
        {
            int count = 0;
            for (int index = start; index < start + length; index++)
            {
                if (index < 64) count += (int)((low >> index) & 1UL);
                else count += (high >> (index - 64)) & 1;
            }
            return count;
        }

        public static bool ValidSession(string key)
        {
            if (key == null || key.Length != 64) return false;
            foreach (char c in key)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static bool TryArenaOrder(string text, out int[] order)
        {
            order = null;
            string[] parts = text.Split(',');
            if (parts.Length != ArenaCount) return false;
            int mask = 0;
            int[] parsed = new int[ArenaCount];
            for (int index = 0; index < ArenaCount; index++)
            {
                int arena;
                if (!Int32.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out arena)
                    || arena < 0 || arena >= ArenaCount || (mask & (1 << arena)) != 0) return false;
                parsed[index] = arena;
                mask |= 1 << arena;
            }
            order = parsed;
            return true;
        }

        public static bool ParseSnapshot(string[] lines, long now, out string key, out int items,
            out int echoes, out ulong checksLow, out int checksHigh, out bool escapeChecks, out bool episodeKeys,
            out bool arenaShuffle, out int[] arenaOrder, out int requiredEchoes, out int goal, out bool deathLink,
            out int deathLinkPercent)
        {
            key = null; items = 0; echoes = 0; checksLow = 0; checksHigh = 0; escapeChecks = false; episodeKeys = false;
            arenaShuffle = false; arenaOrder = null; requiredEchoes = 0; goal = 0; deathLink = false; deathLinkPercent = 0;
            long timestamp; decimal allChecks;
            int escapeValue, keysValue, shuffleValue, deathLinkValue;
            if (lines.Length != 14 || lines[0] != "14" || !ValidSession(lines[1])
                || !Int64.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)
                || timestamp > now + 2 || now - timestamp > 10
                || !Int32.TryParse(lines[3], out items) || items < 0 || items > 127
                || !Int32.TryParse(lines[4], out echoes) || echoes < 0 || echoes > LocationCount
                || !Decimal.TryParse(lines[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out allChecks)
                || !Int32.TryParse(lines[6], out escapeValue) || (escapeValue != 0 && escapeValue != 1)
                || !Int32.TryParse(lines[7], out keysValue) || (keysValue != 0 && keysValue != 1)
                || !Int32.TryParse(lines[8], out shuffleValue) || (shuffleValue != 0 && shuffleValue != 1)
                || !TryArenaOrder(lines[9], out arenaOrder)
                || !Int32.TryParse(lines[10], out requiredEchoes) || requiredEchoes < 25 || requiredEchoes > 84
                || !Int32.TryParse(lines[11], out goal) || (goal != 95 && goal != 99 && goal != 100 && goal != 101)
                || !Int32.TryParse(lines[12], out deathLinkValue) || (deathLinkValue != 0 && deathLinkValue != 1)
                || !Int32.TryParse(lines[13], out deathLinkPercent) || deathLinkPercent < 1 || deathLinkPercent > 100
                || allChecks < 0 || allChecks >= ULongRange * 32768m) return false;
            checksLow = (ulong)(allChecks % ULongRange);
            checksHigh = (int)(allChecks / ULongRange);
            if (checksHigh > HighCheckMask) return false;
            escapeChecks = escapeValue == 1;
            episodeKeys = keysValue == 1;
            arenaShuffle = shuffleValue == 1;
            deathLink = deathLinkValue == 1;
            key = lines[1]; return true;
        }

        public static bool WeaponAllowed(string weapon, int items)
        {
            if (weapon == "Flak") return (items & 1) != 0;
            if (weapon == "Lightning") return (items & 2) != 0;
            if (weapon == "AlienDisk") return (items & 4) != 0;
            if (weapon == "RocketLauncher") return (items & 8) != 0;
            if (weapon == "Rifle") return (items & 16) != 0;
            return true; // Pistol and unknown future weapons remain safe defaults.
        }

        public static bool EpisodeAllowed(int episode, int items, bool keysRequired)
        {
            if (!keysRequired) return true;
            if (episode == 0) return true;
            if (episode == 1) return (items & 32) != 0;
            if (episode == 2) return (items & 64) != 0;
            return false;
        }
    }
}
