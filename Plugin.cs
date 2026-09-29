using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TimeRiftersArchipelago
{
    [BepInPlugin("timerifters.archipelago", "Time Rifters Archipelago", "1.0.13")]
    [BepInProcess("TimeRifters.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance;
        // ShortGameplay stores its fifteen playable arenas by internal enum ID,
        // not in the episode/play order shown by the game.  This maps the
        // Archipelago arena indices (the order in ARENAS) to those IDs.
        private static readonly int[] DefaultArenaIds =
            { 0, 5, 6, 3, 11, 7, 2, 4, 13, 12, 14, 9, 8, 10, 1 };
        private static readonly string[] ArenaNames =
            { "Greeble Box", "Arena Boss", "Long Bridge", "Holodeck", "Tram", "Cave", "Channel",
              "Long Caves", "Egypt Holodeck", "Wide Printer", "Donut Printer", "Water", "Tree",
              "Jungle Holodeck", "Cylinder" };
        // Shop weapon order and upgrade enum values observed in Time Rifters.
        // Keep this in the same order as WEAPON_POWER_UPS in the APWorld.
        private static readonly int[][] WeaponPowerUps =
            { new int[] { 15, 8, 9, 11, 4 }, new int[] { 13, 14, 9, 11, 4 },
              new int[] { 12, 16, 10, 4 }, new int[] { 0, 1, 2, 3, 4 },
              new int[] { 5, 6, 9, 7, 4 }, new int[] { 17, 14, 9, 10, 4 } };
        private static readonly string[] ShopWeaponNames =
            { "Scatter Pistol", "Flak Cannon", "Plasma Beam", "Particle Ball", "Rocket Launcher", "Spread Rifle" };
        private static readonly string[] UpgradeNames =
            { "Proton", "Electron", "Tether", "Collider", "Acid", "Rocket", "Speed", "To The Moon",
              "Plasma", "Rapid", "Punch", "Reflect", "Wave", "Flak", "Focus", "Scatter", "Beam", "Spread" };
        private static readonly string[] UpgradeShortNames =
            { "Pr", "El", "Te", "Co", "A", "Ro", "Sp", "TM", "Pl", "R", "Pu", "Re", "Wa", "Fl", "Fo", "Sc", "Be", "Sr" };
        private static readonly string[] GlobalPowerUpNames =
            { "Projectile", "Damage", "Rapid", "Punch", "Special", "Acid" };
        // Index into GlobalPowerUpNames for each UpgradeData.Name enum value.
        private static readonly int[] GlobalCategoryForUpgrade =
            { 0, 1, 4, 2, 5, 1, 3, 4, 1, 2, 3, 4, 0, 0, 1, 0, 1, 0 };
        private Type gameplay, replay;
        private Type episodeChooserType, arenaChooserType, arenaCardType, uiTextureType, uiSpriteType;
        private Harmony harmony;
        private string folder, session, pendingSession;
        private int items, pendingItems, echoes, pendingEchoes, creditsSpent,
            requiredEchoes, pendingRequiredEchoes, goal, pendingGoal;
        private long powerUps, pendingPowerUps;
        private int deathLinkPercent, pendingDeathLinkPercent, deathLinkDuration, pendingDeathLinkDuration;
        private HashSet<int> checks = new HashSet<int>(), pendingChecks = new HashSet<int>();
        private int milestoneCount, pendingMilestoneCount, upgradeMode, pendingUpgradeMode;
        private bool isActive, fresh, ready, dirty, escapeChecks, pendingEscapeChecks, episodeKeys, pendingEpisodeKeys,
            arenaShuffle, pendingArenaShuffle, arenaOrderApplied, arenaShuffleWaiting, overlayVisible = true;
        private bool deathLink, pendingDeathLink;
        private int[] arenaOrder, pendingArenaOrder, originalArenaOrder;
        private int[] arenaBest = new int[Logic.ArenaCount];
        private bool goalReached;
        private readonly Dictionary<int, CardVisual[]> originalCardVisuals = new Dictionary<int, CardVisual[]>();
        private bool cardVisualsCaptured;
        private float nextPoll;
        private float firingDisabledUntil;
        private long lastNotificationId;
        private string lastLoadedLevel;
        private int lastPolledArena = -1;
        private bool waitingForNewArenaProgress;
        private string message = "WAITING FOR AP CLIENT: connect as your YAML player name, leave the client open, then press F8 or HOME.";
        private GUIStyle overlayStyle;

        private sealed class CardVisual
        {
            internal string typeName;
            internal object value;
        }

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
                episodeChooserType = game.GetType("LSEpisodeChooser", true);
                arenaChooserType = game.GetType("LSArenaChooser", true);
                arenaCardType = game.GetType("LSArena", true);
                uiTextureType = game.GetType("UITexture", false);
                uiSpriteType = game.GetType("UISprite", false);
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TimeRiftersArchipelago");
                Directory.CreateDirectory(folder);
                // Validate all observed members before installing patches.
                foreach (string name in new string[] { "currentEpisode", "currentEpisodeArena",
                    "episodeArenas", "resetPressed", "arenaProgressList", "episodeComplete" }) Field(gameplay, name);
                Field(replay, "gameState");
                Type button = game.GetType("ShopButtonWeapon", true);
                Field(button, "weapon");
                MethodInfo click = button.GetMethod("OnClick", Flags);
                Type upgradeButton = game.GetType("ShopButtonUpgrade", true);
                Field(upgradeButton, "upgradeData");
                MethodInfo upgradeClick = upgradeButton.GetMethod("OnClick", Flags);
                Type shop = game.GetType("Shop", true);
                MethodInfo shopReset = shop.GetMethod("Reset", Flags);
                Type episodeButton = game.GetType("LSPlay", true);
                Field(episodeButton, "episodeIndex");
                MethodInfo episodeClick = episodeButton.GetMethod("OnClick", Flags);
                Type escapeDetector = game.GetType("EscapeDetector", true);
                MethodInfo escaped = escapeDetector.GetMethod("OnTriggerEnter", Flags);
                MethodInfo complete = gameplay.GetMethod("OnEventAreaComplete", Flags);
                MethodInfo progress = gameplay.GetMethod("GetCurrentArenaProgress", Flags);
                if (click == null || upgradeClick == null || shopReset == null || episodeClick == null || escaped == null || complete == null || progress == null)
                    throw new MissingMethodException("Required game hooks missing");
                harmony = new Harmony("timerifters.archipelago");
                harmony.Patch(click, new HarmonyMethod(typeof(Plugin), "BeforeWeaponClick"));
                harmony.Patch(upgradeClick, new HarmonyMethod(typeof(Plugin), "BeforeUpgradeClick"));
                harmony.Patch(shopReset, null, new HarmonyMethod(typeof(Plugin), "AfterShopReset"));
                harmony.Patch(episodeClick, new HarmonyMethod(typeof(Plugin), "BeforeEpisodeClick"),
                    new HarmonyMethod(typeof(Plugin), "AfterEpisodeClick"));
                harmony.Patch(escaped, null, new HarmonyMethod(typeof(Plugin), "AfterEscaped"));
                harmony.Patch(complete, new HarmonyMethod(typeof(Plugin), "BeforeComplete"),
                    new HarmonyMethod(typeof(Plugin), "AfterComplete"));
                PatchWeaponFireMethods(game);
                ready = true;
                Logger.LogInfo("[TRAP] READY 1.0.13. Press F8 or HOME at TitleScreen after connecting the AP client.");
                Logger.LogInfo("[TRAP] Progress folder: " + folder);
            }
            catch (Exception ex)
            {
                if (harmony != null) harmony.UnpatchSelf();
                message = "Setup failed. See LogOutput.log.";
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
        private int LocationTotal { get { return Logic.LocationCount(milestoneCount, escapeChecks); } }
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
            string loadedLevel = Application.loadedLevelName;
            if (isActive && loadedLevel == "TitleScreen" && lastLoadedLevel != "TitleScreen")
                RestoreCreditsAtTitle();
            lastLoadedLevel = loadedLevel;
            if (Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 0.5f;
                fresh = false;
                try
                {
                    string path = Path.Combine(folder, "server.txt");
                    if (File.Exists(path))
                        fresh = Logic.ParseSnapshot(ReadLines(path), Now(), out pendingSession,
                            out pendingItems, out pendingEchoes, out pendingPowerUps, out pendingChecks,
                            out pendingEscapeChecks, out pendingEpisodeKeys, out pendingUpgradeMode, out pendingArenaShuffle, out pendingArenaOrder,
                            out pendingRequiredEchoes, out pendingGoal, out pendingDeathLink, out pendingDeathLinkPercent, out pendingDeathLinkDuration, out pendingMilestoneCount);
                    if (isActive && fresh && session == pendingSession)
                    {
                        int newItems = pendingItems & ~items;
                        int newEchoes = pendingEchoes - echoes;
                        long newPowerUps = pendingPowerUps & ~powerUps;
                        upgradeMode = pendingUpgradeMode;
                        if (newItems != 0) items |= pendingItems;
                        if (newEchoes > 0) echoes = pendingEchoes;
                        if (newPowerUps != 0) powerUps |= pendingPowerUps;
                        if (newItems != 0 || newEchoes > 0 || newPowerUps != 0)
                        {
                            List<string> received = ReceivedItemNames(newItems);
                            if (newEchoes > 0)
                                received.Add(newEchoes + " Time Echo" + (newEchoes == 1 ? "" : "es")
                                    + " (" + Math.Max(0, echoes - creditsSpent) + " credits available)");
                            received.AddRange(ReceivedPowerUpNames(newPowerUps));
                            message = "Received: " + string.Join(", ", received.ToArray());
                            Logger.LogInfo("[TRAP] " + message);
                        }
                        EvaluateGoal();
                        int before = checks.Count; checks.UnionWith(pendingChecks);
                        if (checks.Count != before) dirty = true;
                    }
                    if (isActive && CorrectSession()) ApplyItemNotification();
                    if (dirty) SaveProgress();
                }
                catch (Exception ex)
                {
                    fresh = false;
                    message = "Bridge/file error (will retry): " + ex.Message;
                }
            }
            if (isActive && CorrectSession() && deathLink) ApplyIncomingDeathLink();
            if (isActive && CorrectSession() && NormalPlay() && !(bool)Read("resetPressed"))
            {
                try
                {
                    int arena = CurrentArenaIndex((int)Read("currentEpisode"), (int)Read("currentEpisodeArena"));
                    float fraction = CurrentArenaProgress();
                    if (arena != lastPolledArena)
                    {
                        if (lastPolledArena >= 0) waitingForNewArenaProgress = true;
                        lastPolledArena = arena;
                    }
                    // Time Rifters retains a near-complete old-arena value
                    // after advancing the arena index.  Do not credit the new
                    // arena until its own progress starts below the first
                    // 25% milestone.
                    if (waitingForNewArenaProgress)
                    {
                        if (fraction >= Logic.MilestonePercent(milestoneCount, 0) / 100f) return;
                        waitingForNewArenaProgress = false;
                    }
                    RecordArenaBest(arena, fraction);
                    RecordMilestones(arena, fraction);
                }
                catch (Exception ex) { Logger.LogError("[TRAP] Progress poll failed: " + ex); }
            }
            if (isActive && arenaShuffle && !arenaOrderApplied) TryApplyArenaOrder();
            if (Input.GetKeyDown(KeyCode.F7))
            {
                overlayVisible = !overlayVisible;
                return;
            }
            if (Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.Home))
            {
                if (Application.loadedLevelName != "TitleScreen")
                {
                    message = "Return to the title screen before enabling/disabling AP.";
                    return;
                }
                if (isActive)
                {
                    RestoreArenaOrder();
                    isActive = false;
                    message = "AP disabled. Normal gameplay enabled.";
                    return;
                }
                if (!fresh) { message = "WAITING FOR AP CLIENT: connect as your YAML player name, leave the client open, wait 5 seconds, then press F8 or HOME again."; return; }
                try
                {
                    session = pendingSession;
                    lastNotificationId = 0;
                    items = pendingItems;
                    echoes = pendingEchoes;
                    powerUps = pendingPowerUps;
                    checks = pendingChecks;
                    milestoneCount = pendingMilestoneCount;
                    escapeChecks = pendingEscapeChecks;
                    episodeKeys = pendingEpisodeKeys;
                    upgradeMode = pendingUpgradeMode;
                    arenaShuffle = pendingArenaShuffle;
                    arenaOrder = pendingArenaOrder == null ? null : (int[])pendingArenaOrder.Clone();
                    requiredEchoes = pendingRequiredEchoes;
                    goal = pendingGoal;
                    deathLink = pendingDeathLink;
                    deathLinkPercent = pendingDeathLinkPercent;
                    deathLinkDuration = pendingDeathLinkDuration;
                    arenaOrderApplied = !arenaShuffle;
                    arenaShuffleWaiting = false;
                    goalReached = File.Exists(Path.Combine(folder, session + ".goal"));
                    arenaBest = new int[Logic.ArenaCount];
                    string path = Path.Combine(folder, session + ".progress");
                    if (File.Exists(path))
                    {
                        string[] lines = ReadLines(path);
                        HashSet<int> savedChecks;
                        if (lines.Length != 5 || lines[0] != "6" || lines[1] != session
                            || !TryProgressChecks(lines[2], LocationTotal, out savedChecks)
                            || !Int32.TryParse(lines[3], out creditsSpent) || creditsSpent < 0)
                            throw new InvalidDataException("Progress file invalid; preserved for diagnosis.");
                        checks.UnionWith(savedChecks);
                        if (!TryArenaBest(lines[4], out arenaBest))
                            throw new InvalidDataException("Arena best-percent data is invalid; preserved for diagnosis.");
                    }
                    // Echoes are reusable credits for each episode/replay.
                    // The game's upgrades reset with its shop data, so credits
                    // must reset too or a replay can become impossible to 100%.
                    creditsSpent = 0;
                    dirty = true;
                    SaveProgress();
                    isActive = true;
                    EvaluateGoal();
                    message = "AP enabled. Each arena sends " + milestoneCount + " evenly spaced percentage checks.";
                Logger.LogInfo("[TRAP] Session activated " + session + "; checks=" + checks + "; items=" + items
                    + "; arenaShuffle=" + arenaShuffle);
                }
                catch (Exception ex) { isActive = false; message = ex.Message; Logger.LogError("[TRAP] " + ex); }
            }
        }

        private void SaveProgress()
        {
            string path = Path.Combine(folder, session + ".progress");
            string temp = path + ".tmp";
            File.WriteAllText(temp, "6\n" + session + "\n" + ProgressChecksText()
                + "\n" + creditsSpent.ToString(CultureInfo.InvariantCulture) + "\n" + ArenaBestText() + "\n");
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            dirty = false;
        }

        private void ApplyItemNotification()
        {
            string path = Path.Combine(folder, "notification.txt");
            if (!File.Exists(path)) return;
            string[] lines = ReadLines(path);
            long notificationId;
            if (lines.Length != 4 || lines[0] != "1" || lines[1] != session
                || !Int64.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out notificationId)
                || notificationId <= lastNotificationId || lines[3].Length == 0 || lines[3].Length > 240)
                return;
            lastNotificationId = notificationId;
            message = lines[3];
            Logger.LogInfo("[TRAP] " + message);
        }

        private string ProgressChecksText()
        {
            List<int> values = new List<int>(checks); values.Sort();
            string[] text = new string[values.Count];
            for (int index = 0; index < values.Count; index++) text[index] = values[index].ToString(CultureInfo.InvariantCulture);
            return string.Join(",", text);
        }

        private static bool TryProgressChecks(string text, int total, out HashSet<int> result)
        {
            result = new HashSet<int>(); if (text.Length == 0) return true;
            foreach (string part in text.Split(','))
            {
                int value;
                if (!Int32.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                    || value < 0 || value >= total || !result.Add(value)) return false;
            }
            return true;
        }

        private string ArenaBestText()
        {
            string[] values = new string[Logic.ArenaCount];
            for (int index = 0; index < values.Length; index++)
                values[index] = arenaBest[index].ToString(CultureInfo.InvariantCulture);
            return string.Join(",", values);
        }

        private static bool TryArenaBest(string text, out int[] result)
        {
            result = null;
            string[] values = text.Split(',');
            if (values.Length != Logic.ArenaCount) return false;
            int[] parsed = new int[Logic.ArenaCount];
            for (int index = 0; index < parsed.Length; index++)
                if (!Int32.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed[index])
                    || parsed[index] < 0 || parsed[index] > 10000) return false;
            result = parsed;
            return true;
        }

        private void RecordArenaBest(int arena, float fraction)
        {
            if (arena < 0 || arena >= Logic.ArenaCount || Single.IsNaN(fraction) || Single.IsInfinity(fraction)) return;
            int value = Math.Max(0, Math.Min(10000, (int)Math.Floor(fraction * 10000f + .0001f)));
            if (value <= arenaBest[arena]) return;
            arenaBest[arena] = value;
            dirty = true;
            EvaluateGoal();
        }

        private bool RequiredProgressReady()
        {
            if ((items & 31) != 31 || echoes < requiredEchoes) return false;
            return !episodeKeys || ((items & 32) != 0 && (items & 64) != 0);
        }

        private bool GoalPercentComplete()
        {
            if (goal == 101) return arenaBest[1] >= 10000;
            int total = 0;
            for (int index = 0; index < arenaBest.Length; index++) total += arenaBest[index];
            return total >= goal * Logic.ArenaCount * 100;
        }

        private void EvaluateGoal()
        {
            if (!isActive || goalReached || !RequiredProgressReady() || !GoalPercentComplete()) return;
            goalReached = true;
            string path = Path.Combine(folder, session + ".goal");
            string temp = path + ".tmp";
            File.WriteAllText(temp, "1\n" + session + "\n");
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            message = "GOAL COMPLETE! Archipelago will mark the room complete.";
            Logger.LogInfo("[TRAP] Goal complete for session " + session + ".");
        }

        private float AverageBestPercent()
        {
            int total = 0;
            for (int index = 0; index < arenaBest.Length; index++) total += arenaBest[index];
            return total / (Logic.ArenaCount * 100f);
        }

        private bool CorrectSession() { return !fresh || session == pendingSession; }

        private void PatchWeaponFireMethods(Assembly game)
        {
            foreach (Type type in game.GetTypes())
            {
                if (!type.Name.StartsWith("NinjaWeapon", StringComparison.Ordinal)) continue;
                foreach (MethodInfo method in type.GetMethods(Flags))
                    if (method.Name == "TryFire" && method.GetParameters().Length == 0)
                        harmony.Patch(method, new HarmonyMethod(typeof(Plugin), "BeforeFire"));
            }
        }

        public static bool BeforeFire()
        {
            Plugin p = Instance;
            if (p == null || !p.isActive || Time.realtimeSinceStartup >= p.firingDisabledUntil) return true;
            p.message = "DeathLink active: firing disabled for "
                + Math.Max(1, (int)Math.Ceiling(p.firingDisabledUntil - Time.realtimeSinceStartup)) + " more seconds.";
            return false;
        }

        private void ApplyIncomingDeathLink()
        {
            string path = Path.Combine(folder, session + ".deathlink");
            if (!File.Exists(path) || !NormalPlay()) return;
            string[] lines = ReadLines(path);
            if (lines.Length != 2 || lines[0] != "1" || lines[1] != session)
                throw new InvalidDataException("DeathLink file is invalid; preserved for diagnosis.");
            File.Delete(path);
            firingDisabledUntil = Math.Max(firingDisabledUntil, Time.realtimeSinceStartup + deathLinkDuration);
            DisableActiveShooters(60f);
            message = "DeathLink received! Firing disabled for " + deathLinkDuration + " seconds.";
            Logger.LogInfo("[TRAP] DeathLink received; firing disabled for " + deathLinkDuration + " seconds.");
        }

        private void DisableActiveShooters(float seconds)
        {
            Type shooter = gameplay.Assembly.GetType("vp_Shooter", false);
            if (shooter == null) return;
            MethodInfo disable = shooter.GetMethod("DisableFiring", Flags);
            if (disable == null) return;
            foreach (UnityEngine.Object value in UnityEngine.Object.FindObjectsOfType(shooter))
                disable.Invoke(value, new object[] { seconds });
        }

        public static void AfterShopReset()
        {
            Plugin p = Instance;
            if (p == null || !p.isActive || p.creditsSpent == 0) return;
            try
            {
                p.creditsSpent = 0;
                p.dirty = true;
                p.SaveProgress();
                p.message = "New episode/replay: all Time Echo credits are available again.";
                p.Logger.LogInfo("[TRAP] Shop reset: restored reusable Time Echo credits.");
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Could not restore Time Echo credits: " + ex); }
        }

        private void RestoreCreditsAtTitle()
        {
            if (creditsSpent == 0) return;
            creditsSpent = 0;
            dirty = true;
            SaveProgress();
            message = "Returned to title screen: all Time Echo credits are available again.";
            Logger.LogInfo("[TRAP] Title screen: restored reusable Time Echo credits.");
        }

        private void ApplyArenaOrder(int[] order)
        {
            if (order == null || order.Length != Logic.ArenaCount)
                throw new InvalidDataException("Arena shuffle data is missing or invalid.");
            if (!arenaShuffle)
            {
                arenaOrder = (int[])order.Clone();
                return;
            }
            IList episodes = (IList)Read("episodeArenas");
            // The game has three playable five-arena episodes plus two
            // internal tutorial/bonus entries.  Only shuffle the first three.
            if (episodes == null || episodes.Count < Logic.EpisodeCount)
                throw new InvalidDataException("Game episode arena table is unavailable.");
            int position = 0;
            for (int episode = 0; episode < Logic.EpisodeCount; episode++)
            {
                IList arenas = episodes[episode] as IList;
                if (arenas == null || arenas.Count != 5)
                    throw new InvalidDataException("Game episode arena table has an unexpected layout.");
                for (int slot = 0; slot < arenas.Count; slot++)
                {
                    if (order[position] < 0 || order[position] >= Logic.ArenaCount)
                        throw new InvalidDataException("Arena shuffle contains an invalid arena.");
                    position++;
                }
            }
            if (originalArenaOrder == null)
            {
                originalArenaOrder = new int[Logic.ArenaCount];
                position = 0;
                for (int episode = 0; episode < Logic.EpisodeCount; episode++)
                {
                    IList arenas = (IList)episodes[episode];
                    for (int slot = 0; slot < arenas.Count; slot++)
                        originalArenaOrder[position++] = Convert.ToInt32(arenas[slot], CultureInfo.InvariantCulture);
                }
            }
            position = 0;
            for (int episode = 0; episode < Logic.EpisodeCount; episode++)
            {
                IList arenas = (IList)episodes[episode];
                for (int slot = 0; slot < arenas.Count; slot++)
                    arenas[slot] = order[position++];
            }
            arenaOrder = (int[])order.Clone();
            Logger.LogInfo("[TRAP] Applied " + (arenaShuffle ? "shuffled" : "normal") + " 5/5/5 arena order.");
        }

        private static Component[] ChildComponents(Component card, Type type)
        {
            if (card == null || type == null) return new Component[0];
            return card.GetComponentsInChildren(type);
        }

        private CardVisual[] ReadCardVisuals(Component card)
        {
            List<CardVisual> visuals = new List<CardVisual>();
            foreach (Component texture in ChildComponents(card, uiTextureType))
            {
                PropertyInfo property = texture.GetType().GetProperty("mainTexture", Flags);
                if (property != null && property.CanRead)
                    visuals.Add(new CardVisual { typeName = "UITexture", value = property.GetValue(texture, null) });
            }
            foreach (Component sprite in ChildComponents(card, uiSpriteType))
            {
                PropertyInfo property = sprite.GetType().GetProperty("spriteName", Flags);
                if (property != null && property.CanRead)
                    visuals.Add(new CardVisual { typeName = "UISprite", value = property.GetValue(sprite, null) });
            }
            return visuals.ToArray();
        }

        private void WriteCardVisuals(Component card, CardVisual[] visuals)
        {
            if (visuals == null) return;
            int textureIndex = 0, spriteIndex = 0;
            Component[] textures = ChildComponents(card, uiTextureType);
            Component[] sprites = ChildComponents(card, uiSpriteType);
            foreach (CardVisual visual in visuals)
            {
                if (visual.typeName == "UITexture")
                {
                    if (textureIndex >= textures.Length) continue;
                    PropertyInfo property = textures[textureIndex++].GetType().GetProperty("mainTexture", Flags);
                    if (property != null && property.CanWrite) property.SetValue(textures[textureIndex - 1], visual.value, null);
                }
                else if (visual.typeName == "UISprite")
                {
                    if (spriteIndex >= sprites.Length) continue;
                    PropertyInfo property = sprites[spriteIndex++].GetType().GetProperty("spriteName", Flags);
                    if (property != null && property.CanWrite) property.SetValue(sprites[spriteIndex - 1], visual.value, null);
                }
            }
        }

        private Array EpisodeChoosers()
        {
            UnityEngine.Object[] selectors = UnityEngine.Object.FindObjectsOfType(episodeChooserType);
            if (selectors == null || selectors.Length == 0) return null;
            return Field(episodeChooserType, "arenaChoosers").GetValue(selectors[0]) as Array;
        }

        private void CaptureArenaCardVisuals()
        {
            if (cardVisualsCaptured) return;
            Array choosers = EpisodeChoosers();
            if (choosers == null || choosers.Length < Logic.EpisodeCount) return;
            for (int episode = 0; episode < Logic.EpisodeCount; episode++)
            {
                Array cards = Field(arenaChooserType, "arenas").GetValue(choosers.GetValue(episode)) as Array;
                if (cards == null || cards.Length != 5) return;
                for (int slot = 0; slot < cards.Length; slot++)
                    originalCardVisuals[DefaultArenaIds[episode * 5 + slot]] = ReadCardVisuals(cards.GetValue(slot) as Component);
            }
            cardVisualsCaptured = originalCardVisuals.Count == Logic.ArenaCount;
            if (cardVisualsCaptured) Logger.LogInfo("[TRAP] Captured original episode-card previews.");
        }

        private void ApplyArenaCardVisuals()
        {
            CaptureArenaCardVisuals();
            if (!cardVisualsCaptured || arenaOrder == null) return;
            Array choosers = EpisodeChoosers();
            if (choosers == null) return;
            int position = 0;
            FieldInfo episodeArena = Field(arenaCardType, "episodeArena");
            MethodInfo refresh = arenaCardType.GetMethod("Refresh", Flags);
            for (int episode = 0; episode < Logic.EpisodeCount; episode++)
            {
                Array cards = Field(arenaChooserType, "arenas").GetValue(choosers.GetValue(episode)) as Array;
                if (cards == null || cards.Length != 5) return;
                for (int slot = 0; slot < cards.Length; slot++)
                {
                    int arenaId = DefaultArenaIds[arenaOrder[position++]];
                    Component card = cards.GetValue(slot) as Component;
                    episodeArena.SetValue(card, Enum.ToObject(episodeArena.FieldType, arenaId));
                    CardVisual[] visuals;
                    if (originalCardVisuals.TryGetValue(arenaId, out visuals)) WriteCardVisuals(card, visuals);
                    if (refresh != null) refresh.Invoke(card, null);
                }
            }
        }

        private void RestoreArenaOrder()
        {
            if (originalArenaOrder == null) return;
            try
            {
                IList episodes = (IList)Read("episodeArenas");
                int position = 0;
                for (int episode = 0; episode < Logic.EpisodeCount; episode++)
                {
                    IList arenas = episodes[episode] as IList;
                    if (arenas == null || arenas.Count != 5) throw new InvalidDataException("Game episode arena table changed.");
                    for (int slot = 0; slot < arenas.Count; slot++) arenas[slot] = originalArenaOrder[position++];
                }
                Logger.LogInfo("[TRAP] Restored the game's original arena order.");
            }
            catch (Exception ex) { Logger.LogError("[TRAP] Could not restore arena order: " + ex); }
            finally { originalArenaOrder = null; arenaOrder = null; }
        }

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
            int added = 0;
            List<string> earned = new List<string>();
            for (int milestone = 0; milestone < milestoneCount; milestone++)
                if (Logic.MilestoneReached(true, false, arena, fraction, milestoneCount, milestone)
                    && Logic.AddLocation(checks, Logic.ArenaCheckIndex(arena, milestone, milestoneCount), LocationTotal))
                {
                    added++;
                    earned.Add(Logic.MilestonePercent(milestoneCount, milestone) + "%");
                }
            if (added == 0) return;
            dirty = true;
            message = "Check sent: " + ArenaNames[arena] + " - " + string.Join(", ", earned.ToArray())
                + " Clear";
            Logger.LogInfo("[TRAP] Arena " + arena + " progress " + fraction.ToString(CultureInfo.InvariantCulture)
                + " earned " + added + " milestone(s).");
            SaveProgress();
        }

        private void RecordEpisodeComplete(int episode)
        {
            if (!Logic.AddLocation(checks, Logic.EpisodeCheckIndex(episode, milestoneCount), LocationTotal)) return;
            dirty = true;
            message = "Check sent: Episode " + (episode + 1) + " Complete";
            Logger.LogInfo("[TRAP] Episode " + episode + " completion check earned.");
            SaveProgress();
        }

        private void SendDeathLinkForLowArena(int arena, float fraction)
        {
            if (!deathLink || arena < 0 || arena >= Logic.ArenaCount) return;
            int percent = Math.Max(0, Math.Min(100, (int)Math.Floor(fraction * 100f + .0001f)));
            if (percent >= deathLinkPercent) return;
            string path = Path.Combine(folder, session + ".death");
            string temp = path + ".tmp";
            File.WriteAllText(temp, "1\n" + session + "\n");
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            message = "DeathLink sent: " + ArenaNames[arena] + " ended at " + percent + "% destruction.";
            Logger.LogInfo("[TRAP] DeathLink queued for " + ArenaNames[arena] + " at " + percent + "%.");
        }

        private void RecordEscape(int arena)
        {
            if (!Logic.AddLocation(checks, Logic.EscapeCheckIndex(arena, milestoneCount), LocationTotal)) return;
            dirty = true;
            message = "Check sent: " + ArenaNames[arena] + " - Hidden Escape";
            Logger.LogInfo("[TRAP] Arena " + arena + " hidden escape check earned.");
            SaveProgress();
        }

        private void RecordTitleEscape()
        {
            if (!Logic.AddLocation(checks, Logic.TitleCheckIndex(milestoneCount), LocationTotal)) return;
            dirty = true;
            message = "Check sent: Title Screen - Hidden Escape";
            Logger.LogInfo("[TRAP] Title screen escape check earned.");
            SaveProgress();
        }

        private bool TryUpgradeWeapon(object upgrade, out int weapon)
        {
            weapon = -1;
            try
            {
                Type shopType = upgrade.GetType().Assembly.GetType("Shop", true);
                Array players = Field(shopType, "playerShopData").GetValue(null) as Array;
                if (players == null) return false;
                for (int player = 0; player < players.Length; player++)
                {
                    object data = players.GetValue(player);
                    if (data == null) continue;
                    Array settings = Field(data.GetType(), "weaponSettings").GetValue(data) as Array;
                    if (settings == null) continue;
                    for (int candidateWeapon = 0; candidateWeapon < settings.Length && candidateWeapon < 6; candidateWeapon++)
                    {
                        object setting = settings.GetValue(candidateWeapon);
                        if (setting == null) continue;
                        object weaponData = Field(setting.GetType(), "weaponData").GetValue(setting);
                        Array upgrades = weaponData == null ? null : Field(weaponData.GetType(), "upgrades").GetValue(weaponData) as Array;
                        if (upgrades == null) continue;
                        for (int candidateSlot = 0; candidateSlot < upgrades.Length; candidateSlot++)
                            if (System.Object.ReferenceEquals(upgrade, upgrades.GetValue(candidateSlot)))
                            {
                                weapon = candidateWeapon;
                                return weapon >= 0 && weapon < WeaponPowerUps.Length;
                            }
                    }
                }
            }
            catch (Exception ex) { Logger.LogError("[TRAP] Could not identify shop upgrade: " + ex); }
            return false;
        }

        private static bool ValidUpgradeCategory(int category)
        {
            return category >= 0 && category < UpgradeNames.Length;
        }

        private static int PerWeaponPowerUpIndex(int weapon, int category)
        {
            if (weapon < 0 || weapon >= WeaponPowerUps.Length || !ValidUpgradeCategory(category)) return -1;
            int index = GlobalPowerUpNames.Length;
            for (int candidate = 0; candidate < WeaponPowerUps.Length; candidate++)
            {
                int[] categories = WeaponPowerUps[candidate];
                for (int slot = 0; slot < categories.Length; slot++)
                {
                    if (candidate == weapon && categories[slot] == category) return index;
                    index++;
                }
            }
            return -1;
        }

        private bool HasPowerUp(int weapon, int category)
        {
            if (upgradeMode == 0) return true;
            if (!ValidUpgradeCategory(category)) return false;
            int index;
            if (upgradeMode == 1) index = GlobalCategoryForUpgrade[category];
            else if (upgradeMode == 2) index = PerWeaponPowerUpIndex(weapon, category);
            else return false;
            return index >= 0 && (powerUps & (1L << index)) != 0;
        }

        private string RequiredPowerUpName(int weapon, int category)
        {
            if (!ValidUpgradeCategory(category)) return "this power-up";
            if (upgradeMode == 1) return GlobalPowerUpNames[GlobalCategoryForUpgrade[category]] + " Upgrade";
            if (upgradeMode == 2 && weapon >= 0 && weapon < ShopWeaponNames.Length)
                return ShopWeaponNames[weapon] + " - " + UpgradeNames[category] + " Upgrade";
            return UpgradeNames[category] + " Upgrade";
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

        // Time Echoes are free Archipelago upgrade credits.  The original shop
        // charges arena gold, so we handle the level increase here and skip its
        // normal purchase path.  This deliberately uses the game's own
        // UpgradeData object, which is already persisted by Time Rifters.
        public static bool BeforeUpgradeClick(object __instance)
        {
            Plugin p = Instance;
            if (p == null || !p.isActive) return true;
            try
            {
                object upgrade = Field(__instance.GetType(), "upgradeData").GetValue(__instance);
                if (upgrade == null) return false;
                Type type = upgrade.GetType();
                int weapon = -1;
                int category = Convert.ToInt32(Field(type, "name").GetValue(upgrade), CultureInfo.InvariantCulture);
                if (p.upgradeMode != 0)
                {
                    if (!p.TryUpgradeWeapon(upgrade, out weapon) || !ValidUpgradeCategory(category))
                    {
                        p.message = "Could not identify this shop power-up. See LogOutput.log.";
                        return false;
                    }
                    if (!p.HasPowerUp(weapon, category))
                    {
                        p.message = p.RequiredPowerUpName(weapon, category) + " is locked. Receive it from Archipelago first.";
                        p.Logger.LogInfo("[TRAP] Blocked locked power-up: " + p.RequiredPowerUpName(weapon, category));
                        return false;
                    }
                }
                int available = p.echoes - p.creditsSpent;
                if (available <= 0)
                {
                    p.message = "No Time Echo upgrade credits available. Receive one from Archipelago first.";
                    return false;
                }
                int level = Convert.ToInt32(Field(type, "upgradeLevel").GetValue(upgrade), CultureInfo.InvariantCulture);
                int maximum = Field(type, "upgradeType").GetValue(upgrade).ToString() == "Multiple" ? 5 : 1;
                if (level >= maximum)
                {
                    p.message = "That upgrade is already at its maximum level.";
                    return false;
                }
                FieldInfo levelField = Field(type, "upgradeLevel");
                levelField.SetValue(upgrade, level + 1);
                // UpgradeData is the game's persistent upgrade record.  The
                // gameplay component reads it when the next arena begins.
                // Refreshing the shop buttons is enough for the shop display.
                try { p.RefreshUpgradeShop(); }
                catch
                {
                    levelField.SetValue(upgrade, level);
                    throw;
                }
                p.creditsSpent++;
                p.dirty = true;
                p.SaveProgress();
                p.message = "Time Echo spent! Upgrade applies in the next arena. Credits remaining: "
                    + Math.Max(0, p.echoes - p.creditsSpent) + ".";
                p.Logger.LogInfo("[TRAP] Spent Time Echo upgrade credit; level " + level + " -> " + (level + 1));
                return false;
            }
            catch (Exception ex)
            {
                p.message = "Upgrade credit error; no credit was spent. See log.";
                p.Logger.LogError("[TRAP] Upgrade credit handling failed: " + ex);
                return false;
            }
        }

        public static bool BeforeEpisodeClick(object __instance)
        {
            Plugin p = Instance;
            if (p == null || !p.isActive || !p.CorrectSession()) return true;
            try
            {
                p.TryApplyArenaOrder();
                int episode = (int)Field(__instance.GetType(), "episodeIndex").GetValue(__instance);
                if (Logic.EpisodeAllowed(episode, p.items, p.episodeKeys)) return true;
                p.message = "Episode " + (episode + 1) + " is locked. Find its Archipelago key.";
                p.Logger.LogInfo("[TRAP] Blocked Episode " + (episode + 1) + "; key not received.");
                return false;
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Episode lock failed: " + ex); return true; }
        }

        public static void AfterEpisodeClick()
        {
            Plugin p = Instance;
            if (p == null || !p.isActive || !p.CorrectSession()) return;
            p.TryApplyArenaOrder();
        }

        private void TryApplyArenaOrder()
        {
            if (arenaOrderApplied) return;
            try
            {
                ApplyArenaOrder(arenaOrder);
                arenaOrderApplied = true;
                message = "Arena shuffle applied to this seed.";
            }
            catch (InvalidDataException ex)
            {
                // The title screen exists before ShortGameplay creates its
                // episode table. Try again when the player uses an episode button.
                message = "Arena shuffle is waiting for the episode screen.";
                if (!arenaShuffleWaiting)
                {
                    Logger.LogInfo("[TRAP] Arena shuffle deferred: " + ex.Message);
                    arenaShuffleWaiting = true;
                }
            }
            catch (Exception ex)
            {
                message = "Arena shuffle error; normal arena order is still safe. See log.";
                Logger.LogError("[TRAP] Arena shuffle failed: " + ex);
            }
        }

        private void RefreshUpgradeShop()
        {
            // Do not query Shop.CurrentShopData here.  In this Unity version it
            // is only available during a different lifecycle stage and causes
            // a null reference when an upgrade button is clicked.
            Assembly game = gameplay.Assembly;
            RefreshAll(game.GetType("ShopButtonUpgrade", false), "Refresh", null);
        }

        private static void RefreshAll(Type type, string method, object argument)
        {
            if (type == null) return;
            MethodInfo find = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Type) }, null);
            MethodInfo refresh = null;
            foreach (MethodInfo candidate in type.GetMethods(Flags))
                if (candidate.Name == method && candidate.GetParameters().Length == (argument == null ? 0 : 1))
                    refresh = candidate;
            if (find == null || refresh == null) return;
            Array objects = find.Invoke(null, new object[] { type }) as Array;
            if (objects == null) return;
            foreach (object value in objects)
                refresh.Invoke(value, argument == null ? null : new object[] { argument });
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
                p.RecordArenaBest(__state.GlobalArena, fraction);
                p.RecordMilestones(__state.GlobalArena, fraction);
                if ((bool)p.Read("episodeComplete")) p.RecordEpisodeComplete(__state.Episode);
                p.SendDeathLinkForLowArena(__state.GlobalArena, fraction);
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Check handling failed (pending writes retry): " + ex); }
        }

        public static void AfterEscaped()
        {
            Plugin p = Instance;
            if (p == null || !p.isActive || !p.CorrectSession()) return;
            try
            {
                if (!p.escapeChecks) return;
                if (Application.loadedLevelName == "TitleScreen")
                {
                    p.RecordTitleEscape();
                    return;
                }
                if (!p.NormalPlay() || (bool)p.Read("resetPressed")) return;
                p.RecordEscape(p.CurrentArenaIndex((int)p.Read("currentEpisode"), (int)p.Read("currentEpisodeArena")));
            }
            catch (Exception ex) { p.Logger.LogError("[TRAP] Escape check handling failed: " + ex); }
        }

        private void OnGUI()
        {
            if (!overlayVisible)
            {
                GUIStyle reminderStyle = new GUIStyle(GUI.skin.box);
                reminderStyle.fontSize = 18;
                reminderStyle.padding = new RectOffset(10, 10, 6, 6);
                GUI.Box(new Rect(20, 20, Math.Min(470, Screen.width - 40), 42),
                    "Time Rifters AP  |  F7: show panel  |  F8/Home: enable/disable", reminderStyle);
                return;
            }
            string status = !isActive ? "INACTIVE" :
                !CorrectSession() ? "SESSION CHANGED - restart game before continuing" :
                fresh ? "CONNECTED" : "OFFLINE - checks queued; received unlocks retained";
            int arenaLocations = Logic.ArenaLocationCount(milestoneCount);
            string arenaProgress = "Arena checks (" + milestoneCount + " each): " + Logic.CountLocations(checks, 0,
                arenaLocations) + "/" + arenaLocations;
            string episodeProgress = "Episode checks: " + Logic.CountLocations(checks,
                arenaLocations, Logic.EpisodeCount) + "/" + Logic.EpisodeCount;
            string escapeProgress = escapeChecks ? "\nEscape checks: " + Logic.CountLocations(checks,
                Logic.EscapeLocationStart(milestoneCount), Logic.ArenaCount + 1) + "/" + (Logic.ArenaCount + 1) : "";
            string episodeAccess = episodeKeys ? EpisodeStatus("Episode 1", true) + " | "
                + EpisodeStatus("Episode 2", (items & 32) != 0) + " | "
                + EpisodeStatus("Episode 3", (items & 64) != 0)
                : "<color=#A9B6C9>Episode keys: OFF</color>";
            string shuffleStatus = arenaShuffle ? "<color=#61E58B>Arena shuffle: ON</color>"
                : "<color=#A9B6C9>Arena shuffle: OFF</color>";
            string goalProgress = goal == 101
                ? "Goal: Arena Boss 100% | Current: " + (arenaBest[1] / 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                : "Goal: " + goal + "% average | Current: " + AverageBestPercent().ToString("0.00", CultureInfo.InvariantCulture) + "%";
            if (goalReached) goalProgress = "<color=#61E58B>" + goalProgress + " | COMPLETE</color>";
            string deathLinkStatus = deathLink ? "<color=#FFCF70>DeathLink: ON below " + deathLinkPercent + "% | " + deathLinkDuration + "s lock</color>"
                : "<color=#A9B6C9>DeathLink: OFF</color>";
            if (Time.realtimeSinceStartup < firingDisabledUntil)
                deathLinkStatus += " | <color=#FF6B6B>FIRING LOCK: "
                    + Math.Max(1, (int)Math.Ceiling(firingDisabledUntil - Time.realtimeSinceStartup)) + "s</color>";
            bool atTitle = Application.loadedLevelName == "TitleScreen";
            string shuffleDetails = atTitle
                ? "\n<color=#A9D8FF>Episode 1:</color> " + EpisodeBestPercent(0) + " | " + ArenaLine(0)
                + "\n<color=#A9D8FF>Episode 2:</color> " + EpisodeBestPercent(1) + " | " + ArenaLine(5)
                + "\n<color=#A9D8FF>Episode 3:</color> " + EpisodeBestPercent(2) + " | " + ArenaLine(10) : "";
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.box);
                overlayStyle.fontSize = 24;
                overlayStyle.wordWrap = true;
                overlayStyle.richText = true;
                overlayStyle.alignment = TextAnchor.UpperLeft;
                overlayStyle.padding = new RectOffset(16, 16, 12, 12);
            }
            int overlayWidth = Math.Min(980, Screen.width - 40);
            string upgradeStatus = PowerUpOverlayText();
            string overlayText = "TIME RIFTERS AP 1.0.13  |  " + status + "\n"
                + "F7 hides panel  |  F8/Home at title: enable or refresh AP.\n"
                + WeaponStatus("Flak Cannon", (items & 1) != 0)
                + " | " + WeaponStatus("Plasma Beam", (items & 2) != 0)
                + " | " + WeaponStatus("Particle Ball", (items & 4) != 0) + "\n"
                + WeaponStatus("Rocket Launcher", (items & 8) != 0)
                + " | " + WeaponStatus("Spread Rifle", (items & 16) != 0) + "\n"
                + episodeAccess + "\n"
                + shuffleStatus + "\n"
                + arenaProgress + " | " + episodeProgress + escapeProgress + "\n"
                + goalProgress + "\n"
                + deathLinkStatus + "\n"
                + "Time Echo credits: " + Math.Max(0, echoes - creditsSpent) + shuffleDetails + "\n"
                + upgradeStatus
                + "<color=#FFCF70>Status:</color> " + message;
            int overlayHeight = Mathf.CeilToInt(overlayStyle.CalcHeight(new GUIContent(overlayText), overlayWidth));
            GUI.Box(new Rect(20, 20, overlayWidth, overlayHeight), overlayText, overlayStyle);
        }

        private string PowerUpOverlayText()
        {
            if (upgradeMode == 0) return "";
            if (upgradeMode == 1)
            {
                string text = "Power-up categories: ";
                for (int index = 0; index < GlobalPowerUpNames.Length; index++)
                {
                    if (index > 0) text += " | ";
                    text += PowerUpLabel(GlobalPowerUpNames[index], (powerUps & (1L << index)) != 0);
                }
                return text + "\n";
            }
            return "Power-up items: " + PowerUpLine(0) + " | " + PowerUpLine(3) + "\n"
                + "                " + PowerUpLine(1) + " | " + PowerUpLine(4) + "\n"
                + "                " + PowerUpLine(2) + " | " + PowerUpLine(5) + "\n";
        }

        private string PowerUpLine(int weapon)
        {
            if (weapon < 0 || weapon >= WeaponPowerUps.Length) return "";
            string text = ShopWeaponNames[weapon] + " ";
            int[] categories = WeaponPowerUps[weapon];
            for (int slot = 0; slot < categories.Length; slot++)
            {
                if (slot > 0) text += " ";
                int category = categories[slot];
                text += PowerUpLabel(UpgradeShortNames[category], HasPowerUp(weapon, category));
            }
            return text;
        }

        private static string PowerUpLabel(string name, bool unlocked)
        {
            return "<color=" + (unlocked ? "#61E58B" : "#FF6B6B") + ">" + name + "</color>";
        }

        private List<string> ReceivedPowerUpNames(long changed)
        {
            List<string> names = new List<string>();
            if (upgradeMode == 1)
            {
                for (int index = 0; index < GlobalPowerUpNames.Length; index++)
                    if ((changed & (1L << index)) != 0) names.Add(GlobalPowerUpNames[index] + " Upgrade");
            }
            else if (upgradeMode == 2)
            {
                for (int weapon = 0; weapon < WeaponPowerUps.Length; weapon++)
                    for (int slot = 0; slot < WeaponPowerUps[weapon].Length; slot++)
                    {
                        int category = WeaponPowerUps[weapon][slot];
                        int index = PerWeaponPowerUpIndex(weapon, category);
                        if ((changed & (1L << index)) != 0)
                            names.Add(ShopWeaponNames[weapon] + " - " + UpgradeNames[category] + " Upgrade");
                    }
            }
            return names;
        }

        private string ArenaLine(int firstSlot)
        {
            if (arenaOrder == null || firstSlot < 0 || firstSlot + 5 > arenaOrder.Length) return "loading...";
            string line = "";
            for (int slot = 0; slot < 5; slot++)
            {
                if (slot > 0) line += "  |  ";
                int arena = arenaOrder[firstSlot + slot];
                bool escaped = escapeChecks && checks.Contains(Logic.EscapeCheckIndex(arena, milestoneCount));
                string arenaName = escaped ? "<color=#61E58B>" + ArenaNames[arena] + " E</color>" : ArenaNames[arena];
                line += (slot + 1) + ". " + arenaName + " "
                    + (arenaBest[arena] / 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            }
            return line;
        }

        private static List<string> ReceivedItemNames(int itemBits)
        {
            List<string> names = new List<string>();
            if ((itemBits & 1) != 0) names.Add("Flak Cannon");
            if ((itemBits & 2) != 0) names.Add("Plasma Beam");
            if ((itemBits & 4) != 0) names.Add("Particle Ball");
            if ((itemBits & 8) != 0) names.Add("Rocket Launcher");
            if ((itemBits & 16) != 0) names.Add("Spread Rifle");
            if ((itemBits & 32) != 0) names.Add("Episode 2 Key");
            if ((itemBits & 64) != 0) names.Add("Episode 3 Key");
            return names;
        }

        private string EpisodeBestPercent(int episode)
        {
            if (episode < 0 || episode >= Logic.EpisodeCount) return "0.00%";
            int total = 0;
            for (int slot = 0; slot < 5; slot++)
            {
                int arena = arenaOrder == null ? episode * 5 + slot : arenaOrder[episode * 5 + slot];
                total += arenaBest[arena];
            }
            return (total / 500f).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        private static string WeaponStatus(string weapon, bool unlocked)
        {
            return "<color=" + (unlocked ? "#61E58B" : "#FF6B6B") + ">" + weapon + " "
                + (unlocked ? "UNLOCKED" : "LOCKED") + "</color>";
        }

        private static string EpisodeStatus(string episode, bool unlocked)
        {
            return "<color=" + (unlocked ? "#61E58B" : "#FF6B6B") + ">" + episode + " "
                + (unlocked ? "READY" : "LOCKED") + "</color>";
        }

        private void OnDestroy()
        {
            RestoreArenaOrder();
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}
