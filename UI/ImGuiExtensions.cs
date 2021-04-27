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

        public static Vector2 RotateVector(Vector2 v, float a)
        {
            var aCos = (float)Math.Cos(a);
            var aSin = (float)Math.Sin(a);
            return RotateVector(v, aCos, aSin);
        }

        public static Vector2 RotateVector(Vector2 v, float aCos, float aSin) => new Vector2(v.X * aCos - v.Y * aSin, v.X * aSin + v.Y * aCos);

        public static void AddIcon(this ImDrawListPtr drawList, ImGuiScene.TextureWrap tex, Vector2 pos, Vector2 size, Vector2 uv1, Vector2 uv3, double rotation, bool flipped, uint color, bool hovered, bool active, bool frame)
        {
            if (tex != null)
            {
                var rCos = (float)Math.Cos(rotation);
                var rSin = (float)Math.Sin(rotation);
                var halfSize = size / 2;
                var center = pos + halfSize;
                var p1 = center + RotateVector(-halfSize, rCos, rSin);
                var p2 = center + RotateVector(new Vector2(halfSize.X, -halfSize.Y), rCos, rSin);
                var p3 = center + RotateVector(halfSize, rCos, rSin);
                var p4 = center + RotateVector(new Vector2(-halfSize.X, halfSize.Y), rCos, rSin);
                var max = pos + size;
                var uv2 = new Vector2(uv3.X, uv1.Y);
                var uv4 = new Vector2(uv1.X, uv3.Y);

                if (hovered && !frame)
                    drawList.AddRectFilled(pos, max, active ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.ButtonHovered));

                drawList.PushClipRect(pos, max, true);
                if (!flipped)
                    drawList.AddImageQuad(tex.ImGuiHandle, p1, p2, p3, p4, uv1, uv2, uv3, uv4, color);
                else
                    drawList.AddImageQuad(tex.ImGuiHandle, p2, p1, p4, p3, uv1, uv2, uv3, uv4, color);
                drawList.PopClipRect();

                if (frame)
                    drawList.AddIconFrame(pos, size, hovered);
            }
        }

        public static Vector2 iconFrameUV0 = new Vector2(1f / 426f, 141f / 426f);
        public static Vector2 iconFrameUV1 = new Vector2(47f / 426f, 187f / 426f);
        public static Vector2 iconHoverUV0 = new Vector2(49f / 426f, 238f / 426f);
        public static Vector2 iconHoverUV1 = new Vector2(95f / 426f, 284f / 426f);
        //public static Vector2 iconHoverFrameUV0 = new Vector2(248f / 426f, 149f / 426f);
        //public static Vector2 iconHoverFrameUV1 = new Vector2(304f / 426f, 205f / 426f);

        public static void AddIconFrame(this ImDrawListPtr drawList, Vector2 pos, Vector2 size, bool hovered)
        {
            var frameSheet = QoLBar.TextureDictionary[TextureDictionary.FrameIconID];
            if (frameSheet != null && frameSheet.ImGuiHandle != IntPtr.Zero)
            {
                var _sizeInc = size * 0.075f;
                var _rMin = pos - _sizeInc;
                var _rMax = pos + size + _sizeInc;
                drawList.PushClipRectFullScreen();
                drawList.AddImage(frameSheet.ImGuiHandle, _rMin, _rMax, iconFrameUV0, iconFrameUV1); // Frame
                if (hovered)
                    drawList.AddImage(frameSheet.ImGuiHandle, _rMin, _rMax, iconHoverUV0, iconHoverUV1, 0x85FFFFFF); // Frame Center Glow
                //drawList.AddImage(_buttonshine.ImGuiHandle, _rMin - (_sizeInc * 1.5f), _rMax + (_sizeInc * 1.5f), iconHoverFrameUV0, iconHoverFrameUV1); // Edge glow // TODO: Probably somewhat impossible as is, but fix glow being clipped
                // TODO: Find a way to do the click animation
                drawList.PopClipRect();
            }
        }

        private static void DrawIcon(ImGuiScene.TextureWrap icon, Vector2 size, float zoom, Vector2 offset, double rotation, bool flipped, uint color, bool hovered, bool active, bool frame)
        {
            var z = 0.5f / zoom;
            var uv0 = new Vector2(0.5f - z + offset.X, 0.5f - z + offset.Y);
            var uv1 = new Vector2(0.5f + z + offset.X, 0.5f + z + offset.Y);
            ImGui.GetWindowDrawList().AddIcon(icon, ImGui.GetItemRectMin(), size, uv0, uv1, rotation, flipped, color, hovered, active, frame);
        }

        public static void Icon(ImGuiScene.TextureWrap icon, Vector2 size, float zoom, Vector2 offset, double rotation, bool flipped, uint color, bool frame)
        {
            ImGui.Dummy(size);
            DrawIcon(icon, size, zoom, offset, rotation, flipped, color, false, false, frame);
        }

        public static bool IconButton(ImGuiScene.TextureWrap icon, Vector2 size, float zoom, Vector2 offset, double rotation, bool flipped, uint color, bool frame)
        {
            var ret = ImGui.InvisibleButton("IconInvisibleButton", size);
            DrawIcon(icon, size, zoom, offset, rotation, flipped, color, ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly), ImGui.IsItemActive(), frame);
            return ret;
        }
    }

    // Modified version of https://gist.github.com/thennequin/64b4b996ec990c6ddc13a48c6a0ba68c
    public static class ImGuiPie
    {
        public class PieMenuContext
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
                    public bool m_bActivated;
                }

                public PieMenu()
                {
                    for (int i = 0; i < m_oPieItems.Length; i++)
                        m_oPieItems[i] = new PieItem();
                }

                public PieItem[] m_oPieItems = new PieItem[c_iMaxPieItemCount];
                public int m_iCurrentIndex;
                public float m_fMaxItemSqrDiameter;
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
            public PieMenu.PieItem m_oCurrentItem;
            public int m_iCurrentIndex;
            public int m_iMaxIndex;
            public int m_iLastFrame;
            public int m_iLastHoveredIndex;
            public Vector2 m_oCenter;
            public bool m_bAppearing;
            public bool m_bAdjustPosition;
            public float m_fRadiusOverride;
            public float m_fScale;
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
                        s_oPieMenuContext.m_oCenter = ImGui.GetMousePos();
                        s_oPieMenuContext.m_bAppearing = true;
                    }
                    else
                    {
                        s_oPieMenuContext.m_bAppearing = false;
                    }
                    s_oPieMenuContext.m_iLastFrame = iCurrentFrame;

                    s_oPieMenuContext.m_bAdjustPosition = true;

                    s_oPieMenuContext.m_iMaxIndex = -1;
                    s_oPieMenuContext.m_fRadiusOverride = 0.0f;
                    s_oPieMenuContext.m_fScale = 1.0f;
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

            Vector2 oMousePos = ImGui.GetMousePos();
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

                float fMinRadius = fCurrentRadius;

                float fMaxRadius;
                if (s_oPieMenuContext.m_fRadiusOverride > 0)
                {
                    fMaxRadius = fCurrentRadius + (s_oPieMenuContext.m_fRadiusOverride * s_oPieMenuContext.m_fScale);
                }
                else
                {
                    float fMenuHeight = (float)Math.Sqrt(oPieMenu.m_fMaxItemSqrDiameter);
                    fMaxRadius = fMinRadius + (fMenuHeight * oPieMenu.m_iCurrentIndex) / (2.0f);
                }

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
                    if (s_oPieMenuContext.m_bAdjustPosition)
                    {
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
                    }

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

            if (s_oPieMenuContext.m_bAdjustPosition)
            {
                if (oArea.Min.X < 0.0f)
                {
                    s_oPieMenuContext.m_oCenter.X -= oArea.Min.X;
                }
                if (oArea.Min.Y < 0.0f)
                {
                    s_oPieMenuContext.m_oCenter.Y -= oArea.Min.Y;
                }

                Vector2 oDisplaySize = ImGui.GetMainViewport().Size;
                if (oArea.Max.X > oDisplaySize.X)
                {
                    s_oPieMenuContext.m_oCenter.X = (s_oPieMenuContext.m_oCenter.X - oArea.Max.X) + oDisplaySize.X;
                }
                if (oArea.Max.Y > oDisplaySize.Y)
                {
                    s_oPieMenuContext.m_oCenter.Y = (s_oPieMenuContext.m_oCenter.Y - oArea.Max.Y) + oDisplaySize.Y;
                }
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

        public static bool BeginPieMenu(string pName)
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0 && s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieItemCount);

            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[oPieMenu.m_iCurrentIndex];

            Vector2 oTextSize = ImGui.CalcTextSize(pName);
            oPieItem.m_oItemSize = oTextSize;

            if (s_oPieMenuContext.m_fRadiusOverride <= 0)
            {
                float fSqrDiameter = oTextSize.X * oTextSize.X / 2 * s_oPieMenuContext.m_fScale;

                if (fSqrDiameter > oPieMenu.m_fMaxItemSqrDiameter)
                {
                    oPieMenu.m_fMaxItemSqrDiameter = fSqrDiameter;
                }
            }

            oPieItem.m_oItemIsSubMenu = true;

            oPieItem.m_oItemName = pName;

            oPieItem.m_aDrawOverride = null;

            oPieItem.m_iButtonColor = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            oPieItem.m_iButtonHoveredColor = ImGui.GetColorU32(ImGuiCol.Button);
            oPieItem.m_iTextColor = ImGui.GetColorU32(ImGuiCol.Text);

            s_oPieMenuContext.m_oCurrentItem = oPieItem;

            oPieItem.m_bActivated = oPieMenu.m_iCurrentIndex == oPieMenu.m_iHoveredItem;

            if (oPieMenu.m_iCurrentIndex == oPieMenu.m_iLastHoveredItem)
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

        public static bool PieMenuItem(string pName)
        {
            //IM_ASSERT(s_oPieMenuContext.m_iCurrentIndex >= 0 && s_oPieMenuContext.m_iCurrentIndex < PieMenuContext.c_iMaxPieItemCount);

            PieMenuContext.PieMenu oPieMenu = s_oPieMenuContext.m_oPieMenuStack[s_oPieMenuContext.m_iCurrentIndex];
            PieMenuContext.PieMenu.PieItem oPieItem = oPieMenu.m_oPieItems[oPieMenu.m_iCurrentIndex];

            Vector2 oTextSize = ImGui.CalcTextSize(pName);
            oPieItem.m_oItemSize = oTextSize;

            if (s_oPieMenuContext.m_fRadiusOverride <= 0)
            {
                float fSqrDiameter = oTextSize.X * oTextSize.X / 2 * s_oPieMenuContext.m_fScale;

                if (fSqrDiameter > oPieMenu.m_fMaxItemSqrDiameter)
                {
                    oPieMenu.m_fMaxItemSqrDiameter = fSqrDiameter;
                }
            }

            oPieItem.m_oItemIsSubMenu = false;

            oPieItem.m_oItemName = pName;

            oPieItem.m_aDrawOverride = null;

            oPieItem.m_iButtonColor = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            oPieItem.m_iButtonHoveredColor = ImGui.GetColorU32(ImGuiCol.Button);
            oPieItem.m_iTextColor = ImGui.GetColorU32(ImGuiCol.Text);

            s_oPieMenuContext.m_oCurrentItem = oPieItem;

            oPieItem.m_bActivated = oPieMenu.m_iCurrentIndex == oPieMenu.m_iHoveredItem;
            ++oPieMenu.m_iCurrentIndex;

            if (oPieItem.m_bActivated)
                s_oPieMenuContext.m_bClose = true;
            return oPieItem.m_bActivated;
        }

        public static void PieDrawOverride(Action<Vector2, bool> a) => s_oPieMenuContext.m_oCurrentItem.m_aDrawOverride = a;

        public static void SetPieCenter(Vector2 center) => s_oPieMenuContext.m_oCenter = center;

        public static void SetPieRadius(float size) => s_oPieMenuContext.m_fRadiusOverride = size;

        public static void SetPieScale(float scale) => s_oPieMenuContext.m_fScale = scale;

        public static void DisableRepositioning() => s_oPieMenuContext.m_bAdjustPosition = false;

        public static bool IsPieAppearing() => s_oPieMenuContext.m_bAppearing;

        public static bool IsItemActivated() => s_oPieMenuContext.m_oCurrentItem.m_bActivated;
    }
}
