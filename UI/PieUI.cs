using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface;
using static QoLBar.ShCfg;

namespace QoLBar
{
    public static class PieUI
    {
        // No reason anyone should want or need more than this, and it starts to look horrible
        public const int maxLevels = 3;
        public const int maxItems = 6;

        public static bool enabled = false;
        public static void Draw()
        {
            enabled = true;

            ImGui.PushStyleColor(ImGuiCol.Button, 0x70000000);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xC0404040);

            foreach (var bar in QoLBar.Plugin.ui.bars)
            {
                if (bar.Config.Hotkey > 0 && bar.CheckConditionSet())
                {
                    ImGui.PushID(bar.ID);

                    if (bar.openPie)
                        ImGui.OpenPopup("PieBar");

                    if (ImGuiPie.BeginPiePopup("PieBar", bar.openPie))
                    {
                        ImGuiPie.SetPieRadius(50);
                        ImGuiPie.SetPieScale(ImGuiHelpers.GlobalScale * bar.Config.Scale);
                        ImGuiPie.DisableRepositioning();
                        DrawChildren(bar.children);
                        ImGuiPie.EndPiePopup();
                    }

                    ImGui.PopID();
                }
            }

            ImGui.PopStyleColor(2);
        }

        static int totalLevels = 0;
        public static void DrawChildren(List<ShortcutUI> children)
        {
            ++totalLevels;
            var totalItems = 0;
            foreach (var sh in children)
            {
                ImGui.PushID(sh.ID);

                if (sh.Config.Type == ShortcutType.Category && totalLevels < maxLevels)
                {
                    if (ImGuiPie.BeginPieMenu($"{sh.Config.Name}"))
                    {
                        //ImGuiPie.PieDrawOverride(DrawShortcut(sh.Config));

                        // TODO: category commands

                        DrawChildren(sh.children);

                        ImGuiPie.EndPieMenu();
                    }

                    totalItems++;
                }
                else if (sh.Config.Type != ShortcutType.Spacer)
                {
                    if (ImGuiPie.PieMenuItem($"{sh.Config.Name}"))
                        sh.OnClick(false, false);
                    //ImGuiPie.PieDrawOverride(DrawShortcut(sh.Config));

                    totalItems++;
                }

                ImGui.PopID();

                if (totalItems >= maxItems) break;
            }
            --totalLevels;
        }

        public static Action<Vector2, bool> DrawShortcut(ShCfg sh)
        {
            return (Vector2 center, bool hovered) =>
            {

            };
        }
    }
}