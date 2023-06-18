using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using static QoLBar.ShCfg;

namespace QoLBar;

public class ShortcutUI : IDisposable
{
    private int _id;
    public int ID
    {
        get => _id;
        set
        {
            _id = value;
            Config = (parent != null) ? parent.Config.SubList[value] : parentBar.Config.ShortcutList[value];
        }
    }
    //public ShCfg Config => (parent != null) ? parent.shConfig.SubList[shNumber] : parentBar.barConfig.ShortcutList[shNumber];
    public ShCfg Config { get; private set; }

    private static ImGuiStylePtr Style => ImGui.GetStyle();

    public readonly BarUI parentBar;
    public readonly ShortcutUI parent;
    public readonly List<ShortcutUI> children = new();
    public bool activated = false;
    private bool isHovered;
    private bool isCategoryHovered = false;
    private bool wasCategoryHovered = false;
    private float animTime = -1;

    private bool IsConfigPopupOpen() => QoLBar.Plugin.ui.IsConfigPopupOpen();
    private void SetConfigPopupOpen() => QoLBar.Plugin.ui.SetConfigPopupOpen();

    public ShortcutUI DisplayedUI => (Config.Type == ShortcutType.Category && Config.Mode != ShortcutMode.Default && children.Count > 0) ? children[Math.Min(Config._i, children.Count - 1)] : this;

    public ShortcutUI(BarUI bar)
    {
        parentBar = bar;
        ID = bar.children.Count;
        Initialize();
    }

    public ShortcutUI(ShortcutUI sh)
    {
        parentBar = sh.parentBar;
        parent = sh;
        ID = sh.children.Count;
        Initialize();
    }

    public void Initialize()
    {
        if (Config.SubList == null) return;

        for (int i = 0; i < Config.SubList.Count; i++)
            children.Add(new ShortcutUI(this));
    }

    public void ClearActivated()
    {
        if (animTime == 0)
            animTime = -1;

        activated = false;
        if (Config.Type == ShortcutType.Category)
        {
            foreach (var ui in children)
                ui.ClearActivated();
        }
    }

    public void OnClick(bool v, bool mouse, bool wasHovered, bool outsideDraw = false)
    {
        if (mouse)
            animTime = -1;
        else
            animTime = 0;

        var _i = Config._i;

        var command = Config.Command;
        switch (Config.Type)
        {
            case ShortcutType.Command:
                switch (Config.Mode)
                {
                    case ShortcutMode.Incremental:
                    {
                        var lines = command.Split('\n');
                        command = lines[Math.Min(_i, lines.Length - 1)];
                        Config._i = (_i + 1) % lines.Length;
                        QoLBar.Config.Save();
                        break;
                    }
                    case ShortcutMode.Random:
                    {
                        var lines = command.Split('\n');
                        command = lines[Math.Min(_i, lines.Length - 1)];
                        Config._i = (int)(QoLBar.FrameCount % lines.Length); // With this game's FPS drops? Completely random.
                        QoLBar.Config.Save();
                        break;
                    }
                }
                Game.QueueCommand(command);
                break;
            case ShortcutType.Category:
                switch (Config.Mode)
                {
                    case ShortcutMode.Incremental:
                        if (0 <= _i && _i < children.Count)
                            children[_i].OnClick(v, true, wasHovered, outsideDraw);
                        Config._i = (_i + 1) % Math.Max(1, children.Count);
                        QoLBar.Config.Save();
                        break;
                    case ShortcutMode.Random:
                        if (0 <= _i && _i < children.Count)
                            children[_i].OnClick(v, true, wasHovered, outsideDraw);
                        Config._i = (int)(QoLBar.FrameCount % Math.Max(1, children.Count));
                        QoLBar.Config.Save();
                        break;
                    default:
                        if (!wasHovered)
                            Game.QueueCommand(command);
                        if (!outsideDraw)
                        {
                            parentBar.SetupCategoryPosition(v, parent != null);
                            ImGui.OpenPopup("ShortcutCategory");
                        }
                        break;
                }
                break;
        }
    }

    // TODO: rewrite these functions cause they suck
    public void DrawShortcut(float width)
    {
        if (animTime >= 0.35f)
            animTime = -1;
        else if (animTime >= 0)
            animTime += ImGui.GetIO().DeltaTime;

        var inCategory = parent != null;
        var ui = DisplayedUI;
        var sh = ui.Config;

        var name = sh.Name;
        var useIcon = ParseName(ref name, out var subName, out var tooltip, out var icon, out var args);
        var hasSubName = !string.IsNullOrEmpty(subName);
        var spacer = sh.Type == ShortcutType.Spacer;

        if (inCategory)
        {
            if (useIcon || !parent.Config.CategoryNoBackground)
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            else
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.08f, 0.08f, 0.94f));
        }

        var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
        var clicked = false;

        var c = ImGui.ColorConvertU32ToFloat4(sh.Color);
        c.W += sh.ColorAnimation / 255f; // Temporary
        if (c.W > 1)
            c = AnimateColor(c);

        var cursorPos = Vector2.Zero;

        ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * (!inCategory ? parentBar.Config.FontScale : parent.Config.CategoryFontScale));
        if (!useIcon || hasSubName)
        {
            var size = new Vector2(width, height);

            if (hasSubName)
            {
                ImGui.BeginGroup();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

                cursorPos = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(cursorPos.X + height);

                name = subName;
                if (size.X > 0)
                    size.X = Math.Max(size.X - size.Y, 1);
            }

            if (spacer)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, 0);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
            }

            ImGui.PushStyleColor(ImGuiCol.Text, c);
            clicked = ImGui.Button(name, size);
            ImGui.PopStyleColor();

            if (spacer)
            {
                ImGui.PopStyleColor(3);
                clicked = false;
            }
        }

        if (useIcon)
        {
            if (hasSubName)
            {
                //var lastPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(cursorPos);
                //cursorPos = lastPos;
            }

            ImGuiEx.PushClipRectFullScreen();
            clicked = DrawIcon(icon, new ImGuiEx.IconSettings
            {
                size = new Vector2(height),
                zoom = sh.IconZoom,
                offset = new Vector2(sh.IconOffset[0], sh.IconOffset[1]),
                rotation = sh.IconRotation,
                color = ImGui.ColorConvertFloat4ToU32(c),
                activeTime = animTime,
                cooldownAction = sh.CooldownAction,
                cooldownStyle = (ImGuiEx.IconSettings.CooldownStyle)sh.CooldownStyle
            }, args, false, spacer) || clicked;
            ImGui.PopClipRect();

            if (hasSubName)
            {
                ImGui.PopStyleVar();
                ImGui.EndGroup();
                //ImGui.SetCursorPos(cursorPos);
            }
        }
        ImGuiEx.PopFontScale();

        if (inCategory)
        {
            ImGui.PopStyleColor();
        }
        else
        {
            var size = ImGui.GetItemRectMax() - ImGui.GetWindowPos();
            parentBar.MaxWidth = size.X;
            parentBar.MaxHeight = size.Y;
        }

        isHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly);

        var wasHovered = false;
        clicked = clicked || (activated && !parentBar.WasActivated);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                clicked = true;

            if (!clicked)
            {
                var isHoverEnabled = sh.CategoryOnHover && sh.Type == ShortcutType.Category;
                var allowHover = parentBar.IsFullyRevealed && !IsConfigPopupOpen() && !ImGui.IsPopupOpen("ShortcutCategory") && Game.IsGameFocused && !ImGui.IsAnyMouseDown() && !ImGui.IsMouseReleased(ImGuiMouseButton.Right);
                if (isHoverEnabled && allowHover)
                {
                    wasHovered = true;
                    clicked = true;
                }
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                ImGui.PopFont();
                ImGui.SetTooltip(tooltip);
                ImGui.PushFont(QoLBar.Font);
            }
        }

        if (clicked && !parentBar.IsDragging && !IsConfigPopupOpen())
        {
            if (!inCategory)
                OnClick(parentBar.IsVertical, !activated, wasHovered);
            else
            {
                var cols = parent.Config.CategoryColumns;
                OnClick(cols > 0 && parent.children.Count >= (cols * (cols - 1) + 1), !activated, wasHovered);
                if (!parent.Config.CategoryStaysOpen && sh.Type == ShortcutType.Command)
                    ImGui.SetWindowFocus(null);
            }

            if (activated)
            {
                activated = false;
                parentBar.WasActivated = true;
            }
        }

        ImGui.OpenPopupOnItemClick("editShortcut", ImGuiPopupFlags.MouseButtonRight);

        if (Config.Type == ShortcutType.Category && Config.Mode == ShortcutMode.Default)
        {
            if (parentBar.IsDocked)
                ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(sh.CategorySpacing[0], sh.CategorySpacing[1]));
            ImGuiEx.PushFontScale(1); // Popups will square this value for some reason, so it has to be reset temporarily
            DrawCategory();
            ImGuiEx.PopFontScale();
            ImGui.PopStyleVar();
        }

        PluginUI.DrawExternalWindow(() => DrawConfig(useIcon), parentBar.IsDocked);
    }

    // Bandaid until ImGui fucks off
    private bool _fuckImGui = false;
    private void DrawCategory()
    {
        //parentBar.SetCategoryPosition(ImGuiCond.Appearing);

        if (_fuckImGui)
        {
            parentBar.SetCategoryPosition(ImGuiCond.Always);
            _fuckImGui = false;
        }

        if (!ImGui.BeginPopup("ShortcutCategory", (Config.CategoryNoBackground ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoMove))
        {
            wasCategoryHovered = false;
            return;
        }

        ImGuiEx.PushFontSize(QoLBar.DefaultFontSize * Config.CategoryScale);

        if (ImGui.IsWindowAppearing())
            _fuckImGui = true;

        parentBar.Reveal();

        if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && !ImGui.IsAnyItemHovered()) // Why are the ImGui hover flags just not working ???
            ImGui.OpenPopup("editShortcut");

        // Dupe code but only cause ImGui sucks
        PluginUI.DrawExternalWindow(() => DrawConfig(Config.Name.Contains("::")), parentBar.IsDocked);

        var windowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.ChildWindows);
        isCategoryHovered = windowHovered || !wasCategoryHovered || ImGui.IsPopupOpen(null, ImGuiPopupFlags.AnyPopupId);

        if (Config.CategoryHoverClose)
        {
            var shortcuts = parent?.children ?? parentBar.children;
            if (!wasCategoryHovered && (windowHovered || !isHovered && shortcuts.Any(sh => sh.isHovered)))
                wasCategoryHovered = true;
        }

        var cols = Config.CategoryColumns;
        var width = (float)Math.Round(Config.CategoryWidth * ImGuiHelpers.GlobalScale * Config.CategoryScale);

        for (int i = 0; i < children.Count; i++)
        {
            ImGui.PushID(i);

            children[i].DrawShortcut(width);

            if (cols <= 0 || i % cols != cols - 1)
                ImGui.SameLine();

            ImGui.PopID();
        }

        if (parentBar.Config.Editing || Config.SubList.Count < 1)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, !Config.CategoryNoBackground ? Vector4.Zero : new Vector4(0.08f, 0.08f, 0.08f, 0.94f));
            var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
            ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * Config.CategoryFontScale);
            if (ImGui.Button("+", new Vector2(width, height)))
                ImGui.OpenPopup("addShortcut");
            ImGuiEx.PopFontScale();
            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGuiEx.SetItemTooltip("Add a new shortcut.");
            ImGui.PushFont(QoLBar.Font);
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
            ImGui.OpenPopup("addShortcut");

        PluginUI.DrawExternalWindow(() => DrawAddShortcut(null, this), parentBar.IsDocked);

        ImGuiEx.ClampWindowPosToViewport();

        if (Config.CategoryHoverClose && !isCategoryHovered)
            ImGui.CloseCurrentPopup();

        ImGuiEx.PopFontSize();

        ImGui.EndPopup();
    }

    private void DrawConfig(bool hasIcon)
    {
        if (!ImGui.BeginPopup("editShortcut")) return;

        parentBar.Reveal();
        SetConfigPopupOpen();

        ConfigEditorUI.AutoPasteIcon(Config);

        if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
        {
            if (ImGui.BeginTabItem("Shortcut"))
            {
                ConfigEditorUI.EditShortcutConfigBase(Config, true, hasIcon);

                if (Config.Type != ShortcutType.Spacer)
                    ConfigEditorUI.EditShortcutMode(this);

                ConfigEditorUI.EditShortcutColor(this);

                if (Config.Type != ShortcutType.Spacer)
                    Keybind.KeybindInput(Config);

                ImGui.EndTabItem();
            }

            if (Config.Type == ShortcutType.Category && ImGui.BeginTabItem("Category"))
            {
                ConfigEditorUI.EditShortcutCategoryOptions(this);
                ImGui.EndTabItem();
            }

            if (hasIcon && ImGui.BeginTabItem("Icon"))
            {
                ConfigEditorUI.EditShortcutIconOptions(this);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        bool vertical;
        if (parent != null)
        {
            var cols = parent.Config.CategoryColumns;
            vertical = cols > 0 && parent.children.Count >= (cols * (cols - 1) + 1);
        }
        else
        {
            vertical = parentBar.IsVertical;
        }

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button(vertical ? FontAwesomeIcon.ArrowsAltV.ToIconString() : FontAwesomeIcon.ArrowsAltH.ToIconString(), new Vector2(30 * ImGuiHelpers.GlobalScale, 0));
        ImGui.PopFont();
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.CloseCurrentPopup();

            ImGuiEx.SetupSlider(vertical, 32 * ImGuiHelpers.GlobalScale, (hitInterval, increment, closing) =>
            {
                parentBar.Reveal();
                SetConfigPopupOpen();

                if (hitInterval)
                    ShiftThis(increment);
            });
        }
        ImGuiEx.SetItemTooltip("Drag to move the shortcut.");

        ImGui.SameLine();
        if (ImGui.Button("Export"))
            ImGui.SetClipboardText(Importing.ExportShortcut(Config, false));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Export to clipboard with minimal settings (May change with updates).\n" +
                             "Right click to export with every setting (Longer string, doesn't change).");

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                ImGui.SetClipboardText(Importing.ExportShortcut(Config, true));
        }
        ImGui.SameLine();
        if (ImGui.Button(QoLBar.Config.ExportOnDelete ? "Cut" : "Delete"))
            ConfigEditorUI.DisplayRightClickDeleteMessage();
        //if (ImGui.IsItemClicked(1)) // Jesus christ I hate ImGui who made this function activate on PRESS AND NOT RELEASE??? THIS ISN'T A CLICK
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right click this button to delete the shortcut!" +
                             (QoLBar.Config.ExportOnDelete ? "\nThe shortcut will be exported to clipboard first." : ""));

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                if (QoLBar.Config.ExportOnDelete)
                    ImGui.SetClipboardText(Importing.ExportShortcut(Config, false));

                DeleteThis();
                ImGui.CloseCurrentPopup();
            }
        }

        DrawIconBrowserButton();

        ImGuiEx.ClampWindowPosToViewport();

        ImGui.EndPopup();
    }

    public void AddShortcut(ShCfg sh)
    {
        Config.SubList.Add(sh);
        children.Add(new ShortcutUI(this));
        QoLBar.Config.Save();
    }

    public void RemoveShortcut(int i)
    {
        if (QoLBar.Config.ExportOnDelete)
            ImGui.SetClipboardText(Importing.ExportShortcut(Config.SubList[i], false));

        children[i].Dispose();
        children.RemoveAt(i);
        Config.SubList.RemoveAt(i);
        QoLBar.Config.Save();
        RefreshShortcutIDs();
    }

    public void ShiftShortcut(int i, bool increment)
    {
        if (!increment ? i > 0 : i < (children.Count - 1))
        {
            var j = (increment ? i + 1 : i - 1);
            var ui = children[i];
            children.RemoveAt(i);
            children.Insert(j, ui);

            var sh = Config.SubList[i];
            Config.SubList.RemoveAt(i);
            Config.SubList.Insert(j, sh);
            QoLBar.Config.Save();
            RefreshShortcutIDs();
        }
    }

    private void RefreshShortcutIDs()
    {
        for (int i = 0; i < children.Count; i++)
            children[i].ID = i;
    }

    public void DeleteThis()
    {
        if (parent != null)
            parent.RemoveShortcut(ID);
        else
            parentBar.RemoveShortcut(ID);
    }

    public void ShiftThis(bool increment)
    {
        if (parent != null)
            parent.ShiftShortcut(ID, increment);
        else
            parentBar.ShiftShortcut(ID, increment);
    }

    public void Dispose()
    {
        foreach (var ui in children)
            ui.Dispose();
    }

    public static bool ParseName(ref string name, out string subName, out string tooltip, out int icon, out string args)
    {
        subName = string.Empty;
        args = string.Empty;
        if (name == string.Empty)
        {
            tooltip = string.Empty;
            icon = 0;
            return false;
        }

        var split = name.Split(new[] { "##" }, 2, StringSplitOptions.None);
        name = split[0];

        tooltip = (split.Length > 1) ? split[1] : string.Empty;

        icon = 0;

        var split2 = name.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (split2.Length < 2) return false;

        subName = split2[0];
        name = split2[1];

        var substart = 0;

        // Parse icon arguments
        var done = false;
        while (!done)
        {
            if (name.Length > substart)
            {
                var arg = name[substart];
                switch (arg)
                {
                    case 'f': // Frame
                    case 'n': // No frame
                    case 'l': // LR icon
                    case 'h': // HR icon
                    case 'g': // Grayscale
                    case 'r': // Reverse
                        args += arg;
                        substart++;
                        break;
                    default:
                        done = true;
                        break;
                }
            }
            else
                done = true;
        }

        _ = int.TryParse(name[substart..], out icon);
        return true;
    }

    public static void DrawIconBrowserButton()
    {
        var iconSize = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X + Style.WindowPadding.X - iconSize);
        if (DrawIcon(46, new ImGuiEx.IconSettings { size = new Vector2(iconSize) }, "nl"))
            QoLBar.Plugin.ToggleIconBrowser();
        ImGuiEx.SetItemTooltip("Opens up a list of all icons you can use instead of text.\n" +
                               "Warning: The last 3 tabs contain very large images and will use several GB of memory.\n" +
                               "Clicking on one will copy text to be pasted into the \"Name\" field of a shortcut.\n" +
                               "Additionally, while the browser is open it will autofill the \"Name\" of shortcuts.");
    }

    public static void DrawAddShortcut(BarUI barUI, ShortcutUI shUI)
    {
        if (!ImGui.BeginPopup("addShortcut")) return;

        barUI?.Reveal();
        shUI?.parentBar.Reveal();
        QoLBar.Plugin.ui.SetConfigPopupOpen();

        BarUI.tempSh ??= new ShCfg();
        var newSh = BarUI.tempSh;

        ConfigEditorUI.AutoPasteIcon(newSh);

        ConfigEditorUI.EditShortcutConfigBase(newSh, false, false);

        if (ImGui.Button("Create"))
        {
            barUI?.AddShortcut(newSh);
            shUI?.AddShortcut(newSh);
            BarUI.tempSh = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            var imports = Importing.TryImport(ImGuiEx.TryGetClipboardText(), true);
            if (imports.shortcut != null)
            {
                barUI?.AddShortcut(imports.shortcut);
                shUI?.AddShortcut(imports.shortcut);
            }
            else if (imports.bar != null)
            {
                foreach (var sh in imports.bar.ShortcutList)
                {
                    barUI?.AddShortcut(sh);
                    shUI?.AddShortcut(sh);
                }
            }
            QoLBar.Config.Save();
            ImGui.CloseCurrentPopup();
        }
        ImGuiEx.SetItemTooltip("Import a shortcut from the clipboard,\n" +
                               "or import all of another bar's shortcuts.");

        DrawIconBrowserButton();

        ImGuiEx.ClampWindowPosToViewport();

        ImGui.EndPopup();
    }

    public static bool DrawIcon(int icon, ImGuiEx.IconSettings settings, string args = null, bool retExists = false, bool noButton = false)
    {
        var ret = false;
        var hasArgs = !string.IsNullOrEmpty(args);

        TextureDictionary texd = null;
        if (hasArgs)
        {
            if (args.Contains("l"))
                texd = QoLBar.textureDictionaryLR;
            else if (args.Contains("h"))
                texd = QoLBar.textureDictionaryHR;
        }

        texd ??= QoLBar.TextureDictionary;

        if (hasArgs && args.Contains("g"))
            texd = (texd == QoLBar.textureDictionaryLR) ? QoLBar.textureDictionaryGSLR : QoLBar.textureDictionaryGSHR;

        var tex = texd[icon];
        if (tex == null)
        {
            if (retExists) return false;

            if (icon == 66001)
            {
                if (noButton)
                    ImGui.Dummy(settings.size);
                else
                    ret = ImGui.Button("X##FailedTexture", settings.size);
            }
            else
            {
                ret = DrawIcon(66001, settings, args, false, noButton);
            }
        }
        else
        {
            settings.frame = QoLBar.Config.UseIconFrame;
            if (hasArgs)
            {
                if (args.Contains("f"))
                    settings.frame = true;
                else if (args.Contains("n"))
                    settings.frame = false;
            }

            settings.flipped = hasArgs && args.Contains("r");

            if (!noButton)
            {
                ret = ImGuiEx.IconButton("icon", tex, settings);
            }
            else
            {
                settings.activeTime = 0;
                ImGuiEx.Icon(tex, settings);
            }

            if (retExists) return true;
        }
        return ret;
    }

    public static Vector4 AnimateColor(Vector4 c)
    {
        float r, g, b, a, x;
        r = g = b = a = 1;
        var t = QoLBar.RunTime;
        var anim = Math.Round(c.W * 255) - 256;

        switch (anim)
        {
            case 0: // Slow Rainbow
                ImGui.ColorConvertHSVtoRGB(((t * 15) % 360) / 360, 1, 1, out r, out g, out b);
                break;
            case 1: // Rainbow
                ImGui.ColorConvertHSVtoRGB(((t * 30) % 360) / 360, 1, 1, out r, out g, out b);
                break;
            case 2: // Fast Rainbow
                ImGui.ColorConvertHSVtoRGB(((t * 60) % 360) / 360, 1, 1, out r, out g, out b);
                break;
            case 3: // Slow Fade
                r = c.X; g = c.Y; b = c.Z;
                a = (float)(Math.Sin(((t * 30) % 360) * Math.PI / 180) + 1) / 2;
                break;
            case 4: // Fade
                r = c.X; g = c.Y; b = c.Z;
                a = (float)(Math.Sin(((t * 60) % 360) * Math.PI / 180) + 1) / 2;
                break;
            case 5: // Fast Fade
                r = c.X; g = c.Y; b = c.Z;
                a = (float)(Math.Sin(((t * 120) % 360) * Math.PI / 180) + 1) / 2;
                break;
            case 6: // Red Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (1 - c.X) * x;
                g = c.Y + (0 - c.Y) * x;
                b = c.Z + (0 - c.Z) * x;
                break;
            case 7: // Yellow Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (1 - c.X) * x;
                g = c.Y + (1 - c.Y) * x;
                b = c.Z + (0 - c.Z) * x;
                break;
            case 8: // Green Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (0 - c.X) * x;
                g = c.Y + (1 - c.Y) * x;
                b = c.Z + (0 - c.Z) * x;
                break;
            case 9: // Cyan Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (0 - c.X) * x;
                g = c.Y + (1 - c.Y) * x;
                b = c.Z + (1 - c.Z) * x;
                break;
            case 10: // Blue Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (0 - c.X) * x;
                g = c.Y + (0 - c.Y) * x;
                b = c.Z + (1 - c.Z) * x;
                break;
            case 11: // Purple Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (1 - c.X) * x;
                g = c.Y + (0 - c.Y) * x;
                b = c.Z + (1 - c.Z) * x;
                break;
            case 12: // White Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (1 - c.X) * x;
                g = c.Y + (1 - c.Y) * x;
                b = c.Z + (1 - c.Z) * x;
                break;
            case 13: // Black Transition
                x = Math.Abs(((t * 60) % 360) - 180) / 180;
                r = c.X + (0 - c.X) * x;
                g = c.Y + (0 - c.Y) * x;
                b = c.Z + (0 - c.Z) * x;
                break;
        }

        return new Vector4(r, g, b, a);
    }
}