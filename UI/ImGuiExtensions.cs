using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using Dalamud.Interface;

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

        public static void ClampWindowPosToViewport()
        {
            var viewport = ImGui.GetWindowViewport();
            if (viewport.ID == ImGuiHelpers.MainViewport.ID)
            {
                var pos = viewport.Pos;
                ClampWindowPos(pos, pos + viewport.Size);
            }
        }

        public static void ClampWindowPos(Vector2 max) => ClampWindowPos(Vector2.Zero, max);

        public static void ClampWindowPos(Vector2 min, Vector2 max)
        {
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var x = Math.Min(Math.Max(pos.X, min.X), max.X - size.X);
            var y = Math.Min(Math.Max(pos.Y, min.Y), max.Y - size.Y);
            ImGui.SetWindowPos(new Vector2(x, y));
        }

        public static bool IsWindowInMainViewport() => ImGui.GetWindowViewport().ID == ImGuiHelpers.MainViewport.ID;

        public static bool ShouldDrawInViewport() => IsWindowInMainViewport() || QoLBar.IsGameFocused;

        public static void ShouldDrawInViewport(out bool b) => b = ShouldDrawInViewport();

        // Helper function for displaying / hiding windows outside of the main viewport when the game isn't focused, returns the bool to allow using it in if statements to reduce code
        public static bool SetBoolOnGameFocus(ref bool b)
        {
            if (!b)
                b = QoLBar.IsGameFocused;
            return b;
        }

        // Doesn't really work
        /*public static bool IsWindowDragging() => ImGui.IsWindowFocused() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0);

        private static bool _beganDrag = false;
        public static bool OnStartWindowDrag()
        {
            if (!_beganDrag)
                return _beganDrag = IsWindowDragging();
            else
                return false;
        }

        public static bool OnStopWindowDrag()
        {
            if (_beganDrag)
                return !(_beganDrag = !ImGui.IsMouseReleased(ImGuiMouseButton.Left));
            else
                return false;
        }*/
    }

    // Modified version of https://gist.github.com/thennequin/64b4b996ec990c6ddc13a48c6a0ba68c
    public static class ImGuiPie
    {
        private class PieMenuContext
        {
            public const int c_iMaxPieMenuStack = 8;
            public const int c_iMaxPieItemCount = 12;
            public const int c_iRadiusEmpty = 30;
            public const int c_iRadiusMin = 30;
            public const int c_iMinItemCount = 3;
            public const int c_iMinItemCountPerLevel = 3;

            public class PieMenu
            {
                public class PieItem
                {
                    public bool m_oItemIsSubMenu;
                    public string m_oItemName;
                    public Vector2 m_oItemSize;
                    public Action<Vector2, bool> m_aDrawOverride;
                    public uint m_iButtonColor;
                    public uint m_iButtonHoveredColor;
                    public uint m_iTextColor;
                }

                public PieMenu()
                {
                    for (int i = 0; i < m_oPieItems.Length; i++)
                        m_oPieItems[i] = new PieItem();
                }

                public PieItem[] m_oPieItems = new PieItem[c_iMaxPieItemCount];
                public int m_iCurrentIndex;
                public float m_fMaxItemSqrDiameter;
                public float m_fLastMaxItemSqrDiameter;
                public int m_iHoveredItem;
                public int m_iLastHoveredItem;
            };

            public PieMenuContext()
            {
                m_iCurrentIndex = -1;
                m_iLastFrame = 0;
                for (int i = 0; i < m_oPieMenuStack.Length; i++)
                    m_oPieMenuStack[i] = new PieMenu();
            }

            public PieMenu[] m_oPieMenuStack = new PieMenu[c_iMaxPieMenuStack];
            public int m_iCurrentIndex;
            public int m_iMaxIndex;
            public int m_iLastFrame;
            public int m_iLastHoveredIndex;
            public Vector2 m_oCenter;
            public bool m_bMaintainDraw;
            public bool m_bClose;
        };

        private static readonly PieMenuContext s_oPieMenuContext = new PieMenuContext();

        private static void BeginPieMenuEx()
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieMenuStack);

            ++s_oPieMenuContext.m_iCurrentIndex;
            ++s_oPieMenuContext.m_iMaxIndex;

            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            oPieMenu.m_iCurrentIndex = 0;
            oPieMenu.m_fMaxItemSqrDiameter = 0.0f;
            if (s_oPieMenuContext.m_bMaintainDraw)
                oPieMenu.m_iHoveredItem = -1;
            if (s_oPieMenuContext.m_iCurrentIndex > 0)
                oPieMenu.m_fMaxItemSqrDiameter = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex - 1].m_fMaxItemSqrDiameter;
        }

        private static void EndPieMenuEx()
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0);
            --s_oPieMenuContext.m_iCurrentIndex;
        }

        public static bool BeginPiePopup(string pName, bool bMaintain)
        {
            if (ImGui.IsPopupOpen(pName))
            {
                ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1.0f);

                s_oPieMenuContext.m_bMaintainDraw = bMaintain;
                s_oPieMenuContext.m_bClose = false;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGui.SetNextWindowPos(new Vector2(-100), ImGuiCond.Always);
                bool bOpened = ImGui.BeginPopup(pName);
                if (bOpened)
                {
                    int iCurrentFrame = ImGui.GetFrameCount();
                    if (s_oPieMenuContext.m_iLastFrame < (iCurrentFrame - 1))
                    {
                        s_oPieMenuContext.m_oCenter = ImGui.GetIO().MousePos;
                    }
                    s_oPieMenuContext.m_iLastFrame = iCurrentFrame;

                    s_oPieMenuContext.m_iMaxIndex = -1;
                    BeginPieMenuEx();

                    return true;
                }
                else
                {
                    ImGui.End();
                    ImGui.PopStyleColor(2);
                    ImGui.PopStyleVar(2);
                }
            }
            return false;
        }

        public static void EndPiePopup()
        {
            EndPieMenuEx();

            ImGuiStylePtr oStyle = ImGui.GetStyle();

            ImDrawListPtr pDrawList = ImGui.GetWindowDrawList();
            pDrawList.PushClipRectFullScreen();

            Vector2 oMousePos = ImGui.GetIO().MousePos;
            Vector2 oDragDelta = new Vector2(oMousePos.X - s_oPieMenuContext.m_oCenter.X, oMousePos.Y - s_oPieMenuContext.m_oCenter.Y);
            float fDragDistSqr = oDragDelta.X * oDragDelta.X + oDragDelta.Y * oDragDelta.Y;

            float fCurrentRadius = PieMenuContext.c_iRadiusEmpty;

            (Vector2 Min, Vector2 Max) oArea = (s_oPieMenuContext.m_oCenter, s_oPieMenuContext.m_oCenter);

            bool bItemHovered = false;

            float c_fDefaultRotate = (float)(-Math.PI / 2.0f);
            float fLastRotate = c_fDefaultRotate;
            for (int iIndex = 0; iIndex <= s_oPieMenuContext.m_iMaxIndex; ++iIndex)
            {
                PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[iIndex];

                float fMenuHeight = (float)Math.Sqrt(oPieMenu.m_fMaxItemSqrDiameter);

                float fMinRadius = fCurrentRadius;
                float fMaxRadius = fMinRadius + (fMenuHeight * oPieMenu.m_iCurrentIndex) / (2.0f);

                float item_arc_span = (float)(2 * Math.PI / Math.Max(PieMenuContext.c_iMinItemCount + PieMenuContext.c_iMinItemCountPerLevel * iIndex, oPieMenu.m_iCurrentIndex));
                float drag_angle = (float)Math.Atan2(oDragDelta.Y, oDragDelta.X);

                float fRotate = fLastRotate - item_arc_span * (oPieMenu.m_iCurrentIndex - 1.0f) / 2.0f;
                int item_hovered = -1;
                for (int item_n = 0; item_n < oPieMenu.m_iCurrentIndex; item_n++)
                {
                    PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[item_n];

                    string item_label = oPieItem.m_oItemName;
                    float fMinInnerSpacing = oStyle.ItemInnerSpacing.X / (fMinRadius * 2.0f);
                    float fMaxInnerSpacing = oStyle.ItemInnerSpacing.X / (fMaxRadius * 2.0f);
                    float fAddMinDrawAngle = item_arc_span * fMinInnerSpacing;
                    float fAddMaxDrawAngle = item_arc_span * fMaxInnerSpacing;
                    float item_angle_min = item_arc_span * (item_n - 0.5f) + fRotate;
                    float item_angle_max = item_arc_span * (item_n + 0.5f) + fRotate;
                    float item_inner_ang_min = item_angle_min + fAddMinDrawAngle;
                    float item_inner_ang_max = item_angle_max - fAddMinDrawAngle;
                    float item_outer_ang_min = item_angle_min + fAddMaxDrawAngle;
                    float item_outer_ang_max = item_angle_max - fAddMaxDrawAngle;

                    bool hovered = false;
                    if (fDragDistSqr >= fMinRadius * fMinRadius && ((s_oPieMenuContext.m_iLastHoveredIndex == iIndex) || fDragDistSqr < fMaxRadius * fMaxRadius))
                    {
                        while ((drag_angle - item_angle_min) < 0.0f)
                            drag_angle += (float)(2.0f * Math.PI);
                        while ((drag_angle - item_angle_min) > 2.0f * Math.PI)
                            drag_angle -= (float)(2.0f * Math.PI);

                        if (drag_angle >= item_angle_min && drag_angle < item_angle_max)
                        {
                            hovered = true;
                            bItemHovered = !oPieItem.m_oItemIsSubMenu;
                        }
                    }

                    int arc_segments = (int)(64 * item_arc_span / (2 * Math.PI)) + 1;

                    uint iColor = hovered ? oPieItem.m_iButtonColor : oPieItem.m_iButtonHoveredColor;
                    float fAngleStepInner = (item_inner_ang_max - item_inner_ang_min) / arc_segments;
                    float fAngleStepOuter = (item_outer_ang_max - item_outer_ang_min) / arc_segments;
                    pDrawList.PrimReserve(arc_segments * 6, (arc_segments + 1) * 2);
                    for (int iSeg = 0; iSeg <= arc_segments; ++iSeg)
                    {
                        float fCosInner = (float)Math.Cos(item_inner_ang_min + fAngleStepInner * iSeg);
                        float fSinInner = (float)Math.Sin(item_inner_ang_min + fAngleStepInner * iSeg);
                        float fCosOuter = (float)Math.Cos(item_outer_ang_min + fAngleStepOuter * iSeg);
                        float fSinOuter = (float)Math.Sin(item_outer_ang_min + fAngleStepOuter * iSeg);

                        if (iSeg < arc_segments)
                        {
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 0));
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 2));
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 1));
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 3));
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 2));
                            pDrawList.PrimWriteIdx((ushort)(pDrawList._VtxCurrentIdx + 1));
                        }
                        pDrawList.PrimWriteVtx(new Vector2(s_oPieMenuContext.m_oCenter.X + fCosInner * (fMinRadius + oStyle.ItemInnerSpacing.X), s_oPieMenuContext.m_oCenter.Y + fSinInner * (fMinRadius + oStyle.ItemInnerSpacing.X)), ImGui.GetFontTexUvWhitePixel(), iColor);
                        pDrawList.PrimWriteVtx(new Vector2(s_oPieMenuContext.m_oCenter.X + fCosOuter * (fMaxRadius - oStyle.ItemInnerSpacing.X), s_oPieMenuContext.m_oCenter.Y + fSinOuter * (fMaxRadius - oStyle.ItemInnerSpacing.X)), ImGui.GetFontTexUvWhitePixel(), iColor);
                    }

                    float fRadCenter = (item_arc_span * item_n) + fRotate;
                    Vector2 oOuterCenter = new Vector2((float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fRadCenter) * fMaxRadius), (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fRadCenter) * fMaxRadius));

                    // idk lol
                    static (Vector2, Vector2) ImRect_Add((Vector2 Min, Vector2 Max) rect, Vector2 p)
                    {
                        if (rect.Min.X > p.X)
                            rect.Min.X = p.X;
                        if (rect.Min.Y > p.Y)
                            rect.Min.Y = p.Y;
                        if (rect.Max.X < p.X)
                            rect.Max.X = p.X;
                        if (rect.Max.Y < p.Y)
                            rect.Max.Y = p.Y;
                        return rect;
                    }
                    oArea = ImRect_Add(oArea, oOuterCenter);

                    uint iTextColor = oPieItem.m_iTextColor;

                    if (oPieItem.m_oItemIsSubMenu)
                    {
                        Vector2[] oTrianglePos = new Vector2[3];

                        float fRadLeft = fRadCenter - 5.0f / fMaxRadius;
                        float fRadRight = fRadCenter + 5.0f / fMaxRadius;

                        oTrianglePos[0].X = (float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fRadCenter) * (fMaxRadius - 5.0f));
                        oTrianglePos[0].Y = (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fRadCenter) * (fMaxRadius - 5.0f));
                        oTrianglePos[1].X = (float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fRadLeft) * (fMaxRadius - 10.0f));
                        oTrianglePos[1].Y = (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fRadLeft) * (fMaxRadius - 10.0f));
                        oTrianglePos[2].X = (float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fRadRight) * (fMaxRadius - 10.0f));
                        oTrianglePos[2].Y = (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fRadRight) * (fMaxRadius - 10.0f));

                        pDrawList.AddTriangleFilled(oTrianglePos[0], oTrianglePos[1], oTrianglePos[2], iTextColor);
                    }

                    float fAngleOffsetTheta = (item_inner_ang_min + item_inner_ang_max) * 0.5f;
                    float fRadiusOffset = (fMinRadius + fMaxRadius) * 0.5f;
                    if (oPieItem.m_aDrawOverride != null)
                    {
                        oPieItem.m_aDrawOverride(
                            new Vector2(
                                (float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fAngleOffsetTheta) * fRadiusOffset),
                                (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fAngleOffsetTheta) * fRadiusOffset)),
                            hovered);
                    }
                    else
                    {
                        Vector2 text_size = oPieItem.m_oItemSize;
                        Vector2 text_pos = new Vector2(
                            (float)(s_oPieMenuContext.m_oCenter.X + Math.Cos(fAngleOffsetTheta) * fRadiusOffset - text_size.X * 0.5f),
                            (float)(s_oPieMenuContext.m_oCenter.Y + Math.Sin(fAngleOffsetTheta) * fRadiusOffset - text_size.Y * 0.5f));
                        pDrawList.AddText(text_pos, iTextColor, item_label);
                    }

                    if (hovered)
                        item_hovered = item_n;
                }

                fCurrentRadius = fMaxRadius;

                oPieMenu.m_fLastMaxItemSqrDiameter = oPieMenu.m_fMaxItemSqrDiameter;

                oPieMenu.m_iHoveredItem = item_hovered;

                if (s_oPieMenuContext.m_iLastHoveredIndex != iIndex && fDragDistSqr >= fMaxRadius * fMaxRadius)
                    item_hovered = oPieMenu.m_iLastHoveredItem;

                oPieMenu.m_iLastHoveredItem = item_hovered;

                fLastRotate = item_arc_span * oPieMenu.m_iLastHoveredItem + fRotate;
                if (item_hovered == -1 || !oPieMenu.m_oPieItems[item_hovered].m_oItemIsSubMenu)
                {
                    s_oPieMenuContext.m_iLastHoveredIndex = iIndex;
                    break;
                }
            }

            pDrawList.PopClipRect();

            if (oArea.Min.X < 0.0f)
            {
                s_oPieMenuContext.m_oCenter.X -= oArea.Min.X;
            }
            if (oArea.Min.Y < 0.0f)
            {
                s_oPieMenuContext.m_oCenter.Y -= oArea.Min.Y;
            }

            Vector2 oDisplaySize = ImGui.GetIO().DisplaySize;
            if (oArea.Max.X > oDisplaySize.X)
            {
                s_oPieMenuContext.m_oCenter.X = (s_oPieMenuContext.m_oCenter.X - oArea.Max.X) + oDisplaySize.X;
            }
            if (oArea.Max.Y > oDisplaySize.Y)
            {
                s_oPieMenuContext.m_oCenter.Y = (s_oPieMenuContext.m_oCenter.Y - oArea.Max.Y) + oDisplaySize.Y;
            }

            if (s_oPieMenuContext.m_bClose ||
                (!bItemHovered && !s_oPieMenuContext.m_bMaintainDraw))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        public static bool BeginPieMenu(string pName, bool bEnabled = true)
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0 && s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieItemCount);

            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[oPieMenu.m_iCurrentIndex];

            Vector2 oTextSize = ImGui.CalcTextSize(pName, true);
            oPieItem.m_oItemSize = oTextSize;

            float fSqrDiameter = oTextSize.X * oTextSize.X + oTextSize.Y * oTextSize.Y;

            if (fSqrDiameter > oPieMenu.m_fMaxItemSqrDiameter)
            {
                oPieMenu.m_fMaxItemSqrDiameter = fSqrDiameter;
            }

            oPieItem.m_oItemIsSubMenu = true;

            oPieItem.m_oItemName = pName;

            oPieItem.m_aDrawOverride = null;

            oPieItem.m_iButtonColor = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            oPieItem.m_iButtonHoveredColor = ImGui.GetColorU32(ImGuiCol.Button);
            oPieItem.m_iTextColor = ImGui.GetColorU32(ImGuiCol.Text);

            if (oPieMenu.m_iLastHoveredItem == oPieMenu.m_iCurrentIndex)
            {
                ++oPieMenu.m_iCurrentIndex;

                BeginPieMenuEx();
                return true;
            }
            ++oPieMenu.m_iCurrentIndex;

            return false;
        }

        public static void EndPieMenu()
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0 && s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieItemCount);
            --s_oPieMenuContext.m_iCurrentIndex;
        }

        public static bool PieMenuItem(string pName, bool bEnabled = true)
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0 && s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieItemCount);

            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[oPieMenu.m_iCurrentIndex];

            Vector2 oTextSize = ImGui.CalcTextSize(pName, true);
            oPieItem.m_oItemSize = oTextSize;

            float fSqrDiameter = oTextSize.X * oTextSize.X + oTextSize.Y * oTextSize.Y;

            if (fSqrDiameter > oPieMenu.m_fMaxItemSqrDiameter)
            {
                oPieMenu.m_fMaxItemSqrDiameter = fSqrDiameter;
            }

            oPieItem.m_oItemIsSubMenu = false;

            oPieItem.m_oItemName = pName;

            oPieItem.m_aDrawOverride = null;

            oPieItem.m_iButtonColor = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            oPieItem.m_iButtonHoveredColor = ImGui.GetColorU32(ImGuiCol.Button);
            oPieItem.m_iTextColor = ImGui.GetColorU32(ImGuiCol.Text);

            bool bActive = oPieMenu.m_iCurrentIndex == oPieMenu.m_iHoveredItem;
            ++oPieMenu.m_iCurrentIndex;

            if (bActive)
                s_oPieMenuContext.m_bClose = true;
            return bActive;
        }

        public static void PieDrawOverride(Action<Vector2, bool> a)
        {
            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[oPieMenu.m_iCurrentIndex];

            oPieItem.m_aDrawOverride = a;
        }
    }
}
