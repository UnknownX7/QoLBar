using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using QoLBar.Structures;

namespace QoLBar;

public static unsafe class Game
{
    private const int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more

    private static bool commandReady = true;
    private static bool macroMode = false;
    private static float chatQueueTimer = 0;
    private static readonly Queue<string> commandQueue = new();
    private static readonly Queue<string> macroQueue = new();
    private static readonly Queue<string> chatQueue = new();
    private static uint retryItem = 0;

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    public static bool IsGameFocused
    {
        get
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
                return false;

            var procId = Environment.ProcessId;
            _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);

            return activeProcId == procId;
        }
    }

    public static DateTimeOffset EorzeaTime => DateTimeOffset.FromUnixTimeSeconds(Framework.Instance()->EorzeaTime);

    public static UIModule* uiModule;

    public static IntPtr isTextInputActivePtr = IntPtr.Zero;
    public static bool IsGameTextInputActive => isTextInputActivePtr != IntPtr.Zero && *(bool*)isTextInputActivePtr;
    public static bool IsMacroRunning => *(int*)((IntPtr)raptureShellModule + 0x2C0) >= 0;

    public static AgentModule* agentModule;
    public static IntPtr itemContextMenuAgent;

    public static IntPtr addonConfig;
    private static delegate* unmanaged<IntPtr, byte> getHUDLayout;
    public static byte CurrentHUDLayout => getHUDLayout(addonConfig);

    // Command Execution
    public delegate void ProcessChatBoxDelegate(UIModule* uiModule, IntPtr message, IntPtr unused, byte a4);
    public static ProcessChatBoxDelegate ProcessChatBox;

    public delegate int GetCommandHandlerDelegate(RaptureShellModule* raptureShellModule, IntPtr message, IntPtr unused);
    public static GetCommandHandlerDelegate GetCommandHandler;

    // Macro Execution
    public delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, IntPtr macro);
    public static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
    public static RaptureShellModule* raptureShellModule;
    public static RaptureMacroModule* raptureMacroModule;

    public static IntPtr numCopiedMacroLinesPtr = IntPtr.Zero;
    public static byte NumCopiedMacroLines
    {
        get => *(byte*)numCopiedMacroLinesPtr;
        set
        {
            if (numCopiedMacroLinesPtr != IntPtr.Zero)
                SafeMemory.WriteBytes(numCopiedMacroLinesPtr, new[] {value});
        }
    }

    public static IntPtr numExecutedMacroLinesPtr = IntPtr.Zero;
    public static byte NumExecutedMacroLines
    {
        get => *(byte*)numExecutedMacroLinesPtr;
        set
        {
            if (numExecutedMacroLinesPtr != IntPtr.Zero)
                SafeMemory.WriteBytes(numExecutedMacroLinesPtr, new[] {value});
        }
    }

    // Misc
    private const int aetherCompassID = 2_001_886;
    private static Dictionary<uint, string> usables;
    private static delegate* unmanaged<IntPtr, uint, uint, uint, short, void> useItem;
    private static ActionManager* actionManager;
    private static RaptureAtkUnitManager* raptureAtkUnitManager;
    private static delegate* unmanaged<uint, uint, uint> getActionID;

    public static void Initialize()
    {
        uiModule = Framework.Instance()->GetUiModule();

        raptureShellModule = uiModule->GetRaptureShellModule();
        raptureMacroModule = uiModule->GetRaptureMacroModule();
        addonConfig = ((delegate* unmanaged<UIModule*, IntPtr>)uiModule->vfunc[19])(uiModule);
        agentModule = uiModule->GetAgentModule();
        actionManager = ActionManager.Instance();
        raptureAtkUnitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        try
        {
            GetCommandHandler = Marshal.GetDelegateForFunctionPointer<GetCommandHandlerDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 F8 FE 74 1E"));
            getActionID = (delegate* unmanaged<uint, uint, uint>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 8B 4B 2C");

            try { isTextInputActivePtr = *(IntPtr*)((IntPtr)AtkStage.GetSingleton() + 0x28) + 0x188E; } // Located in AtkInputManager
            catch { PluginLog.LogError("Failed loading textActiveBoolPtr"); }

            try
            {
                ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(DalamudApi.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
            }
            catch { PluginLog.LogError("Failed loading ExecuteCommand"); }

            try
            {
                ExecuteMacroHook = new Hook<ExecuteMacroDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28"), ExecuteMacroDetour);

                numCopiedMacroLinesPtr = DalamudApi.SigScanner.ScanText("49 8D 5E 70 BF ?? 00 00 00") + 0x5;
                numExecutedMacroLinesPtr = DalamudApi.SigScanner.ScanText("41 83 F8 ?? 0F 8D ?? ?? ?? ?? 49 6B C8 68") + 0x3;

                ExecuteMacroHook.Enable();
            }
            catch { PluginLog.LogError("Failed loading ExecuteMacro"); }

            try
            {
                useItem = (delegate* unmanaged<IntPtr, uint, uint, uint, short, void>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 B0 01 BA 13 00 00 00");
                itemContextMenuAgent = GetAgentByInternalID(10);

                usables = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Item>()!.Where(i => i.ItemAction.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower())
                    .Concat(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.EventItem>()!.Where(i => i.Action.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower()))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                usables[aetherCompassID] = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.EventItem>()!.GetRow(aetherCompassID)?.Name.ToString().ToLower();
            }
            catch { PluginLog.LogError("Failed to load UseItem"); }

            try
            {
                getHUDLayout = (delegate* unmanaged<IntPtr, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 4D 8B 4D 50");
            }
            catch { PluginLog.LogError("Failed loading CurrentHUDLayout"); }
        }
        catch { PluginLog.LogError("Failed loading plugin"); }
    }

    public static IntPtr GetAgentByInternalID(uint id) => (IntPtr)agentModule->GetAgentByInternalID(id);

    public static void DEBUG_FindAgent(long agent)
    {
        var found = false;
        for (uint i = 0; i < 800; i++) // Dunno how many there are
        {
            if (GetAgentByInternalID(i).ToInt64() != agent) continue;
            QoLBar.PrintEcho(i.ToString());
            found = true;
            break;
        }

        if (!found)
            QoLBar.PrintEcho($"Failed to find agent {agent:X}");
    }


    public static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, IntPtr macro)
    {
        NumCopiedMacroLines = Macro.numLines;
        NumExecutedMacroLines = Macro.numLines;
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    public static void ReadyCommand()
    {
        if (chatQueueTimer > 0 && (chatQueueTimer -= ImGuiNET.ImGui.GetIO().DeltaTime) <= 0 && chatQueue.Count > 0)
            ExecuteCommand(chatQueue.Dequeue(), true);

        if (retryItem > 0)
        {
            commandReady = false;
            UseItem(retryItem); // Gross bandaid to "fix" failed items
            retryItem = 0;
        }
        else
        {
            commandReady = true;
            RunCommandQueue();
        }

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
                commandQueue.Enqueue(c[..Math.Min(c.Length, maxCommandLength)]);
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
                command = command[2..].ToLower();
                switch (command[0])
                {
                    case 'm': // Execute Macro
                        try
                        {
                            if (int.TryParse(command[1..], out var macro))
                            {
                                if (macro is >= 0 and < 200)
                                    ExecuteMacroHook.Original(raptureShellModule, (IntPtr)raptureMacroModule + 0x58 + (Macro.size * macro));
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
                    case 'i': // Item
                        if (!macroMode)
                        {
                            if (uint.TryParse(command[2..], out var id))
                                UseItem(id);
                            else
                                UseItem(command[2..]);
                        }
                        else
                        {
                            QoLBar.PrintError("Macros do not support item usage.");
                        }
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

                ProcessChatBox(uiModule, stringPtr, IntPtr.Zero, 0);
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

    public static AtkUnitBase* GetAddonStructByName(string name, int index) => (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name, index);

    public static AtkUnitBase* GetFocusedAddon()
    {
        var units = raptureAtkUnitManager->AtkUnitManager.FocusedUnitsList;
        var count = units.Count;
        return count == 0 ? null : (&units.AtkUnitEntries)[count - 1];
    }

    public static void UseItem(uint id)
    {
        if (id == 0 || !usables.ContainsKey(id is >= 1_000_000 and < 2_000_000 ? id - 1_000_000 : id)) return;

        // Aether Compass support
        if (id == aetherCompassID)
        {
            actionManager->UseAction(ActionType.Spell, 26988);
            return;
        }

        // Dumb fix for dumb bug
        if (retryItem == 0 && id < 2_000_000)
        {
            var actionID = getActionID(2, id);
            if (actionID == 0)
            {
                retryItem = id;
                return;
            }
        }

        useItem(itemContextMenuAgent, id, 9999, 0, 0);
    }

    public static void UseItem(string name)
    {
        if (usables == null || string.IsNullOrWhiteSpace(name)) return;

        var newName = name.Replace("\uE03C", ""); // Remove HQ Symbol
        var useHQ = newName != name;
        newName = newName.ToLower().Trim(' ');

        try { UseItem(usables.First(i => i.Value == newName).Key + (uint)(useHQ ? 1_000_000 : 0)); }
        catch { }
    }

    public static uint GetActionID(uint actionType, uint actionCategoryID) => getActionID(actionType, actionCategoryID);

    public static float GetRecastTime(ActionType actionType, uint actionID)
    {
        var recast = actionManager->GetRecastTime(actionType, actionID);
        if (recast == 0) return -1;
        return recast;
    }

    public static float GetRecastTime(byte actionType, uint actionID) => GetRecastTime((ActionType)actionType, actionID);

    public static float GetRecastTimeElapsed(ActionType actionType, uint actionID) => actionManager->GetRecastTimeElapsed(actionType, actionID);

    public static float GetRecastTimeElapsed(byte actionType, uint actionID) => GetRecastTimeElapsed((ActionType)actionType, actionID);

    public static void Dispose()
    {
        ExecuteMacroHook?.Dispose();
        NumCopiedMacroLines = 15;
        NumExecutedMacroLines = 15;
    }
}