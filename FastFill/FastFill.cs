using System;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
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
        public const string MOD_VERSION = "1.0.1";
        public const string VALHEIM_EXE = "valheim.exe";

        public const string HOLD_REPEAT_INTERVAL_NAME = "holdRepeatInterval";
        public const float ORIGINAL_HOLD_REPEAT_INTERVAL = 0.2f;
        public const float DEFAULT_HOLD_REPEAT_INTERVAL = 0.05f;
        private static readonly AcceptableValueRange<float> VALUE_RANGE = new AcceptableValueRange<float>(0.0f, 0.2f);
        private const string CFG_FILE_NAME = MOD_ID + ".cfg";
        private static readonly string CFG_FILE_FULL_PATH = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + CFG_FILE_NAME;

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(MOD_NAME);

        private readonly Harmony harmony = new Harmony(MOD_ID);


        private static ConfigEntry<float> patchedHoldRepeatInterval = null;
        public static float GetPatchedHoldRepeatInterval(Humanoid user)
        {
            // I did not know how to check the value when it is set
            /* v1.0.2: I found the AcceptableValueRange class and the matching
             * ConfigDescription constructor and Config.Bind method. Should make the min/max checks BepInEx's problem.
            if (patchedHoldRepeatInterval.Value > VALUE_RANGE.MaxValue)
            {
                displayChatMessage($"[{MOD_NAME}] {HOLD_REPEAT_INTERVAL_NAME} too large! "
                        + $"Setting back to max value ({VALUE_RANGE.MaxValue} seconds).");
                patchedHoldRepeatInterval.Value = VALUE_RANGE.MaxValue;
            }
            else if (patchedHoldRepeatInterval.Value < 0)
            {
                displayChatMessage($"[{MOD_NAME}] {HOLD_REPEAT_INTERVAL_NAME} too small! "
                    + $"Setting back to {VALUE_RANGE.MinValue} seconds.");
                patchedHoldRepeatInterval.Value = VALUE_RANGE.MinValue;
            }
            */

            return patchedHoldRepeatInterval.Value;
        }

        private static void displayChatMessage(string text)
        {
            if (global::Chat.instance)
            {
                Chat chat = global::Chat.instance;
                FieldInfo chatHideTimerField = typeof(Chat).GetField("m_hideTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                chat.AddString(text);
                // reset hide timer of chat window so it will be visible again for a short time
                chatHideTimerField.SetValue(chat, 0.0f);
            }
            // logger.LogDebug(text);
        }

        /**
         * <summary>
         * Base patch method for most overriding classes that only differs in the class name being printed in the debug log message.
         * </summary>
         * 
         * <param name="patchedClass">The type of the overridden class</param>
         * <param name="character">The character performing the interaction with an Interactable</param>
         * <param name="hold">true if the "Use" key is being held, false otherwise</param>
         * <param name="___m_holdRepeatInterval">Member "m_holdRepeatInterval" from the original Player object</param>
         * 
         * <returns>Generally: true if the original method is to be executed after the patch, false otherwise. This specific method always returns true.</returns>
         */
        public static bool Prefix(string patchedClass, Humanoid character, bool hold, ref float ___m_holdRepeatInterval)
        {
            if (hold)
            {
                FastFill.logger.LogDebug($"{patchedClass}: Trying to set hold repeat interval from {___m_holdRepeatInterval}"
                    + $" to {FastFill.GetPatchedHoldRepeatInterval(character)} seconds.");
                ___m_holdRepeatInterval = FastFill.GetPatchedHoldRepeatInterval(character);
            }

            // true: executes the original method afterwards
            // false: skips the original method
            return true;
        }

        void Awake()
        {
            // Seems a bit overkill for changing a single value.
            // Re-enable if, in future versions, more config entries are added.
            // Config.SaveOnConfigSet = false;   // Disable saving when binding each following config

            // Binds the configuration, the passed variable will always reflect the current value set
            string description = "Time in seconds after which another item is inserted while holding the 'Use' key\n"
                + "(e.g. another piece of wood is inserted into a kiln). Must be between 0 and 0.2 (both inclusive)\n"
                + "where 0 will only insert one single item no matter how long the player holds 'Use'.";
            ConfigDescription configDescription = new ConfigDescription(description, VALUE_RANGE, null);
            patchedHoldRepeatInterval = Config.Bind("General", HOLD_REPEAT_INTERVAL_NAME, DEFAULT_HOLD_REPEAT_INTERVAL, configDescription);

            // Config.Save();   // Save only once

            // Config.SaveOnConfigSet = true;   // Re-enable saving on config changes

            SetupWatcher();
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        /**
         * <summary>Watches for config file changes and applies them to the mod while running the game.</summary>
         */
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
        static bool Prefix(bool hold, ref float ___m_lastHoverInteractTime, Player __instance)
        {
            /*
             * The hold repeat interval of 0.2 seconds is hard-coded in this method so I cannot patch it directly (i.e. cannot use FastFill.Prefix)
             * but have to hack it by changing the time of the last interaction to 0.2 - GetPatchedHoldRepeatInterval() seconds.
             * This has the disadvantage of not allowing any patchedHoldRepeatInterval values greater than 0.2 because otherwise holding
             * the "Use" button will not do anything anymore. Then again, it is called FastFill, not ExtraSlowFill :P
             */
            if (hold)
            {
                FastFill.logger.LogDebug($"Player: Trying to set hold repeat interval from {FastFill.ORIGINAL_HOLD_REPEAT_INTERVAL} to "
                    + $"{FastFill.GetPatchedHoldRepeatInterval(__instance)} seconds.");
                ___m_lastHoverInteractTime -= FastFill.ORIGINAL_HOLD_REPEAT_INTERVAL - FastFill.GetPatchedHoldRepeatInterval(__instance);
            }

            return true;
        }
    }

    // any kind of fuelled fire, incl. all fuelled light sources, ovens and bathtubs,
    // but not kilns, smelters or blast furnaces
    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    class Fireplace_Interact_Patch
    {
        static bool Prefix(Humanoid user, bool hold, ref float ___m_holdRepeatInterval)
        {
            return FastFill.Prefix(nameof(Fireplace), user, hold, ref ___m_holdRepeatInterval);
        }
    }

    // Some objects like kilns use Switch objects for interactions instead of directly implementing Interactable,
    // especially when there is more than one slot to fill, e.g. smelters or stone ovens.
    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    class Switch_Interact_Patch
    {
        static bool Prefix(Humanoid character, bool hold, ref float ___m_holdRepeatInterval)
        {
            return FastFill.Prefix(nameof(Switch), character, hold, ref ___m_holdRepeatInterval);
        }
    }

    // Ballistas
    [HarmonyPatch(typeof(Turret), nameof(Turret.Interact))]
    class Turret_interact_Patch
    {
        static bool Prefix(Humanoid character, bool hold, ref float ___m_holdRepeatInterval)
        {
            return FastFill.Prefix(nameof(Turret), character, hold, ref ___m_holdRepeatInterval);
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Interact))]
    class CookingStation_Interact_Patch
    {
        static bool Prefix(Humanoid user, ref bool hold, ref CookingStation __instance)
        {
            return true;
        }
    }
}
