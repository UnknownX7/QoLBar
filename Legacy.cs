using System;
using System.Numerics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Dalamud.Interface;
using ImGuiNET;

#pragma warning disable CS0612 // Type or member is obsolete

namespace QoLBar
{
    public class BarConfig
    {
        [DefaultValue("")] public string Title = string.Empty;
        [DefaultValue(null)] public List<Shortcut> ShortcutList = new();
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
        public Vector2 CategorySpacing = new(8, 4);
        [DefaultValue(false)] public bool NoBackground = false;
        [DefaultValue(false)] public bool NoCategoryBackgrounds = false;
        [DefaultValue(false)] public bool OpenCategoriesOnHover = false;
        [DefaultValue(false)] public bool OpenSubcategoriesOnHover = false;
        [DefaultValue(-1)] public int ConditionSet = -1;

        public BarCfg Upgrade()
        {
            var window = ImGuiHelpers.MainViewport.Size;
            var oldPos = Position / window;

            var oldOffset = Offset * ImGuiHelpers.GlobalScale;

            var add = 0f;
            switch (Alignment)
            {
                case BarAlign.LeftOrTop:
                    add = 22 + ImGui.GetFontSize();
                    break;
                case BarAlign.RightOrBottom:
                    add = -22 - ImGui.GetFontSize();
                    break;
            }

            switch (DockSide)
            {
                case BarDock.Top:
                case BarDock.Bottom:
                    oldOffset.X += add;
                    break;
                case BarDock.Left:
                case BarDock.Right:
                    oldOffset.Y += add;
                    break;
            }

            oldOffset /= window;

            var bar = new BarCfg
            {
                Name = Title,
                Hidden = Hidden,
                Visibility = (BarCfg.BarVisibility)Visibility,
                Hint = Hint,
                ButtonWidth = ButtonWidth,
                Editing = !HideAdd,
                Position = (DockSide == BarDock.UndockedH || DockSide == BarDock.UndockedV) ? new[] { oldPos.X, oldPos.Y } : new[] { oldOffset.X, oldOffset.Y },
                LockedPosition = DockSide != BarDock.UndockedH && DockSide != BarDock.UndockedV || LockedPosition,
                Scale = Scale,
                RevealAreaScale = RevealAreaScale,
                FontScale = FontScale,
                Spacing = new[] { Spacing, Spacing },
                NoBackground = NoBackground,
                ConditionSet = ConditionSet
            };

            bar.DockSide = DockSide switch
            {
                BarDock.Top => BarCfg.BarDock.Top,
                BarDock.Left => BarCfg.BarDock.Left,
                BarDock.Bottom => BarCfg.BarDock.Bottom,
                BarDock.Right => BarCfg.BarDock.Right,
                BarDock.UndockedH => BarCfg.BarDock.Top,
                BarDock.UndockedV => BarCfg.BarDock.Top,
                _ => BarCfg.BarDock.Top
            };

            bar.Alignment = DockSide switch
            {
                BarDock.UndockedH => BarCfg.BarAlign.LeftOrTop,
                BarDock.UndockedV => BarCfg.BarAlign.LeftOrTop,
                _ => (BarCfg.BarAlign)Alignment
            };

            switch (DockSide)
            {
                case BarDock.Top:
                case BarDock.Bottom:
                case BarDock.UndockedH:
                    bar.Columns = 0;
                    break;
                case BarDock.Left:
                case BarDock.Right:
                case BarDock.UndockedV:
                    bar.Columns = 1;
                    break;
            }

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
                CategoryColumns = Math.Max(CategoryColumns, 1),
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

    public class DisplayCondition
    {
        public enum ConditionType
        {
            Logic,
            ConditionFlag,
            Job,
            Role,
            Misc,
            Zone,
            ConditionSet
        }

        public ConditionType Type = ConditionType.Logic;
        public int Condition = 0;
        public dynamic Arg = 0;

        public CndCfg Upgrade()
        {
            var cndCfg = new CndCfg { Arg = Arg };

            switch (Type)
            {
                case ConditionType.ConditionFlag:
                    cndCfg.ID = new Conditions.ConditionFlagCondition().ID;
                    cndCfg.Arg = Condition;
                    break;
                case ConditionType.Job:
                    cndCfg.ID = new Conditions.JobCondition().ID;
                    cndCfg.Arg = Condition;
                    break;
                case ConditionType.Role:
                    cndCfg.ID = new Conditions.RoleCondition().ID;
                    cndCfg.Arg = Condition;
                    break;
                case ConditionType.Zone:
                    cndCfg.ID = new Conditions.ZoneCondition().ID;
                    cndCfg.Arg = Condition;
                    break;
                case ConditionType.ConditionSet:
                    cndCfg.ID = new Conditions.ConditionSetCondition().ID;
                    cndCfg.Arg = Condition;
                    break;
                case ConditionType.Misc:
                    cndCfg.ID = Condition switch
                    {
                        0 => new Conditions.LoggedInCondition().ID,
                        1 => new Conditions.CharacterCondition().ID,
                        2 => new Conditions.TargetCondition().ID,
                        3 => new Conditions.TargetCondition().ID,
                        4 => new Conditions.WeaponDrawnCondition().ID,
                        5 => new Conditions.EorzeaTimespanCondition().ID,
                        6 => new Conditions.LocalTimespanCondition().ID,
                        7 => new Conditions.HUDLayoutCondition().ID,
                        8 => new Conditions.AddonExistsCondition().ID,
                        9 => new Conditions.AddonVisibleCondition().ID,
                        10 => new Conditions.PluginCondition().ID,
                        _ => throw new ApplicationException($"Unrecognized misc condition: {Condition}")
                    };

                    if (Condition == 3)
                        cndCfg.Arg = 1;

                    break;
            }

            return cndCfg;
        }
    }

    public class DisplayConditionSet
    {
        public string Name = string.Empty;
        public readonly List<DisplayCondition> Conditions = new();

        public CndSetCfg Upgrade()
        {
            var set = new CndSetCfg { Name = Name };

            var not = false;
            var op = ConditionManager.BinaryOperator.AND;
            foreach (var condition in Conditions)
            {
                switch (condition.Type)
                {
                    case DisplayCondition.ConditionType.Logic:
                        if (condition.Condition != 2 && op != ConditionManager.BinaryOperator.AND)
                        {
                            op = ConditionManager.BinaryOperator.AND;
                            break;
                        }

                        switch (condition.Condition)
                        {
                            case 0:
                                op = ConditionManager.BinaryOperator.OR;
                                break;
                            case 1:
                                op = ConditionManager.BinaryOperator.XOR;
                                break;
                            case 2:
                                not ^= true;
                                break;
                            case 3:
                                op = ConditionManager.BinaryOperator.EQUALS;
                                break;
                        }
                        break;
                    default:
                        var cndCfg = condition.Upgrade();
                        cndCfg.Operator = op;
                        cndCfg.Negate = not;
                        op = ConditionManager.BinaryOperator.AND;
                        not = false;
                        set.Conditions.Add(cndCfg);
                        break;
                }
            }

            return set;
        }
    }

    public static class Legacy
    {
        private static readonly Dictionary<string, Action<Configuration, Importing.ExportInfo>> upgradeActions = new()
        {
            ["1.3.2.0"] = (config, import) =>
            {
                static void DeleteRecursive(Shortcut sh)
                {
                    if (sh.Type != Shortcut.ShortcutType.Category) return;

                    sh.Command = string.Empty;
                    if (sh.SubList == null) return;

                    foreach (var sh2 in sh.SubList)
                        DeleteRecursive(sh2);
                }

                if (config != null)
                {
                    foreach (var sh in config.BarConfigs.SelectMany(bar => bar.ShortcutList))
                        DeleteRecursive(sh);
                }

                if (import != null)
                {
                    if (import.b1?.ShortcutList != null)
                    {
                        foreach (var sh in import.b1.ShortcutList)
                            DeleteRecursive(sh);
                    }

                    if (import.s1 != null)
                        DeleteRecursive(import.s1);
                }
            },
            ["2.2.0.1"] = (config, import) =>
            {
                static void FixScaleRecursive(ShCfg sh)
                {
                    if (sh.Type != ShCfg.ShortcutType.Category) return;

                    sh.CategoryScale *= sh.CategoryScale;
                    if (sh.SubList == null) return;

                    foreach (var sh2 in sh.SubList)
                        FixScaleRecursive(sh2);
                }

                if (config != null)
                {
                    foreach (var sh in config.BarCfgs.SelectMany(bar => bar.ShortcutList))
                        FixScaleRecursive(sh);
                }

                if (import != null)
                {
                    if (import.b2?.ShortcutList != null)
                    {
                        foreach (var sh in import.b2?.ShortcutList)
                            FixScaleRecursive(sh);
                    }

                    if (import.s2 != null)
                        FixScaleRecursive(import.s2);
                }
            }
        };

        public static void UpdateConfig(Configuration config)
        {
            if (string.IsNullOrEmpty(config.PluginVersion)) return;

            var v = new Version(config.PluginVersion);
            foreach (var kv in upgradeActions.Where(kv => v <= new Version(kv.Key)))
                kv.Value(config, null);
        }

        public static void UpdateImport(Importing.ExportInfo import)
        {
            if (string.IsNullOrEmpty(import.v)) return;

            var v = new Version(import.v);
            foreach (var kv in upgradeActions.Where(kv => v <= new Version(kv.Key)))
                kv.Value(null, import);
        }
    }
}
