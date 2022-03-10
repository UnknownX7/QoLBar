using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;

// Disclaimer: I have no idea what I'm doing.
namespace QoLBar;

public class QoLBar : IDalamudPlugin
{
    public string Name => "QoL Bar";
    public static QoLBar Plugin { get; private set; }
    public static Configuration Config { get; private set; }

    public PluginUI ui;
    private bool pluginReady = false;

    public static TextureDictionary TextureDictionary => Config.UseHRIcons ? textureDictionaryHR : textureDictionaryLR;
    public static readonly TextureDictionary textureDictionaryLR = new(false, false);
    public static readonly TextureDictionary textureDictionaryHR = new(true, false);
    public static readonly TextureDictionary textureDictionaryGSLR = new(false, true);
    public static readonly TextureDictionary textureDictionaryGSHR = new(true, true);

    public static uint lastHoveredActionID = 0;
    public static uint lastHoveredActionType = 0;

    public const float DefaultFontSize = 17;
    public const float MaxFontSize = 64;
    public static ImFontPtr Font { get; private set; }
    private static bool fontEnabled = false;

    public QoLBar(DalamudPluginInterface pluginInterface)
    {
        Plugin = this;
        DalamudApi.Initialize(this, pluginInterface);

        Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
        Config.Initialize();
        Config.TryBackup(); // Backup on version change

        DalamudApi.Framework.Update += Update;

        ui = new PluginUI();
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
        ToggleFont(Config.FontSize != DefaultFontSize);

        DalamudApi.GameGui.HoveredActionChanged += HoveredActionChanged;

        CheckHideOptOuts();

        ReadyPlugin();
    }

    public void ReadyPlugin()
    {
        IPC.Initialize();

        var iconPath = Config.GetPluginIconPath();
        textureDictionaryLR.AddUserIcons(iconPath);
        textureDictionaryHR.AddUserIcons(iconPath);

        textureDictionaryLR.LoadTexture(46); // Magnifying glass / Search
        TextureDictionary.AddExtraTextures(textureDictionaryLR, textureDictionaryHR);
        TextureDictionary.AddExtraTextures(textureDictionaryGSLR, textureDictionaryGSHR);
        IconBrowserUI.BuildCache(false);

        Game.Initialize();
        ConditionManager.Initialize();

        pluginReady = true;
    }

    public void Reload()
    {
        Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
        Config.Initialize();
        Config.UpdateVersion();
        Config.Save();
        ui.Reload();
        CheckHideOptOuts();
    }

    public void ToggleConfig() => ui.ToggleConfig();

    [Command("/qolbar")]
    [HelpMessage("Open the configuration menu.")]
    public void ToggleConfig(string command, string argument) => ToggleConfig();

    [Command("/qolicons")]
    [HelpMessage("Open the icon browser.")]
    public void ToggleIconBrowser(string command = null, string argument = null) => IconBrowserUI.ToggleIconBrowser();

    [Command("/qolvisible")]
    [HelpMessage("Hide or reveal a bar using its name or index. Usage: /qolvisible [on|off|toggle] <bar>")]
    private void OnQoLVisible(string command, string argument)
    {
        var reg = Regex.Match(argument, @"^(\w+) (.+)");
        if (reg.Success)
        {
            var subcommand = reg.Groups[1].Value.ToLower();
            var bar = reg.Groups[2].Value;
            var useID = int.TryParse(bar, out var id);
            switch (subcommand)
            {
                case "on":
                case "reveal":
                case "r":
                    if (useID)
                        ui.SetBarHidden(id - 1, false, false);
                    else
                        ui.SetBarHidden(bar, false, false);
                    break;
                case "off":
                case "hide":
                case "h":
                    if (useID)
                        ui.SetBarHidden(id - 1, false, true);
                    else
                        ui.SetBarHidden(bar, false, true);
                    break;
                case "toggle":
                case "t":
                    if (useID)
                        ui.SetBarHidden(id - 1, true);
                    else
                        ui.SetBarHidden(bar, true);
                    break;
                default:
                    PrintError("Invalid subcommand.");
                    break;
            }
        }
        else
            PrintError("Usage: /qolvisible [on|off|toggle] <bar>");
    }

    public static bool HasPlugin(string name) => DalamudApi.PluginInterface.PluginInternalNames.Contains(name);

    public static bool IsLoggedIn() => ConditionManager.CheckCondition("l");

    public static float RunTime => (float)DalamudApi.PluginInterface.LoadTimeDelta.TotalSeconds;
    public static long FrameCount => (long)DalamudApi.PluginInterface.UiBuilder.FrameCount;
    private void Update(Framework framework)
    {
        if (!pluginReady) return;

        Config.DoTimedBackup();
        Game.ReadyCommand();
        Keybind.Run();
        Keybind.SetupHotkeys(ui.bars);
        ConditionManager.UpdateCache();
    }

    private void Draw()
    {
        if (_addUserIcons)
            AddUserIcons(ref _addUserIcons);

        if (!pluginReady) return;

        Config.DrawUpdateWindow();
        ui.Draw();
    }

    public void ToggleFont(bool enable)
    {
        if (enable)
        {
            if (!fontEnabled)
                DalamudApi.PluginInterface.UiBuilder.BuildFonts += BuildFonts;
            DalamudApi.PluginInterface.UiBuilder.RebuildFonts();
            fontEnabled = true;
        }
        else
        {
            if (fontEnabled)
            {
                DalamudApi.PluginInterface.UiBuilder.BuildFonts -= BuildFonts;
                //DalamudApi.PluginInterface.UiBuilder.RebuildFonts();
                Font = null;
            }
            fontEnabled = false;
        }
    }

    private unsafe void BuildFonts()
    {
        var fontBuilder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        var fontAtlas = ImGui.GetIO().Fonts;
        fontBuilder.AddRanges(fontAtlas.GetGlyphRangesDefault());
        fontBuilder.AddRanges(fontAtlas.GetGlyphRangesJapanese());
        //fontBuilder.AddRanges(fontAtlas.GetGlyphRangesChineseFull()); // Includes Default and Japanese
        fontBuilder.AddRanges(fontAtlas.GetGlyphRangesCyrillic());
        //fontBuilder.AddRanges(fontAtlas.GetGlyphRangesKorean());
        //fontBuilder.AddRanges(fontAtlas.GetGlyphRangesThai());
        //fontBuilder.AddRanges(fontAtlas.GetGlyphRangesVietnamese());
        fontBuilder.BuildRanges(out var ranges);
        Font = fontAtlas.AddFontFromFileTTF(Path.Combine(DalamudApi.PluginInterface.DalamudAssetDirectory.FullName, "UIRes",
            "NotoSansCJKjp-Medium.otf"), Math.Min(Math.Max(Config.FontSize, 1), MaxFontSize), null, ranges.Data);
        fontBuilder.Destroy();
    }

    private void HoveredActionChanged(object sender, HoveredAction action)
    {
        // 2 = Item, 3 = Event Item, 4 = Interaction, 13 = Mount, 20 = Accessorize (Fashion)
        switch (action.ActionKind)
        {
            case HoverActionKind.Action:
                lastHoveredActionType = 1;
                break;
            case HoverActionKind.GeneralAction:
                lastHoveredActionType = 5;
                break;
            //case HoverActionKind.None:
            //case HoverActionKind.CompanionOrder:
            //case HoverActionKind.MainCommand:
            //case HoverActionKind.ExtraCommand:
            //case HoverActionKind.PetOrder:
            //case HoverActionKind.Trait:
            default:
                return;
        }

        lastHoveredActionID = action.BaseActionID;
    }

    public void CheckHideOptOuts()
    {
        //pluginInterface.UiBuilder.DisableAutomaticUiHide = false;
        DalamudApi.PluginInterface.UiBuilder.DisableUserUiHide = Config.OptOutGameUIOffHide;
        DalamudApi.PluginInterface.UiBuilder.DisableCutsceneUiHide = Config.OptOutCutsceneHide;
        DalamudApi.PluginInterface.UiBuilder.DisableGposeUiHide = Config.OptOutGPoseHide;
    }

    public static Dictionary<int, string> GetUserIcons() => TextureDictionary.GetUserIcons();

    // TODO: .
    private bool _addUserIcons = false;
    private bool _iconsLR = false;
    private bool _iconsHR = false;
    private void AddUserIcons(ref bool b)
    {
        if (!_iconsLR && !_iconsHR)
        {
            _iconsLR = true;
            _iconsHR = true;
        }

        var iconPath = Config.GetPluginIconPath();

        if (_iconsLR)
            _iconsLR = !textureDictionaryLR.AddUserIcons(iconPath);

        if (_iconsHR)
            _iconsHR = !textureDictionaryHR.AddUserIcons(iconPath);

        if (!(b = _iconsLR || _iconsHR))
            IconBrowserUI.BuildCache(false);
    }

    public void AddUserIcons() => _addUserIcons = true;

    public static void CleanTextures(bool disposing)
    {
        if (disposing)
        {
            textureDictionaryLR.Dispose();
            textureDictionaryHR.Dispose();
            textureDictionaryGSLR.Dispose();
            textureDictionaryGSHR.Dispose();
        }
        else
        {
            textureDictionaryLR.TryEmpty();
            textureDictionaryHR.TryEmpty();
            textureDictionaryGSLR.TryEmpty();
            textureDictionaryGSHR.TryEmpty();
        }
    }

    public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[QoLBar] {message}");
    public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[QoLBar] {message}");

    #region IDisposable Support
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        IPC.Dispose();

        Config.Save();
        Config.SaveTempConfig();

        DalamudApi.Framework.Update -= Update;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudApi.GameGui.HoveredActionChanged -= HoveredActionChanged;
        ToggleFont(false);
        DalamudApi.Dispose();

        ui.Dispose();
        Game.Dispose();
        CleanTextures(true);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

public static class Extensions
{
    public static T2 GetDefaultValue<T, T2>(this T _, Expression<Func<T, T2>> expression)
    {
        if (((MemberExpression)expression.Body).Member.GetCustomAttribute(typeof(DefaultValueAttribute)) is DefaultValueAttribute attribute)
            return (T2)attribute.Value;
        else
            return default;
    }

    public static byte[] GetGrayscaleImageData(this Lumina.Data.Files.TexFile tex)
    {
        var rgba = tex.GetRgbaImageData();
        var pixels = rgba.Length / 4;
        var newData = new byte[rgba.Length];
        for (int i = 0; i < pixels; i++)
        {
            var pixel = i * 4;
            var alpha = rgba[pixel + 3];

            if (alpha > 0)
            {
                var avg = (byte)(0.2125f * rgba[pixel] + 0.7154f * rgba[pixel + 1] + 0.0721f * rgba[pixel + 2]);
                newData[pixel] = avg;
                newData[pixel + 1] = avg;
                newData[pixel + 2] = avg;
            }

            newData[pixel + 3] = alpha;
        }
        return newData;
    }

    public static object Cast(this Type Type, object data)
    {
        var DataParam = Expression.Parameter(typeof(object), "data");
        var Body = Expression.Block(Expression.Convert(Expression.Convert(DataParam, data.GetType()), Type));

        var Run = Expression.Lambda(Body, DataParam).Compile();
        var ret = Run.DynamicInvoke(data);
        return ret;
    }
}