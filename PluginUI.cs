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

        private readonly QoLBar plugin;
        private Configuration config;

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

        public PluginUI(QoLBar p, Configuration c)
        {
            plugin = p;
            config = c;

            bars = new List<BarUI>();
            for (int i = 0; i < c.BarConfigs.Count; i++)
                bars.Add(new BarUI(p, c, i));

            Task.Run(async () =>
            {
                while (!p.pluginInterface.Data.IsDataReady)
                    await Task.Delay(1000);
                DisplayConditionSet.classDictionary = p.pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>(p.pluginInterface.ClientState.ClientLanguage).ToDictionary(i => i.RowId);
            });
        }

        public void Reload(Configuration c)
        {
            Dispose();

            config = c;

            bars.Clear();
            for (int i = 0; i < c.BarConfigs.Count; i++)
                bars.Add(new BarUI(plugin, c, i));
        }

        public void Draw()
        {
            if (!IsVisible) return;

            if (IconBrowserUI.iconBrowserOpen)
                IconBrowserUI.Draw(plugin, config);
            else
                IconBrowserUI.doPasteIcon = false;

            if (config.AlwaysDisplayBars || QoLBar.IsLoggedIn())
            {
                foreach (BarUI bar in bars)
                    bar.Draw();
            }
            lastConfigPopupOpen = configPopupOpen;
            configPopupOpen = false;

            if (configOpen)
                DrawPluginConfigWindow();
        }

        private void DrawPluginConfigWindow()
        {
            ImGui.SetNextWindowSizeConstraints(new Vector2(588, 500), ImGui.GetIO().DisplaySize);
            ImGui.Begin("QoL Bar Configuration", ref configOpen);

            if (ImGui.BeginTabBar("Config Tabs"))
            {
                if (ImGui.BeginTabItem("Bar Manager"))
                {
                    Vector2 textsize = new Vector2(-1, 0);
                    float textx = 0.0f;

                    ImGui.Columns(3, "QoLBarsList", false);
                    for (int i = 0; i < bars.Count; i++)
                    {
                        ImGui.PushID(i);

                        var bar = config.BarConfigs[i];

                        ImGui.Text($"#{i + 1}");
                        ImGui.SameLine();

                        textx = ImGui.GetCursorPosX();

                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputText("##Title", ref bar.Title, 32))
                            config.Save();

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
                        var preview = ((bar.ConditionSet >= 0) && (bar.ConditionSet < config.ConditionSets.Count)) ? $"[{bar.ConditionSet + 1}] {config.ConditionSets[bar.ConditionSet].Name}" : "Condition Set";
                        if (ImGui.BeginCombo("##Condition", preview))
                        {
                            if (ImGui.Selectable("None", bar.ConditionSet == -1))
                            {
                                bar.ConditionSet = -1;
                                config.Save();
                            }
                            for (int idx = 0; idx < config.ConditionSets.Count; idx++)
                            {
                                if (ImGui.Selectable($"[{idx + 1}] {config.ConditionSets[idx].Name}", idx == bar.ConditionSet))
                                {
                                    bar.ConditionSet = idx;
                                    config.Save();
                                }
                            }
                            ImGui.EndCombo();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Applies a condition set to the bar that will control when it is shown.\n" +
                                "Useful for making groups of bars that all display at the same time.\n" +
                                "You can make these on the \"Condition Sets\" tab at the top of this window.");

                        ImGui.NextColumn();

                        if (ImGui.Button("↑") && i > 0)
                        {
                            var b = bars[i];
                            bars.RemoveAt(i);
                            bars.Insert(i - 1, b);

                            var b2 = bar;
                            config.BarConfigs.RemoveAt(i);
                            config.BarConfigs.Insert(i - 1, b2);
                            config.Save();
                            RefreshBarIndexes();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("↓") && i < (bars.Count - 1))
                        {
                            var b = bars[i];
                            bars.RemoveAt(i);
                            bars.Insert(i + 1, b);

                            var b2 = bar;
                            config.BarConfigs.RemoveAt(i);
                            config.BarConfigs.Insert(i + 1, b2);
                            config.Save();
                            RefreshBarIndexes();
                        }
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
                            if (ImGui.Button(config.ExportOnDelete ? "Cut" : "Delete"))
                                plugin.ExecuteCommand("/echo <se> Right click to delete!");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!" +
                                    (config.ExportOnDelete ? "\nThe bar will be exported to clipboard first." : ""));

                                if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                                {
                                    if (config.ExportOnDelete)
                                        ImGui.SetClipboardText(plugin.ExportBar(bar, false));

                                    bars.RemoveAt(i);
                                    config.BarConfigs.RemoveAt(i);
                                    config.Save();
                                    RefreshBarIndexes();
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
                        AddBar(new BarConfig());
                    ImGui.NextColumn();
                    ImGui.NextColumn();
                    if (ImGui.Button("Import", textsize))
                        ImportBar(ImGui.GetClipboardText());
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Import a bar from the clipboard,\n" +
                            "or import a single shortcut as a new bar.");

                    ImGui.Columns(1);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Condition Sets"))
                    DisplayConditionSet.DrawEditor(plugin, config);

                if (ImGui.BeginTabItem("Settings"))
                {
                    if (ImGui.Checkbox("Export on Delete", ref config.ExportOnDelete))
                        config.Save();
                    ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                    if (ImGui.Checkbox("Resizing Repositions Bars", ref config.ResizeRepositionsBars))
                        config.Save();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Undocked bars will automatically readjust if you change resolutions.");

                    if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref config.UseIconFrame))
                        config.Save();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("This option will invert the ' f ' argument for all icons.");
                    ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                    if (ImGui.Checkbox("Always Display Bars", ref config.AlwaysDisplayBars))
                        config.Save();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Bars will remain visible even when logged out.");

                    ImGui.Spacing();
                    ImGui.Spacing();
                    ImGui.TextUnformatted("Opt out of Dalamud settings for hiding UI");
                    if (ImGui.Checkbox("Game UI Toggled", ref config.OptOutGameUIOffHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }
                    ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                    if (ImGui.Checkbox("In Cutscene", ref config.OptOutCutsceneHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }
                    ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                    if (ImGui.Checkbox("In /gpose", ref config.OptOutGPoseHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }

                    ImGui.Spacing();
                    ImGui.Spacing();
                    ImGui.TextUnformatted("Temporary settings, ENABLE AT OWN RISK");
                    ImGui.Checkbox("Allow importing conditions", ref plugin.allowImportConditions);
                    ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                    ImGui.Checkbox("Allow importing hotkeys", ref plugin.allowImportHotkeys);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Backups"))
                {
                    var path = config.GetPluginBackupPath();
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
                                        config.LoadConfig(file);
                                }

                                ImGui.SameLine();
                                if (ImGui.SmallButton("Delete"))
                                    plugin.ExecuteCommand("/echo <se> Double right click to delete!");
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

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Debug"))
                {
                    ImGui.TextUnformatted("Game Data Pointers");
                    ImGui.Indent();
                    ImGui.Columns(3, "DebugPointers", false);

                    ImGui.TextUnformatted("Chat UI Module");
                    ImGui.NextColumn();
                    ImGui.TextUnformatted($"{plugin.uiModulePtr.ToString("X")}");
                    ImGui.NextColumn();

                    ImGui.NextColumn();
                    ImGui.TextUnformatted("Game Text Input Active");
                    ImGui.NextColumn();
                    ImGui.TextUnformatted($"{plugin.textActiveBoolPtr.ToString("X")}");
                    ImGui.NextColumn();
                    ImGui.TextUnformatted($"{plugin.GameTextInputActive}");

                    ImGui.Columns(1);
                    ImGui.Unindent();
                    ImGui.Separator();
                    ImGui.Spacing();

                    Keybind.DrawDebug(config);

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void AddBar(BarConfig bar)
        {
            config.BarConfigs.Add(bar);
            bars.Add(new BarUI(plugin, config, bars.Count));
            config.Save();
        }

        public string ExportBar(int i, bool saveAllValues) => plugin.ExportBar(config.BarConfigs[i], saveAllValues);

        public void ImportBar(string import)
        {
            try
            {
                AddBar(plugin.ImportBar(import));
            }
            catch (Exception e) // Try as a shortcut instead
            {
                try
                {
                    var sh = plugin.ImportShortcut(ImGui.GetClipboardText());
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

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
        }

        public bool ToggleBarVisible(int i)
        {
            if (i < 0 || i >= bars.Count)
            {
                plugin.PrintError($"Bar #{i + 1} does not exist.");
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
                if (config.BarConfigs[i].Title == name)
                    found = ToggleBarVisible(i) || found;
            }
            if (!found)
                plugin.PrintError($"Bar \"{name}\" does not exist.");

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

                var path = config.GetPluginBackupPath() + $"\\{name}.json";
                file.CopyTo(path, overwrite);
                PluginLog.LogInformation($"Saved file to {path}");
            }
            catch (Exception e)
            {
                plugin.PrintError($"Failed to save: {e.Message}");
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
                plugin.PrintError($"Failed to delete: {e.Message}");
            }
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }
    }
}
