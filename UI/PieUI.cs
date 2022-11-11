using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface;
using static QoLBar.ShCfg;

namespace QoLBar;

public static class PieUI
{
    // No reason anyone should want or need more than this, and it starts to look horrible
    public const int maxLevels = 3;
    public const int maxItems = 12;

    public static bool enabled = false;
    private static Vector2 _prevMousePos;
    public static void Draw()
    {
        enabled = true;

        foreach (var bar in QoLBar.Plugin.ui.bars)
        {
            if ((bar.Config.Hotkey > 0 || !GamepadBind.IsNullOrUnset(bar.Config.HotPad)) && bar.CheckConditionSet())
            {
                ImGui.PushID(bar.ID);

                if (bar.openPie)
                    ImGui.OpenPopup("PieBar");

                if (ImGuiPie.BeginPiePopup("PieBar", bar.openPie))
                {
                    if (ImGuiPie.IsPieAppearing() && QoLBar.Config.PiesAlwaysCenter)
                    {
                        ImGuiPie.SetPieCenter(ImGuiHelpers.MainViewport.GetCenter());

                        if (QoLBar.Config.PiesMoveMouse)
                        {
                            var io = ImGui.GetIO();
                            _prevMousePos = io.MousePos;

                            io.WantSetMousePos = true;
                            io.MousePos = ImGuiHelpers.MainViewport.GetCenter();
                        }
                    }

                    if (!bar.Config.NoBackground)
                    {
                        var opacity = (uint)Math.Min(Math.Max(QoLBar.Config.PieOpacity, 0), 255);
                        var buttonCol = opacity << 24;
                        var buttonHoverCol = (Math.Min((uint)(opacity * 1.7f), 255) << 24) + 0x404040;
                        ImGui.PushStyleColor(ImGuiCol.Button, buttonCol);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonHoverCol);
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, 0);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                    }

                    ImGuiPie.SetPieRadius(50);
                    ImGuiPie.SetPieScale(ImGuiHelpers.GlobalScale * bar.Config.Scale);
                    if (!QoLBar.Config.PieAlternateAngle)
                        ImGuiPie.SetPieRotationOffset((float)(Math.PI - Math.PI / bar.children.Count));
                    ImGuiPie.DisableRepositioning();

                    DrawChildren(bar.children);

                    ImGuiPie.EndPiePopup();

                    ImGui.PopStyleColor(2);

                    if (!bar.openPie && QoLBar.Config.PiesReturnMouse)
                    {
                        var io = ImGui.GetIO();
                        io.WantSetMousePos = true;
                        io.MousePos = QoLBar.Config.PiesReadjustMouse ? (_prevMousePos + (io.MousePos - ImGuiHelpers.MainViewport.GetCenter())) : _prevMousePos;
                    }
                }

                ImGui.PopID();
            }
        }
    }

    static int totalLevels = 0;
    public static void DrawChildren(List<ShortcutUI> children)
    {
        ++totalLevels;
        var totalItems = 0;
        foreach (var sh in children)
        {
            ImGui.PushID(sh.ID);

            if (totalLevels < maxLevels && sh.Config.Type == ShortcutType.Category && sh.Config.Mode == ShortcutMode.Default)
            {
                var open = ImGuiPie.BeginPieMenu($"{sh.Config.Name}");
                ImGuiPie.PieDrawOverride(DrawShortcut(sh));
                if (open)
                {
                    if (ImGuiPie.IsItemActivated())
                        sh.OnClick(false, true, false);

                    DrawChildren(sh.children);

                    ImGuiPie.EndPieMenu();
                }

                totalItems++;
            }
            else if (sh.Config.Type != ShortcutType.Spacer)
            {
                if (ImGuiPie.PieMenuItem($"{sh.Config.Name}"))
                    sh.OnClick(false, true, false);
                ImGuiPie.PieDrawOverride(DrawShortcut(sh));

                totalItems++;
            }

            ImGui.PopID();

            if (totalItems >= maxItems) break;
        }
        --totalLevels;
    }

    // TODO: what the fuck
    public static Action<Vector2, bool> DrawShortcut(ShortcutUI ui)
    {
        var sh = ui.DisplayedUI.Config;
        var bar = ui.parentBar.Config;
        return (center, hovered) =>
        {
            var name = sh.Name;
            var useIcon = ShortcutUI.ParseName(ref name, out _, out var tooltip, out var icon, out var args);

            var c = ImGui.ColorConvertU32ToFloat4(sh.Color);
            c.W += sh.ColorAnimation / 255f; // Temporary
            if (c.W > 1)
                c = ShortcutUI.AnimateColor(c);
            var color = ImGui.ColorConvertFloat4ToU32(c);

            var drawList = ImGui.GetWindowDrawList();
            if (useIcon)
            {
                ImGui.SetWindowFontScale(bar.Scale);

                var hasArgs = !string.IsNullOrEmpty(args);

                TextureDictionary texd = null;
                if (hasArgs)
                {
                    if (args.Contains("l"))
                        texd = QoLBar.textureDictionaryLR;
                    else if (args.Contains("h"))
                        texd = QoLBar.textureDictionaryHR;
                }

                texd ??= QoLBar.TextureDictionary;

                if (hasArgs && args.Contains("g"))
                    texd = (texd == QoLBar.textureDictionaryLR) ? QoLBar.textureDictionaryGSLR : QoLBar.textureDictionaryGSHR;

                var tex = texd[icon];
                if (tex != null)
                {
                    var size = new Vector2(ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2);
                    var pos = center - size / 2;

                    var frameArg = QoLBar.Config.UseIconFrame;
                    if (hasArgs)
                    {
                        if (args.Contains("f"))
                            frameArg = true;
                        else if (args.Contains("n"))
                            frameArg = false;
                    }

                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                    drawList.AddIcon(tex, pos, new ImGuiEx.IconSettings
                    {
                        size = size,
                        zoom = sh.IconZoom,
                        offset = new Vector2(sh.IconOffset[0], sh.IconOffset[1]),
                        rotation = sh.IconRotation,
                        flipped = hasArgs && args.Contains("r"),
                        color = color,
                        hovered = hovered,
                        activeTime = 0,
                        frame = frameArg,
                    });
                    ImGui.PopStyleColor();
                }
            }
            else
            {
                ImGui.SetWindowFontScale(bar.Scale * bar.FontScale);

                var textSize = ImGui.CalcTextSize(name);
                drawList.AddText(center - (textSize / 2), color, name);
            }

            ImGui.SetWindowFontScale(1);

            if (hovered && tooltip != string.Empty)
                ImGui.SetTooltip(tooltip);
        };
    }
}