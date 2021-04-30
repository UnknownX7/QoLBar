using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ImGuiNET;
using Dalamud.Plugin;
using Dalamud.Interface;

namespace QoLBar
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible => true;

        public readonly List<BarUI> bars;

#if DEBUG
        public bool configOpen = true;
#else
        public bool configOpen = false;
#endif
        public void ToggleConfig() => configOpen = !configOpen;

        private bool lastConfigPopupOpen = false;
        private bool configPopupOpen = false;
        public bool IsConfigPopupOpen() => configPopupOpen || lastConfigPopupOpen;
        public void SetConfigPopupOpen() => configPopupOpen = true;

        public static readonly Vector2 defaultSpacing = new Vector2(8, 4);

        private bool _displayOutsideMain = true;

        public static Vector2 mousePos = ImGui.GetMousePos();

        public PluginUI()
        {
            bars = new List<BarUI>();
            for (int i = 0; i < QoLBar.Config.BarCfgs.Count; i++)
                bars.Add(new BarUI(i));

            Task.Run(async () =>
            {
                while (!QoLBar.Interface.Data.IsDataReady)
                    await Task.Delay(1000);
                DisplayConditionSet.classDictionary = QoLBar.Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>(QoLBar.Interface.ClientState.ClientLanguage).ToDictionary(i => i.RowId);
                DisplayConditionSet.territoryDictionary = QoLBar.Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>(QoLBar.Interface.ClientState.ClientLanguage).ToDictionary(i => i.RowId);
            });

            if (QoLBar.Config.FirstStart)
            {
                AddBar(new BarCfg { Editing = true });
                AddDemoBar();
            }
        }

        public void Reload()
        {
            Dispose();

            bars.Clear();
            for (int i = 0; i < QoLBar.Config.BarCfgs.Count; i++)
                bars.Add(new BarUI(i));
        }

        public void Draw()
        {
            if (!IsVisible) return;

            mousePos = ImGui.GetMousePos();

            IconBrowserUI.Draw();

            if (QoLBar.Config.AlwaysDisplayBars || QoLBar.IsLoggedIn())
            {
                foreach (BarUI bar in bars)
                    bar.Draw();
            }
            lastConfigPopupOpen = configPopupOpen;
            configPopupOpen = false;

            if (configOpen)
                DrawPluginConfig();

            PieUI.Draw();
        }

        private void DrawPluginConfig()
        {
            if (!ImGuiEx.SetBoolOnGameFocus(ref _displayOutsideMain)) return;

            ImGui.SetNextWindowSizeConstraints(new Vector2(588, 500), ImGuiHelpers.MainViewport.Size);
            ImGui.Begin("QoL Bar Configuration", ref configOpen);

            ImGuiEx.ShouldDrawInViewport(out _displayOutsideMain);

            if (ImGui.BeginTabBar("Config Tabs"))
            {
                if (ImGui.BeginTabItem("Bar Manager"))
                {
                    DrawBarManager();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Condition Sets"))
                {
                    DisplayConditionSet.DrawEditor();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Settings"))
                {
                    DrawSettingsMenu();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Backups"))
                {
                    DrawBackupManager();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Debug"))
                {
                    DrawDebugMenu();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void DrawBarManager()
        {
            Vector2 textsize = new Vector2(-1, 0);
            float textx = 0.0f;

            var letterButtonSize = new Vector2(ImGui.CalcTextSize("O").X + ImGui.GetStyle().FramePadding.X * 2, 0);

            ImGui.Columns(3, "QoLBarsList", false);
            for (int i = 0; i < bars.Count; i++)
            {
                ImGui.PushID(i);

                var bar = QoLBar.Config.BarCfgs[i];

                ImGui.Text($"#{i + 1}");
                ImGui.SameLine();

                textx = ImGui.GetCursorPosX();

                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Name", ref bar.Name, 32))
                    QoLBar.Config.Save();

                textsize = ImGui.GetItemRectSize();

                ImGui.NextColumn();

                if (ImGui.Button("O", letterButtonSize))
                    ImGui.OpenPopup($"BarConfig##{i}");
                ImGuiEx.SetItemTooltip("Options");
                bars[i].DrawConfig();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(27 * ImGuiHelpers.GlobalScale);
                if (ImGui.Button(bar.Hidden ? "R" : "H", letterButtonSize))
                    bars[i].IsHidden = !bars[i].IsHidden;
                ImGuiEx.SetItemTooltip(bar.Hidden ? "Reveal" : "Hide");
                ImGui.SameLine();
                var preview = ((bar.ConditionSet >= 0) && (bar.ConditionSet < QoLBar.Config.ConditionSets.Count)) ? $"[{bar.ConditionSet + 1}] {QoLBar.Config.ConditionSets[bar.ConditionSet].Name}" : "Condition Set";
                if (ImGui.BeginCombo("##Condition", preview))
                {
                    if (ImGui.Selectable("None", bar.ConditionSet == -1))
                    {
                        bar.ConditionSet = -1;
                        QoLBar.Config.Save();
                    }
                    for (int idx = 0; idx < QoLBar.Config.ConditionSets.Count; idx++)
                    {
                        if (ImGui.Selectable($"[{idx + 1}] {QoLBar.Config.ConditionSets[idx].Name}", idx == bar.ConditionSet))
                        {
                            bar.ConditionSet = idx;
                            QoLBar.Config.Save();
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGuiEx.SetItemTooltip("Applies a condition set to the bar that will control when it is shown.\n" +
                    "Useful for making groups of bars that all display at the same time.\n" +
                    "You can make these on the \"Condition Sets\" tab at the top of this window.");

                ImGui.NextColumn();

                if (ImGui.Button("↑"))
                    ShiftBar(i, false);
                ImGui.SameLine();
                if (ImGui.Button("↓"))
                    ShiftBar(i, true);
                ImGui.SameLine();
                if (ImGui.Button("Export"))
                    ImGui.SetClipboardText(ExportBar(i, false));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export to clipboard with minimal settings (May change with updates).\n" +
                        "Right click to export with every setting (Longer string, doesn't change).");

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        ImGui.SetClipboardText(ExportBar(i, true));
                }

                if (bars.Count > 1)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(QoLBar.Config.ExportOnDelete ? "Cut" : "Delete"))
                        QoLBar.Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!" +
                            (QoLBar.Config.ExportOnDelete ? "\nThe bar will be exported to clipboard first." : ""));

                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        {
                            ImGui.SetWindowFocus(null); // Kill focus to prevent ImGui from overwriting text box on deletes
                            RemoveBar(i);
                        }
                    }
                }

                ImGui.Separator();
                ImGui.NextColumn();

                ImGui.PopID();
            }

            ImGui.Spacing();
            ImGui.SameLine(textx);
            if (ImGui.Button("+", textsize))
                AddBar(new BarCfg { Editing = true });
            ImGui.NextColumn();
            ImGui.NextColumn();
            if (ImGui.Button("Import", textsize))
                ImportBar(ImGuiEx.TryGetClipboardText());
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Import a bar from the clipboard, or import a single shortcut as a new bar.\n" +
                    "Right click will add a demo bar that showcases various features.");

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    AddDemoBar();
            }

            ImGui.Columns(1);
        }

        private void DrawSettingsMenu()
        {
            var halfWidth = ImGui.GetWindowWidth() / 2;
            var thirdWidth = ImGui.GetWindowWidth() / 3;
            if (ImGui.Checkbox("Export on Delete", ref QoLBar.Config.ExportOnDelete))
                QoLBar.Config.Save();
            ImGui.SameLine(halfWidth);
            if (ImGui.Checkbox("Always Display Bars", ref QoLBar.Config.AlwaysDisplayBars))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Bars will remain visible even when logged out.");

            if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref QoLBar.Config.UseIconFrame))
                QoLBar.Config.Save();
            ImGui.SameLine(halfWidth);
            var _ = QoLBar.Config.UseHRIcons;
            if (ImGui.Checkbox("Use HR Icons", ref _))
            {
                QoLBar.Config.UseHRIcons = _;
                QoLBar.Config.Save();
            }
            ImGuiEx.SetItemTooltip("Loads the high resolution icons instead. Be aware that the Icon Browser will use\n" +
                "up to 5GB of memory until closed if you open the \"Spoilers\" tabs!");

            if (ImGui.Checkbox("Disable Condition Caching", ref QoLBar.Config.NoConditionCache))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Disables the 100ms delay between checking conditions, increasing CPU load.");
            ImGui.SameLine(halfWidth);
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 4);
            if (ImGui.InputInt("Backup Timer", ref QoLBar.Config.BackupTimer))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Number of minutes since the last save to perform a backup. Set to 0 to disable.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Pie Settings");
            if (ImGui.Checkbox("Appear in Center", ref QoLBar.Config.PiesAlwaysCenter))
            {
                if (!QoLBar.Config.PiesAlwaysCenter)
                {
                    QoLBar.Config.PiesMoveMouse = false;
                    QoLBar.Config.PiesReturnMouse = false;
                    QoLBar.Config.PiesReadjustMouse = false;
                }
                QoLBar.Config.Save();
            }
            ImGui.SameLine(halfWidth);
            if (QoLBar.Config.PiesAlwaysCenter && ImGui.Checkbox("Center Mouse on Open", ref QoLBar.Config.PiesMoveMouse))
            {
                if (!QoLBar.Config.PiesMoveMouse)
                {
                    QoLBar.Config.PiesReturnMouse = false;
                    QoLBar.Config.PiesReadjustMouse = false;
                }
                QoLBar.Config.Save();
            }

            if (QoLBar.Config.PiesMoveMouse && ImGui.Checkbox("Return Mouse on Close", ref QoLBar.Config.PiesReturnMouse))
            {
                if (!QoLBar.Config.PiesReturnMouse)
                    QoLBar.Config.PiesReadjustMouse = false;
                QoLBar.Config.Save();
            }
            ImGui.SameLine(halfWidth);
            if (QoLBar.Config.PiesReturnMouse && ImGui.Checkbox("Recorrect Old Mouse Position", ref QoLBar.Config.PiesReadjustMouse))
                QoLBar.Config.Save();
            ImGui.SameLine();

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Opt out of Dalamud settings for hiding UI");
            if (ImGui.Checkbox("Game UI Toggled", ref QoLBar.Config.OptOutGameUIOffHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(thirdWidth);
            if (ImGui.Checkbox("In Cutscene", ref QoLBar.Config.OptOutCutsceneHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(thirdWidth * 2);
            if (ImGui.Checkbox("In /gpose", ref QoLBar.Config.OptOutGPoseHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Temporary settings, ENABLE AT OWN RISK");
            ImGui.Checkbox("Allow importing conditions", ref Importing.allowImportConditions);
            ImGui.SameLine(halfWidth);
            ImGui.Checkbox("Allow importing hotkeys", ref Importing.allowImportHotkeys);
        }

        private void DrawBackupManager()
        {
            var path = QoLBar.Config.GetPluginBackupPath();
            var configFile = Configuration.ConfigFile;

            if (ImGui.Button("Open Folder"))
                Process.Start(path);
            ImGui.SameLine();
            if (ImGui.Button("Create Backup"))
                BackupFile(configFile);

            ImGui.Separator();
            ImGui.Columns(3, "QoLBarBackups", false);
            if (!string.IsNullOrEmpty(path))
            {
                var i = 0;
                var directory = new DirectoryInfo(path);
                foreach (var file in directory.GetFiles())
                {
                    if (file.Extension == ".json")
                    {
                        ImGui.PushID(i);

                        ImGui.TextUnformatted(file.Name);
                        ImGui.NextColumn();
                        ImGui.TextUnformatted(file.LastWriteTime.ToString());
                        ImGui.NextColumn();
                        ImGui.SmallButton("Load");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Double click this button to overwrite and\n" +
                                $"reload the current config with {file.Name}");

                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                QoLBar.Config.LoadConfig(file);
                        }

                        ImGui.SameLine();
                        if (ImGui.SmallButton("Delete"))
                            QoLBar.Plugin.ExecuteCommand("/echo <se> Double right click to delete!");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Double right click this button to delete {file.Name}");

                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right))
                                DeleteFile(file);
                        }

                        ImGui.Separator();
                        ImGui.NextColumn();

                        ImGui.PopID();
                        i++;
                    }
                }
            }
            ImGui.Columns(1);
        }

        private string debug_SerializedImport = string.Empty;
        private string debug_DeserializedImport = string.Empty;
        private void DrawDebugMenu()
        {
            ImGui.TextUnformatted("Game Data Pointers");
            ImGui.Indent();
            ImGui.Columns(3, "DebugPointers", false);

            ImGui.TextUnformatted("UI Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.uiModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.NextColumn();

            ImGui.TextUnformatted("Rapture Shell Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.raptureShellModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.NextColumn();

            ImGui.TextUnformatted("Rapture Macro Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.raptureMacroModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.IsMacroRunning}");
            ImGui.NextColumn();

            ImGui.TextUnformatted("Game Text Input Active");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.textActiveBoolPtr.ToString("X")}");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.IsGameTextInputActive}");

            ImGui.Columns(1);
            ImGui.Unindent();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.TreeNodeEx("Export Editor", ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.NoTreePushOnOpen))
            {
                var available = ImGui.GetContentRegionAvail();
                ImGui.TextUnformatted("Serialized String");
                ImGui.SetNextItemWidth(available.X);
                if (ImGui.InputText("##Serialized", ref debug_SerializedImport, 1000000, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.NoHorizontalScroll))
                {
                    var import = Importing.TryImport(debug_SerializedImport);
                    if (import.bar != null)
                        debug_DeserializedImport = JsonConvert.SerializeObject(import.bar, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                    else if (import.shortcut != null)
                        debug_DeserializedImport = JsonConvert.SerializeObject(import.shortcut, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                }

                ImGui.TextUnformatted("Deserialized String");
                if (ImGui.InputTextMultiline("##Deserialized", ref debug_DeserializedImport, 1000000, new Vector2(available.X, available.Y / 2)))
                {
                    try
                    {
                        debug_SerializedImport = Importing.ExportBar(Importing.ImportObject<BarCfg>(Importing.CompressString(debug_DeserializedImport)), true);
                    }
                    catch
                    {
                        try
                        {
                            debug_SerializedImport = Importing.ExportShortcut(Importing.ImportObject<ShCfg>(Importing.CompressString(debug_DeserializedImport)), true);
                        }
                        catch { }
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            Keybind.DrawDebug();
        }

        private void AddBar(BarCfg bar)
        {
            QoLBar.Config.BarCfgs.Add(bar);
            bars.Add(new BarUI(bars.Count));
            QoLBar.Config.Save();
        }

        private void RemoveBar(int i)
        {
            if (QoLBar.Config.ExportOnDelete)
                ImGui.SetClipboardText(Importing.ExportBar(QoLBar.Config.BarCfgs[i], false));

            bars[i].Dispose();
            bars.RemoveAt(i);
            QoLBar.Config.BarCfgs.RemoveAt(i);
            QoLBar.Config.Save();
            RefreshBarIndexes();
        }

        private void ShiftBar(int i, bool increment)
        {
            if (!increment ? i > 0 : i < (bars.Count - 1))
            {
                var j = (increment ? i + 1 : i - 1);
                var b = bars[i];
                bars.RemoveAt(i);
                bars.Insert(j, b);

                var b2 = QoLBar.Config.BarCfgs[i];
                QoLBar.Config.BarCfgs.RemoveAt(i);
                QoLBar.Config.BarCfgs.Insert(j, b2);
                QoLBar.Config.Save();
                RefreshBarIndexes();
            }
        }

        public string ExportBar(int i, bool saveAllValues) => Importing.ExportBar(QoLBar.Config.BarCfgs[i], saveAllValues);

        public void ImportBar(string import)
        {
            var imports = Importing.TryImport(import, true);
            if (imports.bar != null)
                AddBar(imports.bar);
            else if (imports.shortcut != null)
            {
                var bar = new BarCfg { Editing = true };
                bar.ShortcutList.Add(imports.shortcut);
                AddBar(bar);
            }
        }

        public void AddDemoBar()
        {
            var prev = Importing.allowImportHotkeys;
            Importing.allowImportHotkeys = true;
            ImportBar("H4sIAAAAAAAEALVa/Y7buBF/Fa5cIFec1yvJtvyBokDiJLcpGmywm8PhWucPWqItdiVSJal1fIcDDu0D9QX6THmGzpCSbe16v6wtkHgtipwZzgxnfjP0r94fzKZg3tRjXtdbhN701+0IPHU9AV9O3777eHEKD/q" +
                "v3vTvuwm6noDvyZvXl/BovGnY9eLstTcNuh6/gAV+z+/C/y/weOlN8cmLrwp4Me4OvvzWPUDvA6E5ocSwr4bE1LCVVBtHO7hfiJnMpGKJXdXpfHZrBVkwEldv/lFqQzJ+zQiM81iKPXm96SCcDCZBfzQ5Xu5PimlNckaW" +
                "UpG8zAwvMkZYnEoNM2KYcYYP5JxlmZwL92BSptiJ156pkZYVCbok7JI+4YJIlTDVYB3UbMP6Sx/e522s1dg1JYqKROZWlAbnn2VJ1lyc1HzxOZOa9Xo9J0F4tASdTuUxi4yKa7IojbHGbed+ulzUztfpnMsbprhYEVkwg" +
                "bvtkg1sAD2MZlqSwiphZlRGvidXYAu5ddhr+OwHwXB4v+9Op5Hvj4Nb/jgah8NhP/KP3sgPUoLXy8XJlvATCH15YNZP8AX+zNyf7Rr4fu5NjSrZQTHefWVxaRg4ZMJveFLSjOQ0VpJ0/NpDzsjnlGvwjywj7P7p4DtnuX" +
                "+8ZWtJdEoxIDxFiltTrQSB/wIyxBCN4JxYwrcEgH+08d5yhQ/7kMn4Gr6vKTe742xDCvkTDvaCPzdjC/n2799rCnb4zYaUGp157uGoBzs2qSwNcBVlvmBq59y6YDFfbsg65XFKCqqMJnKJhCGs5jmcdTt1LcsscbEVwpA" +
                "q4VTgHpzWuNCG0eSJMe4R/wt9O/3C+dsTz7IL92R3nH+028dBXYtnt4VZA7TPqN6ckL9gvtDM2N0K6kLsHA5rACqDWKclDEnUcaW2R3PUdLoMxp3OBcQQS3SpOBOoN21qF3BD1ciRLgZsooEfBJ3OWypiVpNO8IHk0vA2" +
                "0RH3gJv4KEthiALDM2O2PGgMGnKvLqtXc6/dToL+AHbyiizJK0LVqswZEP9uqcAgf2xkmNeVmbeTVpJpAABmzUDfaDiwG3qstae1GFo0lwl6eCrX9oUlAT6QcF1kdMOSNtILlD5E6UVDeiHJdgNHE8+QeB+JZw3iGewE0" +
                "pHMSrR0KxYpshghi7TBIuWr9KV4rJDHAHmsGjxWim50TLN2SlJIfYjUVYO6YpDQNSNnZJnxohWLU3DPOm6vMeLWsdu6UieooiMtCkYV4UsbMWmSQFqRgjVceJay+Hrnhgsl15qpVxq8d2ZpggcbukC3zYCYcF4rkRquyt" +
                "tsIwIMEiCUqmAzFRsHnV2Ep8LGqB0yiYJhe24XcAQBMWrILGgWwE0nFZfQ7/v+oB/0W3Hxh3hCfq6y2S8SrSJsJvqbNx32jsdW0+nED4PhjrZO+dKlCp7T1T6iD8dAOwyfJ/gY6pEd8RI8FUjjp1wxm3LA6tV20FLrbRX" +
                "R2Nep3wu68OGPn8U9GIYhBt2P0mVAA9lS136RyHIBZQ34RYxwN854fI11F0RSKgCtZRuQqQA0cUOzkulKpvGeroOBlSl6proHEZjySm6xSYIAh+utV0L95gcjxMu2/hw5xn5vuFPGYIych8EzU1DUDwbOV2t+QD9y9E+D" +
                "Vl7kPwebHyYxiiDGIaYBtFrQGMKaDQkZXyHKsKbDygxChEUs1j3rgroN39aiWxy0H+h2Dm+LKulwEoRVB1fxcIHwAtI1Pq4yqhG+VWhUCTgVAOJ0KpUBiA1DYslX29L7nzKzgK+23/gl4GgQRE04CrVRvyYQ+t0AV8RXM" +
                "BHdMH6PS6PooDZmFTolb1hKb7hUutOZpVSstluXCcMN0i2QdaVK5iBqTuJNnGGcAFAG6RmGOKQbYZgSUEPVatGPotSGDWKUAJJBW1vXIr9CROb2V0f7VnQ/iFgxTOk0m7alZZg2nQ5+Pqqj4PhMGzYy/mcb1bfGcUqvi0" +
                "9nwqrQ0i2aRP0n116tWkGXtvfTyg6PFVAWLLYuKIatgBIQiJ6j0PaNl2aACet1MPGwkK8FRHkDWdl2RfVeXHUvHLCDo7hxB7wOMQ7v0axIKeYQhPkINWAKAcT3qHnCYQQZGuuPS8rFQq5fqiMcDqEIOUjz+NMeDqGKfU8" +
                "B4Rwk3G9BeFJp4T1NWJPq4HiqkY/i3iZ4PDoOo6Da/12qUQuqUO5egufdJTpqQRRBPLa4Dun0aYn8MF2AdT8obA/cJTtpQRbqgtmGHqIaHA8WwwhO1xvA1IfItjhbEZytT6XCu4oDhFscsAgO2E8pN3foIlofj/3hBGq8" +
                "mk+L8xZNUC8UKucD8h9/4mxXfhS44JljM/BWgeMqMlfmYCg9VBkdiKz3KuJpSeLR6IRd0sH9qPSBzjSU4uSDWEry7V//eTTYz6RIOHZ/yBUzkGLeCYq7xwZbwrX9vqCYY6i2nQ5QlOKy1EQbyEC6W7dWc1IWNYqf36Jat" +
                "Tuke1tkJWSqCte3BXvn0lyzDcpdgS26qxzqfvjWgtx0968PSSoN7K1HPiAaX2BXR9ebTrowW+MMJE+oYtgLX0Pw6rUV+d3XAgQEeSC94LWE0y8qfIcfMaPv+928WgWadPdi2KIShKGS3ZS8NQTPt2K5r7bOtJWnFUcWtl" +
                "yDcMi+utdGuQHbtBI7gecVLbySsC8Uo1ZMp2TsuVcbbSvzG4gWZaGxcccqh7LXLqWRgI94bE/5gtrOBrinlWbPBbEkLRJEWLurkkxS14QjSyXzSvkVn/+jH79li3JFPjJRYrmI7cNtycfhKCvcDvDdk8mueEAiOJllnNo" +
                "bnNjwG1b7cltJP3EIk2eAthIOwqHEutnhcla2N0cFh3oXZqAD0SRxPuDkwFiBJ6zuS72AWOSSoU/GqCmQyQpqS+vqEgBdAOBw5DzVhrbdiSsAIaM/d61Du5l9O6LJd2FdqyPJhDHbaW4l7qy6drMXgBgIajlAi0Iaq0h3" +
                "7+bu7ni8LSC3dp17dvXc64LlGcRnd2XXvK+zUbC+Imwr9RXKAlaHakOBV2GP6tvv/92ZX0GlUcVb9El723a2vWK06dZ2tUA2hhHkDrnnCPhQpbVfXj3cuJrZw1IqWt2n1U0m8OHjq8tznjDX8nJkaqI3XHOLLZZL4n6PU" +
                "1/m/ijS/TUHEsAlnHWBN5tGViMVBc9F1QMhAH+pkch1F0MuOtS8KYSoZXji1R4MJxaBUPvLgQUglDEULoVrj0+i0WAwhOX9/jiYDEaIcaQ1g676Zssr1zeDEdtX64agPe8GsV/P7w16gffb/wCk4SCvyiQAAA==");
            Importing.allowImportHotkeys = prev;
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].ID = i;
        }

        public void SetBarHidden(int i, bool toggle, bool b = false)
        {
            if (i < 0 || i >= bars.Count)
                QoLBar.PrintError($"Bar #{i + 1} does not exist.");
            else
            {
                if (toggle)
                    bars[i].IsHidden = !bars[i].IsHidden;
                else
                    bars[i].IsHidden = b;
            }
        }

        public void SetBarHidden(string name, bool toggle, bool b = false)
        {
            var found = false;
            for (int i = 0; i < bars.Count; ++i)
            {
                if ((QoLBar.Config.BarCfgs[i]).Name == name)
                {
                    found = true;
                    SetBarHidden(i, toggle, b);
                }
            }
            if (!found)
                QoLBar.PrintError($"Bar \"{name}\" does not exist.");
        }

        private void BackupFile(FileInfo file, string name = "", bool overwrite = false)
        {
            try
            {
                if (file.Extension != ".json")
                    throw new InvalidOperationException("File must be json!");

                if (string.IsNullOrEmpty(name))
                    name = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");

                var path = QoLBar.Config.GetPluginBackupPath() + $"\\{name}.json";
                file.CopyTo(path, overwrite);
                PluginLog.LogInformation($"Saved file to {path}");
            }
            catch (Exception e)
            {
                QoLBar.PrintError($"Failed to save: {e.Message}");
            }
        }

        private void DeleteFile(FileInfo file)
        {
            try
            {
                if (file.Extension != ".json")
                    throw new InvalidOperationException("File must be json!");

                file.Delete();
                PluginLog.LogInformation($"Deleted file {file.FullName}");
            }
            catch (Exception e)
            {
                QoLBar.PrintError($"Failed to delete: {e.Message}");
            }
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }

        // Helper function
        public static void DrawExternalWindow(Action draw, bool noViewport)
        {
            if (noViewport)
                ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, defaultSpacing);
            ImGuiEx.PushFontScale(1);
            draw();
            ImGuiEx.PopFontScale();
            ImGui.PopStyleVar();
        }
    }
}
