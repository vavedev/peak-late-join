using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace PeakLateJoin
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "vavedev.PeakLateJoin";
        public const string PLUGIN_NAME = "Peak Late Join";
        public const string PLUGIN_VERSION = "1.3.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"Plugin {PLUGIN_NAME} v{PLUGIN_VERSION} loaded!");

            // Attach late join handler
            gameObject.AddComponent<LateJoinHandler>().InitLogger(Log);
        }
    }
}