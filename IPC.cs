using Dalamud.Plugin.Ipc;

namespace QoLBar
{
    public static class IPC
    {
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
    }
}