using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly Queue<string> commandQueue = new Queue<string>();
        private static readonly Queue<string> macroQueue = new Queue<string>();

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

        public static IntPtr textActiveBoolPtr = IntPtr.Zero;
        public static unsafe bool IsGameTextInputActive => textActiveBoolPtr != IntPtr.Zero && *(bool*)textActiveBoolPtr;
        public static unsafe bool IsMacroRunning => *(int*)(raptureShellModule + 0x2C0) >= 0;

        // Command Execution
        public delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
        public delegate IntPtr GetModuleDelegate(IntPtr basePtr);
        public static ProcessChatBoxDelegate ProcessChatBox;
        public static IntPtr uiModule = IntPtr.Zero;

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
            try { textActiveBoolPtr = *(IntPtr*)(QoLBar.Interface.Framework.Gui.GetBaseUIObject() + 0x28) + 0x188E; }
            catch { PluginLog.Error("Failed loading textActiveBoolPtr"); }

            try
            {
                ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(QoLBar.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                uiModule = QoLBar.Interface.Framework.Gui.GetUIModule();

                try
                {
                    ExecuteMacroHook = new Hook<ExecuteMacroDelegate>(QoLBar.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28"), new ExecuteMacroDelegate(ExecuteMacroDetour));

                    numCopiedMacroLinesPtr = QoLBar.Interface.TargetModuleScanner.ScanText("49 8D 5E 70 BF ?? 00 00 00") + 0x5;
                    numExecutedMacroLinesPtr = QoLBar.Interface.TargetModuleScanner.ScanText("41 83 F8 ?? 0F 8D ?? ?? ?? ?? 49 6B C8 68") + 0x3;

                    var vtbl = (IntPtr*)(*(IntPtr*)uiModule);
                    var GetRaptureShellModule = Marshal.GetDelegateForFunctionPointer<GetModuleDelegate>(*(vtbl + 9)); // Client__UI__UIModule_GetRaptureShellModule / vf9
                    var GetRaptureMacroModule = Marshal.GetDelegateForFunctionPointer<GetModuleDelegate>(*(vtbl + 12)); // Client__UI__UIModule_GetRaptureMacroModule / vf12

                    raptureShellModule = GetRaptureShellModule(uiModule);
                    raptureMacroModule = GetRaptureMacroModule(uiModule);

                    ExecuteMacroHook.Enable();
                }
                catch { PluginLog.Error("Failed loading ExecuteMacro"); }
            }
            catch { PluginLog.Error("Failed loading ExecuteCommand"); }
        }

        public static void ExecuteMacroDetour(IntPtr raptureShellModule, IntPtr macro)
        {
            NumCopiedMacroLines = Macro.numLines;
            NumExecutedMacroLines = Macro.numLines;
            ExecuteMacroHook.Original(raptureShellModule, macro);
        }

        public static void ReadyCommand()
        {
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
            RunCommandQueue(); // Attempt to run immediately
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
                                    if (0 <= macro && macro < 200)
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
                        ExecuteCommand(command);
                }
            }
        }

        public static void ExecuteCommand(string command)
        {
            var stringPtr = IntPtr.Zero;

            try
            {
                stringPtr = Marshal.AllocHGlobal(UTF8String.size);
                using var str = new UTF8String(stringPtr, command);
                Marshal.StructureToPtr(str, stringPtr, false);

                ProcessChatBox(uiModule, stringPtr, IntPtr.Zero, 0);
            }
            catch { QoLBar.PrintError("Failed injecting command"); }

            Marshal.FreeHGlobal(stringPtr);
        }

        private static void CreateAndExecuteMacro()
        {
            var macroPtr = IntPtr.Zero;

            try
            {
                macroPtr = Marshal.AllocHGlobal(ExtendedMacro.size);
                using var macro = new ExtendedMacro(macroPtr, string.Empty, macroQueue.ToArray());
                Marshal.StructureToPtr(macro, macroPtr, false);

                NumCopiedMacroLines = ExtendedMacro.numLines;
                NumExecutedMacroLines = ExtendedMacro.numLines;

                ExecuteMacroHook.Original(raptureShellModule, macroPtr);

                NumCopiedMacroLines = Macro.numLines;
            }
            catch { QoLBar.PrintError("Failed injecting macro"); }

            Marshal.FreeHGlobal(macroPtr);
            macroQueue.Clear();
        }

        public static unsafe bool IsWeaponDrawn(PlayerCharacter player) => (*(byte*)(player.Address + 0x19A0) & 0b100) > 0;
    }
}