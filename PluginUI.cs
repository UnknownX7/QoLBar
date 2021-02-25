using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            ImportBar("H4sIAAAAAAAEALVZbW/bOBL+K4y8QPdwjk/vtvOtSdttDpdNEWfRTTeHBS3RNjcS6ROpOr5F//vOkJJfEktK7lwgiPXC4QwfDmeeGf3p/KDXS+acOVOn79xyneH16bv3V9en8GCykIVOSv0vrrRz9tufm9EKXv5Mc7zE" +
                "seT87Q3Km5dB37lMpLjlAmS2IiEM+NU58wZu37mrfr9Uv5/NrxuMfS8Ovn3rH1R0SWhOKNHsUZOEajaXxXqj1Adjy2mbnRcykwVLjXyvd2tnEWTKSFK9+aNUmmT8gRF4zmEJr1ySO/BGdg3e06U1relTwZQiOSMzWZC8zDRfZo" +
                "ywZCHVRjlMdiHznIoUBP6B78hHlmXyXtgbvWAFO3G6NGhp5iVen/h9EhAuiCxSVrTp8Wodfn0RwPArmeLwlyyJkgLmk7lR3abpTpZkxcVJrQfvM6nYYDDYaPQbNPZ6lWtMMyoeyLTUGvau3YtUOa19qNf7KL+ygos5kUsm0PY+" +
                "WYN+dA+aKUmWZkkXusjI38kEkJS7fvdR6ge2hlUFnhdFnX54dha77sh7iW/dvciHfpISfFdOT7ZTfvs3mMVT9jYFhHVRMgC8Wu1nnuoF+Or2CRyMMhcK8e0U890dwYmma3UNkNnBrYDbA0W2mP+iEHB8qMAVlWY0JXJmTzdXhF" +
                "G1PiH/xBOpmEYfJ4JaL74HCL17B7yXKAmPJB4AIsp8uuPM3fHg7GzmjXo9tN9MPys4EymcfxDZ80/7wjxvWCJMFYeu5/V676hI2L54io9ILjVvdkq0BY25kqXQpJBlxrR+Mg9NYOV2wE014N5pnjD2ghAM+lla4GkxL3MmtHKe" +
                "n723Yn8MmUumIDDqFQNsEG5AGwTsLhiccR9ymfLZmizkyrwwU8DOpVwtM7pmaRtaYJzf670hM/Jmq/ZHLefzDHcCBv6tWfx3FA9Q/Pc9cT4XEMe3SwW/SrIyNX6WLzOecE2kYKpl6lOA7HYBq4C/FcZVkoALQvwyq+t5ZAXQp4" +
                "Qul4wWhM9MnKBpCtkDZj6A7cWCJQ9bfKaFXClWvFEA64WZGaDVdIp4ZjClsHBKnBOl8pYNhjDiYeirshgVa5vJjE0rKtCNX5qI2yPMRtk1bDwEdLWkBSIOgfDkFUp2UqI7CIduNBr5wbBFqRvhNt9Vofi/EvdB1Bq/wK1zFoHV" +
                "TfJj1/eirbxa8JmNJDync1bPcz2bQYTZtd23trsDf1Tldd9vsXIU+uOtllIx1IH/5ZyZ0ATbWdmOm7Sy6frJIjpsOQVuYWyBC3fUbIwX+T4e+ytpA6cG51e1h6SynMLxAg9JMJUlcCIekBDBUaaipFm2BhOXpSZfaVYytWfiqN" +
                "NEsDDcmBg3mzh2wxh2dSI3OTZFDsP39bmDqBuScFQrjICNNPrhPhXzhyGMbolNceCF1tWfGnXq1Si8ntz6QeSPwxbF7k76bhozjCH0Ye5kBM5gwgplYkUGkQ+txb1G0gWxw2RG4+c7JLl54m7lJk/uxrCtyxuOJG0ehbhZmsyO" +
                "5wzUC0gReDvPqMKAbJ4nshBwLiDdq6rAgEdixudPstN/ZGYIwv8WY+A38IKhH0XfuomN58WNxOYAVWpCqR5IztmCfuWyUL3exYKK+QYSYLK4cLqhQkB7swyCvwlNwJQSOLg85xk1gYMSIUXKZhRqA1sgZFywDW6v4Dt7u5WgTZ" +
                "AzOre9tvINEgK7pDrstwteiqRgmIZpdtY5WDMFNRn+f8WCvKbc6B/Iw7cmJNewKYsCe2RwY+I1h6BoRVRjKRU46Eft1c+NKXdaVvxyeoosJ+wgUVE7AYx3LG5j990O31R7vRUQZDRkEVNeq52gYF9YSgLOs7Y+V58Dy1RotlxQ" +
                "dPMFn5tMCUMIHNhX4ORHMSSTDFjTDeViKlffvQ/hR8Ne73jKhqMQEk6zMigLPlDI5kfT6HnDOBw2Zmg/GleAfqApO4K6KB7FfiPN82MXF3gUTePYHY6bFXkVksfR1pHQ/RgKnBs4GEfS1sZafCRUd9gTOtambRJnk0JgSD8VWB" +
                "4eSZ/BspHY+jHw+Is1PZo6OODgls3qIKacAwc+kroQ4Qxatg8CyqeywNbfkRTa4qpZIQSVzwuuX6UPWDYWbFFQFUWbuy97d9aCyB2P3LDFgcaIMIXa+DgrjkKo9tpKSXfo2eSUY0/pScFjCzZb9mCqOlQpHchc3wO4F3DVoLkH" +
                "d4iqfqc88f5RF5RcitleM7SzCS9Sjp0wMmEa2MJ7QRFobC6lXJnrKUW6QJVpqcCeFFyWiigN61L9uhmYk3JZVxP3T2atOirSvl1mJZCOqr7oJKK2l4uGVdSQbkuUFdcLiaVK7Q1c93e/GZCF1GD8gFwivZ9if0jVq0r7MFrhCJ" +
                "yeUGxTKbKCkD3otOn94xIsAIWQlhcoaRBCyLZ0FunVrpPeV1KAhW2HYzdLEIYw2SF5N4PPN3rtpSkqTZlp9MmlqewgKLNH+1oX9oFpXYmtRffVXGCNMQMqA2rssDBhI/dpPdNk1DmEjHKpsEvHqk1FmGmpJdBNnphDPaWmsQEu" +
                "YtTtuAGWp8sUCeu2xZ9JanttZFbIvIKv0vP/+NI7Ni3n5IqJEmtAbAPCEWSFoBlczGSB9sLEO0qNRItKcP8yWaDnQJXIv7Lan3ZMaQ4enVTeVPgXRlFZGNueVeOwW00FB2q19T8OOvylB2aoWrz2C2P9secXsdiVPuDNN4CKwE" +
                "8DWlZPqhkc60EHwMLPSSkQoqohijuNVoPoczscUye9k8nDhGOtFPWdc3Nsqog7ip7B+kkqE3IOdKbCOKiiqu+b/qQzAb9ENLCptYncO88+SKGre3cQ73Qinr2YLGkCsOx9wNk8e2aJ79aGuMYO3OLJ5ssXZ+pamG9f1QecvwA3" +
                "WO0HDh4AAA==");
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
