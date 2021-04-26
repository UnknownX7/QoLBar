using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using Dalamud.Interface;
using static QoLBar.ShCfg;

namespace QoLBar
{
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
        public readonly List<ShortcutUI> children = new List<ShortcutUI>();

        private bool IsConfigPopupOpen() => QoLBar.Plugin.ui.IsConfigPopupOpen();
        private void SetConfigPopupOpen() => QoLBar.Plugin.ui.SetConfigPopupOpen();

        public int _i = 0;
        public ShortcutUI DisplayedUI => (Config.Type == ShortcutType.Category && Config.Mode != ShortcutMode.Default && children.Count > 0) ? children[Math.Min(_i, children.Count - 1)] : this;

        public bool _activated = false;

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
            if (Config.Mode == ShortcutMode.Random)
            {
                var count = Math.Max(1, (Config.Type == ShortcutType.Category) ? Config.SubList.Count : Config.Command.Split('\n').Length);
                _i = DateTime.Now.Millisecond % count;
            }

            if (Config.SubList != null)
            {
                for (int i = 0; i < Config.SubList.Count; i++)
                    children.Add(new ShortcutUI(this));
            }
        }

        public void SetupHotkeys()
        {
            if (Config.Hotkey > 0 && Config.Type != ShortcutType.Spacer)
                Keybind.AddHotkey(this);

            if (Config.Type == ShortcutType.Category)
            {
                foreach (var ui in children)
                    ui.SetupHotkeys();
            }
        }

        public void ClearActivated()
        {
            _activated = false;
            if (Config.Type == ShortcutType.Category)
            {
                foreach (var ui in children)
                    ui.ClearActivated();
            }
        }

        public void OnClick(bool v, bool wasHovered)
        {
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
                                _i = (_i + 1) % lines.Length;
                                break;
                            }
                        case ShortcutMode.Random:
                            {
                                var lines = command.Split('\n');
                                command = lines[Math.Min(_i, lines.Length - 1)];
                                _i = (int)(QoLBar.GetFrameCount() % lines.Length); // With this game's FPS drops? Completely random.
                                break;
                            }
                    }
                    QoLBar.Plugin.ExecuteCommand(command);
                    break;
                case ShortcutType.Category:
                    switch (Config.Mode)
                    {
                        case ShortcutMode.Incremental:
                            if (0 <= _i && _i < children.Count)
                                children[_i].OnClick(v, wasHovered);
                            _i = (_i + 1) % Math.Max(1, children.Count);
                            break;
                        case ShortcutMode.Random:
                            if (0 <= _i && _i < children.Count)
                                children[_i].OnClick(v, wasHovered);
                            _i = (int)(QoLBar.GetFrameCount() % Math.Max(1, children.Count));
                            break;
                        default:
                            if (!wasHovered)
                                QoLBar.Plugin.ExecuteCommand(command);
                            parentBar.SetupCategoryPosition(v, parent != null);
                            ImGui.OpenPopup("ShortcutCategory");
                            break;
                    }
                    break;
            }
        }

        // TODO: rewrite these functions cause they suck
        public void DrawShortcut(float width)
        {
            var inCategory = parent != null;
            var ui = DisplayedUI;
            var sh = ui.Config;

            var name = sh.Name;
            var useIcon = ParseName(ref name, out string tooltip, out int icon, out string args);

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

            ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * (!inCategory ? parentBar.Config.FontScale : parent.Config.CategoryFontScale));
            if (sh.Type == ShortcutType.Spacer)
            {
                if (useIcon)
                    DrawIcon(icon, new Vector2(height), sh.IconZoom, new Vector2(sh.IconOffset[0], sh.IconOffset[1]), c, QoLBar.Config.UseIconFrame, args, false, true);
                else
                {
                    var wantedSize = ImGui.GetFontSize();
                    var textSize = ImGui.CalcTextSize(name);
                    ImGui.BeginChild((uint)ID, new Vector2((width == 0) ? (textSize.X + Style.FramePadding.X * 2) : width, height));
                    ImGui.SameLine((ImGui.GetContentRegionAvail().X - textSize.X) / 2);
                    ImGui.SetCursorPosY((ImGui.GetContentRegionAvail().Y - textSize.Y) / 2);
                    // What the fuck ImGui
                    ImGui.SetWindowFontScale(wantedSize / ImGui.GetFontSize());
                    ImGui.TextColored(c, name);
                    ImGui.SetWindowFontScale(1);
                    ImGui.EndChild();
                }
            }
            else if (useIcon)
                clicked = DrawIcon(icon, new Vector2(height), sh.IconZoom, new Vector2(sh.IconOffset[0], sh.IconOffset[1]), c, QoLBar.Config.UseIconFrame, args);
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, c);
                clicked = ImGui.Button(name, new Vector2(width, height));
                ImGui.PopStyleColor();
            }
            ImGuiEx.PopFontScale();

            if (!inCategory)
            {
                var size = ImGui.GetItemRectMax() - ImGui.GetWindowPos();
                parentBar.MaxWidth = size.X;
                parentBar.MaxHeight = size.Y;
            }

            if (inCategory)
                ImGui.PopStyleColor();

            var wasHovered = false;
            clicked = clicked || (_activated && !parentBar.WasActivated);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    clicked = true;

                if (!clicked)
                {
                    var isHoverEnabled = sh.CategoryOnHover && sh.Type == ShortcutType.Category;
                    var allowHover = parentBar.IsFullyRevealed && !IsConfigPopupOpen() && !ImGui.IsPopupOpen("ShortcutCategory") && QoLBar.IsGameFocused && !ImGui.IsAnyMouseDown() && !ImGui.IsMouseReleased(ImGuiMouseButton.Right);
                    if (isHoverEnabled && allowHover)
                    {
                        wasHovered = true;
                        clicked = true;
                    }
                }

                if (!string.IsNullOrEmpty(tooltip))
                    ImGui.SetTooltip(tooltip);
            }

            if (clicked && !parentBar.IsDragging)
            {
                if (_activated)
                {
                    _activated = false;
                    parentBar.WasActivated = true;
                }

                if (!inCategory)
                    OnClick(parentBar.IsVertical, wasHovered);
                else
                {
                    var cols = parent.Config.CategoryColumns;
                    OnClick(cols > 0 && parent.children.Count >= (cols * (cols - 1) + 1), wasHovered);
                    if (!parent.Config.CategoryStaysOpen && sh.Type == ShortcutType.Command)
                        ImGui.SetWindowFocus(null);
                }
            }

            ImGui.OpenPopupOnItemClick("editShortcut", ImGuiPopupFlags.MouseButtonRight);

            if (Config.Type == ShortcutType.Category && Config.Mode == ShortcutMode.Default)
            {
                if (parentBar.IsDocked)
                    ImGuiHelpers.ForceNextWindowMainViewport();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(sh.CategorySpacing[0], sh.CategorySpacing[1]));
                ImGuiEx.PushFontScale(sh.CategoryScale);
                DrawCategory();
                ImGuiEx.PopFontScale();
                ImGui.PopStyleVar();
            }

            PluginUI.DrawExternalWindow(() => DrawConfig(useIcon), parentBar.IsDocked);
        }

        private void DrawCategory()
        {
            parentBar.SetCategoryPosition();
            if (ImGui.BeginPopup("ShortcutCategory", (Config.CategoryNoBackground ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoMove))
            {
                parentBar.Reveal();

                if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    ImGui.OpenPopup("editShortcut");

                // Dupe code but only cause ImGui sucks
                PluginUI.DrawExternalWindow(() => DrawConfig(Config.Name.StartsWith("::")), parentBar.IsDocked);

                var cols = Config.CategoryColumns;
                var width = Config.CategoryWidth * ImGui.GetIO().FontGlobalScale * Config.CategoryScale;

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
                    if (!Config.CategoryNoBackground)
                        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    else
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.08f, 0.08f, 0.94f));
                    var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
                    ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * Config.CategoryFontScale);
                    if (ImGui.Button("+", new Vector2(width, height)))
                        ImGui.OpenPopup("addShortcut");
                    ImGuiEx.PopFontScale();
                    ImGui.PopStyleColor();
                    ImGuiEx.SetItemTooltip("Add a new shortcut.");
                }

                if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                    ImGui.OpenPopup("addShortcut");

                PluginUI.DrawExternalWindow(() => DrawAddShortcut(null, this), parentBar.IsDocked);

                ImGuiEx.ClampWindowPosToViewport();

                ImGui.EndPopup();
            }
        }

        private void DrawConfig(bool hasIcon)
        {
            if (ImGui.BeginPopup("editShortcut"))
            {
                parentBar.Reveal();
                SetConfigPopupOpen();

                if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
                {
                    if (ImGui.BeginTabItem("Shortcut"))
                    {
                        ConfigEditorUI.EditShortcutConfigBase(Config, true);

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

                if (ImGui.Button("↑") && ID > 0)
                {
                    ShiftThis(false);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("↓") && ID < ((parent != null) ? (parent.children.Count - 1) : (parentBar.children.Count)))
                {
                    ShiftThis(true);
                    ImGui.CloseCurrentPopup();
                }
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
                    QoLBar.Plugin.ExecuteCommand("/echo <se> Right click to delete!");
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

        public static bool ParseName(ref string name, out string tooltip, out int icon, out string args)
        {
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
            if (name.StartsWith("::"))
            {
                var substart = 2;

                // Parse icon arguments
                var done = false;
                while (!done)
                {
                    if (name.Length > substart)
                    {
                        var arg = name[substart];
                        switch (arg)
                        {
                            case '_': // Disable all args
                                args = "_";
                                substart = 3;
                                done = true;
                                break;
                            case 'f': // Use game icon frame
                            case 'l': // Use LR icon
                            case 'h': // Use HR icon
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

                int.TryParse(name.Substring(substart), out icon);
                return true;
            }
            else
                return false;
        }

        public static void DrawIconBrowserButton()
        {
            var iconSize = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
            ImGui.SameLine(ImGui.GetWindowContentRegionWidth() + Style.WindowPadding.X - iconSize);
            if (DrawIcon(46, new Vector2(iconSize), 1.0f, Vector2.Zero, Vector4.One, false, "l"))
                QoLBar.Plugin.ToggleIconBrowser();
            ImGuiEx.SetItemTooltip("Opens up a list of all icons you can use instead of text.\n" +
                "Warning: This will load EVERY icon available so it will probably lag for a moment.\n" +
                "Clicking on one will copy text to be pasted into the \"Name\" field of a shortcut.\n" +
                "Additionally, while the browser is open it will autofill the \"Name\" of shortcuts.");
        }

        public static void DrawAddShortcut(BarUI barUI, ShortcutUI shUI)
        {
            if (ImGui.BeginPopup("addShortcut"))
            {
                barUI?.Reveal();
                shUI?.parentBar.Reveal();
                QoLBar.Plugin.ui.SetConfigPopupOpen();

                BarUI.tempSh ??= new ShCfg();
                var newSh = BarUI.tempSh;
                ConfigEditorUI.EditShortcutConfigBase(newSh, false);

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
                    var imports = Importing.TryImport(ImGui.GetClipboardText(), true);
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
        }

        public static Vector2 iconFrameUV0 = new Vector2(1f / 426f, 141f / 426f);
        public static Vector2 iconFrameUV1 = new Vector2(47f / 426f, 187f / 426f);
        public static Vector2 iconHoverUV0 = new Vector2(49f / 426f, 238f / 426f);
        public static Vector2 iconHoverUV1 = new Vector2(95f / 426f, 284f / 426f);
        //public static Vector2 iconHoverFrameUV0 = new Vector2(248f / 426f, 149f / 426f);
        //public static Vector2 iconHoverFrameUV1 = new Vector2(304f / 426f, 205f / 426f);

        public static bool DrawIcon(int icon, Vector2 size, float zoom, Vector2 offset, Vector4 tint, bool invertFrame, string args = "_", bool retExists = false, bool noButton = false)
        {
            bool ret = false;

            var hasArgs = args != "_";

            TextureDictionary texd = null;
            if (hasArgs)
            {
                if (args.Contains("l"))
                    texd = QoLBar.textureDictionaryLR;
                else if (args.Contains("h"))
                    texd = QoLBar.textureDictionaryHR;
            }

            texd ??= QoLBar.TextureDictionary;
            var tex = texd[icon];
            if (tex == null)
            {
                if (!retExists)
                {
                    if (icon == 66001)
                    {
                        if (noButton)
                            ImGui.Dummy(size);
                        else
                            ret = ImGui.Button("X##FailedTexture", size);
                    }
                    else
                        ret = DrawIcon(66001, size, zoom, offset, tint, invertFrame, args, retExists, noButton);
                }
            }
            else
            {
                var frameArg = false;
                if (hasArgs)
                {
                    frameArg = args.Contains("f");
                    if (invertFrame)
                        frameArg = !frameArg;
                }

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);

                if (frameArg)
                {
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
                }

                var z = 0.5f / zoom;
                var uv0 = new Vector2(0.5f - z + offset.X, 0.5f - z + offset.Y);
                var uv1 = new Vector2(0.5f + z + offset.X, 0.5f + z + offset.Y);
                if (!noButton)
                    ret = ImGui.ImageButton(tex.ImGuiHandle, size, uv0, uv1, 0, Vector4.Zero, tint);
                else
                    ImGui.Image(tex.ImGuiHandle, size, uv0, uv1, tint);

                if (frameArg)
                {
                    var frame = QoLBar.TextureDictionary[TextureDictionary.FrameIconID];
                    if (frame != null && frame.ImGuiHandle != IntPtr.Zero)
                    {
                        var _sizeInc = size * 0.075f;
                        var _rMin = ImGui.GetItemRectMin() - _sizeInc;
                        var _rMax = ImGui.GetItemRectMax() + _sizeInc;
                        ImGui.GetWindowDrawList().AddImage(frame.ImGuiHandle, _rMin, _rMax, iconFrameUV0, iconFrameUV1); // Frame
                        if (!noButton && ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
                        {
                            ImGui.GetWindowDrawList().AddImage(frame.ImGuiHandle, _rMin, _rMax, iconHoverUV0, iconHoverUV1, 0x85FFFFFF); // Frame Center Glow
                            //ImGui.GetWindowDrawList().AddImage(_buttonshine.ImGuiHandle, _rMin - (_sizeInc * 1.5f), _rMax + (_sizeInc * 1.5f), iconHoverFrameUV0, iconHoverFrameUV1); // Edge glow // TODO: Probably somewhat impossible as is, but fix glow being clipped
                        }
                        // TODO: Find a way to do the click animation
                    }
                }

                ImGui.PopStyleColor(frameArg ? 3 : 1);
                if (retExists)
                    ret = true;
            }
            return ret;
        }

        public static Vector4 AnimateColor(Vector4 c)
        {
            float r, g, b, a, x;
            r = g = b = a = 1;
            var t = QoLBar.GetDrawTime();
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
}
