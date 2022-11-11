using System;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Game.ClientState.GamePad;
using ImGuiNET;

namespace Gamepad
{
    public class GamepadBind
    {
        [DefaultValue(0)] 
        public int Button = 0;
        
        [DefaultValue(false)]                
        public bool PassThrough = false;

        private bool _held;
        
        private GamepadButtons _tmpButton = 0;
        private DateTime _lastInputChange = DateTime.Now;

        private static float deadzone = 0.1f;

        public GamepadBind(int button)
        {
            Button = button;
        }

        public override string ToString()
        {
            return ((GamepadButtons)Button).ToString();
        }

        public bool IsPress(GamepadState state)
        {
            return PressCheck(state).Item2;
        }

        public bool IsRelease(GamepadState state)
        {
            return PressCheck(state).Item3;
        }

        public bool IsActive(GamepadState state)
        {
            return PressCheck(state).Item1;
        }
        
        private (bool, bool, bool) PressCheck(GamepadState state)
        {
            if (Button <= 0) return (false, false, false);
            
            bool active = GetKeyPressed(state) == (GamepadButtons)Button;

            bool press = false;
            bool release = false;
            if (active)
            {
                if (!_held)
                {
                    _held = true;
                    press = true;
                
                    // block keybind for the game?
                    if (!PassThrough)
                    {
                        try
                        {
                            ImGuiIOPtr io = ImGui.GetIO();
                            //io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            else
            {
                if (_held)
                {
                    _held = false;
                    release = true;
                
                    // unblock keybind for the game
                    if (!PassThrough)
                    {
                        try
                        {
                            ImGuiIOPtr io = ImGui.GetIO();
                            //io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return (active, press, release);
        }
        
        public static Vector2? RightStick(GamepadState state)
        {
            if(state.RightStickRight 
               + state.RightStickLeft 
               + state.RightStickUp 
               + state.RightStickDown 
               > deadzone*2)
            {
                Vector2 di = new Vector2(state.RightStickRight, -state.RightStickUp);
                
                if (state.RightStickLeft > 0)
                {
                    di.X = -state.RightStickLeft;
                }
                if (state.RightStickDown > 0)
                {
                    di.Y = state.RightStickDown;
                }
                
                return di;
            }

            return null;
        }

        public static GamepadBind DrawInputConfig(GamepadBind current, GamepadState state)
        {
            bool changed = false;
            if (current == null)
            {
                current = new GamepadBind((int)GamepadButtons.None);
                changed = true;
            }

            if (current.InputHotPad("HotPad", state))
            {
                changed = true;
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
                ImGui.SetTooltip("Hold gamepad combo for two seconds to set binding.\n" +
                                 "Press escape to clear the binding.");

            if (changed)
            {
                return current;
            }

            return null;
        }

        public static bool IsNullOrUnset(GamepadBind bind)
        {
            if (bind == null) return true;
            if (((GamepadButtons)bind.Button) == GamepadButtons.None) return true;
            return false;
        }

        public static bool IsPress(GamepadBind bind, GamepadState state)
        {
            if (bind == null) return false;
            return bind.IsPress(state);
        }

        public static bool IsRelease(GamepadBind bind, GamepadState state)
        {
            if (bind == null) return false;
            return bind.IsRelease(state);
        }
        public static bool IsActive(GamepadBind bind, GamepadState state)
        {
            if (bind == null) return false;
            return bind.IsActive(state);
        }

        public bool InputHotPad(string id, GamepadState state)
        {
            var dispKey = ToString();
            ImGui.InputText($"{id}##{ToString()}", ref dispKey, 200,
                ImGuiInputTextFlags.ReadOnly |
                ImGuiInputTextFlags.AllowTabInput); // delete the box to delete focus 4head
            
            if (ImGui.IsItemActive())
            {
                var ButtonPressed = GetKeyPressed(state);
                if (ButtonPressed != _tmpButton)
                {
                    _lastInputChange = DateTime.Now;
                    _tmpButton = ButtonPressed;
                }
                
                if (ButtonPressed > GamepadButtons.None && ( DateTime.Now - _lastInputChange ).Seconds >= 2)
                {
                    Button = (int)ButtonPressed;
                    return true;
                }
            }

            if (!ImGui.IsItemDeactivated()
                || (!ImGui.GetIO().KeysDown[27] && !ImGui.GetIO().KeysDown[8]))
            {
                return false;
            }

            Reset();
            return true;
        }

        public GamepadButtons GetKeyPressed(GamepadState state)
        {
            GamepadButtons held = GamepadButtons.None;
            for (int i = 0; i < _supportedButtons.Length; i++)
            {
                if (state.Raw(_supportedButtons[i]) > deadzone)
                {
                    held |= _supportedButtons[i];
                }
            }

            return held;
        }

        private GamepadButtons[] _supportedButtons = new GamepadButtons[]
        {
            GamepadButtons.L1,
            GamepadButtons.L2,
            GamepadButtons.L3,
            GamepadButtons.R1,
            GamepadButtons.L2,
            GamepadButtons.R3,
            GamepadButtons.Select,
            GamepadButtons.Start,
            GamepadButtons.DpadLeft,
            GamepadButtons.DpadUp,
            GamepadButtons.DpadRight,
            GamepadButtons.DpadDown,
            GamepadButtons.West,
            GamepadButtons.North,
            GamepadButtons.East,
            GamepadButtons.South
        };

        public void Reset()
        {
            Button = (int)GamepadButtons.None;
        }

        public bool Equals(GamepadBind bind)
        {
            return Button == bind.Button;
        }

    }
}