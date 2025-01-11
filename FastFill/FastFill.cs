using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FastFill

{
    [BepInPlugin(MOD_ID, MOD_NAME, MOD_VERSION)]
    [BepInProcess(VALHEIM_EXE)]
    public class FastFill : BaseUnityPlugin
    {
        public const string MOD_ID = "madmarty28.FastFill";
        public const string MOD_NAME = "FastFill";
        public const string MOD_VERSION = "1.0.0";
        public const string VALHEIM_EXE = "valheim.exe";

        public const float ORIGINAL_HOLD_REPEAT_INTERVAL = 0.2f;
        public const float DEFAULT_HOLD_REPEAT_INTERVAL = 0.05f;
        private const string CFG_FILE_NAME = MOD_ID + ".cfg";
        private static readonly string CFG_FILE_FULL_PATH = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + CFG_FILE_NAME;

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(MOD_NAME);

        private readonly Harmony harmony = new Harmony(MOD_ID);


        private static ConfigEntry<float> patchedHoldRepeatInterval = null;
        public static float GetPatchedHoldRepeatInterval()
        {
            if (patchedHoldRepeatInterval.Value > ORIGINAL_HOLD_REPEAT_INTERVAL)
            {
                logger.LogWarning("patchedHoldRepeatInterval is too large! Must be between 0 and "
                    + $"{ORIGINAL_HOLD_REPEAT_INTERVAL} but is {patchedHoldRepeatInterval.Value}. Re-setting to max value.");
                patchedHoldRepeatInterval.Value = ORIGINAL_HOLD_REPEAT_INTERVAL;
            }
            else if (patchedHoldRepeatInterval.Value < 0)
            {
                logger.LogWarning("patchedHoldRepeatInterval is too small! Must be between 0 and "
                    + $"{ORIGINAL_HOLD_REPEAT_INTERVAL} but is {patchedHoldRepeatInterval.Value}. Re-setting to max value.");
                patchedHoldRepeatInterval.Value = 0;
            }
            

            return patchedHoldRepeatInterval.Value;
        }

        void Awake()
        {
            Config.SaveOnConfigSet = false;   // Disable saving when binding each following config

            // Binds the configuration, the passed variable will always reflect the current value set
            patchedHoldRepeatInterval = Config.Bind("General", "holdRepeatInterval", DEFAULT_HOLD_REPEAT_INTERVAL,
                "Time in seconds after which 'Use' is executed again while holding the 'Use' key "
                + "(e.g. another piece of wood is inserted into a kiln). Must be between 0 and 0.2!");

            Config.Save();   // Save only once
            Config.SaveOnConfigSet = true;   // Re-enable saving on config changes

            SetupWatcher();
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(BepInEx.Paths.ConfigPath, CFG_FILE_NAME);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(CFG_FILE_FULL_PATH)) return;
            try
            {
                logger.LogDebug("Attempting to reload configuration...");
                Config.Reload();
            }
            catch
            {
                logger.LogError($"There was an issue loading {CFG_FILE_NAME}");
            }
        }
    }

    // The original Interact method is private so nameof does not work here.
    [HarmonyPatch(typeof(Player), "Interact")]
    class Player_Interact_Patch
    {
        static bool Prefix(GameObject go, bool hold, bool alt, ref float ___m_lastHoverInteractTime)
        {
            /*
             * The hold repeat interval of 0.2 seconds is hard-coded in this method so I cannot patch it directly but have to hack it
             * by changing the time of the last interaction to 0.2 - patchedHoldRepeatInterval seconds.
             * This has the disadvantage of not allowing any patchedHoldRepeatInterval values greater than 0.2 because otherwise holding
             * the "Use" button will not do anything anymore.
             */
            if (hold)
            {
                FastFill.logger.LogDebug("Player: Trying to set hold repeat interval from 0.2f to "
                    + FastFill.GetPatchedHoldRepeatInterval() + " seconds.");
                ___m_lastHoverInteractTime -= FastFill.ORIGINAL_HOLD_REPEAT_INTERVAL - FastFill.GetPatchedHoldRepeatInterval();
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    class Fireplace_Interact_Patch
    {
        static bool Prefix(Humanoid user, bool hold, bool alt, ref float ___m_holdRepeatInterval)
        {
            if (hold)
            {
                FastFill.logger.LogDebug("Fireplace: Trying to set hold repeat interval from " + ___m_holdRepeatInterval
                    + " to " + FastFill.GetPatchedHoldRepeatInterval() + " seconds.");
                ___m_holdRepeatInterval = FastFill.GetPatchedHoldRepeatInterval();
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    class Switch_Interact_Patch
    {
        static bool Prefix(Humanoid character, bool hold, bool alt, ref float ___m_holdRepeatInterval)
        {
            if (hold)
            {
                FastFill.logger.LogDebug("Switch: Trying to set hold repeat interval from " + ___m_holdRepeatInterval
                    + " to " + FastFill.GetPatchedHoldRepeatInterval() + " seconds.");
                ___m_holdRepeatInterval = FastFill.GetPatchedHoldRepeatInterval();
            }

            return true;
        }
    }
}
