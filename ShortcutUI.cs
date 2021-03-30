using System;
using System.Numerics;
using System.Collections.Generic;
using System.Windows.Forms;
using ImGuiNET;
using Dalamud.Plugin;
using static QoLBar.BarCfg;

namespace QoLBar
{
    public static class ShortcutUI
    {
        // TODO: Refactor BarUI into separate files (Most of BarUI needs to be rewritten to achieve this)

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
