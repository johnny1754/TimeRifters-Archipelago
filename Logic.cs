using System;
using System.Globalization;

namespace TimeRiftersArchipelago
{
    public static class Logic
    {
        public const int ArenaCount = 15;
        public const int MilestonesPerArena = 4;
        public const int LocationCount = ArenaCount * MilestonesPerArena;
        public const int HighCheckMask = 0; // All 60 locations fit in the low mask.
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

        public static bool ValidSession(string key)
        {
            if (key == null || key.Length != 64) return false;
            foreach (char c in key)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        public static bool ParseSnapshot(string[] lines, long now, out string key, out int items,
            out ulong checksLow, out int checksHigh)
        {
            key = null; items = 0; checksLow = 0; checksHigh = 0;
            long timestamp; decimal allChecks;
            if (lines.Length != 5 || lines[0] != "3" || !ValidSession(lines[1])
                || !Int64.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)
                || timestamp > now + 2 || now - timestamp > 10
                || !Int32.TryParse(lines[3], out items) || items < 0 || items > 31
                || !Decimal.TryParse(lines[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out allChecks)
                || allChecks < 0 || allChecks >= ULongRange * 16m) return false;
            checksLow = (ulong)(allChecks % ULongRange);
            checksHigh = (int)(allChecks / ULongRange);
            if (checksHigh > HighCheckMask) return false;
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
    }
}
