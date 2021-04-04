using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;

namespace QoLBar
{
    // I can't believe C# wont let me just add this to the fucking class
    public static class ImGuiEx
    {
        public static void SetItemTooltip(string s, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
        {
            if (ImGui.IsItemHovered(flags))
                ImGui.SetTooltip(s);
        }

        // Why is this not a basic feature of ImGui...
        private static readonly Stack<float> _fontScaleStack = new Stack<float>();
        private static float _curScale = 1;
        public static void PushFontScale(float scale)
        {
            _fontScaleStack.Push(_curScale);
            _curScale = scale;
            ImGui.SetWindowFontScale(_curScale);
        }

        public static void PopFontScale()
        {
            _curScale = _fontScaleStack.Pop();
            ImGui.SetWindowFontScale(_curScale);
        }

        public static float GetFontScale() => _curScale;

        public static void ClampWindowPos(Vector2 max) => ClampWindowPos(Vector2.Zero, max);

        public static void ClampWindowPos(Vector2 min, Vector2 max)
        {
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var x = Math.Min(Math.Max(pos.X, min.X), max.X - size.X);
            var y = Math.Min(Math.Max(pos.Y, min.Y), max.Y - size.Y);
            ImGui.SetWindowPos(new Vector2(x, y));
        }
    }
}
