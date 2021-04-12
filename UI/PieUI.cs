using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using static QoLBar.ShCfg;

namespace QoLBar
{
    public static class PieUI
    {
        public static bool enabled = false;
        public static void Draw()
        {
            enabled = true;

            foreach (var bar in QoLBar.Plugin.ui.bars)
            {
                if (bar.Config.Hotkey > 0 && bar.CheckConditionSet())
                {
                    ImGui.PushID(bar.ID);

                    if (bar.openPie)
                        ImGui.OpenPopup("PieBar");

                    if (ImGuiPie.BeginPiePopup("PieBar", bar.openPie))
                    {
                        DrawChildren(bar.children);
                        ImGuiPie.EndPiePopup();
                    }

                    ImGui.PopID();
                }
            }
        }

        static int totalLevels = 1;
        public static void DrawChildren(List<ShortcutUI> children)
        {
            if (totalLevels >= ImGuiPie.PieMenuContext.c_iMaxPieMenuStack) return;

            ++totalLevels;
            var totalItems = 0;
            foreach (var sh in children)
            {
                ImGui.PushID(sh.ID);

                if (sh.Config.Type == ShortcutType.Category)
                {
                    if (ImGuiPie.BeginPieMenu($"{sh.Config.Name}"))
                    {
                        //ImGuiPie.PieDrawOverride(DrawShortcut(sh.Config));

                        // TODO: category commands

                        DrawChildren(sh.children);

                        ImGuiPie.EndPieMenu();

                        totalItems++;
                    }
                }
                else if (sh.Config.Type == ShortcutType.Command)
                {
                    if (ImGuiPie.PieMenuItem($"{sh.Config.Name}"))
                        sh.OnClick(false, false);
                    //ImGuiPie.PieDrawOverride(DrawShortcut(sh.Config));

                    totalItems++;
                }

                ImGui.PopID();

                if (totalItems >= ImGuiPie.PieMenuContext.c_iMaxPieItemCount) { --totalLevels; return; }
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