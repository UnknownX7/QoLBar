using System;
using System.Numerics;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using ImGuiNET;

namespace QoLBar
{
    public class BarConfig
    {
        [DefaultValue("")] public string Title = string.Empty;
        [DefaultValue(null)] public List<Shortcut> ShortcutList = new List<Shortcut>();
        [DefaultValue(false)] public bool Hidden = false;
        public enum VisibilityMode
        {
            Slide,
            Immediate,
            Always
        }
        [DefaultValue(VisibilityMode.Always)] public VisibilityMode Visibility = VisibilityMode.Always;
        public enum BarAlign
        {
            LeftOrTop,
            Center,
            RightOrBottom
        }
        [DefaultValue(BarAlign.Center)] public BarAlign Alignment = BarAlign.Center;
        public enum BarDock
        {
            Top,
            Left,
            Bottom,
            Right,
            UndockedH,
            UndockedV
        }
        [DefaultValue(BarDock.Bottom)] public BarDock DockSide = BarDock.Bottom;
        [DefaultValue(false)] public bool Hint = false;
        [DefaultValue(100)] public int ButtonWidth = 100;
        [DefaultValue(false)] public bool HideAdd = false;
        public Vector2 Position = Vector2.Zero;
        [DefaultValue(false)] public bool LockedPosition = false;
        public Vector2 Offset = Vector2.Zero;
        [DefaultValue(1.0f)] public float Scale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryScale = 1.0f;
        [DefaultValue(1.0f)] public float RevealAreaScale = 1.0f;
        [DefaultValue(1.0f)] public float FontScale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryFontScale = 1.0f;
        [DefaultValue(8)] public int Spacing = 8;
        public Vector2 CategorySpacing = new Vector2(8, 4);
        [DefaultValue(false)] public bool NoBackground = false;
        [DefaultValue(false)] public bool NoCategoryBackgrounds = false;
        [DefaultValue(false)] public bool OpenCategoriesOnHover = false;
        [DefaultValue(false)] public bool OpenSubcategoriesOnHover = false;
        [DefaultValue(-1)] public int ConditionSet = -1;

        public BarCfg Upgrade()
        {
            var oldPos = Position / ImGui.GetIO().DisplaySize;
            var oldOffset = Offset / ImGui.GetIO().DisplaySize;

            var bar = new BarCfg
            {
                Name = Title,
                Hidden = Hidden,
                Visibility = (BarCfg.BarVisibility)Visibility,
                Alignment = (BarCfg.BarAlign)Alignment,
                DockSide = (BarCfg.BarDock)DockSide,
                Hint = Hint,
                ButtonWidth = ButtonWidth,
                Editing = !HideAdd,
                Position = (DockSide == BarDock.UndockedH || DockSide == BarDock.UndockedV) ? new[] { oldPos.X, oldPos.Y } : new[] { oldOffset.X, oldOffset.Y },
                LockedPosition = LockedPosition,
                Scale = Scale,
                RevealAreaScale = RevealAreaScale,
                FontScale = FontScale,
                Spacing = new[] { Spacing, Spacing },
                NoBackground = NoBackground,
                ConditionSet = ConditionSet
            };

            foreach (var sh in ShortcutList)
                bar.ShortcutList.Add(sh.Upgrade(this, false));

            return bar;
        }
    }

    public class Shortcut
    {
        [DefaultValue("")] public string Name = string.Empty;
        public enum ShortcutType
        {
            Command,
            Multiline_DEPRECATED,
            Category,
            Spacer
        }
        [DefaultValue(ShortcutType.Command)] public ShortcutType Type = ShortcutType.Command;
        [DefaultValue("")] public string Command = string.Empty;
        [DefaultValue(0)] public int Hotkey = 0;
        [DefaultValue(false)] public bool KeyPassthrough = false;
        [DefaultValue(null)] public List<Shortcut> SubList;
        [DefaultValue(false)] public bool HideAdd = false;
        public enum ShortcutMode
        {
            Default,
            Incremental,
            Random
        }
        [DefaultValue(ShortcutMode.Default)] public ShortcutMode Mode = ShortcutMode.Default;
        [DefaultValue(140)] public int CategoryWidth = 140;
        [DefaultValue(false)] public bool CategoryStaysOpen = false;
        [DefaultValue(1)] public int CategoryColumns = 1;
        [DefaultValue(1.0f)] public float IconZoom = 1.0f;
        public Vector2 IconOffset = Vector2.Zero;
        public Vector4 IconTint = Vector4.One;

        [JsonIgnore] public int _i = 0;
        [JsonIgnore] public Shortcut _parent = null;
        [JsonIgnore] public bool _activated = false;

        public ShCfg Upgrade(BarConfig bar, bool sub)
        {
            var sh = new ShCfg
            {
                Name = Name,
                Type = Type switch
                {
                    ShortcutType.Category => ShCfg.ShortcutType.Category,
                    ShortcutType.Spacer => ShCfg.ShortcutType.Spacer,
                    _ => ShCfg.ShortcutType.Command
                },
                Command = Command,
                Hotkey = Hotkey,
                KeyPassthrough = KeyPassthrough,
                Mode = (ShCfg.ShortcutMode)Mode,
                Color = ImGui.ColorConvertFloat4ToU32(IconTint),
                IconZoom = IconZoom,
                IconOffset = new[] { IconOffset.X, IconOffset.Y },
                CategoryWidth = CategoryWidth,
                CategoryStaysOpen = CategoryStaysOpen,
                CategoryColumns = CategoryColumns,
                CategorySpacing = new[] { (int)bar.CategorySpacing.X, (int)bar.CategorySpacing.Y },
                CategoryScale = bar.CategoryScale,
                CategoryFontScale = bar.CategoryFontScale,
                CategoryNoBackground = bar.NoCategoryBackgrounds,
                CategoryOnHover = !sub ? bar.OpenCategoriesOnHover : bar.OpenSubcategoriesOnHover
            };

            if (IconTint.W > 1)
                sh.ColorAnimation = (int)Math.Round(IconTint.W * 255) - 255;

            if (SubList != null)
            {
                sh.SubList ??= new List<ShCfg>();
                foreach (var s in SubList)
                    sh.SubList.Add(s.Upgrade(bar, true));
            }

            return sh;
        }
    }
}
