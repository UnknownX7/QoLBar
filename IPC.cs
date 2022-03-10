using System.Linq;
using Dalamud.Plugin.Ipc;

namespace QoLBar;

public static class IPC
{
    public const int IPCVersion = 1;
    public static readonly ICallGateProvider<string> GetVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<string>("QoLBar.GetVersion");
    public static readonly ICallGateProvider<int> GetIPCVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<int>("QoLBar.GetIPCVersion");
    public static readonly ICallGateProvider<string, object> ImportBarProvider = DalamudApi.PluginInterface.GetIpcProvider<string, object>("QoLBar.ImportBar");
    public static readonly ICallGateProvider<string[]> GetConditionSetsProvider = DalamudApi.PluginInterface.GetIpcProvider<string[]>("QoLBar.GetConditionSets");
    public static readonly ICallGateProvider<int, bool> CheckConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("QoLBar.CheckConditionSet");
    public static readonly ICallGateProvider<int, int, object> MovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, int, object>("QoLBar.MovedConditionSet");
    public static readonly ICallGateProvider<int, object> RemovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, object>("QoLBar.RemovedConditionSet");

    // Penumbra support
    public static bool PenumbraEnabled { get; private set; } = false;
    private static ICallGateSubscriber<int> penumbraApiVersionSubscriber;
    private static ICallGateSubscriber<string, string> penumbraResolveDefaultSubscriber;
    public static int PenumbraApiVersion
    {
        get
        {
            try { return penumbraApiVersionSubscriber.InvokeFunc(); }
            catch { return 0; }
        }
    }

    public static void Initialize()
    {
        GetVersionProvider.RegisterFunc(() => QoLBar.Config.PluginVersion);
        GetIPCVersionProvider.RegisterFunc(() => IPCVersion);
        ImportBarProvider.RegisterAction(import => QoLBar.Plugin.ui.ImportBar(import));
        GetConditionSetsProvider.RegisterFunc(() => QoLBar.Config.CndSetCfgs.Select(s => s.Name).ToArray());
        CheckConditionSetProvider.RegisterFunc(i => i >= 0 && i < QoLBar.Config.CndSetCfgs.Count && ConditionManager.CheckConditionSet(i));

        penumbraApiVersionSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<int>("Penumbra.ApiVersion");

        if (PenumbraApiVersion == 3)
        {
            penumbraResolveDefaultSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<string, string>("Penumbra.ResolveDefaultPath");
            PenumbraEnabled = true;
        }
    }

    public static string ResolvePenumbraPath(string path)
    {
        try { return penumbraResolveDefaultSubscriber.InvokeFunc(path); }
        catch { return path; }
    }

    public static void Dispose()
    {
        GetVersionProvider.UnregisterFunc();
        GetIPCVersionProvider.UnregisterFunc();
        ImportBarProvider.UnregisterAction();
        GetConditionSetsProvider.UnregisterFunc();
        CheckConditionSetProvider.UnregisterFunc();
    }
}