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

namespace QoLBar
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible => true;

        private readonly List<BarUI> bars;

        private static QoLBar Plugin => QoLBar.Plugin;
        private static Configuration Config => QoLBar.Config;

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

        public PluginUI()
        {
            bars = new List<BarUI>();
            for (int i = 0; i < Config.BarConfigs.Count; i++)
                bars.Add(new BarUI(i));

            Task.Run(async () =>
            {
                while (!QoLBar.Interface.Data.IsDataReady)
                    await Task.Delay(1000);
                DisplayConditionSet.classDictionary = QoLBar.Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>(QoLBar.Interface.ClientState.ClientLanguage).ToDictionary(i => i.RowId);
                DisplayConditionSet.territoryDictionary = QoLBar.Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>(QoLBar.Interface.ClientState.ClientLanguage).ToDictionary(i => i.RowId);
            });
        }

        public void Reload()
        {
            Dispose();

            bars.Clear();
            for (int i = 0; i < Config.BarConfigs.Count; i++)
                bars.Add(new BarUI(i));
        }

        public void Draw()
        {
            if (!IsVisible) return;

            IconBrowserUI.Draw();

            if (Config.AlwaysDisplayBars || QoLBar.IsLoggedIn())
            {
                foreach (BarUI bar in bars)
                    bar.Draw();
            }
            lastConfigPopupOpen = configPopupOpen;
            configPopupOpen = false;

            if (configOpen)
                DrawPluginConfig();
        }

        private void DrawPluginConfig()
        {
            ImGui.SetNextWindowSizeConstraints(new Vector2(588, 500), ImGui.GetIO().DisplaySize);
            ImGui.Begin("QoL Bar Configuration", ref configOpen);

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

            ImGui.Columns(3, "QoLBarsList", false);
            for (int i = 0; i < bars.Count; i++)
            {
                ImGui.PushID(i);

                var bar = Config.BarConfigs[i];

                ImGui.Text($"#{i + 1}");
                ImGui.SameLine();

                textx = ImGui.GetCursorPosX();

                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Title", ref bar.Title, 32))
                    Config.Save();

                textsize = ImGui.GetItemRectSize();

                ImGui.NextColumn();

                if (ImGui.Button("O"))
                    ImGui.OpenPopup($"BarConfig##{i}");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Options");
                bars[i].BarConfigPopup();
                ImGui.SameLine();
                if (ImGui.Button(bar.Hidden ? "R" : "H"))
                    bars[i].ToggleVisible();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(bar.Hidden ? "Reveal" : "Hide");
                ImGui.SameLine();
                var preview = ((bar.ConditionSet >= 0) && (bar.ConditionSet < Config.ConditionSets.Count)) ? $"[{bar.ConditionSet + 1}] {Config.ConditionSets[bar.ConditionSet].Name}" : "Condition Set";
                if (ImGui.BeginCombo("##Condition", preview))
                {
                    if (ImGui.Selectable("None", bar.ConditionSet == -1))
                    {
                        bar.ConditionSet = -1;
                        Config.Save();
                    }
                    for (int idx = 0; idx < Config.ConditionSets.Count; idx++)
                    {
                        if (ImGui.Selectable($"[{idx + 1}] {Config.ConditionSets[idx].Name}", idx == bar.ConditionSet))
                        {
                            bar.ConditionSet = idx;
                            Config.Save();
                        }
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Applies a condition set to the bar that will control when it is shown.\n" +
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

                if (i > 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(Config.ExportOnDelete ? "Cut" : "Delete"))
                        Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!" +
                            (Config.ExportOnDelete ? "\nThe bar will be exported to clipboard first." : ""));

                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                            RemoveBar(i);
                    }
                }

                ImGui.Separator();
                ImGui.NextColumn();

                ImGui.PopID();
            }

            ImGui.Spacing();
            ImGui.SameLine(textx);
            if (ImGui.Button("+", textsize))
                AddBar(new BarConfig());
            ImGui.NextColumn();
            ImGui.NextColumn();
            if (ImGui.Button("Import", textsize))
                ImportBar(ImGui.GetClipboardText());
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
            if (ImGui.Checkbox("Export on Delete", ref Config.ExportOnDelete))
                Config.Save();
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox("Resizing Repositions Bars", ref Config.ResizeRepositionsBars))
                Config.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Undocked bars will automatically readjust if you change resolutions.");

            if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref Config.UseIconFrame))
                Config.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This option will invert the ' f ' argument for all icons.");
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox("Always Display Bars", ref Config.AlwaysDisplayBars))
                Config.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Bars will remain visible even when logged out.");

            if (ImGui.Checkbox("Disable Condition Caching", ref Config.NoConditionCache))
                Config.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Disables the 100ms delay between checking conditions, increasing CPU load.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Opt out of Dalamud settings for hiding UI");
            if (ImGui.Checkbox("Game UI Toggled", ref Config.OptOutGameUIOffHide))
            {
                Config.Save();
                Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            if (ImGui.Checkbox("In Cutscene", ref Config.OptOutCutsceneHide))
            {
                Config.Save();
                Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            if (ImGui.Checkbox("In /gpose", ref Config.OptOutGPoseHide))
            {
                Config.Save();
                Plugin.CheckHideOptOuts();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Temporary settings, ENABLE AT OWN RISK");
            ImGui.Checkbox("Allow importing conditions", ref QoLBar.allowImportConditions);
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            ImGui.Checkbox("Allow importing hotkeys", ref QoLBar.allowImportHotkeys);
        }

        private void DrawBackupManager()
        {
            var path = Config.GetPluginBackupPath();
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
                                Config.LoadConfig(file);
                        }

                        ImGui.SameLine();
                        if (ImGui.SmallButton("Delete"))
                            Plugin.ExecuteCommand("/echo <se> Double right click to delete!");
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

            ImGui.TextUnformatted("Chat UI Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{Plugin.uiModulePtr.ToString("X")}");
            ImGui.NextColumn();

            ImGui.NextColumn();
            ImGui.TextUnformatted("Game Text Input Active");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{Plugin.textActiveBoolPtr.ToString("X")}");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{Plugin.GameTextInputActive}");

            ImGui.Columns(1);
            ImGui.Unindent();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.TreeNodeEx("Import Editor", ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.NoTreePushOnOpen))
            {
                var available = ImGui.GetContentRegionAvail();
                ImGui.TextUnformatted("Serialized String");
                ImGui.SetNextItemWidth(available.X);
                if (ImGui.InputText("##Serialized", ref debug_SerializedImport, 1000000, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.NoHorizontalScroll))
                {
                    try
                    {
                        var o = QoLBar.ImportObject<BarConfig>(debug_SerializedImport);
                        debug_DeserializedImport = JsonConvert.SerializeObject(o, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                    }
                    catch
                    {
                        try
                        {
                            var o = QoLBar.ImportObject<Shortcut>(debug_SerializedImport);
                            debug_DeserializedImport = JsonConvert.SerializeObject(o, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                        }
                        catch { }
                    }
                }

                ImGui.TextUnformatted("Deserialized String");
                if (ImGui.InputTextMultiline("##Deserialized", ref debug_DeserializedImport, 1000000, new Vector2(available.X, available.Y / 2)))
                {
                    try
                    {
                        debug_SerializedImport = QoLBar.ExportBar(QoLBar.ImportObject<BarConfig>(QoLBar.CompressString(debug_DeserializedImport)), true);
                    }
                    catch
                    {
                        try
                        {
                            debug_SerializedImport = QoLBar.ExportShortcut(QoLBar.ImportObject<Shortcut>(QoLBar.CompressString(debug_DeserializedImport)), true);
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

        private void AddBar(BarConfig bar)
        {
            Config.BarConfigs.Add(bar);
            bars.Add(new BarUI(bars.Count));
            Config.Save();
        }

        private void RemoveBar(int i)
        {
            if (Config.ExportOnDelete)
                ImGui.SetClipboardText(QoLBar.ExportBar(Config.BarConfigs[i], false));

            bars.RemoveAt(i);
            Config.BarConfigs.RemoveAt(i);
            Config.Save();
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

                var b2 = Config.BarConfigs[i];
                Config.BarConfigs.RemoveAt(i);
                Config.BarConfigs.Insert(j, b2);
                Config.Save();
                RefreshBarIndexes();
            }
        }

        public string ExportBar(int i, bool saveAllValues) => QoLBar.ExportBar(Config.BarConfigs[i], saveAllValues);

        public void ImportBar(string import)
        {
            try
            {
                AddBar(QoLBar.ImportBar(import));
            }
            catch (Exception e) // Try as a shortcut instead
            {
                try
                {
                    var sh = QoLBar.ImportShortcut(ImGui.GetClipboardText());
                    var bar = new BarConfig();
                    bar.ShortcutList.Add(sh);
                    AddBar(bar);
                }
                catch (Exception e2)
                {
                    PluginLog.LogError("Invalid import string!");
                    PluginLog.LogError($"{e.GetType()}\n{e.Message}");
                    PluginLog.LogError($"{e2.GetType()}\n{e2.Message}");
                }
            }
        }

        private void AddDemoBar()
        {
            var prev = QoLBar.allowImportHotkeys;
            QoLBar.allowImportHotkeys = true;
            ImportBar("H4sIAAAAAAAEALVZbW/bOBL+K4y8QPdwjk+SJdnOtyZttzlcNkWcRTfdHBa0RFvcSKRPpOr4Fv3vO0NKfkksKblzgaKxKHJm+HA488zoT+cHvV4y58yZOX3nlusMf5++e391fQoD01QWOi71v7jSztlvf25mK3j5M83xJ84l529vcL15" +
                "Oew7l7EUt1zAmu2SACb86px5A7fv3FV/v1R/P5u/7nDie9Hw27f+QUWXhOaEEs0eNYmpZgtZrDdKfTC2nLXZeSEzWbDErO/1bq0UQWaMxNWbP0qlScYfGIFxDlt45ZbcgTe2e/Cebq1pT58KphTJGZnLguRlpvkyY4TFqcRJFzLPqUhg3j9wiH" +
                "xkWSbvhX3QKSvYidMlWEsjjnh94vfJkHBBZJGw4oB4rxbt1z+GMOtKJiDQe8kGKClAnsyNxgMK7mRJVlyc1OLxOZOKDQaDjSK/QVGvV53/LKPigcxKreGA2l1FlbPaUXq9j/IrK7hYELlkAk3ukzXoRx+gmZJkaXZyoYuM/J1MATe561wfpX5g" +
                "a8Bh6Hlh2OlsZ2eR6469lzjQ3Ysc5ScpwUHl7GQr8tu/wSyesLcJIKyLkgHg1W4/80Sn4JDbEfD+MhcK8e1c5rs7C6eartU1QGYntwJubw3ZYv6LQsBxUIHjKc1oQuTcXmGuCKNqfUL+iddOMY0eTQS1PnsPEHr3DvgqURKGJLo7EWU+M6770k" +
                "t/djb3xr0e2m/EzwvORAKXHJbs+ad9YcYbtgiiosD1vF7vHRUx21+e4BDJpebNTom2oDFXshSaFLLMmNZP5NAYdm4n3FQT7p1mgZE3DMCgn6UFnhaLMmdCH4odb8X+HLKQTEH00ysG2CDcgDYssKdgcMZzyGXC52uSypV5YUTAySVcLTO6Zkkb" +
                "WmCc3+u9IXPyZqv2Ry0XiwxPAib+rXn577h8iMt/31vOFwKC9Xar4FdxVibGz/JlxmOuiRRMtYg+BchuU9gF/FthFCUxuCCELbO7nkdWAH1C6HLJaEH43MQJmiSQIkDyAWwvUhY/bPGZFXKlWPFGAawXRjJAq+kM8cxApLBwSpSJq/KWA4Yw4m" +
                "Hoq1IVFWubroxNKyrQjV+abdsjzEbZNRw8xHG1pAUiDoHw5BVKdvKeOwhGbjge+8NRi1I3xGO+q0LxfyWeg6g1foFH5ywEq5vWT1zfC7frVcrnNpLwnC5YLed6PocIs2u7b213B/64St6+32LlOPAnWy2lYqgD/5cLZkITHGdlOx7SyibnJ5vo" +
                "sOUUCISxBX6442ZjvND38dpfSRs4NTi/qj0kkeUMrhd4SIypLIYb8YCsB64yFSXNsjWYuCw1+Uqzkqk9E8edJoKFwcbEqNnEiRtEcKpTucmxCTIWvq/PHYTdkATjWmEIJKTRD/f5lj8KYHZLbIqGXmBd/alRp16NwusZrD8M/UnQotjdSd9Nc0" +
                "YRhD7MnYzAHYxZoUysyCDyobV41si1IHaYzGj8fIcJNwvuVm7y5G4M27q84UjS5lGIm6XJ7HjPQL2AFIGPi4wqDMhmPJaFgHsB6V5VVQQMiTlfPMlO/5GZIQj/W4yBv0NvOPLD8Fs3sfG8qJHYHKBKTSjVE8k5S+lXLgvV612kVCw2kACTxY3T" +
                "DRUC2ptlEPxNaMpJvI4zjCBAAhYpDnHIM0KzQtBsA5d6Bc/ZO6UYbYFc0XnctXVvkAjYrdThvn3hpYgLhumXZmedkzVTUHDh/6/YkNeUE/0D+ffWhOINbhYF9sjggVXoxnaJaiyYhg76T3uxc2Oqm5Ydv5yWIrsJOshT2E78oh2L21h9t6M31V" +
                "xvBQQXDdnD1M5qJxjYF5aKgPOsrc/V/m8ZCs2WKcXQlfKFyZAwhcBFfQVOfhhBEsmALd1QLmZy9d2bDH446vWOp2w0DiDRNCuDcuADhSx+NI2eN4qCUWNm9sNJBegHmrAjqAujceQ30js/cnGDR9E0idzRpFmRVyF5HG0didyPoLC5gYtxJG1t" +
                "bMVHInWHnZ9jHdomYTYpBGb0U4Fl4ZH0GSwbCa0fAX+/WNOjqYMLDm7ZrA5iyjlw3yOpCxDOYcvxQUD5VBbY1zuSQltUNSuEoPI55fpV+oBdY6EWDqtiaPP0Ze/JWhC6k7EbtDjQBBGmUBMfZ8dhAFVeWwnpjjybnHLsJT0pdGyhZssdTFWHKq" +
                "QDmet7APcCjjps7r0doqjfKU+8f9QFJZdivtcE7eywi4RjB4xMmQa28F5QBBqbSglX5veMIl2gyrRS4EwKLktFlIZ9qX7dBMxJuayriPsnUqtOirRvl1kJpKOqKzqJqO3homEVNaTb0mTFdSqxRKm9gev+7gcBkkoNxg/IJdL6GfaFVL2rpA+z" +
                "Fc5A8YRie0qRFYTsQadN7x+XYAEohLSc4kqDEEK2pbNIr3ad9L5aBVjYNjh2sQRhCJOdkncz+Hyj1/40xaQpL40+uTQVHQRl9mhf68IOmJaV2Fp0X8kCa4wZUBlQY4eFCRu41U46jTqHkFEuFXbnWHWoCDMttQS6yWNzqWfUNDTARYy6HTfAsn" +
                "SZIGHdtvYzSW2PjcwLmVfwVXr+H196x2blglwxUWLth+2/Tf3G4dYUaC8I3lFqVrSoBPcv4xQ9h8aaf2W1P+2Y0hw8Oqm8qewvjKKyMLY9q8LhtJoKDtRq636c9HRh1dG1Xw3rbzu/iHR30QEnvgEwBH4J0LIaqSQ41nEOYIRfjxLgQVX/Ew8Y" +
                "jYWlz+1wTHn0TsYPU44lUth3zs1tqQLtOHyG5iepTKQ50IgKomEVTH3ftCOdKbgj1orYw9oE7J2xD1Lo6tkdRDuNh2cvpksaAyx732s2Y88s8d3aENfYgSc73Xzo4kxdC/Opq/pe8xefJRVj4h0AAA==");
            QoLBar.allowImportHotkeys = prev;
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
        }

        public bool ToggleBarVisible(int i)
        {
            if (i < 0 || i >= bars.Count)
            {
                QoLBar.PrintError($"Bar #{i + 1} does not exist.");
                return false;
            }
            else
            {
                bars[i].ToggleVisible();
                return true;
            }
        }

        public bool ToggleBarVisible(string name)
        {
            var found = false;
            for (int i = 0; i < bars.Count; ++i)
            {
                if (Config.BarConfigs[i].Title == name)
                    found = ToggleBarVisible(i) || found;
            }
            if (!found)
                QoLBar.PrintError($"Bar \"{name}\" does not exist.");

            return found;
        }

        private void BackupFile(FileInfo file, string name = "", bool overwrite = false)
        {
            try
            {
                if (file.Extension != ".json")
                    throw new InvalidOperationException("File must be json!");

                if (string.IsNullOrEmpty(name))
                    name = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");

                var path = Config.GetPluginBackupPath() + $"\\{name}.json";
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
    }
}
