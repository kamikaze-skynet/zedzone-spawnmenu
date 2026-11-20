using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace SpawnMenu
{
    [BepInPlugin(Guid, "Spawn Menu", Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "girthy_mods.zedzone.spawnmenu";
        public const string Version = "1.4.1";

        internal static ConfigEntry<KeyCode> MenuKey;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<bool> ShowHiddenItems;
        internal static ConfigEntry<bool> FillMagazines;
        internal static ConfigEntry<int> MaxPerClick;
        internal static ConfigEntry<bool> VerboseLog;

        public override void Load()
        {
            MenuKey = Config.Bind("Keys", "OpenMenuKey", KeyCode.Insert,
                "Opens and closes the spawn menu.");
            UiScale = Config.Bind("Display", "UiScale", 1f,
                "Size of the menu. 1 = sized for the screen height, 1.25 = a quarter bigger.");
            ShowHiddenItems = Config.Bind("Items", "ListGameHiddenItems", true,
                "List items the game flags as hidden. The game flags about 300 normal items this way (Iron Plates among them), so leave this on unless the list has items you never want to see.");
            FillMagazines = Config.Bind("Kits", "FillMagazines", true,
                "Kits with a 'fillmags' line load their magazines from the ammo in the kit.");
            MaxPerClick = Config.Bind("Items", "MaxPerClick", 100,
                "Most separate items one click can spawn (guns, clothes and other items that don't stack). Stops a typo from flooding the world.");
            VerboseLog = Config.Bind("Debug", "VerboseLog", false, "Extra detail in BepInEx\\SpawnMenu.log.");

            SLog.Verbose = VerboseLog.Value;
            SLog.Open(Log, Version);
            SLog.Info($"Config: MenuKey={MenuKey.Value} UiScale={UiScale.Value} ShowHiddenItems={ShowHiddenItems.Value} FillMagazines={FillMagazines.Value} MaxPerClick={MaxPerClick.Value}");

            Kits.EnsureFile();

            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<MenuBehaviour>();
                var go = new GameObject("SpawnMenu");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<MenuBehaviour>();
            }
            catch (Exception e) { SLog.Error("Couldn't create the menu object", e); }

            Patches.Apply(new Harmony(Guid));
            SLog.Info($"Spawn Menu {Version} loaded. Press {MenuKey.Value} in game.");
        }
    }

    public class MenuBehaviour : MonoBehaviour
    {
        public MenuBehaviour(IntPtr ptr) : base(ptr) { }

        void Update()
        {
            try { Menu.Tick(); }
            catch (Exception e) { SLog.Error("Menu update failed", e); Menu.Close(); }
        }

        void OnGUI()
        {
            try { Menu.Draw(); }
            catch (Exception e) { SLog.Error("Menu draw failed", e); Menu.Close(); }
        }
    }
}
