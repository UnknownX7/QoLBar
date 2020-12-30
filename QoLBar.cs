using System;
using System.Numerics;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Game.Chat;

// Disclaimer: I have no idea what I'm doing.
namespace QoLBar
{
    public class BarConfig
    {
        public string Title = string.Empty;
        public List<Shortcut> ShortcutList = new List<Shortcut>();
        public bool Hidden = false;
        public enum VisibilityMode
        {
            Slide,
            Immediate,
            Always
        }
        public VisibilityMode Visibility = VisibilityMode.Always;
        public enum BarAlign
        {
            LeftOrTop,
            Center,
            RightOrBottom
        }
        public BarAlign Alignment = BarAlign.Center;
        public enum BarDock
        {
            Top,
            Left,
            Bottom,
            Right,
            UndockedH,
            UndockedV
        }
        public BarDock DockSide = BarDock.Bottom;
        public int ButtonWidth = 100;
        public bool AutoButtonWidth = false;
        public bool HideAdd = false;
        public Vector2 Position = new Vector2();
        public bool LockedPosition = false;
        public float Scale = 1.0f;
    }

    public class Shortcut
    {
        public string Name = string.Empty;
        public enum ShortcutType
        {
            Single,
            Multiline,
            Category
        }
        public ShortcutType Type = ShortcutType.Single;
        public string Command = string.Empty;
        public List<Shortcut> SubList;
        public bool HideAdd = false;
        public int CategoryWidth = 140;
    }

    public class QoLBar : IDalamudPlugin
    {
        public DalamudPluginInterface pluginInterface;
        private Configuration config;
        private PluginUI ui;

        public string Name => "QoL Bar";

        public void Initialize(DalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;

            config = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
            config.Initialize(pluginInterface);

            ui = new PluginUI(this, config);
            pluginInterface.UiBuilder.OnOpenConfigUi += ToggleConfig;
            pluginInterface.UiBuilder.OnBuildUi += ui.Draw;
            pluginInterface.ClientState.OnLogin += InitCommands;
        }

        public void ToggleConfig(object sender = null, EventArgs e = null) => ui.ToggleConfig();

        // I'm too dumb to do any of this so its (almost) all taken from here https://git.sr.ht/~jkcclemens/CCMM/tree/master/Custom%20Commands%20and%20Macro%20Macros/GameFunctions.cs
        #region Chat Injection
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private delegate void EasierProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

        private GetUIModuleDelegate GetUIModule;
        private EasierProcessChatBoxDelegate _EasierProcessChatBox;

        private IntPtr uiModulePtr;

        private void InitCommands(object sender = null, EventArgs e = null)
        {
            try
            {
                var getUIModulePtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
                var easierProcessChatBoxPtr = pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
                uiModulePtr = pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");

                GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);
                _EasierProcessChatBox = Marshal.GetDelegateForFunctionPointer<EasierProcessChatBoxDelegate>(easierProcessChatBoxPtr);
            }
            catch
            {
                var chat = pluginInterface.Framework.Gui.Chat;
                chat.PrintChat(new XivChatEntry { MessageBytes = Encoding.UTF8.GetBytes("[QoLBar] Error with loading signatures") });
            }
        }

        public void ExecuteCommand(string command)
        {
            try
            {
                if (uiModulePtr == null || uiModulePtr == IntPtr.Zero)
                    InitCommands();

                var uiModule = GetUIModule(Marshal.ReadIntPtr(uiModulePtr));

                if (uiModule == IntPtr.Zero)
                {
                    throw new ApplicationException("uiModule was null");
                }

                var bytes = Encoding.UTF8.GetBytes(command);

                var mem1 = Marshal.AllocHGlobal(400);
                var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

                Marshal.Copy(bytes, 0, mem2, bytes.Length);
                Marshal.WriteByte(mem2 + bytes.Length, 0);
                Marshal.WriteInt64(mem1, mem2.ToInt64());
                Marshal.WriteInt64(mem1 + 8, 64);
                Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
                Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

                _EasierProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

                Marshal.FreeHGlobal(mem1);
                Marshal.FreeHGlobal(mem2);
            }
            catch
            {
                var chat = pluginInterface.Framework.Gui.Chat;
                chat.PrintChat(new XivChatEntry { MessageBytes = Encoding.UTF8.GetBytes("[QoLBar] Error with injecting command") });
            }
        }
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            pluginInterface.SavePluginConfig(config);

            pluginInterface.UiBuilder.OnOpenConfigUi -= ToggleConfig;
            pluginInterface.UiBuilder.OnBuildUi -= ui.Draw;
            pluginInterface.ClientState.OnLogin -= InitCommands;

            pluginInterface.Dispose();

            ui.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
