﻿using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Lumina.Excel;

namespace QoLBar;

// I can't believe C# wont let me just add this to the fucking class
public static class ImGuiEx
{
    public static void SetItemTooltip(string s, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
        if (ImGui.IsItemHovered(flags))
            ImGui.SetTooltip(s);
    }

    // Why is this not a basic feature of ImGui...
    private static readonly Stack<float> fontScaleStack = new();
    private static float curScale = 1;
    public static void PushFontScale(float scale)
    {
        fontScaleStack.Push(curScale);
        curScale = scale;
        ImGui.SetWindowFontScale(curScale);
    }

    public static void PopFontScale()
    {
        curScale = fontScaleStack.Pop();
        ImGui.SetWindowFontScale(curScale);
    }

    public static void PushFontSize(float size) => PushFontScale(size / ImGui.GetFont().FontSize);

    public static void PopFontSize() => PopFontScale();

    public static float GetFontScale() => curScale;

    public static void ClampWindowPosToViewport()
    {
        var viewport = ImGui.GetWindowViewport();
        if (ImGui.IsWindowAppearing() || viewport.ID != ImGuiHelpers.MainViewport.ID) return;

        var pos = viewport.Pos;
        ClampWindowPos(pos, pos + viewport.Size);
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

    public static bool ShouldDrawInViewport() => IsWindowInMainViewport() || Game.IsGameFocused;

    public static void ShouldDrawInViewport(out bool b) => b = ShouldDrawInViewport();

    // Helper function for displaying / hiding windows outside of the main viewport when the game isn't focused, returns the bool to allow using it in if statements to reduce code
    public static bool SetBoolOnGameFocus(ref bool b)
    {
        if (!b)
            b = Game.IsGameFocused;
        return b;
    }

    public static string TryGetClipboardText()
    {
        try { return ImGui.GetClipboardText(); }
        catch { return string.Empty; }
    }

    private static bool sliderEnabled = false;
    private static bool sliderVertical = false;
    private static float sliderInterval = 0;
    private static int lastHitInterval = 0;
    private static Action<bool, bool, bool> sliderAction;
    public static void SetupSlider(bool vertical, float interval, Action<bool, bool, bool> action)
    {
        sliderEnabled = true;
        sliderVertical = vertical;
        sliderInterval = interval;
        lastHitInterval = 0;
        sliderAction = action;
    }

    public static void DoSlider()
    {
        if (!sliderEnabled) return;

        // You can blame ImGui for this
        var popupOpen = !ImGui.IsPopupOpen("_SLIDER") && ImGui.IsPopupOpen(default, ImGuiPopupFlags.AnyPopup);
        if (!popupOpen)
        {
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(new Vector2(-100));
            ImGui.OpenPopup("_SLIDER", ImGuiPopupFlags.NoOpenOverItems);
            if (!ImGui.BeginPopup("_SLIDER")) return;
        }

        var drag = sliderVertical ? ImGui.GetMouseDragDelta().Y : ImGui.GetMouseDragDelta().X;
        var dragInterval = (int)(drag / sliderInterval);
        var hit = false;
        var increment = false;
        if (dragInterval > lastHitInterval)
        {
            hit = true;
            increment = true;
        }
        else if (dragInterval < lastHitInterval)
            hit = true;

        var closing = !ImGui.IsMouseDown(ImGuiMouseButton.Left);

        if (lastHitInterval != dragInterval)
        {
            while (lastHitInterval != dragInterval)
            {
                lastHitInterval += increment ? 1 : -1;
                sliderAction(hit, increment, closing && lastHitInterval == dragInterval);
            }
        }
        else
            sliderAction(false, false, closing);

        if (closing)
            sliderEnabled = false;

        if (!popupOpen)
            ImGui.EndPopup();
    }

    // ?????????
    public static void PushClipRectFullScreen() => ImGui.GetWindowDrawList().PushClipRectFullScreen();

    public static void TextCopyable(string text)
    {
        ImGui.TextUnformatted(text);

        if (!ImGui.IsItemHovered()) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsItemClicked())
            ImGui.SetClipboardText(text);
    }

    public static Vector2 RotateVector(Vector2 v, float a)
    {
        var aCos = (float)Math.Cos(a);
        var aSin = (float)Math.Sin(a);
        return RotateVector(v, aCos, aSin);
    }

    public static Vector2 RotateVector(Vector2 v, float aCos, float aSin) => new(v.X * aCos - v.Y * aSin, v.X * aSin + v.Y * aCos);

    public class IconSettings
    {
        [Flags]
        public enum CooldownStyle
        {
            None = 0,
            Number = 1,
            Disable = 2,
            Cooldown = 4,
            GCDCooldown = 8,
            ChargeCooldown = 16
        }

        public Vector2 size = Vector2.One;
        public float zoom = 1;
        public Vector2 offset = Vector2.Zero;
        public double rotation = 0;
        public bool flipped = false;
        public uint color = 0xFFFFFFFF;
        public bool hovered = false;
        public float activeTime = -1;
        public bool frame = false;
        public float cooldownCurrent = -1;
        public float cooldownMax = -1;
        public uint cooldownAction = 0;
        public CooldownStyle cooldownStyle = CooldownStyle.None;
    }

    public static void AddIcon(this ImDrawListPtr drawList, IDalamudTextureWrap tex, Vector2 pos, IconSettings settings)
    {
        if (tex == null) return;

        var z = 0.5f / settings.zoom;
        var uv1 = new Vector2(0.5f - z + settings.offset.X, 0.5f - z + settings.offset.Y);
        var uv3 = new Vector2(0.5f + z + settings.offset.X, 0.5f + z + settings.offset.Y);

        var p1 = pos;
        var p2 = pos + new Vector2(settings.size.X, 0);
        var p3 = pos + settings.size;
        var p4 = pos + new Vector2(0, settings.size.Y);

        var rCos = (float)Math.Cos(settings.rotation);
        var rSin = (float)-Math.Sin(settings.rotation);
        var uvHalfSize = (uv3 - uv1) / 2;
        var uvCenter = uv1 + uvHalfSize;
        uv1 = uvCenter + RotateVector(-uvHalfSize, rCos, rSin);
        var uv2 = uvCenter + RotateVector(new Vector2(uvHalfSize.X, -uvHalfSize.Y), rCos, rSin);
        uv3 = uvCenter + RotateVector(uvHalfSize, rCos, rSin);
        var uv4 = uvCenter + RotateVector(new Vector2(-uvHalfSize.X, uvHalfSize.Y), rCos, rSin);

        if (settings.hovered && !settings.frame)
            drawList.AddRectFilled(p1, p3, (settings.activeTime != 0) ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.ButtonHovered));

        if (!settings.flipped)
            drawList.AddImageQuad(tex.Handle, p1, p2, p3, p4, uv1, uv2, uv3, uv4, settings.color);
        else
            drawList.AddImageQuad(tex.Handle, p2, p1, p4, p3, uv1, uv2, uv3, uv4, settings.color);

        if (settings.cooldownAction > 0)
        {
            settings.cooldownCurrent = Game.GetRecastTimeElapsed(1, settings.cooldownAction);
            settings.cooldownMax = Game.GetRecastTime(1, settings.cooldownAction);
        }

        drawList.AddIconFrame(p1, settings.size, settings.frame, settings.hovered, settings.activeTime, settings.cooldownCurrent, settings.cooldownMax, settings.cooldownStyle);
    }

    private static readonly Vector2 iconFrameUV0 = new(1f / 426f, 141f / 426f);
    private static readonly Vector2 iconFrameUV1 = new(47f / 426f, 187f / 426f);
    private static readonly Vector2 iconHoverUV0 = new(49f / 426f, 238f / 426f);
    private static readonly Vector2 iconHoverUV1 = new(95f / 426f, 284f / 426f);
    private static readonly Vector2 iconHoverFrameUV0 = new(242f / 426f, 143f / 426f);
    private static readonly Vector2 iconHoverFrameUV1 = new(310f / 426f, 211f / 426f);
    private static readonly Vector2 iconClickUV0 = new(241f / 426f, 214f / 426f);
    private static readonly Vector2 iconClickUV1 = new(303f / 426f, 276f / 426f);

    public static void AddIconFrame(this ImDrawListPtr drawList, Vector2 pos, Vector2 size, bool frame, bool hovered, float activeTime, float cooldownCurrent, float cooldownMax, IconSettings.CooldownStyle cooldownStyle)
    {
        var frameSheet = QoLBar.TextureDictionary[TextureDictionary.FrameIconID];
        if (frameSheet == null || frameSheet.Handle == nint.Zero) return;

        var halfSize = size / 2;
        var center = pos + halfSize;
        var frameSize = size * 0.075f;
        var fMin = pos - frameSize;
        var fMax = pos + size + frameSize;

        if (frame && (cooldownMax < 0 || (cooldownStyle & (IconSettings.CooldownStyle.Cooldown | IconSettings.CooldownStyle.Disable)) == 0))
            drawList.AddImage(frameSheet.Handle, fMin, fMax, iconFrameUV0, iconFrameUV1); // Frame

        // Cooldown Spin
        if (cooldownMax > 0)
        {
            var progress = cooldownCurrent / cooldownMax;
            if ((cooldownStyle & IconSettings.CooldownStyle.Disable) != 0)
                drawList.AddIconCooldown(fMin, fMax, 0, 0);
            if ((cooldownStyle & IconSettings.CooldownStyle.GCDCooldown) != 0)
                drawList.AddIconCooldown(fMin, fMax, progress, 1);
            if ((cooldownStyle & IconSettings.CooldownStyle.ChargeCooldown) != 0)
                drawList.AddIconCooldown(fMin, fMax, progress, 2);
            if ((cooldownStyle & IconSettings.CooldownStyle.Cooldown) != 0)
                drawList.AddIconCooldown(fMin, fMax, progress, 0);

            if ((cooldownStyle & IconSettings.CooldownStyle.Number) != 0)
            {
                QoLBar.Font.Push();

                var wantedSize = size.X * 0.75f;
                var str = $"{Math.Ceiling(cooldownMax - cooldownCurrent)}";

                PushFontSize(wantedSize);

                var textSizeHalf = ImGui.CalcTextSize(str) / (2 * ImGuiHelpers.GlobalScale); // I don't know but it works

                // Outline
                using (var font = QoLBar.Font.Lock())
                {
                    var textOutlinePos = center - textSizeHalf + new Vector2(0, wantedSize * 0.05f);
                    drawList.AddText(font.ImFont, wantedSize, textOutlinePos, 0xFF000000, str);

                    var textPos = center - textSizeHalf - Vector2.UnitY;
                    drawList.AddText(font.ImFont, wantedSize, textPos, 0xFFFFFFFF, str);
                }

                PopFontSize();

                QoLBar.Font.Pop();
            }
        }

        if (!frame || !hovered) return;

        drawList.AddImage(frameSheet.Handle, fMin, fMax, iconHoverUV0, iconHoverUV1, 0x85FFFFFF); // Frame Center Glow
        fMax.Y += frameSize.Y * 0.70f; // I love rectangles
        drawList.AddImage(frameSheet.Handle, fMin - (frameSize * 3.5f), fMax + (frameSize * 3.5f), iconHoverFrameUV0, iconHoverFrameUV1); // Edge glow (its a fucking rectangle why)
        if (activeTime == 0) return;

        var animScale = ((activeTime >= 0) ? activeTime : ImGui.GetIO().MouseDownDuration[0]) / 0.2f;
        if (animScale >= 1.5) return;

        var animSize = new Vector2(1.5f) + halfSize * animScale;
        ImGui.GetForegroundDrawList().AddImage(frameSheet.Handle, center - animSize, center + animSize, iconClickUV0, iconClickUV1, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1 - 0.65f * animScale))); // Click
    }

    private const byte maxCooldownPhase = 80;
    private static readonly Vector2 iconCooldownSize = new(44);
    private static readonly Vector2 iconCooldownSection = new(44, 48);
    private static readonly Vector2 iconCooldownSheetSize1 = new(432, 432);
    private static readonly Vector2 iconCooldownSheetSize2 = new(792, 792);
    private static readonly Vector2 iconCooldownUV0Mult1 = iconCooldownSection / iconCooldownSheetSize1;
    private static readonly Vector2 iconCooldownUV0Mult2 = iconCooldownSection / iconCooldownSheetSize2;
    private static readonly Vector2 iconCooldownUV1Add1 = iconCooldownSize / iconCooldownSheetSize1;
    private static readonly Vector2 iconCooldownUV1Add2 = iconCooldownSize / iconCooldownSheetSize2;
    private static readonly Vector2 iconCooldownSheetUVOffset1 = new Vector2(18, -1) / iconCooldownSheetSize1; // Due to squaring
    private static readonly Vector2 iconCooldownSheetUVOffset2 = new Vector2(0, 179) / iconCooldownSheetSize2;

    public static void AddIconCooldown(this ImDrawListPtr drawList, Vector2 min, Vector2 max, float progress, byte style)
    {
        var cooldownSheet = style switch
        {
            0 => QoLBar.TextureDictionary[TextureDictionary.GetSafeIconID(1)],
            1 => QoLBar.TextureDictionary[TextureDictionary.GetSafeIconID(2)],
            2 => QoLBar.TextureDictionary[TextureDictionary.GetSafeIconID(2)],
            _ => null
        };

        if (cooldownSheet == null || cooldownSheet.Handle == nint.Zero) return;

        var phase = (byte)Math.Min(Math.Max(Math.Ceiling(maxCooldownPhase * progress), 0), maxCooldownPhase);
        var row = Math.DivRem(phase, 9, out var column);
        var uv0 = new Vector2(column, row);
        Vector2 uv1;
        switch (style)
        {
            case 0:
                min += Vector2.One;
                max -= Vector2.One;
                uv0 = uv0 * iconCooldownUV0Mult1 + iconCooldownSheetUVOffset1;
                uv1 = uv0 + iconCooldownUV1Add1;
                break;
            case 1:
                uv0 = uv0 * iconCooldownUV0Mult2 + iconCooldownSheetUVOffset2;
                uv1 = uv0 + iconCooldownUV1Add2;
                break;
            default:
                uv0 = uv0 * iconCooldownUV0Mult2 + iconCooldownSheetUVOffset2 + new Vector2(0.5f, 0);
                uv1 = uv0 + iconCooldownUV1Add2;
                break;
        }

        drawList.AddImage(cooldownSheet.Handle, min, max, uv0, uv1);
    }

    private static void DrawIcon(IDalamudTextureWrap icon, IconSettings settings) => ImGui.GetWindowDrawList().AddIcon(icon, ImGui.GetItemRectMin(), settings);

    public static void Icon(IDalamudTextureWrap icon, IconSettings settings)
    {
        ImGui.Dummy(settings.size);
        DrawIcon(icon, settings);
    }

    public static bool IconButton(string id, IDalamudTextureWrap icon, IconSettings settings)
    {
        var ret = ImGui.InvisibleButton(id, settings.size);
        settings.activeTime = (settings.activeTime >= 0) ? settings.activeTime : (ImGui.IsItemActive() ? -1 : 0);
        settings.hovered = settings.activeTime > 0 || ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly);
        DrawIcon(icon, settings);
        return ret;
    }

    // 😔
    public static bool AddHeaderIconButton(string id, int icon, float zoom, Vector2 offset, float rotation, uint color, string args)
    {
        if (ImGui.IsWindowCollapsed()) return false;

        var scale = ImGuiHelpers.GlobalScale;
        var prevCursorPos = ImGui.GetCursorPos();
        var buttonSize = new Vector2(20 * scale);
        var buttonPos = new Vector2(ImGui.GetWindowWidth() - buttonSize.X - 34 * scale - ImGui.GetStyle().FramePadding.X * 2, 2);
        ImGui.SetCursorPos(buttonPos);
        PushClipRectFullScreen();

        var pressed = false;
        ImGui.InvisibleButton(id, buttonSize);
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(itemMin, itemMax, false))
        {
            var halfSize = ImGui.GetItemRectSize() / 2;
            var center = itemMin + halfSize;
            ImGui.GetWindowDrawList().AddCircleFilled(center, halfSize.X, ImGui.GetColorU32(ImGui.IsMouseDown(ImGuiMouseButton.Left) ? ImGuiCol.ButtonActive : ImGuiCol.ButtonHovered));
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                pressed = true;
        }

        ImGui.SetCursorPos(buttonPos);
        ShortcutUI.DrawIcon(icon, new IconSettings
        {
            size = buttonSize,
            zoom = zoom,
            offset = offset,
            rotation = rotation,
            color = color
        }, args, false, true);

        ImGui.PopClipRect();
        ImGui.SetCursorPos(prevCursorPos);

        return pressed;
    }

    private static string search = string.Empty;
    private static HashSet<uint> filtered;
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, ImGuiComboFlags flags, Func<T, string, bool> searchPredicate, Func<T, bool> selectableDrawing) where T : struct, IExcelRow<T>
    {
        var sheet = DalamudApi.DataManager.GetExcelSheet<T>();
        return ExcelSheetCombo(id, out selected, getPreview(sheet), flags, sheet, searchPredicate, selectableDrawing);
    }

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, ImGuiComboFlags flags, ExcelSheet<T> sheet, Func<T, string, bool> searchPredicate, Func<T, bool> drawRow) where T : struct, IExcelRow<T>
    {
        selected = null;
        if (!ImGui.BeginCombo(id, preview, flags)) return false;

        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            search = string.Empty;
            filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref search, 128))
            filtered = null;

        filtered ??= sheet.Where(s => searchPredicate(s, search)).Select(s => s.RowId).ToHashSet();

        var i = 0;
        foreach (var rowID in filtered)
        {
            if (sheet.GetRowOrDefault(rowID) is not { } row) continue;

            ImGui.PushID(i++);
            if (drawRow(row))
                selected = row;
            ImGui.PopID();

            if (selected == null) continue;
            ImGui.EndCombo();
            return true;
        }

        ImGui.EndCombo();
        return false;
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
        public float m_fRotationOffset;
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
                s_oPieMenuContext.m_fRotationOffset = 0;
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

        float c_fDefaultRotate = (float)(-Math.PI / 2.0f) + s_oPieMenuContext.m_fRotationOffset;
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
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 0));
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 2));
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 1));
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 3));
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 2));
                        pDrawList.PrimWriteIdx((ushort)(pDrawList.VtxCurrentIdx + 1));
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

    public static void SetPieRotationOffset(float rotation) => s_oPieMenuContext.m_fRotationOffset = rotation;

    public static void DisableRepositioning() => s_oPieMenuContext.m_bAdjustPosition = false;

    public static bool IsPieAppearing() => s_oPieMenuContext.m_bAppearing;

    public static bool IsItemActivated() => s_oPieMenuContext.m_oCurrentItem.m_bActivated;
}