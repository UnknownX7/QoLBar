using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using static QoLBar.BarCfg;
using static QoLBar.ShCfg;

namespace QoLBar
{
    // TODO: Split this file into ShortcutUI
    public class BarUI : IDisposable
    {
        private int barNumber;
        public BarCfg barConfig => Config.BarCfgs[barNumber];
        public void SetBarNumber(int i)
        {
            barNumber = i;
            SetupPosition();
        }

        public bool IsVisible => !barConfig.Hidden && CheckConditionSet();
        public bool GetHidden() => barConfig.Hidden;
        public void SetHidden() => SetHidden(!barConfig.Hidden);
        public void SetHidden(bool b)
        {
            barConfig.Hidden = b;
            Config.Save();
        }

        private static ImGuiStylePtr Style => ImGui.GetStyle();

        private Vector2 ConfigPosition => new Vector2((float)Math.Floor(barConfig.Position[0] * window.X), (float)Math.Floor(barConfig.Position[1] * window.Y));
        private Vector2 ConfigOffset => new Vector2((float)Math.Floor(barConfig.Offset[0] * window.X), (float)Math.Floor(barConfig.Offset[1] * window.Y));

        private static ShCfg _sh;
        private Vector2 window = ImGui.GetIO().DisplaySize;
        private static Vector2 mousePos = ImGui.GetIO().MousePos;
        private static float globalSize = ImGui.GetIO().FontGlobalScale;
        private Vector2 barSize = new Vector2(200, 38);
        private Vector2 barPos;
        private ImGuiWindowFlags flags;
        private Vector2 piv = Vector2.Zero;
        private Vector2 hidePos = Vector2.Zero;
        private Vector2 revealPos = Vector2.Zero;
        private bool vertical = false;
        private bool docked = true;

        private bool _reveal = false;
        public void Reveal() => _reveal = true;
        public void ForceReveal() => _lastReveal = _reveal = true;
        public void Hide() => _reveal = false;

        private bool IsConfigPopupOpen() => Plugin.ui.IsConfigPopupOpen();
        private void SetConfigPopupOpen() => Plugin.ui.SetConfigPopupOpen();

        private bool _firstframe = true;
        private bool _setPos = true;
        private bool _lastReveal = true;
        private bool _mouseRevealed = false;
        private float _maxW = 0;
        private Vector2 _tweenStart;
        private float _tweenProgress = 1;
        private Vector2 _catpiv = Vector2.Zero;
        private Vector2 _catpos = Vector2.Zero;
        private Vector2 _maincatpos = Vector2.Zero;
        private bool _activated = false;

        private static QoLBar Plugin => QoLBar.Plugin;
        private static Configuration Config => QoLBar.Config;

        public BarUI(int nbar)
        {
            barNumber = nbar;
            SetupPosition();

            static void RandomizeShortcuts(List<ShCfg> shortcuts)
            {
                foreach (var sh in shortcuts)
                {
                    if (sh.Mode == ShortcutMode.Random)
                    {
                        var count = Math.Max(1, (sh.Type == ShortcutType.Category) ? sh.SubList.Count : sh.Command.Split('\n').Length);
                        sh._i = DateTime.Now.Millisecond % count;
                    }
                    if (sh.Type == ShortcutType.Category)
                        RandomizeShortcuts(sh.SubList);
                }
            }

            RandomizeShortcuts(barConfig.ShortcutList);
        }

        private bool CheckConditionSet()
        {
            if (barConfig.ConditionSet >= 0 && barConfig.ConditionSet < Config.ConditionSets.Count)
                return Config.ConditionSets[barConfig.ConditionSet].CheckConditions();
            else
                return true;
        }

        private void SetupPosition()
        {
            var pivX = 0.0f;
            var pivY = 0.0f;
            var defPos = 0.0f;
            var offset = 0.0f;
            switch (barConfig.DockSide)
            {
                case BarDock.Top: //    0.0 1.0, 0.5 1.0, 1.0 1.0 // 0 0(+H),    winX/2 0(+H),    winX 0(+H)
                    pivY = 1.0f;
                    defPos = 0.0f;
                    vertical = false;
                    docked = true;
                    break;
                case BarDock.Left: //   1.0 0.0, 1.0 0.5, 1.0 1.0 // 0(+W) 0,    0(+W) winY/2,    0(+W) winY
                    pivY = 1.0f;
                    defPos = 0.0f;
                    vertical = true;
                    docked = true;
                    break;
                case BarDock.Bottom: // 0.0 0.0, 0.5 0.0, 1.0 0.0 // 0 winY(-H), winX/2 winY(-H), winX winY(-H)
                    pivY = 0.0f;
                    defPos = window.Y;
                    vertical = false;
                    docked = true;
                    break;
                case BarDock.Right: //  0.0 0.0, 0.0 0.5, 0.0 1.0 // winX(-W) 0, winX(-W) winY/2, winX(-W) winY
                    pivY = 0.0f;
                    defPos = window.X;
                    vertical = true;
                    docked = true;
                    break;
                case BarDock.UndockedH:
                    piv = Vector2.Zero;
                    vertical = false;
                    docked = false;
                    _setPos = true;
                    return;
                case BarDock.UndockedV:
                    piv = Vector2.Zero;
                    vertical = true;
                    docked = false;
                    _setPos = true;
                    return;
                default:
                    break;
            }

            switch (barConfig.Alignment)
            {
                case BarAlign.LeftOrTop:
                    pivX = 0.0f;
                    offset = 22 + ImGui.GetFontSize();
                    break;
                case BarAlign.Center:
                    pivX = 0.5f;
                    break;
                case BarAlign.RightOrBottom:
                    pivX = 1.0f;
                    offset = -22 - ImGui.GetFontSize();
                    break;
                default:
                    break;
            }

            if (!vertical)
            {
                piv.X = pivX;
                piv.Y = pivY;

                hidePos.X = window.X * pivX + offset + (ConfigOffset.X * globalSize);
                hidePos.Y = defPos;
                revealPos.X = hidePos.X;
            }
            else
            {
                piv.X = pivY;
                piv.Y = pivX;

                hidePos.X = defPos;
                hidePos.Y = window.Y * pivX + offset + (ConfigOffset.Y * globalSize);
                revealPos.Y = hidePos.Y;
            }

            SetupRevealPosition();

            barPos = hidePos;
            _tweenStart = hidePos;
        }

        private void SetupRevealPosition()
        {
            switch (barConfig.DockSide)
            {
                case BarDock.Top:
                    revealPos.Y = Math.Max(hidePos.Y + barSize.Y + (ConfigOffset.Y * globalSize), GetHidePosition().Y + 1);
                    break;
                case BarDock.Left:
                    revealPos.X = Math.Max(hidePos.X + barSize.X + (ConfigOffset.X * globalSize), GetHidePosition().X + 1);
                    break;
                case BarDock.Bottom:
                    revealPos.Y = Math.Min(hidePos.Y - barSize.Y + (ConfigOffset.Y * globalSize), GetHidePosition().Y - 1);
                    break;
                case BarDock.Right:
                    revealPos.X = Math.Min(hidePos.X - barSize.X + (ConfigOffset.X * globalSize), GetHidePosition().X - 1);
                    break;
                default:
                    break;
            }
        }

        private void SetupImGuiFlags()
        {
            flags = ImGuiWindowFlags.None;

            flags |= ImGuiWindowFlags.NoDecoration;
            if (docked || barConfig.LockedPosition)
                flags |= ImGuiWindowFlags.NoMove;
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
            if (barConfig.NoBackground)
                flags |= ImGuiWindowFlags.NoBackground;
            flags |= ImGuiWindowFlags.NoSavedSettings;
            flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }

        private Vector2 GetHidePosition()
        {
            var _hidePos = hidePos;
            if (barConfig.Hint)
            {
                var _winPad = Style.WindowPadding * 2;

                switch (barConfig.DockSide)
                {
                    case BarDock.Top:
                        _hidePos.Y += _winPad.Y;
                        break;
                    case BarDock.Left:
                        _hidePos.X += _winPad.X;
                        break;
                    case BarDock.Bottom:
                        _hidePos.Y -= _winPad.Y;
                        break;
                    case BarDock.Right:
                        _hidePos.X -= _winPad.X;
                        break;
                    default:
                        break;
                }
            }
            return _hidePos;
        }

        public void SetupHotkeys(List<ShCfg> shortcuts, ShCfg parent = null)
        {
            foreach (var sh in shortcuts)
            {
                sh._parent = parent;

                if (sh.Hotkey > 0 && sh.Type != ShortcutType.Spacer)
                    Keybind.AddHotkey(this, sh);
                if (sh.Type == ShortcutType.Category)
                    SetupHotkeys(sh.SubList, sh);
            }
        }

        private void ClearActivated(List<ShCfg> shortcuts)
        {
            foreach (var sh in shortcuts)
            {
                if (!_activated)
                    sh._activated = false;
                if (sh.Type == ShortcutType.Category)
                    ClearActivated(sh.SubList);
            }
        }

        public void Draw()
        {
            CheckGameResolution();

            if (!IsVisible) return;

            var io = ImGui.GetIO();
            mousePos = io.MousePos;
            globalSize = io.FontGlobalScale;

            if (docked || barConfig.Visibility == BarVisibility.Immediate)
            {
                SetupRevealPosition();

                CheckMousePosition();
            }
            else
                Reveal();

            if (!docked && !_firstframe && !_reveal && !_lastReveal)
            {
                ClearActivated(barConfig.ShortcutList);
                return;
            }

            if (_firstframe || _reveal || (barPos != hidePos) || (!docked && _lastReveal)) // Don't bother to render when fully off screen
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(barConfig.Spacing[0]));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.286f, 0.286f, 0.286f, 0.9f));

                if (docked)
                    ImGui.SetNextWindowPos(barPos, ImGuiCond.Always, piv);
                else if (_setPos || barConfig.LockedPosition)
                {
                    if (!_firstframe)
                    {
                        ImGui.SetNextWindowPos(ConfigPosition);
                        _setPos = false;
                    }
                    else
                        ImGui.SetNextWindowPos(window);
                }
                ImGui.SetNextWindowSize(barSize);

                SetupImGuiFlags();
                ImGui.Begin($"QoLBar##{barNumber}", flags);

                PushFontScale(barConfig.Scale);

                if (_mouseRevealed && ImGui.IsWindowHovered(ImGuiHoveredFlags.RectOnly))
                    Reveal();
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
                    ImGui.OpenPopup($"BarConfig##{barNumber}");

                DrawItems();

                if (barConfig.Editing || barConfig.ShortcutList.Count < 1)
                    DrawAddButton();

                if (!barConfig.LockedPosition && !_firstframe && !docked && ImGui.GetWindowPos() != ConfigPosition)
                {
                    var newPos = ImGui.GetWindowPos() / window;
                    barConfig.Position = new[] { newPos.X, newPos.Y };
                    Config.Save();
                }

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
                PushFontScale(1);
                BarConfigPopup();
                PopFontScale();
                ImGui.PopStyleVar();

                SetBarSize();

                PopFontScale();

                ImGui.End();

                ImGui.PopStyleColor();
                ImGui.PopStyleVar(3);
            }

            if (!_reveal)
                _mouseRevealed = false;

            if (docked)
            {
                SetBarPosition();
                Hide(); // Allows other objects to reveal the bar
            }
            else
                _lastReveal = _reveal;

            ClearActivated(barConfig.ShortcutList);
            _activated = false;

            _firstframe = false;
        }

        private void CheckGameResolution()
        {
            var io = ImGui.GetIO();
            // Fix bar positions when the game is resized
            if (io.DisplaySize != window)
            {
                window = io.DisplaySize;
                SetupPosition();
            }
        }

        private (Vector2, Vector2) CalculateRevealPosition()
        {
            var pos = docked ? revealPos : ConfigPosition;
            var min = new Vector2(pos.X - (barSize.X * piv.X), pos.Y - (barSize.Y * piv.Y));
            var max = new Vector2(pos.X + (barSize.X * (1 - piv.X)), pos.Y + (barSize.Y * (1 - piv.Y)));
            return (min, max);
        }

        private void CheckMousePosition()
        {
            if (docked && _reveal)
                return;

            (var _min, var _max) = CalculateRevealPosition();

            switch (barConfig.DockSide)
            {
                case BarDock.Top:
                    _max.Y = Math.Max(Math.Max(_max.Y - barSize.Y * (1 - barConfig.RevealAreaScale), _min.Y + 1), GetHidePosition().Y + 1);
                    break;
                case BarDock.Left:
                    _max.X = Math.Max(Math.Max(_max.X - barSize.X * (1 - barConfig.RevealAreaScale), _min.X + 1), GetHidePosition().X + 1);
                    break;
                case BarDock.Bottom:
                    _min.Y = Math.Min(Math.Min(_min.Y + barSize.Y * (1 - barConfig.RevealAreaScale), _max.Y - 1), GetHidePosition().Y - 1);
                    break;
                case BarDock.Right:
                    _min.X = Math.Min(Math.Min(_min.X + barSize.X * (1 - barConfig.RevealAreaScale), _max.X - 1), GetHidePosition().X - 1);
                    break;
                default:
                    break;
            }

            var mX = mousePos.X;
            var mY = mousePos.Y;

            //if (ImGui.IsMouseHoveringRect(_min, _max, true)) // This only works in the context of a window... thanks ImGui
            if (barConfig.Visibility == BarVisibility.Always || (_min.X <= mX && mX < _max.X && _min.Y <= mY && mY < _max.Y))
            {
                _mouseRevealed = true;
                Reveal();
            }
            else
                Hide();
        }

        private bool ParseName(ref string name, out string tooltip, out int icon, out string args)
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

        private void DrawItems()
        {
            for (int i = 0; i < barConfig.ShortcutList.Count; i++)
            {
                ImGui.PushID(i);

                DrawShortcut(i, barConfig.ShortcutList, barConfig.ButtonWidth * globalSize * barConfig.Scale, (sh, wasHovered) =>
                {
                    ItemClicked(sh, vertical, false, wasHovered);
                });

                if (!vertical && i != barConfig.ShortcutList.Count - 1)
                    ImGui.SameLine();

                ImGui.PopID();
            }
        }

        private void DrawShortcut(int i, List<ShCfg> shortcuts, float width, Action<ShCfg, bool> callback)
        {
            var inCategory = (shortcuts != barConfig.ShortcutList);
            var sh = shortcuts[i];
            var type = sh.Type;
            ShCfg parentShortcut = null;
            if (type == ShortcutType.Category && sh.Mode != ShortcutMode.Default && sh.SubList.Count > 0)
            {
                parentShortcut = sh;
                sh = sh.SubList[Math.Min(sh._i, sh.SubList.Count - 1)];
                type = sh.Type;
            }
            var name = sh.Name;

            var useIcon = ParseName(ref name, out string tooltip, out int icon, out string args);

            if (inCategory)
            {
                if (useIcon || !sh._parent.CategoryNoBackground)
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                else
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.08f, 0.08f, 0.94f));
            }

            var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
            var clicked = false;

            var c = ImGui.ColorConvertU32ToFloat4(sh.Color);
            c.W += sh.ColorAnimation / 255f; // Temporary
            if (c.W > 1)
                c = ShortcutUI.AnimateColor(c);

            PushFontScale(GetFontScale() * (!inCategory ? barConfig.FontScale : sh._parent.CategoryFontScale));
            if (type == ShortcutType.Spacer)
            {
                if (useIcon)
                    ShortcutUI.DrawIcon(icon, new Vector2(height), sh.IconZoom, new Vector2(sh.IconOffset[0], sh.IconOffset[1]), c, Config.UseIconFrame, args, true, true);
                else
                {
                    var wantedSize = ImGui.GetFontSize();
                    var textSize = ImGui.CalcTextSize(name);
                    ImGui.BeginChild((uint)i, new Vector2((width == 0) ? (textSize.X + Style.FramePadding.X * 2) : width, height));
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
                clicked = ShortcutUI.DrawIcon(icon, new Vector2(height), sh.IconZoom, new Vector2(sh.IconOffset[0], sh.IconOffset[1]), c, Config.UseIconFrame, args);
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, c);
                clicked = ImGui.Button(name, new Vector2(width, height));
                ImGui.PopStyleColor();
            }
            PopFontScale();

            if (!inCategory && _maxW < ImGui.GetItemRectSize().X)
                _maxW = ImGui.GetItemRectSize().X;

            if (inCategory)
                ImGui.PopStyleColor();

            var wasHovered = false;
            clicked = clicked || (sh._activated && !_activated);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    clicked = true;

                if (!clicked)
                {
                    var isHoverEnabled = sh.CategoryOnHover && type == ShortcutType.Category;
                    var allowHover = (!docked || barPos == revealPos) && !IsConfigPopupOpen() && !ImGui.IsPopupOpen("ShortcutCategory") && Keybind.GameHasFocus() && !ImGui.IsAnyMouseDown() && !ImGui.IsMouseReleased(ImGuiMouseButton.Right);
                    if (isHoverEnabled && allowHover)
                    {
                        wasHovered = true;
                        clicked = true;
                    }
                }

                if (!string.IsNullOrEmpty(tooltip))
                    ImGui.SetTooltip(tooltip);
            }

            if (clicked)
            {
                if (sh._activated)
                {
                    sh._activated = false;
                    _activated = true;
                }

                if (parentShortcut != null)
                {
                    switch (parentShortcut.Mode)
                    {
                        case ShortcutMode.Incremental:
                            parentShortcut._i = (parentShortcut._i + 1) % parentShortcut.SubList.Count;
                            break;
                        case ShortcutMode.Random:
                            parentShortcut._i = (int)(QoLBar.GetFrameCount() % parentShortcut.SubList.Count);
                            break;
                    }
                }

                callback(sh, wasHovered);
            }

            ImGui.OpenPopupContextItem("editItem");

            if (type == ShortcutType.Category)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(sh.CategorySpacing[0], sh.CategorySpacing[1]));
                PushFontScale(sh.CategoryScale);
                CategoryPopup(sh);
                PopFontScale();
                ImGui.PopStyleVar();
            }

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
            PushFontScale(1);
            ItemConfigPopup(shortcuts, i, useIcon);
            PopFontScale();
            ImGui.PopStyleVar();
        }

        private void DrawAddButton()
        {
            if (!vertical && barConfig.ShortcutList.Count > 0)
                ImGui.SameLine();

            var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
            PushFontScale(GetFontScale() * barConfig.FontScale);
            if (ImGui.Button("+", new Vector2(barConfig.ButtonWidth * globalSize * barConfig.Scale, height)))
                ImGui.OpenPopup("addItem");
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
                ImGui.SetTooltip("Add a new shortcut.\nRight click this (or the bar background) for options.\nRight click other shortcuts to edit them.");
            PopFontScale();

            if (_maxW < ImGui.GetItemRectSize().X)
                _maxW = ImGui.GetItemRectSize().X;

            //ImGui.OpenPopupContextItem($"BarConfig##{barNumber}"); // Technically unneeded

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
            PushFontScale(1);
            ItemCreatePopup(barConfig.ShortcutList);
            PopFontScale();
            ImGui.PopStyleVar();
        }

        public void ItemClicked(ShCfg sh, bool v, bool subItem, bool wasHovered)
        {
            var type = sh.Type;
            var command = sh.Command;

            switch (type)
            {
                case ShortcutType.Command:
                    switch (sh.Mode)
                    {
                        case ShortcutMode.Incremental:
                            {
                                var lines = command.Split('\n');
                                command = lines[Math.Min(sh._i, lines.Length - 1)];
                                sh._i = (sh._i + 1) % lines.Length;
                                break;
                            }
                        case ShortcutMode.Random:
                            {
                                var lines = command.Split('\n');
                                command = lines[Math.Min(sh._i, lines.Length - 1)];
                                sh._i = (int)(QoLBar.GetFrameCount() % lines.Length); // With this game's FPS drops? Completely random.
                                break;
                            }
                    }
                    Plugin.ExecuteCommand(command);
                    break;
                case ShortcutType.Category:
                    switch (sh.Mode)
                    {
                        case ShortcutMode.Incremental:
                            if (0 <= sh._i && sh._i < sh.SubList.Count)
                                ItemClicked(sh.SubList[sh._i], v, true, wasHovered);
                            sh._i = (sh._i + 1) % Math.Max(1, sh.SubList.Count);
                            break;
                        case ShortcutMode.Random:
                            if (0 <= sh._i && sh._i < sh.SubList.Count)
                                ItemClicked(sh.SubList[sh._i], v, true, wasHovered);
                            sh._i = (int)(QoLBar.GetFrameCount() % Math.Max(1, sh.SubList.Count));
                            break;
                        default:
                            if (!wasHovered)
                                Plugin.ExecuteCommand(command);
                            SetupCategoryPosition(v, subItem);
                            ImGui.OpenPopup("ShortcutCategory");
                            break;
                    }
                    break;
            }
        }

        private void SetupCategoryPosition(bool v, bool subItem)
        {
            Vector2 pos, wMin, wMax;
            if (!subItem)
            {
                (wMin, wMax) = CalculateRevealPosition();
                pos = wMin + ((ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() / 2)) - ImGui.GetWindowPos());
                _maincatpos = pos; // Forces all subcategories to position based on the original category
            }
            else
            {
                wMin = ImGui.GetWindowPos();
                wMax = ImGui.GetWindowPos() + ImGui.GetWindowSize();
                pos = ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() / 2);
            }

            var piv = Vector2.Zero;

            if (!v)
            {
                piv.X = 0.5f;
                if (_maincatpos.Y < window.Y / 2)
                {
                    piv.Y = 0.0f;
                    pos.Y = wMax.Y - Style.WindowPadding.Y / 2;
                }
                else
                {
                    piv.Y = 1.0f;
                    pos.Y = wMin.Y + Style.WindowPadding.Y / 2;
                }
            }
            else
            {
                piv.Y = 0.5f;
                if (_maincatpos.X < window.X / 2)
                {
                    piv.X = 0.0f;
                    pos.X = wMax.X - Style.WindowPadding.X / 2;
                }
                else
                {
                    piv.X = 1.0f;
                    pos.X = wMin.X + Style.WindowPadding.X / 2;
                }
            }
            _catpiv = piv;
            _catpos = pos;
        }

        private void CategoryPopup(ShCfg sh)
        {
            ImGui.SetNextWindowPos(_catpos, ImGuiCond.Appearing, _catpiv);
            if (ImGui.BeginPopup("ShortcutCategory", (sh.CategoryNoBackground ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoMove))
            {
                Reveal();

                var sublist = sh.SubList;
                var cols = Math.Max(sh.CategoryColumns, 1);
                var width = sh.CategoryWidth * globalSize * sh.CategoryScale;

                for (int j = 0; j < sublist.Count; j++)
                {
                    ImGui.PushID(j);

                    var stayOpen = sh.CategoryStaysOpen;
                    DrawShortcut(j, sublist, width, (sh, wasHovered) =>
                    {
                        ItemClicked(sh, sublist.Count >= (cols * (cols - 1) + 1), true, wasHovered);
                        if (!stayOpen && sh.Type != ShortcutType.Category && sh.Type != ShortcutType.Spacer)
                            ImGui.CloseCurrentPopup();
                    });

                    if (j % cols != cols - 1)
                        ImGui.SameLine();

                    ImGui.PopID();
                }

                if (barConfig.Editing || sh.SubList.Count < 1)
                {
                    if (!sh.CategoryNoBackground)
                        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    else
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.08f, 0.08f, 0.94f));
                    var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
                    PushFontScale(GetFontScale() * sh.CategoryFontScale);
                    if (ImGui.Button("+", new Vector2(width, height)))
                        ImGui.OpenPopup("addItem");
                    PopFontScale();
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Add a new shortcut.");
                }

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
                PushFontScale(1);
                ItemCreatePopup(sublist);
                PopFontScale();
                ImGui.PopStyleVar();

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void ItemBaseUI(ShCfg sh, bool editing)
        {
            if (IconBrowserUI.iconBrowserOpen && IconBrowserUI.doPasteIcon)
            {
                var split = sh.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
                sh.Name = $"::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
                if (editing)
                    Config.Save();
                IconBrowserUI.doPasteIcon = false;
            }
            if (ImGui.InputText("Name                    ", ref sh.Name, 256) && editing) // Not a bug... just want the window to not change width depending on which type it is...
                Config.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Start the name with ::x where x is a number to use icons, i.e. \"::2914\".\n" +
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
                    Config.Save();
            }

            if (sh.Type != ShortcutType.Spacer && (sh.Type != ShortcutType.Category || sh.Mode == ShortcutMode.Default))
            {
                var height = ImGui.GetFontSize() * Math.Min(sh.Command.Split('\n').Length + 1, 7) + Style.FramePadding.Y * 2; // ImGui issue #238: can't disable multiline scrollbar and it appears a whole line earlier than it should, so thats cool I guess
                if (ImGui.InputTextMultiline("Command##Input", ref sh.Command, 65535, new Vector2(0, height)) && editing)
                    Config.Save();
            }
        }

        private void ItemCreatePopup(List<ShCfg> shortcuts)
        {
            if (ImGui.BeginPopup("addItem"))
            {
                Reveal();
                SetConfigPopupOpen();

                _sh ??= new ShCfg();
                ItemBaseUI(_sh, false);

                if (ImGui.Button("Create"))
                {
                    shortcuts.Add(_sh);
                    Config.Save();
                    _sh = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Import"))
                {
                    var imports = Importing.TryImport(ImGui.GetClipboardText(), true);
                    if (imports.shortcut != null)
                        shortcuts.Add(imports.shortcut);
                    else if (imports.bar != null)
                    {
                        foreach (var sh in imports.bar.ShortcutList)
                            shortcuts.Add(sh);
                    }
                    Config.Save();
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Import a shortcut from the clipboard,\n" +
                        "or import all of another bar's shortcuts.");

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void ItemConfigPopup(List<ShCfg> shortcuts, int i, bool hasIcon)
        {
            if (ImGui.BeginPopup("editItem"))
            {
                Reveal();
                SetConfigPopupOpen();

                var sh = shortcuts[i];
                if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
                {
                    if (ImGui.BeginTabItem("Shortcut"))
                    {
                        ItemBaseUI(sh, true);

                        if (sh.Type != ShortcutType.Spacer)
                        {
                            var _m = (int)sh.Mode;
                            ImGui.TextUnformatted("Mode");
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Changes the behavior when pressed.\n" +
                                    "Note: Not intended to be used with categories containing subcategories.");
                            ImGui.RadioButton("Default", ref _m, 0);
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Default behavior, categories must be set to this to edit their shortcuts!");
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Incremental", ref _m, 1);
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Executes each line/shortcut in order over multiple presses.");
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton("Random", ref _m, 2);
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Executes a random line/shortcut when pressed.");
                            if (_m != (int)sh.Mode)
                            {
                                sh.Mode = (ShortcutMode)_m;
                                Config.Save();

                                if (sh.Mode == ShortcutMode.Random)
                                {
                                    var c = Math.Max(1, (sh.Type == ShortcutType.Category) ? sh.SubList.Count : sh.Command.Split('\n').Length);
                                    sh._i = (int)(QoLBar.GetFrameCount() % c);
                                }
                                else
                                    sh._i = 0;
                            }
                        }

                        var color = ImGui.ColorConvertU32ToFloat4(sh.Color);
                        color.W += sh.ColorAnimation / 255f; // Temporary
                        if (ImGui.ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.AlphaPreviewHalf))
                        {
                            sh.Color = ImGui.ColorConvertFloat4ToU32(color);
                            sh.ColorAnimation = Math.Max((int)Math.Round(color.W * 255) - 255, 0);
                            Config.Save();
                        }

                        if (sh.Type != ShortcutType.Spacer)
                            Keybind.KeybindInput(sh);

                        ImGui.EndTabItem();
                    }

                    if (sh.Type == ShortcutType.Category && ImGui.BeginTabItem("Category"))
                    {
                        if (ImGui.SliderInt("Width", ref sh.CategoryWidth, 0, 200))
                            Config.Save();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Set to 0 to use text width.");

                        if (ImGui.SliderInt("Columns", ref sh.CategoryColumns, 1, 12))
                            Config.Save();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Number of shortcuts in each row before starting another.");

                        if (ImGui.DragFloat("Scale", ref sh.CategoryScale, 0.002f, 0.7f, 1.5f, "%.2f"))
                            Config.Save();

                        if (ImGui.DragFloat("Font Scale", ref sh.CategoryFontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
                            Config.Save();

                        var spacing = new Vector2(sh.CategorySpacing[0], sh.CategorySpacing[1]);
                        if (ImGui.DragFloat2("Spacing", ref spacing, 0.12f, 0, 32, "%.f"))
                        {
                            sh.CategorySpacing[0] = (int)spacing.X;
                            sh.CategorySpacing[1] = (int)spacing.Y;
                            Config.Save();
                        }

                        if (ImGui.Checkbox("Open on Hover", ref sh.CategoryOnHover))
                            Config.Save();
                        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                        if (ImGui.Checkbox("Stay Open on Selection", ref sh.CategoryStaysOpen))
                            Config.Save();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Keeps the category open when pressing shortcuts within it.\nMay not work if the shortcut interacts with other plugins.");

                        if (ImGui.Checkbox("No Background", ref sh.CategoryNoBackground))
                            Config.Save();

                        ImGui.EndTabItem();
                    }

                    if (hasIcon && ImGui.BeginTabItem("Icon"))
                    {
                        // Name is available here for ease of access since it pertains to the icon as well
                        if (IconBrowserUI.iconBrowserOpen && IconBrowserUI.doPasteIcon)
                        {
                            var split = sh.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
                            sh.Name = $"::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
                            Config.Save();
                            IconBrowserUI.doPasteIcon = false;
                        }
                        if (ImGui.InputText("Name", ref sh.Name, 256))
                            Config.Save();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Icons accept arguments between \"::\" and their ID. I.e. \"::f21\".\n" +
                                "\t' f ' - Applies the hotbar frame (or removes it if applied globally).\n" +
                                "\t' _ ' - Disables arguments, including implicit ones. Cannot be used with others.");

                        if (ImGui.DragFloat("Zoom", ref sh.IconZoom, 0.005f, 1.0f, 5.0f, "%.2f"))
                            Config.Save();

                        var offset = new Vector2(sh.IconOffset[0], sh.IconOffset[1]);
                        if (ImGui.DragFloat2("Offset", ref offset, 0.002f, -0.5f, 0.5f, "%.2f"))
                        {
                            sh.IconOffset[0] = offset.X;
                            sh.IconOffset[1] = offset.Y;
                            Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                if (ImGui.Button((shortcuts == barConfig.ShortcutList && !vertical) ? "←" : "↑") && i > 0)
                {
                    shortcuts.RemoveAt(i);
                    shortcuts.Insert(i - 1, sh);
                    Config.Save();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button((shortcuts == barConfig.ShortcutList && !vertical) ? "→" : "↓") && i < (shortcuts.Count - 1))
                {
                    shortcuts.RemoveAt(i);
                    shortcuts.Insert(i + 1, sh);
                    Config.Save();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Export"))
                    ImGui.SetClipboardText(Importing.ExportShortcut(sh, false));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export to clipboard with minimal settings (May change with updates).\n" +
                        "Right click to export with every setting (Longer string, doesn't change).");

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        ImGui.SetClipboardText(Importing.ExportShortcut(sh, true));
                }
                ImGui.SameLine();
                if (ImGui.Button(Config.ExportOnDelete ? "Cut" : "Delete"))
                    Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                //if (ImGui.IsItemClicked(1)) // Jesus christ I hate ImGui who made this function activate on PRESS AND NOT RELEASE??? THIS ISN'T A CLICK
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right click this button to delete the shortcut!" +
                        (Config.ExportOnDelete ? "\nThe shortcut will be exported to clipboard first." : ""));

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    {
                        if (Config.ExportOnDelete)
                            ImGui.SetClipboardText(Importing.ExportShortcut(sh, false));

                        shortcuts.RemoveAt(i);
                        Config.Save();
                        ImGui.CloseCurrentPopup();
                    }
                }

                var iconSize = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() + Style.WindowPadding.X - iconSize);
                if (ShortcutUI.DrawIcon(46, new Vector2(iconSize), 1.0f, Vector2.Zero, Vector4.One, false))
                    Plugin.ToggleIconBrowser();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Opens up a list of all icons you can use instead of text.\n" +
                        "Warning: This will load EVERY icon available so it will probably lag for a moment.\n" +
                        "Clicking on one will copy text to be pasted into the \"Name\" field of a shortcut.\n" +
                        "Additionally, while the browser is open it will autofill the \"Name\" of shortcuts.");

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        public void BarConfigPopup()
        {
            if (ImGui.BeginPopup($"BarConfig##{barNumber}"))
            {
                Reveal();

                if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
                {
                    if (ImGui.BeginTabItem("General"))
                    {
                        if (ImGui.InputText("Name", ref barConfig.Name, 256))
                            Config.Save();

                        var _dock = (int)barConfig.DockSide;
                        if (ImGui.Combo("Side", ref _dock, "Top\0Left\0Bottom\0Right\0Undocked\0Undocked (Vertical)"))
                        {
                            barConfig.DockSide = (BarDock)_dock;
                            if (barConfig.DockSide == BarDock.UndockedH || barConfig.DockSide == BarDock.UndockedV)
                                barConfig.Visibility = BarVisibility.Always;
                            Config.Save();
                            SetupPosition();
                        }

                        if (docked)
                        {
                            var _align = (int)barConfig.Alignment;
                            ImGui.Text("Alignment");
                            ImGui.RadioButton(vertical ? "Top" : "Left", ref _align, 0);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Center", ref _align, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton(vertical ? "Bottom" : "Right", ref _align, 2);
                            if (_align != (int)barConfig.Alignment)
                            {
                                barConfig.Alignment = (BarAlign)_align;
                                Config.Save();
                                SetupPosition();
                            }

                            var _visibility = (int)barConfig.Visibility;
                            ImGui.Text("Animation");
                            ImGui.RadioButton("Slide", ref _visibility, 0);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Immediate", ref _visibility, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton("Always Visible", ref _visibility, 2);
                            if (_visibility != (int)barConfig.Visibility)
                            {
                                barConfig.Visibility = (BarVisibility)_visibility;
                                Config.Save();
                            }

                            if ((barConfig.Visibility != BarVisibility.Always) && ImGui.DragFloat("Reveal Area Scale", ref barConfig.RevealAreaScale, 0.01f, 0.0f, 1.0f, "%.2f"))
                                Config.Save();

                            var offset = ConfigOffset;
                            if (ImGui.DragFloat2("Offset", ref offset, 0.2f, -500, 500, "%.f"))
                            {
                                barConfig.Offset[0] = offset.X / window.X;
                                barConfig.Offset[1] = offset.Y / window.Y;
                                Config.Save();
                                SetupPosition();
                            }

                            if (ImGui.Checkbox("Edit Mode", ref barConfig.Editing))
                            {
                                if (!barConfig.Editing)
                                    Plugin.ExecuteCommand("/echo <se> You can right click on the bar itself (the black background) to reopen this settings menu!");
                                Config.Save();
                            }

                            if (barConfig.Visibility != BarVisibility.Always)
                            {
                                ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                                if (ImGui.Checkbox("Hint", ref barConfig.Hint))
                                    Config.Save();
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip("Will prevent the bar from sleeping, increasing CPU load.");
                            }
                        }
                        else
                        {
                            var _visibility = (int)barConfig.Visibility;
                            ImGui.Text("Animation");
                            ImGui.RadioButton("Immediate", ref _visibility, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                            ImGui.RadioButton("Always Visible", ref _visibility, 2);
                            if (_visibility != (int)barConfig.Visibility)
                            {
                                barConfig.Visibility = (BarVisibility)_visibility;
                                Config.Save();
                            }

                            if (ImGui.Checkbox("Edit Mode", ref barConfig.Editing))
                            {
                                if (!barConfig.Editing)
                                    Plugin.ExecuteCommand("/echo <se> You can right click on the bar itself (the black background) to reopen this settings menu!");
                                Config.Save();
                            }
                            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                            if (ImGui.Checkbox("Lock Position", ref barConfig.LockedPosition))
                                Config.Save();

                            if (!barConfig.LockedPosition)
                            {
                                var pos = ConfigPosition;
                                if (ImGui.DragFloat2("Position", ref pos, 1, -Style.WindowPadding.X, (window.X > window.Y) ? window.X : window.Y, "%.f"))
                                {
                                    barConfig.Position[0] = pos.X / window.X;
                                    barConfig.Position[1] = pos.Y / window.Y;
                                    Config.Save();
                                    _setPos = true;
                                }
                            }
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Style"))
                    {
                        if (ImGui.DragFloat("Scale", ref barConfig.Scale, 0.002f, 0.7f, 2.0f, "%.2f"))
                            Config.Save();

                        if (ImGui.SliderInt("Button Width", ref barConfig.ButtonWidth, 0, 200))
                            Config.Save();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Set to 0 to use text width.");

                        if (ImGui.DragFloat("Font Scale", ref barConfig.FontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
                            Config.Save();

                        if (ImGui.SliderInt("Spacing", ref barConfig.Spacing[0], 0, 32))
                        {
                            barConfig.Spacing[1] = barConfig.Spacing[0];
                            Config.Save();
                        }

                        if (ImGui.Checkbox("No Background", ref barConfig.NoBackground))
                            Config.Save();

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.Spacing();
                ImGui.Spacing();
                if (ImGui.Button("Export"))
                    ImGui.SetClipboardText(Importing.ExportBar(barConfig, false));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export to clipboard with minimal settings (May change with updates).\n" +
                        "Right click to export with every setting (Longer string, doesn't change).");

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        ImGui.SetClipboardText(Importing.ExportBar(barConfig, true));
                }
                ImGui.SameLine();
                if (ImGui.Button("QoL Bar Config"))
                    Plugin.ToggleConfig();

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void SetBarSize()
        {
            barSize.Y = ImGui.GetCursorPosY() + Style.WindowPadding.Y - Style.ItemSpacing.Y;
            if (!vertical)
            {
                ImGui.SameLine();
                barSize.X = ImGui.GetCursorPosX() + Style.WindowPadding.X - Style.ItemSpacing.X;
            }
            else
            {
                barSize.X = _maxW + (Style.WindowPadding.X * 2);
                _maxW = 0;
            }
        }

        private void ClampWindowPos()
        {
            var _lastPos = ImGui.GetWindowPos();
            var _size = ImGui.GetWindowSize();
            var _x = Math.Min(Math.Max(_lastPos.X, 0), window.X - _size.X);
            var _y = Math.Min(Math.Max(_lastPos.Y, 0), window.Y - _size.Y);
            ImGui.SetWindowPos(new Vector2(_x, _y));
        }

        private void SetBarPosition()
        {
            if (barConfig.Visibility == BarVisibility.Slide)
                TweenBarPosition();
            else
                barPos = _reveal ? revealPos : GetHidePosition();
        }

        private void TweenBarPosition()
        {
            var _hidePos = GetHidePosition();

            if (_reveal != _lastReveal)
            {
                _lastReveal = _reveal;
                _tweenStart = barPos;
                _tweenProgress = 0;
            }

            if (_tweenProgress >= 1)
            {
                barPos = _reveal ? revealPos : _hidePos;
            }
            else
            {
                var dt = ImGui.GetIO().DeltaTime * 2;
                _tweenProgress = Math.Min(_tweenProgress + dt, 1);

                var x = -1 * ((float)Math.Pow(_tweenProgress - 1, 4) - 1); // Quartic ease out
                var deltaX = ((_reveal ? revealPos.X : _hidePos.X) - _tweenStart.X) * x;
                var deltaY = ((_reveal ? revealPos.Y : _hidePos.Y) - _tweenStart.Y) * x;

                barPos.X = _tweenStart.X + deltaX;
                barPos.Y = _tweenStart.Y + deltaY;
            }
        }

        // Why is this not a basic feature of ImGui...
        private readonly Stack<float> _fontScaleStack = new Stack<float>();
        private float _curScale = 1;
        private void PushFontScale(float scale)
        {
            _fontScaleStack.Push(_curScale);
            _curScale = scale;
            ImGui.SetWindowFontScale(_curScale);
        }

        private void PopFontScale()
        {
            _curScale = _fontScaleStack.Pop();
            ImGui.SetWindowFontScale(_curScale);
        }

        private float GetFontScale() => _curScale;

        public void Dispose()
        {
        }
    }
}
