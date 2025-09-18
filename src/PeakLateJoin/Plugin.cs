using BepInEx;
using BepInEx.Logging;

namespace PeakLateJoin;


[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public partial class Plugin : BaseUnityPlugin
{

    public const string PLUGIN_GUID = "vavedev.PeakNoiseSuppression";
    public const string PLUGIN_NAME = "Peak Noise Suppression";
    public const string PLUGIN_VERSION = "1.0.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");
    }
}
