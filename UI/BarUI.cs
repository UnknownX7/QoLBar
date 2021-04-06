using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using static QoLBar.BarCfg;

namespace QoLBar
{
    public class BarUI : IDisposable
    {
        private int _id;
        public int ID
        {
            get => _id;
            set
            {
                _id = value;
                Config = QoLBar.Config.BarCfgs[value];
                SetupPosition();
            }
        }
        public BarCfg Config { get; private set; }

        public bool IsVisible => !IsHidden && CheckConditionSet();
        public bool IsHidden
        {
            get => Config.Hidden;
            set
            {
                Config.Hidden = value;
                QoLBar.Config.Save();
            }
        }

        private static ImGuiStylePtr Style => ImGui.GetStyle();

        private Vector2 ConfigPosition => new Vector2((float)Math.Floor(Config.Position[0] * window.X), (float)Math.Floor(Config.Position[1] * window.Y));

        public bool IsVertical { get; private set; } = false;
        public bool IsDocked { get; private set; } = true;
        public bool IsDragging { get; private set; } = false;

        public List<ShortcutUI> children = new List<ShortcutUI>();

        public static ShCfg tempSh;
        private Vector2 window = ImGui.GetIO().DisplaySize;
        private static Vector2 mousePos = ImGui.GetIO().MousePos;
        private static float globalSize = ImGui.GetIO().FontGlobalScale;
        private Vector2 barSize = new Vector2(200, 38);
        private Vector2 barPos;
        private ImGuiWindowFlags flags;
        private Vector2 piv = Vector2.Zero;
        private Vector2 hidePos = Vector2.Zero;
        private Vector2 revealPos = Vector2.Zero;

        private bool _reveal = false;
        public void Reveal() => _reveal = true;
        public void ForceReveal() => _lastReveal = _reveal = true;
        public void Hide() => _reveal = false;
        public bool IsFullyRevealed => !IsDocked || barPos == revealPos;

        private void SetConfigPopupOpen() => QoLBar.Plugin.ui.SetConfigPopupOpen();

        private bool _firstframe = true;
        private bool _setPos = true;
        private bool _lastReveal = true;
        private bool _mouseRevealed = false;
        public float _maxW = 0; // TODO: same as below
        private Vector2 _tweenStart;
        private float _tweenProgress = 1;
        private Vector2 _catpiv = Vector2.Zero;
        private Vector2 _catpos = Vector2.Zero;
        private Vector2 _maincatpos = Vector2.Zero;
        public bool _activated = false; // TODO: this variable sucks make it pretty

        public BarUI(int n)
        {
            ID = n;

            for (int i = 0; i < Config.ShortcutList.Count; i++)
                children.Add(new ShortcutUI(this));
        }

        private bool CheckConditionSet()
        {
            if (Config.ConditionSet >= 0 && Config.ConditionSet < QoLBar.Config.ConditionSets.Count)
                return QoLBar.Config.ConditionSets[Config.ConditionSet].CheckConditions();
            else
                return true;
        }

        private void SetupPosition()
        {
            var pivX = 0.0f;
            var pivY = 0.0f;
            var defPos = 0.0f;
            switch (Config.DockSide)
            {
                case BarDock.Top: //    0.0 1.0, 0.5 1.0, 1.0 1.0 // 0 0(+H),    winX/2 0(+H),    winX 0(+H)
                    pivY = 1.0f;
                    defPos = 0.0f;
                    IsVertical = false;
                    IsDocked = true;
                    break;
                case BarDock.Left: //   1.0 0.0, 1.0 0.5, 1.0 1.0 // 0(+W) 0,    0(+W) winY/2,    0(+W) winY
                    pivY = 1.0f;
                    defPos = 0.0f;
                    IsVertical = true;
                    IsDocked = true;
                    break;
                case BarDock.Bottom: // 0.0 0.0, 0.5 0.0, 1.0 0.0 // 0 winY(-H), winX/2 winY(-H), winX winY(-H)
                    pivY = 0.0f;
                    defPos = window.Y;
                    IsVertical = false;
                    IsDocked = true;
                    break;
                case BarDock.Right: //  0.0 0.0, 0.0 0.5, 0.0 1.0 // winX(-W) 0, winX(-W) winY/2, winX(-W) winY
                    pivY = 0.0f;
                    defPos = window.X;
                    IsVertical = true;
                    IsDocked = true;
                    break;
                case BarDock.Undocked:
                    piv = Vector2.Zero;
                    IsVertical = false;
                    IsDocked = false;
                    _setPos = true;
                    return;
                case BarDock.UndockedV:
                    piv = Vector2.Zero;
                    IsVertical = true;
                    IsDocked = false;
                    _setPos = true;
                    return;
            }

            switch (Config.Alignment)
            {
                case BarAlign.LeftOrTop:
                    pivX = 0.0f;
                    break;
                case BarAlign.Center:
                    pivX = 0.5f;
                    break;
                case BarAlign.RightOrBottom:
                    pivX = 1.0f;
                    break;
            }

            if (!IsVertical)
            {
                piv.X = pivX;
                piv.Y = pivY;

                hidePos.X = window.X * pivX + ConfigPosition.X;
                hidePos.Y = defPos;
                revealPos.X = hidePos.X;
            }
            else
            {
                piv.X = pivY;
                piv.Y = pivX;

                hidePos.X = defPos;
                hidePos.Y = window.Y * pivX + ConfigPosition.Y;
                revealPos.Y = hidePos.Y;
            }

            SetupRevealPosition();

            barPos = hidePos;
            _tweenStart = hidePos;
        }

        private void SetupRevealPosition()
        {
            switch (Config.DockSide)
            {
                case BarDock.Top:
                    revealPos.Y = Math.Max(hidePos.Y + barSize.Y + ConfigPosition.Y, GetHidePosition().Y + 1);
                    break;
                case BarDock.Left:
                    revealPos.X = Math.Max(hidePos.X + barSize.X + ConfigPosition.X, GetHidePosition().X + 1);
                    break;
                case BarDock.Bottom:
                    revealPos.Y = Math.Min(hidePos.Y - barSize.Y + ConfigPosition.Y, GetHidePosition().Y - 1);
                    break;
                case BarDock.Right:
                    revealPos.X = Math.Min(hidePos.X - barSize.X + ConfigPosition.X, GetHidePosition().X - 1);
                    break;
            }
        }

        private void SetupImGuiFlags()
        {
            flags = ImGuiWindowFlags.None;

            flags |= ImGuiWindowFlags.NoDecoration;
            if (IsDocked || Config.LockedPosition)
                flags |= ImGuiWindowFlags.NoMove;
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
            if (Config.NoBackground)
                flags |= ImGuiWindowFlags.NoBackground;
            flags |= ImGuiWindowFlags.NoSavedSettings;
            flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }

        private Vector2 GetHidePosition()
        {
            var _hidePos = hidePos;
            if (Config.Hint)
            {
                var _winPad = Style.WindowPadding * 2;

                switch (Config.DockSide)
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
                }
            }
            return _hidePos;
        }

        public void SetupHotkeys()
        {
            foreach (var ui in children)
                ui.SetupHotkeys();
        }

        private void ClearActivated()
        {
            if (!_activated)
            {
                foreach (var ui in children)
                    ui.ClearActivated();
            }
        }

        public void Draw()
        {
            CheckGameResolution();

            if (!IsVisible) return;

            var io = ImGui.GetIO();
            mousePos = io.MousePos;
            globalSize = io.FontGlobalScale;

            if (IsDocked || Config.Visibility == BarVisibility.Immediate)
            {
                SetupRevealPosition();

                CheckMousePosition();
            }
            else
                Reveal();

            if (!IsDocked && !_firstframe && !_reveal && !_lastReveal)
            {
                ClearActivated();
                return;
            }

            if (_firstframe || _reveal || (barPos != hidePos) || (!IsDocked && _lastReveal)) // Don't bother to render when fully off screen
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(Config.Spacing[0]));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.286f, 0.286f, 0.286f, 0.9f));

                if (IsDocked)
                    ImGui.SetNextWindowPos(barPos, ImGuiCond.Always, piv);
                else if (_setPos || Config.LockedPosition)
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
                ImGui.Begin($"QoLBar##{ID}", flags);

                ImGuiEx.PushFontScale(Config.Scale);

                if (_mouseRevealed && ImGui.IsWindowHovered(ImGuiHoveredFlags.RectOnly))
                    Reveal();
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
                    ImGui.OpenPopup($"BarConfig##{ID}");

                DrawItems();

                DrawAddButton();

                if (!_firstframe && !Config.LockedPosition)
                {
                    if (IsDocked)
                    {
                        // I greatly dislike this
                        var dragging = !IsDragging
                            ? ImGui.IsWindowFocused() && ImGui.IsWindowHovered() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0)
                            : IsDragging && !ImGui.IsMouseReleased(ImGuiMouseButton.Left);

                        // Began dragging
                        if (dragging && dragging != IsDragging)
                            IsDragging = true;

                        if (IsDragging)
                        {
                            Reveal();
                            var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0) / window;
                            ImGui.ResetMouseDragDelta();
                            Config.Position[0] = Math.Min(Config.Position[0] + delta.X, 1);
                            Config.Position[1] = Math.Min(Config.Position[1] + delta.Y, 1);
                            SetupPosition();
                        }

                        // Stopped dragging
                        if (!dragging && dragging != IsDragging)
                        {
                            IsDragging = false;
                            QoLBar.Config.Save();
                        }
                    }
                    else
                    {
                        if (ImGui.GetWindowPos() != ConfigPosition)
                        {
                            var newPos = ImGui.GetWindowPos() / window;
                            Config.Position[0] = newPos.X;
                            Config.Position[1] = newPos.Y;
                            QoLBar.Config.Save();
                        }
                    }
                }

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
                ImGuiEx.PushFontScale(1);
                BarConfigPopup();
                ImGuiEx.PopFontScale();
                ImGui.PopStyleVar();

                SetBarSize();

                ImGuiEx.PopFontScale();

                ImGui.End();

                ImGui.PopStyleColor();
                ImGui.PopStyleVar(3);
            }

            if (!_reveal)
                _mouseRevealed = false;

            if (IsDocked)
            {
                SetBarPosition();
                Hide(); // Allows other objects to reveal the bar
            }
            else
                _lastReveal = _reveal;

            ClearActivated();
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
            var pos = IsDocked ? revealPos : ConfigPosition;
            var min = new Vector2(pos.X - (barSize.X * piv.X), pos.Y - (barSize.Y * piv.Y));
            var max = new Vector2(pos.X + (barSize.X * (1 - piv.X)), pos.Y + (barSize.Y * (1 - piv.Y)));
            return (min, max);
        }

        private void CheckMousePosition()
        {
            if (IsDocked && _reveal)
                return;

            (var _min, var _max) = CalculateRevealPosition();

            switch (Config.DockSide)
            {
                case BarDock.Top:
                    _max.Y = Math.Max(Math.Max(_max.Y - barSize.Y * (1 - Config.RevealAreaScale), _min.Y + 1), GetHidePosition().Y + 1);
                    break;
                case BarDock.Left:
                    _max.X = Math.Max(Math.Max(_max.X - barSize.X * (1 - Config.RevealAreaScale), _min.X + 1), GetHidePosition().X + 1);
                    break;
                case BarDock.Bottom:
                    _min.Y = Math.Min(Math.Min(_min.Y + barSize.Y * (1 - Config.RevealAreaScale), _max.Y - 1), GetHidePosition().Y - 1);
                    break;
                case BarDock.Right:
                    _min.X = Math.Min(Math.Min(_min.X + barSize.X * (1 - Config.RevealAreaScale), _max.X - 1), GetHidePosition().X - 1);
                    break;
                default:
                    break;
            }

            var mX = mousePos.X;
            var mY = mousePos.Y;

            //if (ImGui.IsMouseHoveringRect(_min, _max, true)) // This only works in the context of a window... thanks ImGui
            if (Config.Visibility == BarVisibility.Always || (_min.X <= mX && mX < _max.X && _min.Y <= mY && mY < _max.Y))
            {
                _mouseRevealed = true;
                Reveal();
            }
            else
                Hide();
        }

        private void DrawItems()
        {
            var width = Config.ButtonWidth * globalSize * Config.Scale;
            for (int i = 0; i < children.Count; i++)
            {
                var ui = children[i];
                ImGui.PushID(ui.ID);

                ui.DrawShortcut(width);

                if (!IsVertical && ui.ID != children.Count - 1)
                    ImGui.SameLine();

                ImGui.PopID();
            }
        }

        private void DrawAddButton()
        {
            if (Config.Editing || children.Count < 1)
            {
                if (!IsVertical && children.Count > 0)
                    ImGui.SameLine();

                var height = ImGui.GetFontSize() + Style.FramePadding.Y * 2;
                ImGuiEx.PushFontScale(ImGuiEx.GetFontScale() * Config.FontScale);
                if (ImGui.Button("+", new Vector2(Config.ButtonWidth * globalSize * Config.Scale, height)))
                    ImGui.OpenPopup("addItem");
                ImGuiEx.SetItemTooltip("Add a new shortcut.\nRight click this (or the bar background) for options.\nRight click other shortcuts to edit them.", ImGuiHoveredFlags.AllowWhenBlockedByPopup);
                ImGuiEx.PopFontScale();

                if (_maxW < ImGui.GetItemRectSize().X)
                    _maxW = ImGui.GetItemRectSize().X;

                //ImGui.OpenPopupContextItem($"BarConfig##{barNumber}"); // Technically unneeded
            }

            if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                ImGui.OpenPopup("addItem");

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, PluginUI.defaultSpacing);
            ImGuiEx.PushFontScale(1);
            ItemCreatePopup();
            ImGuiEx.PopFontScale();
            ImGui.PopStyleVar();
        }

        // TODO: rewrite this, preferably insert into ShortcutUI
        public void SetupCategoryPosition(bool v, bool subItem)
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

        public void SetCategoryPosition() => ImGui.SetNextWindowPos(_catpos, ImGuiCond.Appearing, _catpiv);

        // TODO: dupe code remove somehow
        private void ItemCreatePopup()
        {
            if (ImGui.BeginPopup("addItem"))
            {
                Reveal();
                SetConfigPopupOpen();

                tempSh ??= new ShCfg();
                ShortcutUI.ItemBaseUI(tempSh, false);

                if (ImGui.Button("Create"))
                {
                    AddShortcut(tempSh);
                    tempSh = null;
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

                ImGuiEx.ClampWindowPos(window);

                ImGui.EndPopup();
            }
        }

        public void BarConfigPopup()
        {
            if (ImGui.BeginPopup($"BarConfig##{ID}"))
            {
                Reveal();

                if (ImGui.BeginTabBar("Config Tabs", ImGuiTabBarFlags.NoTooltip))
                {
                    if (ImGui.BeginTabItem("General"))
                    {
                        if (ImGui.InputText("Name", ref Config.Name, 256))
                            QoLBar.Config.Save();

                        var _dock = (int)Config.DockSide;
                        if (ImGui.Combo("Side", ref _dock, "Top\0Right\0Bottom\0Left"))
                        {
                            Config.DockSide = (BarDock)_dock;
                            if (Config.DockSide == BarDock.Undocked || Config.DockSide == BarDock.UndockedV)
                                Config.Visibility = BarVisibility.Always;
                            QoLBar.Config.Save();
                            SetupPosition();
                        }

                        if (IsDocked)
                        {
                            var _align = (int)Config.Alignment;
                            ImGui.Text("Alignment");
                            ImGui.RadioButton(IsVertical ? "Top" : "Left", ref _align, 0);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Center", ref _align, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton(IsVertical ? "Bottom" : "Right", ref _align, 2);
                            if (_align != (int)Config.Alignment)
                            {
                                Config.Alignment = (BarAlign)_align;
                                QoLBar.Config.Save();
                                SetupPosition();
                            }

                            var _visibility = (int)Config.Visibility;
                            ImGui.Text("Animation");
                            ImGui.RadioButton("Slide", ref _visibility, 0);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                            ImGui.RadioButton("Immediate", ref _visibility, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                            ImGui.RadioButton("Always Visible", ref _visibility, 2);
                            if (_visibility != (int)Config.Visibility)
                            {
                                Config.Visibility = (BarVisibility)_visibility;
                                QoLBar.Config.Save();
                            }

                            if ((Config.Visibility != BarVisibility.Always) && ImGui.DragFloat("Reveal Area Scale", ref Config.RevealAreaScale, 0.01f, 0.0f, 1.0f, "%.2f"))
                                QoLBar.Config.Save();
                        }
                        else
                        {
                            var _visibility = (int)Config.Visibility;
                            ImGui.Text("Animation");
                            ImGui.RadioButton("Immediate", ref _visibility, 1);
                            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                            ImGui.RadioButton("Always Visible", ref _visibility, 2);
                            if (_visibility != (int)Config.Visibility)
                            {
                                Config.Visibility = (BarVisibility)_visibility;
                                QoLBar.Config.Save();
                            }
                        }

                        if (ImGui.Checkbox("Edit Mode", ref Config.Editing))
                        {
                            if (!Config.Editing)
                                QoLBar.Plugin.ExecuteCommand("/echo <se> You can right click on the bar itself (the black background) to reopen this settings menu!");
                            QoLBar.Config.Save();
                        }
                        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                        if (ImGui.Checkbox("Lock Position", ref Config.LockedPosition))
                            QoLBar.Config.Save();

                        if (!Config.LockedPosition)
                        {
                            var pos = ConfigPosition;
                            var max = (window.X > window.Y) ? window.X : window.Y;
                            if (ImGui.DragFloat2(IsDocked ? "Offset" : "Position", ref pos, 1, -max, max, "%.f"))
                            {
                                Config.Position[0] = Math.Min(pos.X / window.X, 1);
                                Config.Position[1] = Math.Min(pos.Y / window.Y, 1);
                                QoLBar.Config.Save();
                                if (IsDocked)
                                    SetupPosition();
                                else
                                    _setPos = true;
                            }
                        }

                        if (IsDocked && Config.Visibility != BarVisibility.Always)
                        {
                            if (ImGui.Checkbox("Hint", ref Config.Hint))
                                QoLBar.Config.Save();
                            ImGuiEx.SetItemTooltip("Will prevent the bar from sleeping, increasing CPU load.");
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Style"))
                    {
                        if (ImGui.DragFloat("Scale", ref Config.Scale, 0.002f, 0.7f, 2.0f, "%.2f"))
                            QoLBar.Config.Save();

                        if (ImGui.SliderInt("Button Width", ref Config.ButtonWidth, 0, 200))
                            QoLBar.Config.Save();
                        ImGuiEx.SetItemTooltip("Set to 0 to use text width.");

                        if (ImGui.DragFloat("Font Scale", ref Config.FontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
                            QoLBar.Config.Save();

                        if (ImGui.SliderInt("Spacing", ref Config.Spacing[0], 0, 32))
                        {
                            Config.Spacing[1] = Config.Spacing[0];
                            QoLBar.Config.Save();
                        }

                        if (ImGui.Checkbox("No Background", ref Config.NoBackground))
                            QoLBar.Config.Save();

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.Spacing();
                ImGui.Spacing();
                if (ImGui.Button("Export"))
                    ImGui.SetClipboardText(Importing.ExportBar(Config, false));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export to clipboard with minimal settings (May change with updates).\n" +
                        "Right click to export with every setting (Longer string, doesn't change).");

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        ImGui.SetClipboardText(Importing.ExportBar(Config, true));
                }
                ImGui.SameLine();
                if (ImGui.Button("QoL Bar Config"))
                    QoLBar.Plugin.ToggleConfig();

                ImGuiEx.ClampWindowPos(window);

                ImGui.EndPopup();
            }
        }

        private void SetBarSize()
        {
            barSize.Y = ImGui.GetCursorPosY() + Style.WindowPadding.Y - Style.ItemSpacing.Y;
            if (!IsVertical)
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

        private void SetBarPosition()
        {
            if (Config.Visibility == BarVisibility.Slide)
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

        public void AddShortcut(ShCfg sh)
        {
            Config.ShortcutList.Add(sh);
            children.Add(new ShortcutUI(this));
            QoLBar.Config.Save();
        }

        public void RemoveShortcut(int i)
        {
            if (QoLBar.Config.ExportOnDelete)
                ImGui.SetClipboardText(Importing.ExportShortcut(Config.ShortcutList[i], false));

            children[i].Dispose();
            children.RemoveAt(i);
            Config.ShortcutList.RemoveAt(i);
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

                var sh = Config.ShortcutList[i];
                Config.ShortcutList.RemoveAt(i);
                Config.ShortcutList.Insert(j, sh);
                QoLBar.Config.Save();
                RefreshShortcutIDs();
            }
        }

        private void RefreshShortcutIDs()
        {
            for (int i = 0; i < children.Count; i++)
                children[i].ID = i;
        }

        public void Dispose()
        {
            foreach (var ui in children)
                ui.Dispose();
        }
    }
}
