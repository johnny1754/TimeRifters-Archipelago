using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TimeRiftersArchipelago
{
    [BepInPlugin("johnny.timerifters.ap.prototype", "Time Rifters AP Prototype", "0.4.1")]
    [BepInProcess("TimeRifters.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance;
        private Type gameplay, replay;
        private Harmony harmony;
        private string folder, session, pendingSession;
        private int items, pendingItems, checksHigh, pendingChecksHigh;
        private ulong checks, pendingChecks;
        private bool isActive, fresh, ready, dirty;
        private float nextPoll;
        private string message = "WAITING FOR AP CLIENT: connect as Johnny, leave the client open, then press F8 or HOME.";
        private GUIStyle overlayStyle;

        private void Start()
        {
            Instance = this;
            try
            {
                Assembly game = null;
                foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
                    if (candidate.GetName().Name == "Assembly-CSharp") game = candidate;
                if (game == null) throw new InvalidOperationException("Game assembly missing");
                gameplay = game.GetType("ShortGameplay", true);
                replay = game.GetType("ReplayGameplay", true);
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TimeRiftersArchipelago");
                Directory.CreateDirectory(folder);
                // Validate all observed members before installing patches.
                foreach (string name in new string[] { "currentEpisode", "currentEpisodeArena",
                    "episodeArenas", "resetPressed", "arenaProgressList" }) Field(gameplay, name);
                Field(replay, "gameState");
                Type button = game.GetType("ShopButtonWeapon", true);
                Field(button, "weapon");
                MethodInfo click = button.GetMethod("OnClick", Flags);
                MethodInfo complete = gameplay.GetMethod("OnEventAreaComplete", Flags);
                MethodInfo progress = gameplay.GetMethod("GetCurrentArenaProgress", Flags);
                if (click == null || complete == null || progress == null) throw new MissingMethodException("Required game hooks missing");
                harmony = new Harmony("johnny.timerifters.ap.prototype");
                harmony.Patch(click, new HarmonyMethod(typeof(Plugin), "BeforeWeaponClick"));
                harmony.Patch(complete, new HarmonyMethod(typeof(Plugin), "BeforeComplete"),
                    new HarmonyMethod(typeof(Plugin), "AfterComplete"));
                ready = true;
                Logger.LogInfo("[TRAP] READY 0.4.1. Press F8 or HOME at TitleScreen after connecting the AP client.");
                Logger.LogInfo("[TRAP] Progress folder: " + folder);
            }
            catch (Exception ex)
            {
                if (harmony != null) harmony.UnpatchSelf();
                message = "Prototype setup failed. See LogOutput.log.";
                Logger.LogError("[TRAP] " + ex);
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo result = type.GetField(name, Flags);
            if (result == null) throw new MissingFieldException(type.FullName, name);
            return result;
        }
        private object Read(string name) { return Field(gameplay, name).GetValue(null); }
        private bool NormalPlay() { return Field(replay, "gameState").GetValue(null).ToString() == "Play"; }
        private static long Now() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds; }

        private static string[] ReadLines(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd().TrimEnd('\r', '\n').Replace("\r", "").Split('\n');
        }

        private void Update()
        {
            if (!ready) return;
            if (Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 0.5f;
                fresh = false;
                try
                {
                    string path = Path.Combine(folder, "server.txt");
                    if (File.Exists(path))
                        fresh = Logic.ParseSnapshot(ReadLines(path), Now(), out pendingSession,
                            out pendingItems, out pendingChecks, out pendingChecksHigh);
                    if (isActive && fresh && session == pendingSession)
                    {
                        if ((items | pendingItems) != items)
                        {
                            items |= pendingItems;
                            message = "Weapon received! Flak=" + ((items & 1) != 0)
                + ", Lightning=" + ((items & 2) != 0)
                + ", AlienDisk=" + ((items & 4) != 0)
                + ", RocketLauncher=" + ((items & 8) != 0)
                + ", Rifle=" + ((items & 16) != 0);
                            Logger.LogInfo("[TRAP] " + message);
                        }
                        ulong combined = checks | pendingChecks;
                        int combinedHigh = checksHigh | pendingChecksHigh;
                        if (combined != checks || combinedHigh != checksHigh)
                        { checks = combined; checksHigh = combinedHigh; dirty = true; }
                    }
                    if (dirty) SaveProgress();
                }
                catch (Exception ex)
                {
                    fresh = false;
                    message = "Bridge/file error (will retry): " + ex.Message;
                }
            }
            if (isActive && CorrectSession() && NormalPlay() && !(bool)Read("resetPressed"))
            {
                try { RecordMilestones(CurrentArenaIndex((int)Read("currentEpisode"), (int)Read("currentEpisodeArena")),
                    CurrentArenaProgress()); }
                catch (Exception ex) { Logger.LogError("[TRAP] Progress poll failed: " + ex); }
            }
            if (Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.Home))
            {
                if (Application.loadedLevelName != "TitleScreen")
                {
                    message = "Return to the title screen before enabling/disabling AP.";
                    return;
                }
                if (isActive) { isActive = false; message = "AP disabled. Normal gameplay enabled."; return; }
                if (!fresh) { message = "WAITING FOR AP CLIENT: connect as Johnny, leave the client open, wait 5 seconds, then press F8 or HOME again."; return; }
                try
                {
                    session = pendingSession;
                    items = pendingItems;
                    checks = pendingChecks;
                    checksHigh = pendingChecksHigh;
                    string path = Path.Combine(folder, session + ".progress");
                    if (File.Exists(path))
                    {
                        string[] lines = ReadLines(path);
                        ulong savedLow; int savedHigh;
                        if (lines.Length != 4 || lines[0] != "3" || lines[1] != session
                            || !UInt64.TryParse(lines[2], out savedLow) || !Int32.TryParse(lines[3], out savedHigh)
                            || savedHigh < 0 || savedHigh > Logic.HighCheckMask)
                            throw new InvalidDataException("Progress file invalid; preserved for diagnosis.");
                        checks |= savedLow;
                        checksHigh |= savedHigh;
                    }
                    dirty = true;
                    SaveProgress();
                    isActive = true;
                    message = "AP enabled. Each arena sends checks at 25%, 50%, 75%, and 100%.";
                    Logger.LogInfo("[TRAP] Session activated " + session + "; checks=" + checks + "; items=" + items);
                }
                catch (Exception ex) { isActive = false; message = ex.Message; Logger.LogError("[TRAP] " + ex); }
            }
        }

        private void SaveProgress()
        {
            string path = Path.Combine(folder, session + ".progress");
            string temp = path + ".tmp";
            File.WriteAllText(temp, "3\n" + session + "\n" + checks.ToString(CultureInfo.InvariantCulture)
                + "\n" + checksHigh.ToString(CultureInfo.InvariantCulture) + "\n");
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            dirty = false;
        }

        private bool CorrectSession() { return !fresh || session == pendingSession; }

        private int CurrentArenaIndex(int episode, int arena)
        {
            IList episodes = (IList)Read("episodeArenas");
            if (episode < 0 || episode >= episodes.Count) return -1;
            IList arenas = episodes[episode] as IList;
            if (arenas == null || arena < 0 || arena >= arenas.Count) return -1;
            return Convert.ToInt32(arenas[arena], CultureInfo.InvariantCulture);
        }

        private float CurrentArenaProgress()
        {
            object instance = Field(gameplay, "instance").GetValue(null);
            object value = gameplay.GetMethod("GetCurrentArenaProgress", Flags).Invoke(instance, null);
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private void RecordMilestones(int arena, float fraction)
        {
            int milestones = Logic.MilestoneMask(true, false, arena, fraction);
            if (milestones == 0) return;
            int added = 0;
            for (int milestone = 0; milestone < Logic.MilestonesPerArena; milestone++)
                if ((milestones & (1 << milestone)) != 0 && Logic.AddCheck(ref checks, ref checksHigh, arena, milestone)) added++;
            if (added == 0) return;
            dirty = true;
            message = "Arena milestone earned: " + (int)(fraction * 100f) + "% | Total: "
                + Logic.CountChecks(checks, checksHigh) + "/" + Logic.LocationCount;
            Logger.LogInfo("[TRAP] Arena " + arena + " progress " + fraction.ToString(CultureInfo.InvariantCulture)
                + " earned " + added + " milestone(s).");
            SaveProgress();
        }

        public static bool BeforeWeaponClick(object __instance)
        {
            Plugin p = Instance;
            if (p == null || !p.isActive) return true;
            try
            {
                string weapon = Field(__instance.GetType(), "weapon").GetValue(__instance).ToString();
                if (Logic.WeaponAllowed(weapon, p.items)) return true;
                p.message = weapon + " is locked. Receive its Archipelago item first.";
                p.Logger.LogInfo("[TRAP] Blocked shop selection: " + weapon);
                return false;
            }
            catch (Exception ex)
            {
                p.message = "Weapon hook error; selection blocked. See log.";
                p.Logger.LogError("[TRAP] " + ex);
                return false;
            }
        }

        public sealed class CompletionCapture
        {
            public bool Valid;
            public int Episode, Arena, GlobalArena, Count;
        }

        public static void BeforeComplete(out CompletionCapture __state)
        {
            __state = new CompletionCapture();
            Plugin p = Instance;
            if (p == null || !p.isActive || !p.CorrectSession()) return;
            try
            {
                __state.Episode = (int)p.Read("currentEpisode");
                __state.Arena = (int)p.Read("currentEpisodeArena");
                __state.GlobalArena = p.CurrentArenaIndex(__state.Episode, __state.Arena);
                __state.Count = ((IList)p.Read("arenaProgressList")).Count;
                __state.Valid = p.NormalPlay() && !(bool)p.Read("resetPressed")
                    && __state.GlobalArena >= 0 && __state.GlobalArena < Logic.ArenaCount;
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Completion capture failed: " + ex); }
        }

        public static void AfterComplete(CompletionCapture __state)
        {
            Plugin p = Instance;
            if (p == null || __state == null || !__state.Valid) return;
            try
            {
                IList records = (IList)p.Read("arenaProgressList");
                if (records.Count != __state.Count + 1) return;
                object result = records[records.Count - 1];
                float fraction = (float)Field(result.GetType(), "percentComplete").GetValue(result);
                p.RecordMilestones(__state.GlobalArena, fraction);
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Check handling failed (pending writes retry): " + ex); }
        }

        private void OnGUI()
        {
            string status = !isActive ? "INACTIVE - F8 or HOME at title screen" :
                !CorrectSession() ? "SESSION CHANGED - restart game before continuing" :
                fresh ? "CONNECTED" : "OFFLINE - checks queued; received unlocks retained";
            string progress = "Arena checks: " + Logic.CountChecks(checks, checksHigh) + "/" + Logic.LocationCount
                + " | Weapons: " + Logic.CountChecks((ulong)items, 0) + "/5";
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.box);
                overlayStyle.fontSize = 24;
                overlayStyle.wordWrap = true;
                overlayStyle.alignment = TextAnchor.UpperLeft;
                overlayStyle.padding = new RectOffset(16, 16, 12, 12);
            }
            GUI.Box(new Rect(20, 20, Math.Min(980, Screen.width - 40), 205),
                "TIME RIFTERS AP PROTOTYPE 0.4.1  |  " + status + "\n"
                + "Flak " + ((items & 1) != 0 ? "OK" : "locked")
                + " | Lightning " + ((items & 2) != 0 ? "OK" : "locked")
                + " | Alien " + ((items & 4) != 0 ? "OK" : "locked") + "\n"
                + "Rocket " + ((items & 8) != 0 ? "OK" : "locked")
                + " | Rifle " + ((items & 16) != 0 ? "OK" : "locked") + "\n"
                + progress + "\n" + message, overlayStyle);
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}
