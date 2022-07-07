using System.Linq;
using Dalamud.Plugin.Ipc;

namespace QoLBar;

public static class IPC
{
    public const int IPCVersion = 1;
    public static readonly ICallGateProvider<object> InitializedProvider = DalamudApi.PluginInterface.GetIpcProvider<object>("QoLBar.Initialized");
    public static readonly ICallGateProvider<object> DisposedProvider = DalamudApi.PluginInterface.GetIpcProvider<object>("QoLBar.Disposed");
    public static readonly ICallGateProvider<string> GetVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<string>("QoLBar.GetVersion");
    public static readonly ICallGateProvider<int> GetIPCVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<int>("QoLBar.GetIPCVersion");
    public static readonly ICallGateProvider<string, object> ImportBarProvider = DalamudApi.PluginInterface.GetIpcProvider<string, object>("QoLBar.ImportBar");
    public static readonly ICallGateProvider<string[]> GetConditionSetsProvider = DalamudApi.PluginInterface.GetIpcProvider<string[]>("QoLBar.GetConditionSets");
    public static readonly ICallGateProvider<int, bool> CheckConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("QoLBar.CheckConditionSet");
    public static readonly ICallGateProvider<int, int, object> MovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, int, object>("QoLBar.MovedConditionSet");
    public static readonly ICallGateProvider<int, object> RemovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, object>("QoLBar.RemovedConditionSet");

    // Penumbra support
    public static bool PenumbraEnabled { get; private set; } = false;
    private static ICallGateSubscriber<object> penumbraInitializedSubscriber;
    private static ICallGateSubscriber<object> penumbraDisposedSubscriber;
    private static ICallGateSubscriber<(int Breaking, int Features)> penumbraApiVersionsSubscriber;
    private static ICallGateSubscriber<string, string> penumbraResolveDefaultSubscriber;
    public static int PenumbraApiVersion
    {
        get
        {
            try { return penumbraApiVersionsSubscriber.InvokeFunc().Breaking; }
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

        penumbraInitializedSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
        penumbraDisposedSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<object>("Penumbra.Disposed");
        penumbraApiVersionsSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersions");
        penumbraResolveDefaultSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<string, string>("Penumbra.ResolveDefaultPath");

        penumbraInitializedSubscriber.Subscribe(EnablePenumbraApi);
        penumbraDisposedSubscriber.Subscribe(DisablePenumbraApi);

        // Check if Penumbra is already available
        EnablePenumbraApi();
    }

    public static void EnablePenumbraApi()
    {
        if (PenumbraApiVersion != 4) return;

        PenumbraEnabled = true;

        if (QoLBar.Config.UsePenumbra)
            DalamudApi.Framework.RunOnTick(() => QoLBar.CleanTextures(false));
    }

    public static void DisablePenumbraApi()
    {
        if (!PenumbraEnabled) return;

        PenumbraEnabled = false;

        if (QoLBar.Config.UsePenumbra)
            QoLBar.CleanTextures(false);
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

        penumbraInitializedSubscriber.Unsubscribe(EnablePenumbraApi);
        penumbraDisposedSubscriber.Unsubscribe(DisablePenumbraApi);
    }
}
