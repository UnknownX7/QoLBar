using ImGuiNET;
using System;
using System.Numerics;
using System.Collections.Generic;
using static QoLBar.BarConfig;
using Dalamud.Plugin;

namespace QoLBar
{
    public class BarUI : IDisposable
    {
        private int barNumber;
        private BarConfig barConfig => config.BarConfigs[barNumber];
        public void SetBarNumber(int i)
        {
            barNumber = i;
            SetupPosition();
            _setPos = true;
        }

#if DEBUG
        public bool IsVisible => !barConfig.Hidden;
#else
        public bool IsVisible => !barConfig.Hidden && plugin.pluginInterface.ClientState.LocalPlayer != null;
#endif
        public void ToggleVisible()
        {
            barConfig.Hidden = !barConfig.Hidden;
            config.Save();
        }

        private Shortcut _sh;
        private static Vector2 window = ImGui.GetIO().DisplaySize;
        private static Vector2 mousePos = ImGui.GetIO().MousePos;
        private Vector2 barSize = new Vector2(200, 38);
        private Vector2 barPos;
        private ImGuiWindowFlags flags;
        private readonly int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more
        private Vector2 piv = new Vector2();
        private Vector2 hidePos = new Vector2();
        private Vector2 revealPos = new Vector2();
        private bool vertical = false;
        private bool docked = true;

        private bool _reveal = false;
        private void Reveal() => _reveal = true;
        private void Hide() => _reveal = false;

        private bool _firstframe = true;
        private bool _setPos = true;
        private bool _lastReveal = true;
        private Vector2 _tweenStart;
        private float _tweenProgress = 1;
        private float _mx = 0f;
        private float _my = 0f;
        private Vector2 _catpiv = new Vector2();
        private Vector2 _catpos = new Vector2();

        private readonly QoLBar plugin;
        private readonly Configuration config;

        public BarUI(QoLBar p, Configuration config, int nbar)
        {
            plugin = p;
            this.config = config;
            barNumber = nbar;
            SetupPosition();
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
                    vertical = false;
                    docked = false;
                    return;
                case BarDock.UndockedV:
                    vertical = true;
                    docked = false;
                    return;
                default:
                    break;
            }

            switch (barConfig.Alignment)
            {
                case BarAlign.LeftOrTop:
                    pivX = 0.0f;
                    offset = 39;
                    break;
                case BarAlign.Center:
                    pivX = 0.5f;
                    break;
                case BarAlign.RightOrBottom:
                    pivX = 1.0f;
                    offset = -39;
                    break;
                default:
                    break;
            }

            if (!vertical)
            {
                piv.X = pivX;
                piv.Y = pivY;

                hidePos.X = window.X * pivX + offset;
                hidePos.Y = defPos;
                revealPos.X = hidePos.X;
            }
            else
            {
                piv.X = pivY;
                piv.Y = pivX;

                hidePos.X = defPos;
                hidePos.Y = window.Y * pivX + offset;
                revealPos.Y = hidePos.Y;
            }

            //PluginLog.Log($"piv {piv.X} {piv.Y} hide {hidePos.X} {hidePos.Y} reveal {revealPos.X} {revealPos.Y}");

            SetupRevealPosition();

            barPos = hidePos;
            _tweenStart = hidePos;
        }

        private void SetupRevealPosition()
        {
            switch (barConfig.DockSide)
            {
                case BarDock.Top:
                    revealPos.Y = hidePos.Y + barSize.Y;
                    break;
                case BarDock.Left:
                    revealPos.X = hidePos.X + barSize.X;
                    break;
                case BarDock.Bottom:
                    revealPos.Y = hidePos.Y - barSize.Y;
                    break;
                case BarDock.Right:
                    revealPos.X = hidePos.X - barSize.X;
                    break;
                case BarDock.UndockedH:
                    break;
                case BarDock.UndockedV:
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
            flags |= ImGuiWindowFlags.NoSavedSettings;
            flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }

        public void Draw()
        {
            if (!IsVisible) return;

            var io = ImGui.GetIO();
            window = io.DisplaySize;
            mousePos = io.MousePos;

            if (docked)
            {
                SetupRevealPosition();

                CheckMouse();
            }
            else
                Reveal();

            if (_firstframe || _reveal || barPos != hidePos) // Don't bother to render when fully off screen
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);

                if (docked)
                    ImGui.SetNextWindowPos(barPos, ImGuiCond.Always, piv);
                else if (_setPos)
                {
                    ImGui.SetNextWindowPos(barConfig.Position);
                    _setPos = false;
                }
                ImGui.SetNextWindowSize(barSize);

                SetupImGuiFlags();
                ImGui.Begin($"QoLBar##{barNumber}", flags);

                if (ImGui.IsWindowHovered())
                {
                    Reveal();

                    if (ImGui.IsMouseReleased(1))
                        ImGui.OpenPopup($"BarConfig##{barNumber}");
                }

                DrawItems();

                if (!barConfig.HideAdd || barConfig.ShortcutList.Count < 1)
                    DrawAddButton();

                if (!docked && ImGui.GetWindowPos() != barConfig.Position)
                {
                    barConfig.Position = ImGui.GetWindowPos();
                    config.Save();
                }

                BarConfigPopup();

                SetBarSize();

                ImGui.End();

                ImGui.PopStyleVar(2);
            }

            if (docked)
            {
                SetBarPosition();
                Hide(); // Allows other objects to reveal the bar
            }

            _firstframe = false;
        }

        private void CheckMouse()
        {
            // Check if mouse is nearby
            /*if (barConfig.Visibility == VisibilityMode.Always || (Math.Abs(mousePos.X - barPos.X) <= (barSize.X / 2) && (window.Y - mousePos.Y) <= barSize.Y))
                Reveal();
            else
                Hide();*/

            // Invisible UI to check if the mouse is nearby
            ImGui.SetNextWindowPos(revealPos, ImGuiCond.Always, piv);
            ImGui.SetNextWindowSize(barSize);
            ImGui.Begin($"QoLBarMouseDetection##{barNumber}", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus);
            if (_reveal || barConfig.Visibility == VisibilityMode.Always || ImGui.IsWindowHovered())
                Reveal();
            else
                Hide();
            ImGui.End();
        }

        private void DrawItems()
        {
            for (int i = 0; i < barConfig.ShortcutList.Count; i++)
            {
                var sh = barConfig.ShortcutList[i];
                var name = sh.Name;
                var type = sh.Type;
                var command = sh.Command;

                ImGui.PushID(i);

                if ((!vertical && barConfig.AutoButtonWidth) ? ImGui.Button(name) : ImGui.Button(name, new Vector2(barConfig.ButtonWidth, 23)))
                    ItemClicked(type, command, $"{name}Category");
                if (ImGui.IsItemHovered())
                {
                    Reveal();

                    if (type == Shortcut.ShortcutType.Category && !string.IsNullOrEmpty(command))
                        ImGui.SetTooltip(command);
                }

                if (type == Shortcut.ShortcutType.Category)
                    CategoryPopup(i);

                ImGui.OpenPopupOnItemClick("editItem", 1);

                ItemConfigPopup(barConfig.ShortcutList, i);

                if (!vertical && i != barConfig.ShortcutList.Count - 1)
                    ImGui.SameLine();

                ImGui.PopID();
            }
        }

        private void DrawAddButton()
        {
            if (!vertical && barConfig.ShortcutList.Count > 0)
                ImGui.SameLine();

            if ((!vertical && barConfig.AutoButtonWidth) ? ImGui.Button("+") : ImGui.Button("+", new Vector2(barConfig.ButtonWidth, 23)))
            {
                Reveal();
                _sh = new Shortcut();

                ImGui.OpenPopup("addItem");
            }
            if (ImGui.IsItemHovered())
            {
                Reveal();

                ImGui.SetTooltip("Add a new button.\nRight click this (or the bar background) for options.\nRight click other buttons to edit them.");
            }

            ImGui.OpenPopupOnItemClick($"BarConfig##{barNumber}", 1);

            ItemCreatePopup(barConfig.ShortcutList);
        }

        private void ItemClicked(Shortcut.ShortcutType type, string command, string categoryid = "")
        {
            Reveal();

            switch (type)
            {
                case Shortcut.ShortcutType.Single:
                    if (!string.IsNullOrEmpty(command))
                        plugin.ExecuteCommand(command.Substring(0, Math.Min(command.Length, maxCommandLength)));
                    break;
                case Shortcut.ShortcutType.Multiline:
                    foreach (string c in command.Split('\n'))
                    {
                        if (!string.IsNullOrEmpty(c))
                            plugin.ExecuteCommand(c.Substring(0, Math.Min(c.Length, maxCommandLength)));
                    }
                    break;
                case Shortcut.ShortcutType.Category:
                    _mx = mousePos.X;
                    _my = mousePos.Y;
                    // I feel like I'm overcomplicating this...
                    float pX, pY;
                    var mousePadding = 6.0f;
                    if (docked)
                    {
                        if (!vertical)
                        {
                            pX = piv.X;
                            pY = Math.Abs(piv.Y - 1.0f);
                            _my += mousePadding - ((mousePadding * 2) * pY);
                        }
                        else
                        {
                            pX = Math.Abs(piv.X - 1.0f);
                            pY = piv.Y;
                            _mx += (mousePadding - ((mousePadding * 2) * pX)) * (1 - (2 * Math.Abs(pY - 0.5f)));
                            _my += -(mousePadding * 2) * (pY - 0.5f);
                        }
                    }
                    else
                    {
                        if (!vertical)
                        {
                            if (_mx < window.X / 3)
                                pX = 0.0f;
                            else if (_mx < window.X * 2 / 3)
                                pX = 0.5f;
                            else
                                pX = 1.0f;

                            if (_my < window.Y / 3)
                            {
                                pY = 0.0f;
                                _my += mousePadding;
                            }
                            else
                            {
                                pY = 1.0f;
                                _my -= mousePadding;
                            }
                        }
                        else
                        {
                            if (_mx < window.X * 2 / 3)
                            {
                                pX = 0.0f;
                                _mx += mousePadding;
                            }
                            else
                            {
                                pX = 1.0f;
                                _mx -= mousePadding;
                            }

                            if (_my < window.Y / 3)
                                pY = 0.0f;
                            else if (_my < window.Y * 2 / 3)
                                pY = 0.5f;
                            else
                                pY = 1.0f;
                        }
                    }
                    _catpiv = new Vector2(pX, pY);
                    _catpos = new Vector2(_mx, _my);
                    ImGui.OpenPopup(categoryid);
                    break;
                default:
                    break;
            }
        }

        private void CategoryPopup(int i)
        {
            var sh = barConfig.ShortcutList[i];
            var name = sh.Name;

            //var buttonPos = ImGui.GetCursorPos(); // I give up... just place the popup above the mouse
            //PluginLog.Log($"{buttonPos.X} {buttonPos.Y} {ImGui.GetItemRectSize().X} {ImGui.GetItemRectSize().Y}");
            ImGui.SetNextWindowPos(_catpos, ImGuiCond.Always, _catpiv);
            if (ImGui.BeginPopup($"{name}Category"))
            {
                Reveal();

                var sublist = sh.SubList;

                for (int j = 0; j < sublist.Count; j++)
                {
                    var _name = sublist[j].Name;
                    var _type = sublist[j].Type;
                    var _command = sublist[j].Command;

                    ImGui.PushID(j);

                    if (ImGui.Selectable(_name, false, ImGuiSelectableFlags.None, new Vector2(140, 20)))
                        ItemClicked(_type, _command);

                    ImGui.OpenPopupOnItemClick("editItem", 1);

                    ItemConfigPopup(sublist, j);

                    ImGui.PopID();
                }

                if (!sh.HideAdd)
                {
                    if (ImGui.Selectable("                         +", false, ImGuiSelectableFlags.DontClosePopups, new Vector2(140, 20)))
                    {
                        _sh = new Shortcut();
                        ImGui.OpenPopup("addItem");
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Add a new button.");
                }

                ItemCreatePopup(sublist);

                ImGui.EndPopup();
            }
        }

        private void ItemBaseUI(Shortcut sh, bool editing, bool notSubItem)
        {
            Reveal();

            if (ImGui.InputText("Name          ", ref sh.Name, 256) && editing) // Not a bug... just ImGui not extending the window to fit multiline's name...
                config.Save();

            // No nested categories
            var _t = (int)sh.Type;
            if (ImGui.Combo("Type", ref _t, notSubItem ? "Single\0Multiline\0Category" : "Single\0Multiline"))
            {
                sh.Type = (Shortcut.ShortcutType)_t;
                if (sh.Type == Shortcut.ShortcutType.Single)
                    sh.Command = sh.Command.Split('\n')[0];
                else if (sh.Type == Shortcut.ShortcutType.Category)
                    sh.SubList ??= new List<Shortcut>();

                if (editing)
                    config.Save();
            }

            switch (sh.Type)
            {
                case Shortcut.ShortcutType.Single:
                    if (ImGui.InputText("Command", ref sh.Command, (uint)maxCommandLength) && editing)
                        config.Save();
                    break;
                case Shortcut.ShortcutType.Multiline:
                    if (ImGui.InputTextMultiline("Command##Multi", ref sh.Command, (uint)maxCommandLength * 15, new Vector2(272, 124)) && editing)
                        config.Save();
                    break;
                case Shortcut.ShortcutType.Category:
                    if (ImGui.InputText("Tooltip", ref sh.Command, (uint)maxCommandLength) && editing)
                        config.Save();
                    break;
                default:
                    break;
            }
        }

        private void ItemCreatePopup(List<Shortcut> shortcuts)
        {
            if (ImGui.BeginPopup("addItem"))
            {
                ItemBaseUI(_sh, false, shortcuts == barConfig.ShortcutList);

                if (ImGui.Button("Create"))
                {
                    shortcuts.Add(_sh);
                    config.Save();
                    ImGui.CloseCurrentPopup();
                }

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void ItemConfigPopup(List<Shortcut> shortcuts, int i)
        {
            if (ImGui.BeginPopup("editItem"))
            {
                var sh = shortcuts[i];

                ItemBaseUI(sh, true, shortcuts == barConfig.ShortcutList);

                if (sh.Type == Shortcut.ShortcutType.Category)
                {
                    if (ImGui.Checkbox("Hide + Button", ref sh.HideAdd))
                        config.Save();
                }

                if (ImGui.Button((shortcuts == barConfig.ShortcutList && !vertical) ? "←" : "↑") && i > 0)
                {
                    shortcuts.RemoveAt(i);
                    shortcuts.Insert(i - 1, sh);
                    config.Save();
                    ImGui.CloseCurrentPopup(); // Sry its just simpler to close this than to deal with updating the menus without remaking them
                }
                ImGui.SameLine();
                if (ImGui.Button((shortcuts == barConfig.ShortcutList && !vertical) ? "→" : "↓") && i < (shortcuts.Count - 1))
                {
                    shortcuts.RemoveAt(i);
                    shortcuts.Insert(i + 1, sh);
                    config.Save();
                    ImGui.CloseCurrentPopup(); // Sry its just simpler to close this than to deal with updating the menus without remaking them
                }
                ImGui.SameLine();
                if (ImGui.Button("Delete"))
                    plugin.ExecuteCommand("/echo <se> Right click to delete!");
                //if (ImGui.IsItemClicked(1)) // Jesus christ I hate ImGui who made this function activate on PRESS AND NOT RELEASE??? THIS ISN'T A CLICK
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right click this button to delete the shortcut!");

                    if (ImGui.IsMouseReleased(1))
                    {
                        shortcuts.RemoveAt(i);
                        config.Save();
                        ImGui.CloseCurrentPopup();
                    }
                }

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        public void BarConfigPopup()
        {
            if (ImGui.BeginPopup($"BarConfig##{barNumber}"))
            {
                Reveal();

                var _dock = (int)barConfig.DockSide;
                if (ImGui.Combo("Bar Side", ref _dock, "Top\0Left\0Bottom\0Right\0Undocked\0Undocked (Vertical)"))
                {
                    barConfig.DockSide = (BarDock)_dock;
                    config.Save();
                    SetupPosition();
                }

                if (docked)
                {
                    var _align = (int)barConfig.Alignment;
                    if (ImGui.Combo("Bar Alignment", ref _align, vertical ? "Top\0Center\0Bottom" : "Left\0Center\0Right"))
                    {
                        barConfig.Alignment = (BarAlign)_align;
                        config.Save();
                        SetupPosition();
                    }

                    var _visibility = (int)barConfig.Visibility;
                    if (ImGui.Combo("Bar Animation", ref _visibility, "Slide\0Immediate\0Always Visible"))
                    {
                        barConfig.Visibility = (VisibilityMode)_visibility;
                        config.Save();
                    }
                }
                else
                {
                    if (ImGui.Checkbox("Lock Position", ref barConfig.LockedPosition))
                        config.Save();
                }

                if (!vertical)
                {
                    if (ImGui.Checkbox("Automatic Button Width", ref barConfig.AutoButtonWidth))
                        config.Save();
                }

                if (vertical || !barConfig.AutoButtonWidth)
                {
                    if (ImGui.SliderInt("Button Width", ref barConfig.ButtonWidth, 16, 200))
                        config.Save();
                }

                if (barConfig.ShortcutList.Count > 0)
                {
                    if (ImGui.Checkbox("Hide + Button", ref barConfig.HideAdd))
                    {
                        if (barConfig.HideAdd)
                            plugin.ExecuteCommand("/echo <se> You can right click on the bar itself (the black background) to reopen this settings menu!");
                        config.Save();
                    }
                }

                ImGui.Spacing();
                ImGui.Spacing();
                if (ImGui.Button("QoL Bar Config"))
                    plugin.ToggleConfig();

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void SetBarSize()
        {
            barSize.Y = ImGui.GetCursorPosY() + 4;
            ImGui.SameLine();
            barSize.X = ImGui.GetCursorPosX();
            //PluginLog.Log($"{ImGui.GetCursorPosX()} {ImGui.GetCursorPosY()}");
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
            if (barConfig.Visibility == VisibilityMode.Slide)
                TweenBarPosition();
            else
                barPos = _reveal ? revealPos : hidePos;
        }

        private void TweenBarPosition()
        {
            if (_reveal != _lastReveal)
            {
                _lastReveal = _reveal;
                _tweenStart = barPos;
                _tweenProgress = 0;
            }

            if (_tweenProgress >= 1)
            {
                barPos = _reveal ? revealPos : hidePos;
            }
            else
            {
                var dt = ImGui.GetIO().DeltaTime * 2;
                _tweenProgress = Math.Min(_tweenProgress + dt, 1);

                var x = -1 * ((float)Math.Pow(_tweenProgress - 1, 4) - 1); // Quartic ease out
                var deltaX = ((_reveal ? revealPos.X : hidePos.X) - _tweenStart.X) * x;
                var deltaY = ((_reveal ? revealPos.Y : hidePos.Y) - _tweenStart.Y) * x;

                barPos.X = _tweenStart.X + deltaX;
                barPos.Y = _tweenStart.Y + deltaY;
            }
        }

        public void Dispose()
        {

        }
    }
}
