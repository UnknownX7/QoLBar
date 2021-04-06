using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using static QoLBar.ShCfg;

namespace QoLBar
{
    public class ShortcutUI : IDisposable
    {
        public int ID { get; private set; }
        //public ShCfg shConfig => (parent != null) ? parent.shConfig.SubList[shNumber] : parentBar.barConfig.ShortcutList[shNumber];
        public ShCfg Config { get; private set; }
        public void SetShortcutNumber(int n)
        {
            ID = n;
            Config = (parent != null) ? parent.Config.SubList[n] : parentBar.Config.ShortcutList[n];
        }

        private static ImGuiStylePtr Style => ImGui.GetStyle();

        public readonly BarUI parentBar;
        public readonly ShortcutUI parent;
        public readonly List<ShortcutUI> children = new List<ShortcutUI>();

        private bool IsConfigPopupOpen() => QoLBar.Plugin.ui.IsConfigPopupOpen();
        private void SetConfigPopupOpen() => QoLBar.Plugin.ui.SetConfigPopupOpen();

        private int _i = 0;
        public bool _activated = false;

        public ShortcutUI(BarUI bar)
        {
            parentBar = bar;
            SetShortcutNumber(bar.children.Count);
            Initialize();
        }

        public ShortcutUI(ShortcutUI sh)
        {
            parentBar = sh.parentBar;
            parent = sh;
            SetShortcutNumber(sh.children.Count);
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
            var ui = this;
            if (Config.Type == ShortcutType.Category && Config.Mode != ShortcutMode.Default && children.Count > 0)
                ui = children[Math.Min(_i, children.Count - 1)];
            var sh = ui.Config;

            var name = sh.Name;
            var useIcon = ParseName(ref name, out string tooltip, out int icon, out string args);

            if (inCategory)
            {
                if (useIcon || !ui.parent.Config.CategoryNoBackground)
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

            ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * (!inCategory ? parentBar.Config.FontScale : ui.parent.Config.CategoryFontScale));
            if (sh.Type == ShortcutType.Spacer)
            {
                if (useIcon)
                    DrawIcon(icon, new Vector2(height), sh.IconZoom, new Vector2(sh.IconOffset[0], sh.IconOffset[1]), c, QoLBar.Config.UseIconFrame, args, true, true);
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

            if (!inCategory && parentBar._maxW < ImGui.GetItemRectSize().X)
                parentBar._maxW = ImGui.GetItemRectSize().X;

            if (inCategory)
                ImGui.PopStyleColor();

            var wasHovered = false;
            clicked = clicked || (_activated && !parentBar._activated);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    clicked = true;

                if (!clicked)
                {
                    var isHoverEnabled = sh.CategoryOnHover && sh.Type == ShortcutType.Category;
                    var allowHover = parentBar.IsFullyRevealed && !IsConfigPopupOpen() && !ImGui.IsPopupOpen("ShortcutCategory") && Keybind.GameHasFocus() && !ImGui.IsAnyMouseDown() && !ImGui.IsMouseReleased(ImGuiMouseButton.Right);
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
                    parentBar._activated = true;
                }

                if (ui != this)
                {
                    switch (Config.Mode)
                    {
                        case ShortcutMode.Incremental:
                            _i = (_i + 1) % children.Count;
                            break;
                        case ShortcutMode.Random:
                            _i = (int)(QoLBar.GetFrameCount() % children.Count);
                            break;
                    }
                }

                if (!inCategory)
                    OnClick(parentBar.IsVertical, wasHovered);
                else
                {
                    var cols = Math.Max(parent.Config.CategoryColumns, 1);
                    OnClick(parent.children.Count >= (cols * (cols - 1) + 1), wasHovered);
                    if (!parent.Config.CategoryStaysOpen && sh.Type != ShortcutType.Category && sh.Type != ShortcutType.Spacer)
                        ImGui.CloseCurrentPopup();
                }
            }

            ImGui.OpenPopupContextItem("editItem");

            if (sh.Type == ShortcutType.Category)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(sh.CategorySpacing[0], sh.CategorySpacing[1]));
                ImGuiEx.PushFontScale(sh.CategoryScale);
                DrawCategory();
                ImGuiEx.PopFontScale();
                ImGui.PopStyleVar();
            }

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
            ImGuiEx.PushFontScale(1);
            DrawConfig(useIcon);
            ImGuiEx.PopFontScale();
            ImGui.PopStyleVar();
        }

        private void DrawCategory()
        {
            parentBar.SetCategoryPosition();
            if (ImGui.BeginPopup("ShortcutCategory", (Config.CategoryNoBackground ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoMove))
            {
                parentBar.Reveal();

                var cols = Math.Max(Config.CategoryColumns, 1);
                var width = Config.CategoryWidth * ImGui.GetIO().FontGlobalScale * Config.CategoryScale;

                for (int i = 0; i < children.Count; i++)
                {
                    ImGui.PushID(i);

                    children[i].DrawShortcut(width);

                    if (i % cols != cols - 1)
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
                        ImGui.OpenPopup("addItem");
                    ImGuiEx.PopFontScale();
                    ImGui.PopStyleColor();
                    ImGuiEx.SetItemTooltip("Add a new shortcut.");
                }

                if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                    ImGui.OpenPopup("addItem");

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
                ImGuiEx.PushFontScale(1);
                DrawAdd();
                ImGuiEx.PopFontScale();
                ImGui.PopStyleVar();

                ImGuiEx.ClampWindowPos(ImGui.GetIO().DisplaySize);

                ImGui.EndPopup();
            }
        }

        private void DrawAdd()
        {
            if (ImGui.BeginPopup("addItem"))
            {
                parentBar.Reveal();
                SetConfigPopupOpen();

                BarUI.tempSh ??= new ShCfg();
                var newSh = BarUI.tempSh;
                ItemBaseUI(newSh, false);

                if (ImGui.Button("Create"))
                {
                    AddShortcut(newSh);
                    BarUI.tempSh = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Import"))
                {
                    var imports = Importing.TryImport(ImGui.GetClipboardText(), true);
                    if (imports.shortcut != null)
                        AddShortcut(imports.shortcut);
                    else if (imports.bar != null)
                    {
                        foreach (var sh in imports.bar.ShortcutList)
                            AddShortcut(sh);
                    }
                    QoLBar.Config.Save();
                    ImGui.CloseCurrentPopup();
                }
                ImGuiEx.SetItemTooltip("Import a shortcut from the clipboard,\n" +
                    "or import all of another bar's shortcuts.");

                ImGuiEx.ClampWindowPos(ImGui.GetIO().DisplaySize);

                ImGui.EndPopup();
            }
        }

        private void DrawConfig(bool hasIcon)
        {
            if (ImGui.BeginPopup("editItem"))
            {
                parentBar.Reveal();
                SetConfigPopupOpen();

                if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
                {
                    if (ImGui.BeginTabItem("Shortcut"))
                    {
                        ItemBaseUI(Config, true);

                        if (Config.Type != ShortcutType.Spacer)
                        {
                            var _m = (int)Config.Mode;
                            ImGui.TextUnformatted("Mode");
                            ImGuiEx.SetItemTooltip("Changes the behavior when pressed.\n" +
                                "Note: Not intended to be used with categories containing subcategories.");

                            ImGui.RadioButton("Default", ref _m, 0);
                            ImGuiEx.SetItemTooltip("Default behavior, categories must be set to this to edit their shortcuts!");

                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Incremental", ref _m, 1);
                            ImGuiEx.SetItemTooltip("Executes each line/shortcut in order over multiple presses.");

                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton("Random", ref _m, 2);
                            ImGuiEx.SetItemTooltip("Executes a random line/shortcut when pressed.");

                            if (_m != (int)Config.Mode)
                            {
                                Config.Mode = (ShortcutMode)_m;
                                QoLBar.Config.Save();

                                if (Config.Mode == ShortcutMode.Random)
                                {
                                    var c = Math.Max(1, (Config.Type == ShortcutType.Category) ? children.Count : Config.Command.Split('\n').Length);
                                    _i = (int)(QoLBar.GetFrameCount() % c);
                                }
                                else
                                    _i = 0;
                            }
                        }

                        var color = ImGui.ColorConvertU32ToFloat4(Config.Color);
                        color.W += Config.ColorAnimation / 255f; // Temporary
                        if (ImGui.ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.AlphaPreviewHalf))
                        {
                            Config.Color = ImGui.ColorConvertFloat4ToU32(color);
                            Config.ColorAnimation = Math.Max((int)Math.Round(color.W * 255) - 255, 0);
                            QoLBar.Config.Save();
                        }

                        if (Config.Type != ShortcutType.Spacer)
                            Keybind.KeybindInput(Config);

                        ImGui.EndTabItem();
                    }

                    if (Config.Type == ShortcutType.Category && ImGui.BeginTabItem("Category"))
                    {
                        if (ImGui.SliderInt("Width", ref Config.CategoryWidth, 0, 200))
                            QoLBar.Config.Save();
                        ImGuiEx.SetItemTooltip("Set to 0 to use text width.");

                        if (ImGui.SliderInt("Columns", ref Config.CategoryColumns, 1, 12))
                            QoLBar.Config.Save();
                        ImGuiEx.SetItemTooltip("Number of shortcuts in each row before starting another.");

                        if (ImGui.DragFloat("Scale", ref Config.CategoryScale, 0.002f, 0.7f, 1.5f, "%.2f"))
                            QoLBar.Config.Save();

                        if (ImGui.DragFloat("Font Scale", ref Config.CategoryFontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
                            QoLBar.Config.Save();

                        var spacing = new Vector2(Config.CategorySpacing[0], Config.CategorySpacing[1]);
                        if (ImGui.DragFloat2("Spacing", ref spacing, 0.12f, 0, 32, "%.f"))
                        {
                            Config.CategorySpacing[0] = (int)spacing.X;
                            Config.CategorySpacing[1] = (int)spacing.Y;
                            QoLBar.Config.Save();
                        }

                        if (ImGui.Checkbox("Open on Hover", ref Config.CategoryOnHover))
                            QoLBar.Config.Save();
                        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                        if (ImGui.Checkbox("Stay Open on Selection", ref Config.CategoryStaysOpen))
                            QoLBar.Config.Save();
                        ImGuiEx.SetItemTooltip("Keeps the category open when pressing shortcuts within it.\nMay not work if the shortcut interacts with other plugins.");

                        if (ImGui.Checkbox("No Background", ref Config.CategoryNoBackground))
                            QoLBar.Config.Save();

                        ImGui.EndTabItem();
                    }

                    if (hasIcon && ImGui.BeginTabItem("Icon"))
                    {
                        // Name is available here for ease of access since it pertains to the icon as well
                        if (IconBrowserUI.iconBrowserOpen && IconBrowserUI.doPasteIcon)
                        {
                            var split = Config.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
                            Config.Name = $"::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
                            QoLBar.Config.Save();
                            IconBrowserUI.doPasteIcon = false;
                        }
                        if (ImGui.InputText("Name", ref Config.Name, 256))
                            QoLBar.Config.Save();
                        ImGuiEx.SetItemTooltip("Icons accept arguments between \"::\" and their ID. I.e. \"::f21\".\n" +
                            "\t' f ' - Applies the hotbar frame (or removes it if applied globally).\n" +
                            "\t' _ ' - Disables arguments, including implicit ones. Cannot be used with others.");

                        if (ImGui.DragFloat("Zoom", ref Config.IconZoom, 0.005f, 1.0f, 5.0f, "%.2f"))
                            QoLBar.Config.Save();

                        var offset = new Vector2(Config.IconOffset[0], Config.IconOffset[1]);
                        if (ImGui.DragFloat2("Offset", ref offset, 0.002f, -0.5f, 0.5f, "%.2f"))
                        {
                            Config.IconOffset[0] = offset.X;
                            Config.IconOffset[1] = offset.Y;
                            QoLBar.Config.Save();
                        }

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
                if (ImGui.Button("↓") && ID < (parent.children.Count - 1))
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

                var iconSize = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() + Style.WindowPadding.X - iconSize);
                if (DrawIcon(46, new Vector2(iconSize), 1.0f, Vector2.Zero, Vector4.One, false))
                    QoLBar.Plugin.ToggleIconBrowser();
                ImGuiEx.SetItemTooltip("Opens up a list of all icons you can use instead of text.\n" +
                    "Warning: This will load EVERY icon available so it will probably lag for a moment.\n" +
                    "Clicking on one will copy text to be pasted into the \"Name\" field of a shortcut.\n" +
                    "Additionally, while the browser is open it will autofill the \"Name\" of shortcuts.");

                ImGuiEx.ClampWindowPos(ImGui.GetIO().DisplaySize);

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
                children[i].SetShortcutNumber(i);
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

        public static void ItemBaseUI(ShCfg sh, bool editing)
        {
            if (IconBrowserUI.iconBrowserOpen && IconBrowserUI.doPasteIcon)
            {
                var split = sh.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
                sh.Name = $"::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
                if (editing)
                    QoLBar.Config.Save();
                IconBrowserUI.doPasteIcon = false;
            }
            if (ImGui.InputText("Name                    ", ref sh.Name, 256) && editing) // Not a bug... just want the window to not change width depending on which type it is...
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Start the name with ::x where x is a number to use icons, i.e. \"::2914\".\n" +
                "Use ## anywhere in the name to make the text afterwards into a tooltip,\ni.e. \"Name##This is a Tooltip\".");

            var _t = (int)sh.Type;
            ImGui.TextUnformatted("Type");
            ImGui.RadioButton("Command", ref _t, 0);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            ImGui.RadioButton("Category", ref _t, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            ImGui.RadioButton("Spacer", ref _t, 2);
            if (_t != (int)sh.Type)
            {
                sh.Type = (ShortcutType)_t;
                if (sh.Type == ShortcutType.Category)
                    sh.SubList ??= new List<ShCfg>();

                if (editing)
                    QoLBar.Config.Save();
            }

            if (sh.Type != ShortcutType.Spacer && (sh.Type != ShortcutType.Category || sh.Mode == ShortcutMode.Default))
            {
                var height = ImGui.GetFontSize() * Math.Min(sh.Command.Split('\n').Length + 1, 7) + Style.FramePadding.Y * 2; // ImGui issue #238: can't disable multiline scrollbar and it appears a whole line earlier than it should, so thats cool I guess
                if (ImGui.InputTextMultiline("Command##Input", ref sh.Command, 65535, new Vector2(0, height)) && editing)
                    QoLBar.Config.Save();
            }
        }

        private static ImGuiScene.TextureWrap _buttonshine;
        private static Vector2 _uvMin, _uvMax, _uvMinHover, _uvMaxHover;//, _uvMinHover2, _uvMaxHover2;
        public static bool DrawIcon(int icon, Vector2 size, float zoom, Vector2 offset, Vector4 tint, bool invertFrame, string args = "_", bool retExists = false, bool noButton = false)
        {
            bool ret = false;
            var texd = QoLBar.textureDictionary;
            var tex = texd[icon];
            if (tex == null)
            {
                if (!retExists)
                {
                    if (icon == 66001)
                        ret = ImGui.Button("  X  ##FailedTexture");
                    else
                        ret = DrawIcon(66001, size, zoom, offset, tint, invertFrame, args);
                }
            }
            else
            {
                var frameArg = false;
                if (args != "_")
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

                if (frameArg && texd[QoLBar.FrameIconID] != null)
                {
                    if (_buttonshine == null)
                    {
                        _buttonshine = texd[QoLBar.FrameIconID];
                        _uvMin = new Vector2(1f / _buttonshine.Width, 0f / _buttonshine.Height);
                        _uvMax = new Vector2(47f / _buttonshine.Width, 46f / _buttonshine.Height);
                        _uvMinHover = new Vector2(49f / _buttonshine.Width, 97f / _buttonshine.Height);
                        _uvMaxHover = new Vector2(95f / _buttonshine.Width, 143f / _buttonshine.Height);
                        //_uvMinHover2 = new Vector2(248f / _buttonshine.Width, 8f / _buttonshine.Height);
                        //_uvMaxHover2 = new Vector2(304f / _buttonshine.Width, 64f / _buttonshine.Height);
                    }
                    var _sizeInc = size * 0.075f;
                    var _rMin = ImGui.GetItemRectMin() - _sizeInc;
                    var _rMax = ImGui.GetItemRectMax() + _sizeInc;
                    ImGui.GetWindowDrawList().AddImage(_buttonshine.ImGuiHandle, _rMin, _rMax, _uvMin, _uvMax); // Frame
                    if (!noButton && ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
                    {
                        ImGui.GetWindowDrawList().AddImage(_buttonshine.ImGuiHandle, _rMin, _rMax, _uvMinHover, _uvMaxHover, 0x85FFFFFF); // Frame Center Glow
                        //ImGui.GetWindowDrawList().AddImage(_buttonshine.ImGuiHandle, _rMin - (_sizeInc * 1.5f), _rMax + (_sizeInc * 1.5f), _uvMinHover2, _uvMaxHover2); // Edge glow // TODO: Probably somewhat impossible as is, but fix glow being clipped
                    }
                    // TODO: Find a way to do the click animation

                    ImGui.PopStyleColor(2);
                }

                ImGui.PopStyleColor();
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
