using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using QoLBar.Structures;

namespace QoLBar
{
    public static class Game
    {
        private const int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more

        private static bool commandReady = true;
        private static bool macroMode = false;
        private static float chatQueueTimer = 0;
        private static readonly Queue<string> commandQueue = new();
        private static readonly Queue<string> macroQueue = new();
        private static readonly Queue<string> chatQueue = new();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        public static bool IsGameFocused
        {
            get
            {
                var activatedHandle = GetForegroundWindow();
                if (activatedHandle == IntPtr.Zero)
                    return false;

                var procId = Process.GetCurrentProcess().Id;
                GetWindowThreadProcessId(activatedHandle, out var activeProcId);

                return activeProcId == procId;
            }
        }

        public static unsafe DateTimeOffset EorzeaTime => DateTimeOffset.FromUnixTimeSeconds(*(long*)(QoLBar.Interface.Framework.Address.BaseAddress + 0x1608));

        public static Wrappers.UIModule uiModule;

        public static IntPtr textActiveBoolPtr = IntPtr.Zero;
        public static unsafe bool IsGameTextInputActive => textActiveBoolPtr != IntPtr.Zero && *(bool*)textActiveBoolPtr;
        public static unsafe bool IsMacroRunning => *(int*)(raptureShellModule + 0x2C0) >= 0;

        public static IntPtr agentModule = IntPtr.Zero;

        public static IntPtr addonConfig = IntPtr.Zero;
        public static unsafe byte CurrentHUDLayout => *(byte*)(*(IntPtr*)(addonConfig + 0x50) + 0x59E8);

        // Command Execution
        public delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
        public static ProcessChatBoxDelegate ProcessChatBox;

        public delegate int GetCommandHandlerDelegate(IntPtr raptureShellModule, IntPtr message, IntPtr unused);
        public static GetCommandHandlerDelegate GetCommandHandler;

        // Macro Execution
        public delegate void ExecuteMacroDelegate(IntPtr raptureShellModule, IntPtr macro);
        public static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
        public static IntPtr raptureShellModule = IntPtr.Zero;
        public static IntPtr raptureMacroModule = IntPtr.Zero;

        public static IntPtr numCopiedMacroLinesPtr = IntPtr.Zero;
        public static unsafe byte NumCopiedMacroLines
        {
            get => *(byte*)numCopiedMacroLinesPtr;
            set
            {
                if (numCopiedMacroLinesPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(numCopiedMacroLinesPtr, new[] {value});
            }
        }

        public static IntPtr numExecutedMacroLinesPtr = IntPtr.Zero;
        public static unsafe byte NumExecutedMacroLines
        {
            get => *(byte*)numExecutedMacroLinesPtr;
            set
            {
                if (numExecutedMacroLinesPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(numExecutedMacroLinesPtr, new[] {value});
            }
        }

        public static unsafe void Initialize()
        {
            uiModule = new Wrappers.UIModule(QoLBar.Interface.Framework.Gui.GetUIModule());

            raptureShellModule = uiModule.GetRaptureShellModule();
            raptureMacroModule = uiModule.GetRaptureMacroModule();
            addonConfig = uiModule.GetAddonConfig();
            agentModule = uiModule.GetAgentModule();

            try { textActiveBoolPtr = *(IntPtr*)(QoLBar.Interface.Framework.Gui.GetBaseUIObject() + 0x28) + 0x188E; }
            catch { PluginLog.Error("Failed loading textActiveBoolPtr"); }

            try
            {
                GetCommandHandler = Marshal.GetDelegateForFunctionPointer<GetCommandHandlerDelegate>(QoLBar.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 83 F8 FE 74 1E"));

                try
                {
                    ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(QoLBar.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                }
                catch { PluginLog.Error("Failed loading ExecuteCommand"); }

                try
                {
                    ExecuteMacroHook = new Hook<ExecuteMacroDelegate>(QoLBar.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28"), new ExecuteMacroDelegate(ExecuteMacroDetour));

                    numCopiedMacroLinesPtr = QoLBar.Interface.TargetModuleScanner.ScanText("49 8D 5E 70 BF ?? 00 00 00") + 0x5;
                    numExecutedMacroLinesPtr = QoLBar.Interface.TargetModuleScanner.ScanText("41 83 F8 ?? 0F 8D ?? ?? ?? ?? 49 6B C8 68") + 0x3;

                    ExecuteMacroHook.Enable();
                }
                catch { PluginLog.Error("Failed loading ExecuteMacro"); }
            }
            catch { PluginLog.Error("Failed loading plugin"); }
        }

        public static unsafe IntPtr GetAgentByInternalID(int id) => *(IntPtr*)(agentModule + 0x20 + id * 0x8); // Client::UI::Agent::AgentModule_GetAgentByInternalID, not going to try and sig a function this small

        public static void DEBUG_FindAgent(long agent)
        {
            var found = false;
            for (int i = 0; i < 800; i++) // Dunno how many there are
            {
                if (GetAgentByInternalID(i).ToInt64() != agent) continue;
                QoLBar.PrintEcho(i.ToString());
                found = true;
                break;
            }

            if (!found)
                QoLBar.PrintEcho($"Failed to find agent {agent:X}");
        }


        public static void ExecuteMacroDetour(IntPtr raptureShellModule, IntPtr macro)
        {
            NumCopiedMacroLines = Macro.numLines;
            NumExecutedMacroLines = Macro.numLines;
            ExecuteMacroHook.Original(raptureShellModule, macro);
        }

        public static void ReadyCommand()
        {
            if (chatQueueTimer > 0 && (chatQueueTimer -= ImGuiNET.ImGui.GetIO().DeltaTime) <= 0 && chatQueue.Count > 0)
                ExecuteCommand(chatQueue.Dequeue(), true);

            commandReady = true;
            RunCommandQueue();

            if (!commandReady) return;

            macroMode = false;

            // If the user forgot to close off the macro with "//m" then try to execute it now
            if (macroQueue.Count > 0)
                CreateAndExecuteMacro();
        }

        public static void QueueCommand(string command)
        {
            foreach (var c in command.Split('\n'))
            {
                if (!string.IsNullOrEmpty(c))
                    commandQueue.Enqueue(c.Substring(0, Math.Min(c.Length, maxCommandLength)));
            }
        }

        private static void RunCommandQueue()
        {
            while (commandQueue.Count > 0 && commandReady)
            {
                commandReady = false;
                var command = commandQueue.Dequeue();

                if (command.StartsWith("//"))
                {
                    command = command.Substring(2).ToLower();
                    switch (command[0])
                    {
                        case 'm': // Execute Macro
                            try
                            {
                                if (int.TryParse(command.Substring(1), out var macro))
                                {
                                    if (macro is >= 0 and < 200)
                                        ExecuteMacroHook.Original(raptureShellModule, raptureMacroModule + 0x58 + (Macro.size * macro));
                                    else
                                        QoLBar.PrintError("Invalid macro. Usage: \"//m0\" for individual macro #0, \"//m100\" for shared macro #0, valid up to 199.");
                                }
                                else
                                {
                                    if (macroMode)
                                    {
                                        macroMode = false;
                                        CreateAndExecuteMacro();
                                    }
                                    else
                                    {
                                        macroMode = true;
                                        commandReady = true;
                                    }
                                }
                            }
                            catch { QoLBar.PrintError("Failed running macro"); }
                            break;
                        case ' ': // Comment
                            commandReady = true;
                            break;
                    }
                }
                else
                {
                    if (macroMode)
                    {
                        if (macroQueue.Count < ExtendedMacro.numLines)
                        {
                            macroQueue.Enqueue(command + "\0");
                            commandReady = true;
                        }
                        else
                            QoLBar.PrintError("Failed to add command to macro, capacity reached. Please close off the macro with another \"//m\" if you didn't intend to do this.");
                    }
                    else
                        ExecuteCommand(command, IsChatSendCommand(command));
                }
            }
        }

        public static void ExecuteCommand(string command, bool chat = false)
        {
            var stringPtr = IntPtr.Zero;

            try
            {
                stringPtr = Marshal.AllocHGlobal(UTF8String.size);
                using var str = new UTF8String(stringPtr, command);
                Marshal.StructureToPtr(str, stringPtr, false);

                if (!chat || chatQueueTimer <= 0)
                {
                    if (chat)
                        chatQueueTimer = 1f / 6f;

                    ProcessChatBox(uiModule.Address, stringPtr, IntPtr.Zero, 0);
                }
                else
                    chatQueue.Enqueue(command);
            }
            catch { QoLBar.PrintError("Failed injecting command"); }

            Marshal.FreeHGlobal(stringPtr);
        }

        public static bool IsChatSendCommand(string command)
        {
            var split = command.IndexOf(' ');
            if (split < 1) return split == 0 || !command.StartsWith("/");

            var handler = 0;
            var stringPtr = IntPtr.Zero;

            try
            {
                stringPtr = Marshal.AllocHGlobal(UTF8String.size);
                using var str = new UTF8String(stringPtr, command.Substring(0, split));
                Marshal.StructureToPtr(str, stringPtr, false);
                handler = GetCommandHandler(raptureShellModule, stringPtr, IntPtr.Zero);
            }
            catch { }

            Marshal.FreeHGlobal(stringPtr);

            // TODO probably swap to using the TextCommand.csv and checking 2nd to last column since it appears to be flags of some sort (all of the chat senders including echo are 1021, say is 1023)
            return handler switch
            {
                8 or (>= 13 and <= 20) or (>= 91 and <= 119 and not 116) => true,
                _ => false,
            };
        }

        private static void CreateAndExecuteMacro()
        {
            var macroPtr = IntPtr.Zero;

            try
            {
                var count = (byte)Math.Max(Macro.numLines, macroQueue.Count);
                if (count > Macro.numLines && macroQueue.Any(IsChatSendCommand))
                {
                    QoLBar.PrintError("Macros using more than 15 lines do not support chat message commands!");
                    throw new InvalidOperationException();
                }

                macroPtr = Marshal.AllocHGlobal(ExtendedMacro.size);
                using var macro = new ExtendedMacro(macroPtr, string.Empty, macroQueue.ToArray());
                Marshal.StructureToPtr(macro, macroPtr, false);

                NumCopiedMacroLines = count;
                NumExecutedMacroLines = count;

                ExecuteMacroHook.Original(raptureShellModule, macroPtr);

                NumCopiedMacroLines = Macro.numLines;
            }
            catch { QoLBar.PrintError("Failed injecting macro"); }

            Marshal.FreeHGlobal(macroPtr);
            macroQueue.Clear();
        }

        public static unsafe bool IsWeaponDrawn(PlayerCharacter player) => (*(byte*)(player.Address + 0x19A0) & 0b100) > 0;

        public static void Dispose()
        {
            ExecuteMacroHook?.Dispose();
            NumCopiedMacroLines = 15;
            NumExecutedMacroLines = 15;
        }
    }
}
