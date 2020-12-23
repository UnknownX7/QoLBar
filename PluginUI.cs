using ImGuiNET;
using System;
using System.Numerics;
using System.Collections.Generic;
using static ShortcutPlugin.BarConfig;
using Dalamud.Plugin;

namespace ShortcutPlugin
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible { get; set; } = true;

        private readonly BarConfig barConfig;

        private string _inputname = string.Empty;
        private int _inputtype = 0;
        private string _inputcommand = string.Empty;
        private bool _hideadd = false;
        private static Vector2 window = ImGui.GetIO().DisplaySize;
        private static Vector2 mousePos = ImGui.GetIO().MousePos;
        private Vector2 barSize = new Vector2(200, 38);
        private Vector2 barPos;
        private readonly ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings;
        private readonly int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more
        private Vector2 piv = new Vector2();
        private Vector2 hidePos = new Vector2();
        private Vector2 revealPos = new Vector2();
        private bool vertical = false;

        private bool _reveal = true;
        private bool _lastReveal = true;
        private Vector2 _tweenStart;
        private float _tweenProgress = 1;
        private float _mx = 0f;
        private float _my = 0f;
        private Vector2 _catpiv = new Vector2();
        private Vector2 _catpos = new Vector2();

        private readonly ShortcutPlugin plugin;
        private readonly Configuration config;

        public PluginUI(ShortcutPlugin p, Configuration config)
        {
            plugin = p;
            this.config = config;
            barConfig = config.BarConfigs[0]; // For now, only one shortcut bar exists
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
                    break;
                case BarDock.Left: //   1.0 0.0, 1.0 0.5, 1.0 1.0 // 0(+W) 0,    0(+W) winY/2,    0(+W) winY
                    pivY = 1.0f;
                    defPos = 0.0f;
                    vertical = true;
                    break;
                case BarDock.Bottom: // 0.0 0.0, 0.5 0.0, 1.0 0.0 // 0 winY(-H), winX/2 winY(-H), winX winY(-H)
                    pivY = 0.0f;
                    defPos = window.Y;
                    vertical = false;
                    break;
                case BarDock.Right: //  0.0 0.0, 0.0 0.5, 0.0 1.0 // winX(-W) 0, winX(-W) winY/2, winX(-W) winY
                    pivY = 0.0f;
                    defPos = window.X;
                    vertical = true;
                    break;
                case BarDock.UndockedH:
                    break;
                case BarDock.UndockedV:
                    vertical = true;
                    break;
                default:
                    break;
            }

            switch (barConfig.Alignment)
            {
                case BarAlign.LeftOrTop:
                    pivX = 0.0f;
                    offset = 10;
                    break;
                case BarAlign.Center:
                    pivX = 0.5f;
                    break;
                case BarAlign.RightOrBottom:
                    pivX = 1.0f;
                    offset = -10;
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

            barPos = revealPos;
            _tweenStart = hidePos;
        }

        public void Draw()
        {
            if (!IsVisible || plugin.pluginInterface.ClientState.LocalPlayer == null) return;

            var io = ImGui.GetIO();
            window = io.DisplaySize;
            mousePos = io.MousePos;

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

            // Check if mouse is nearby
            /*if (barConfig.Visibility == VisibilityMode.Always || (Math.Abs(mousePos.X - barPos.X) <= (barSize.X / 2) && (window.Y - mousePos.Y) <= barSize.Y))
                Reveal();
            else
                Hide();*/

            // Invisible UI to check if the mouse is nearby
            ImGui.SetNextWindowPos(revealPos, ImGuiCond.Always, piv);
            ImGui.SetNextWindowSize(barSize);
            ImGui.Begin("ShortcutBarMouseDetection", flags | ImGuiWindowFlags.NoBackground);
            if (barConfig.Visibility == VisibilityMode.Always || ImGui.IsWindowHovered())
                Reveal();
            else
                Hide();
            ImGui.End();

            if (_reveal || barPos != hidePos) // Don't bother to render when fully off screen
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);

                ImGui.SetNextWindowPos(barPos, ImGuiCond.Always, piv);
                ImGui.SetNextWindowSize(barSize);

                ImGui.Begin("ShortcutBar", flags);

                if (ImGui.IsWindowHovered())
                    Reveal();

                for (int i = 0; i < barConfig.ShortcutList.Count; i++)
                {
                    var _sh = barConfig.ShortcutList[i];
                    var name = _sh.Name;
                    var type = _sh.Type;
                    var command = _sh.Command;
                    var hideadd = _sh.HideAdd;

                    if (!vertical ? ImGui.Button($"{name}##{i}") : ImGui.Button($"{name}##{i}", new Vector2(barConfig.ButtonWidth, 23)))
                        ItemClicked(type, command, $"{name}{i}Category");
                    if (ImGui.IsItemHovered())
                    {
                        Reveal();
                        _inputname = name; // Don't ask
                        _inputtype = (int)type;
                        _inputcommand = command;
                        _hideadd = hideadd;

                        if (type == Shortcut.ShortcutType.Category && !string.IsNullOrEmpty(command))
                            ImGui.SetTooltip(command);
                    }

                    if (type == Shortcut.ShortcutType.Category)
                        CategoryPopup(i);

                    ImGui.OpenPopupOnItemClick($"editItem{i}", 1);

                    ItemConfigPopup($"editItem{i}", barConfig.ShortcutList, i);

                    if (!vertical)
                        ImGui.SameLine();
                }

                if (!vertical ? ImGui.Button("+") : ImGui.Button("+", new Vector2(barConfig.ButtonWidth, 23)))
                {
                    Reveal();
                    _inputname = string.Empty;
                    _inputtype = 0;
                    _inputcommand = string.Empty;
                    _hideadd = false;

                    ImGui.OpenPopup("addItem");
                }
                if (ImGui.IsItemHovered())
                {
                    Reveal();

                    ImGui.SetTooltip("Add a new button.\nRight click this for options.\nRight click other buttons to edit them.");
                }

                ImGui.OpenPopupOnItemClick("BarConfig", 1);

                ItemConfigPopup("addItem", barConfig.ShortcutList, -1);

                BarConfigPopup();

                barSize.Y = ImGui.GetCursorPosY() + 4;
                ImGui.SameLine();
                barSize.X = ImGui.GetCursorPosX();
                //PluginLog.Log($"{ImGui.GetCursorPosX()} {ImGui.GetCursorPosY()}");

                ImGui.End();
            }

            if (barConfig.Visibility == VisibilityMode.Slide)
                TweenPosition();

            if (barConfig.Visibility == VisibilityMode.Immediate || barConfig.Visibility == VisibilityMode.Always)
            {
                if (_reveal)
                    barPos = revealPos;
                else
                    barPos = hidePos;
            }
        }

        private void Reveal()
        {
            _reveal = true;
        }

        private void Hide()
        {
            _reveal = false;
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
            if (ImGui.BeginPopup($"{name}{i}Category"))
            {
                Reveal();

                var sublist = sh.SubList;

                for (int j = 0; j < sublist.Count; j++)
                {
                    var _name = sublist[j].Name;
                    var _type = sublist[j].Type;
                    var _command = sublist[j].Command;

                    if (ImGui.Selectable($"{_name}##{name} {i} {j}", false, ImGuiSelectableFlags.None, new Vector2(140, 20)))
                        ItemClicked(_type, _command);
                    if (ImGui.IsItemHovered())
                    {
                        _inputname = _name; // Don't ask
                        _inputtype = (int)_type;
                        _inputcommand = _command;
                        _hideadd = false;
                    }

                    ImGui.OpenPopupOnItemClick($"{name}{i}editItem{j}", 1);

                    ItemConfigPopup($"{name}{i}editItem{j}", sublist, j);
                }

                if (!sh.HideAdd)
                {
                    if (ImGui.Selectable("                         +", false, ImGuiSelectableFlags.DontClosePopups, new Vector2(140, 20)))
                    {
                        _inputname = string.Empty;
                        _inputtype = 0;
                        _inputcommand = string.Empty;
                        _hideadd = false;

                        ImGui.OpenPopup($"{name}{i}addItem");
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Add a new button.");
                }

                ItemConfigPopup($"{name}{i}addItem", sublist, -1);

                ImGui.EndPopup();
            }
        }

        private void ItemConfigPopup(string id, List<Shortcut> shortcuts, int i)
        {
            if (ImGui.BeginPopup(id))
            {
                Reveal();

                ImGui.Text("Name");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(230);
                ImGui.InputText("##NameInput", ref _inputname, 256);

                // No nested categories
                if (ImGui.Combo("##TypeInput", ref _inputtype, (shortcuts == barConfig.ShortcutList) ? "Single\0Multiline\0Category" : "Single\0Multiline"))
                {
                    if (_inputtype == (int)Shortcut.ShortcutType.Single)
                        _inputcommand = _inputcommand.Split('\n')[0];
                }

                switch ((Shortcut.ShortcutType)_inputtype)
                {
                    case Shortcut.ShortcutType.Single:
                        ImGui.Text("Command");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(205);
                        ImGui.InputText("##CommandInput", ref _inputcommand, (uint)maxCommandLength);
                        break;
                    case Shortcut.ShortcutType.Multiline:
                        ImGui.Text("Command");
                        ImGui.SameLine();
                        ImGui.InputTextMultiline("##CommandInput", ref _inputcommand, (uint)maxCommandLength * 15, new Vector2(205, 124));
                        break;
                    case Shortcut.ShortcutType.Category:
                        ImGui.Text("Tooltip");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(223);
                        ImGui.InputText("##CommandInput", ref _inputcommand, (uint)maxCommandLength);

                        ImGui.Text("Hide + Button");
                        ImGui.SameLine();
                        ImGui.Checkbox("##Hide+", ref _hideadd);
                        break;
                    default:
                        break;
                }

                if (i >= 0)
                {
                    if (ImGui.Button((shortcuts == barConfig.ShortcutList) ? "←" : "↑") && i > 0)
                    {
                        var sh = shortcuts[i];
                        shortcuts.RemoveAt(i);
                        shortcuts.Insert(i - 1, sh);
                        config.Save();
                        ImGui.CloseCurrentPopup(); // Sry its just simpler to close this than to deal with updating the menus without remaking them
                    }
                    ImGui.SameLine();
                }

                if (ImGui.Button("Save") && !string.IsNullOrEmpty(_inputname) && (_inputtype == (int)Shortcut.ShortcutType.Category || !string.IsNullOrEmpty(_inputcommand)))
                {
                    List<Shortcut> _sublist;
                    if (_inputtype == (int)Shortcut.ShortcutType.Category)
                    {
                        if (i >= 0)
                            _sublist = shortcuts[i].SubList ?? new List<Shortcut>();
                        else
                            _sublist = new List<Shortcut>();
                    }
                    else
                    {
                        _sublist = null;
                        _hideadd = false;
                    }

                    var sh = new Shortcut {
                        Name = _inputname,
                        Type = (Shortcut.ShortcutType)_inputtype,
                        Command = _inputcommand,
                        SubList = _sublist,
                        HideAdd = _hideadd
                    };
                    if (i >= 0)
                        shortcuts[i] = sh;
                    else
                        shortcuts.Add(sh);
                    
                    config.Save();
                    ImGui.CloseCurrentPopup();
                }

                if (i >= 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Delete"))
                        plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    /*if (ImGui.IsItemClicked(1)) // Jesus christ I hate ImGui who made this function activate on PRESS AND NOT RELEASE??? THIS ISN'T A CLICK
                    {
                        shortcuts.RemoveAt(i);
                        config.Save();
                        ImGui.CloseCurrentPopup();
                    }*/
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

                    ImGui.SameLine();
                    if (ImGui.Button((shortcuts == barConfig.ShortcutList) ? "→" : "↓") && i < (shortcuts.Count - 1))
                    {
                        var sh = shortcuts[i];
                        shortcuts.RemoveAt(i);
                        shortcuts.Insert(i + 1, sh);
                        config.Save();
                        ImGui.CloseCurrentPopup(); // Sry its just simpler to close this than to deal with updating the menus without remaking them
                    }
                }

                ClampWindowPos();

                ImGui.EndPopup();
            }
        }

        private void BarConfigPopup()
        {
            if (ImGui.BeginPopup("BarConfig"))
            {
                Reveal();

                var _dock = (int)barConfig.DockSide;
                ImGui.Text("Bar Side");
                ImGui.SameLine();
                if (ImGui.Combo("##Dock", ref _dock, "Top\0Left\0Bottom\0Right\0Undocked\0Undocked (Vertical)"))
                {
                    barConfig.DockSide = (BarDock)_dock;
                    config.Save();
                    SetupPosition();
                }

                if (barConfig.DockSide != BarDock.UndockedH && barConfig.DockSide != BarDock.UndockedV)
                {
                    var _align = (int)barConfig.Alignment;
                    ImGui.Text("Bar Alignment");
                    ImGui.SameLine();
                    if (ImGui.Combo("##Alignment", ref _align, vertical ? "Top\0Center\0Bottom" : "Left\0Center\0Right"))
                    {
                        barConfig.Alignment = (BarAlign)_align;
                        config.Save();
                        SetupPosition();
                    }

                    var _visibility = (int)barConfig.Visibility;
                    ImGui.Text("Bar Animation");
                    ImGui.SameLine();
                    if (ImGui.Combo("##Animation", ref _visibility, "Slide\0Immediate\0Always Visible"))
                    {
                        barConfig.Visibility = (VisibilityMode)_visibility;
                        config.Save();
                    }
                }

                if (vertical)
                {
                    ImGui.Text("Button Width");
                    ImGui.SameLine();
                    if (ImGui.SliderInt("##ButtonWidth", ref barConfig.ButtonWidth, 16, 200))
                        config.Save();
                }

                ClampWindowPos();

                ImGui.EndPopup();
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
        
        private void TweenPosition()
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
