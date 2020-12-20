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
        private float barW = 200;
        private float barH = 38;
        private float barX = window.X / 2;
        private float barY = window.Y - 1; // -1 so the bar will draw for a single frame to initialize variables on start
        private readonly ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings;

        private float _lastY, _curY, _nextY, _tweenProgress;
        private float _mx = 0f;
        private float _my = 0f;

        private readonly ShortcutPlugin plugin;
        private readonly Configuration config;

        public PluginUI(ShortcutPlugin p, Configuration config)
        {
            plugin = p;
            this.config = config;
            barConfig = config.BarConfigs[0]; // For now, only one shortcut bar exists

            _lastY = barY; // Previous Y before tweening was reset
            _curY = barY; // Current tweening target for Y
            _nextY = barY; // Resets Y tweening when changed
            _tweenProgress = 0;
        }

        public void Draw()
        {
            if (!IsVisible || plugin.pluginInterface.ClientState.LocalPlayer == null) return;

            // Refresh these to correct any window size changes
            var io = ImGui.GetIO();
            window = io.DisplaySize;
            barX = window.X / 2;

            mousePos = io.MousePos;

            // Check if mouse is nearby
            if (barConfig.Visibility == VisibilityMode.Always || (Math.Abs(mousePos.X - barX) <= (barW / 2) && (window.Y - mousePos.Y) <= barH))
                _nextY = window.Y - barH;
            else
                _nextY = window.Y;

            // Old invisible UI method just in case
            /*ImGui.SetNextWindowPos(new Vector2(window.X / 2, window.Y - barH / 1.75f), ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            ImGui.SetNextWindowSize(new Vector2(barW, barH / 1.75f));
            ImGui.Begin("ShortcutBarMouseDetection", flags | ImGuiWindowFlags.NoBackground);
            if (ImGui.IsWindowHovered())
            {
                _nextY = window.Y - barH;

                ImGui.SetTooltip($"Math.Abs(mousePos.X - barX) {Math.Abs(mousePos.X - barX)} (barW / 2) {(barW / 2)} (window.Y - mousePos.Y) {(window.Y - mousePos.Y)} barh {barH}");
            }
            ImGui.End();*/

            if (barY != window.Y || _nextY != window.Y) // Don't bother to render when fully off screen
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);

                ImGui.SetNextWindowPos(new Vector2(barX, barY), ImGuiCond.Always, new Vector2(0.5f, 0.0f));
                ImGui.SetNextWindowSize(new Vector2(barW, barH));

                ImGui.Begin("ShortcutBar", flags);

                if (ImGui.IsWindowHovered())
                    _nextY = window.Y - barH;

                for (int i = 0; i < barConfig.ShortcutList.Count; i++)
                {
                    var _sh = barConfig.ShortcutList[i];
                    var name = _sh.Name;
                    var type = _sh.Type;
                    var command = _sh.Command;
                    var hideadd = _sh.HideAdd;

                    if (ImGui.Button($"{name}##{i}"))
                    {
                        _nextY = window.Y - barH;

                        switch (type)
                        {
                            case Shortcut.ShortcutType.Single:
                                plugin.ExecuteCommand(command);
                                break;
                            case Shortcut.ShortcutType.Multiline:
                                foreach (string c in command.Split('\n'))
                                {
                                    if (!string.IsNullOrEmpty(c))
                                        plugin.ExecuteCommand(c);
                                }
                                break;
                            case Shortcut.ShortcutType.Category:
                                ImGui.OpenPopup($"{name}{i}Category");
                                _mx = mousePos.X;
                                _my = mousePos.Y;
                                break;
                            default:
                                break;
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        _nextY = window.Y - barH;
                        _inputname = name; // Don't ask
                        _inputtype = (int)type;
                        _inputcommand = command;
                        _hideadd = hideadd;

                        if (type == Shortcut.ShortcutType.Category && !string.IsNullOrEmpty(command))
                            ImGui.SetTooltip(command);
                    }

                    if (type == Shortcut.ShortcutType.Category)
                    {
                        //var buttonPos = ImGui.GetCursorPos(); // I give up... just place the popup above the mouse
                        //PluginLog.Log($"{buttonPos.X} {buttonPos.Y} {ImGui.GetItemRectSize().X} {ImGui.GetItemRectSize().Y}");
                        ImGui.SetNextWindowPos(new Vector2(_mx, _my - 6), ImGuiCond.Always, new Vector2(0.5f, 1.0f));
                        if (ImGui.BeginPopup($"{name}{i}Category"))
                        {
                            _nextY = window.Y - barH;

                            var sublist = _sh.SubList;

                            for (int j = 0; j < sublist.Count; j++)
                            {
                                var _name = sublist[j].Name;
                                var _type = sublist[j].Type;
                                var _command = sublist[j].Command;

                                if (ImGui.Selectable($"{_name}##{name} {i} {j}", false, ImGuiSelectableFlags.None, new Vector2(140, 20)))
                                {
                                    switch (_type)
                                    {
                                        case Shortcut.ShortcutType.Single:
                                            plugin.ExecuteCommand(_command);
                                            break;
                                        case Shortcut.ShortcutType.Multiline:
                                            plugin.ExecuteCommand(_command);
                                            break;
                                        default:
                                            break;
                                    }
                                }
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

                            if (!hideadd && ImGui.Selectable("                         +", false, ImGuiSelectableFlags.DontClosePopups, new Vector2(140, 20)))
                            {
                                _inputname = string.Empty;
                                _inputtype = 0;
                                _inputcommand = string.Empty;
                                _hideadd = false;

                                ImGui.OpenPopup($"{name}{i}addItem");
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Add a new button.");

                            ItemConfigPopup($"{name}{i}addItem", sublist, -1);

                            ImGui.EndPopup();
                        }
                    }

                    ImGui.OpenPopupOnItemClick($"editItem{i}", 1);

                    ItemConfigPopup($"editItem{i}", barConfig.ShortcutList, i);

                    ImGui.SameLine();
                }

                if (ImGui.Button("+"))
                {
                    _nextY = window.Y - barH;
                    _inputname = string.Empty;
                    _inputtype = 0;
                    _inputcommand = string.Empty;
                    _hideadd = false;

                    ImGui.OpenPopup("addItem");
                }
                if (ImGui.IsItemHovered())
                {
                    _nextY = window.Y - barH;

                    ImGui.SetTooltip("Add a new button.\nRight click for options.");
                }

                ImGui.OpenPopupOnItemClick("BarConfig", 1);

                ItemConfigPopup("addItem", barConfig.ShortcutList, -1);

                BarConfigPopup();

                // I hope this works
                ImGui.SameLine();
                //PluginLog.Log($"{ImGui.GetCursorPosX()} {ImGui.GetCursorPosY()}");
                barW = ImGui.GetCursorPosX();

                ImGui.End();
            }

            if (barConfig.Visibility == VisibilityMode.Slide)
                TweenPositionY();

            if (barConfig.Visibility == VisibilityMode.Immediate || barConfig.Visibility == VisibilityMode.Always)
                barY = _nextY;
        }

        private void ItemConfigPopup(string id, List<Shortcut> shortcuts, int i)
        {
            if (ImGui.BeginPopup(id))
            {
                _nextY = window.Y - barH;

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
                        ImGui.InputText("##CommandInput", ref _inputcommand, 256);
                        break;
                    case Shortcut.ShortcutType.Multiline:
                        ImGui.Text("Command");
                        ImGui.SameLine();
                        ImGui.InputTextMultiline("##CommandInput", ref _inputcommand, 1024, new Vector2(205, 124));
                        break;
                    case Shortcut.ShortcutType.Category:
                        ImGui.Text("Tooltip");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(223);
                        ImGui.InputText("##CommandInput", ref _inputcommand, 256);

                        ImGui.Text("Hide + button");
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
                    ImGui.Button("Delete");
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

                // Clamp popup to screen
                var _lastPos = ImGui.GetWindowPos();
                var _size = ImGui.GetWindowSize();
                var _x = Math.Min(Math.Max(_lastPos.X, 0), window.X - _size.X);
                var _y = Math.Min(Math.Max(_lastPos.Y, 0), window.Y - _size.Y);
                ImGui.SetWindowPos(new Vector2(_x, _y));

                ImGui.EndPopup();
            }
        }

        private void BarConfigPopup()
        {
            if (ImGui.BeginPopup("BarConfig"))
            {
                _nextY = window.Y - barH;

                var _visibility = (int)barConfig.Visibility;

                ImGui.Text("Bar Animation");
                ImGui.SameLine();
                if (ImGui.Combo("##Animation", ref _visibility, "Slide\0Immediate\0Always Visible"))
                {
                    barConfig.Visibility = (VisibilityMode)_visibility;
                    config.Save();
                }

                ImGui.EndPopup();
            }
        }
        
        public void Dispose()
        {

        }

        private void TweenPositionY()
        {
            if (_curY != _nextY)
            {
                _lastY = barY;
                _curY = _nextY;
                _tweenProgress = 0;
            }
            var dt = ImGui.GetIO().DeltaTime * 2;
            _tweenProgress = Math.Min(_tweenProgress + dt, 1);

            var delta = _curY - _lastY;
            delta *= -1 * ((float)Math.Pow(_tweenProgress - 1, 4) - 1); // Quartic ease out
            barY = _lastY + delta;
        }
    }
}
