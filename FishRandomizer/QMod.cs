using System.Reflection;
using HarmonyLib;
using QModManager.API.ModLoading;
using Logger = QModManager.Utility.Logger;

namespace FishRandomizer
{
    [QModCore]
        public static class QMod
        {
            [QModPatch]
            public static void Patch()
            {
                var assembly = Assembly.GetExecutingAssembly();
                var modName = ($"Nyxbis_{assembly.GetName().Name}");
                Logger.Log(Logger.Level.Info, $"Patching {modName}");
                Harmony harmony = new Harmony(modName);
                harmony.PatchAll(assembly);
                Logger.Log(Logger.Level.Info, "Patched successfully!");
            }
        }
}