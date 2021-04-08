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
                ImGui.SetNextItemWidth(27 * ImGui.GetIO().FontGlobalScale);
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

                if (i > 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(QoLBar.Config.ExportOnDelete ? "Cut" : "Delete"))
                        QoLBar.Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!" +
                            (QoLBar.Config.ExportOnDelete ? "\nThe bar will be exported to clipboard first." : ""));

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
                AddBar(new BarCfg { Editing = true });
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
            if (ImGui.Checkbox("Export on Delete", ref QoLBar.Config.ExportOnDelete))
                QoLBar.Config.Save();
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref QoLBar.Config.UseIconFrame))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("This option will invert the ' f ' argument for all icons.");

            if (ImGui.Checkbox("Always Display Bars", ref QoLBar.Config.AlwaysDisplayBars))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Bars will remain visible even when logged out.");
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox("Disable Condition Caching", ref QoLBar.Config.NoConditionCache))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("Disables the 100ms delay between checking conditions, increasing CPU load.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Opt out of Dalamud settings for hiding UI");
            if (ImGui.Checkbox("Game UI Toggled", ref QoLBar.Config.OptOutGameUIOffHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            if (ImGui.Checkbox("In Cutscene", ref QoLBar.Config.OptOutCutsceneHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            if (ImGui.Checkbox("In /gpose", ref QoLBar.Config.OptOutGPoseHide))
            {
                QoLBar.Config.Save();
                QoLBar.Plugin.CheckHideOptOuts();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Temporary settings, ENABLE AT OWN RISK");
            ImGui.Checkbox("Allow importing conditions", ref Importing.allowImportConditions);
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
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

            ImGui.TextUnformatted("Chat UI Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.Plugin.uiModulePtr.ToString("X")}");
            ImGui.NextColumn();

            ImGui.NextColumn();
            ImGui.TextUnformatted("Game Text Input Active");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.Plugin.textActiveBoolPtr.ToString("X")}");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.Plugin.GameTextInputActive}");

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

        private void AddDemoBar()
        {
            var prev = Importing.allowImportHotkeys;
            Importing.allowImportHotkeys = true;
            ImportBar("H4sIAAAAAAAEAK1ZaXPbNhD9KzDZmbRTWuUhUce3RDmcTlNn7GQyaZ3JQCQkoiYBlgAtq53+9+4CpI5YPsh2kijigYfFnm9Xfzvf6U3JnJnDHM9ZhM7s7+0duPIcAV9OX756d34KF+oXZ/b77gXVvoDPyYvnF3CpnVnoOUn+" +
                "3JkFnsPPYYE/8D349wVuX5ZwPfGGX/7xjsC8JbQglGh2q0lCNVvJamMhg/v3nstcViw1q1z3g10ryIKRpHnyR600yfk1I3CfJ1LsienMhuF0OA2i8bSzuO8rphQpGFnKihR1rnmZM8KSTCp4I4E3fsILcsbyXF4Je6EzVrETp/deWp" +
                "odSOCR0CMR4YLIKmXVwY5Bu1vYfongedHDJAdnpKSiIpWFkeBgw8+yJmsuTtrt8DqXig0GA7tx2HVj1228YZFTcU0WtdbGcL08StWL1p9c90zesIqLFZElE3g2j2xAXHQamitJSnPkua5y8iO5BIXLrQ9ew2cUBKPR/e44m8W+Pwm+" +
                "cbHxJByNotjvKv8bKcF/5eJki3f/+i93H35yZrBlMrf/bV+F72fOTFc1u2dR6Jv3z+1LT9SyjS2yU/RHhVrGmwrcVGlGUyKXNry5IoyqzQn5GYNTMY2BQQS1Hn4FagyuHPBsoiTckhg1RNTFwjj6wwlhNlsGE9c9B+sa0GXFmUghAS" +
                "jd+qy91dzpZhFAj4d+ELjuSyoS1iKmeEEKqXkPL0WJUeR3shaaVLLOmdZbaJqAPuyji+bRldNjjziIhiD2r9LaiVarumBCH+aq5+LwKVlJpiCZ6jUDdaJdwCyQBKy5jEHQYIVM+XJDMrk2DwwEmDjlqszphqW91Azyhq77jCzJs508" +
                "32u5WuVoVfCVH3rgfkXcCHG/HuDylYBqsVMLuGyS16lx4aLMecI1kYKpPnuegt4/ZKAQ+LvGAkAScHtIpEZRbkDWYNiU0LJktCJ8aRISTVMoXrDlgYHmGUuud0peVHKtWPVMgW3mBhPso+kCjZIDmLA2kYiGq4o+jgMZLcB83NRVKj" +
                "a2thox11SYuNrluTgY9d7kHNwJiowqaYVWgeR70oCHfuT7wyiI+oD7IzT55ybN/yVR9cJU4d+c2WjQOS/PZlM/DEY7SJXxpc1hvKCr/QIfTgAyDJ8k5gTIyA6zVgwR8VOumEmBYMhGeLTCesslDk5x6g8CDz78yVM2DUZhiHnhnbSJ" +
                "WIPHq9bUqawXEGxg6gTrYQJhcI1cCyKeiprm+QZEKWtNbmheM9WIMtlTaDA0osRP0+kwBjNdym1BTpEwcbX1L6BqfjDGOmoY5tju5w9Gu6MPJ7jhKHhajomjYGi9rt0GYGMLexr0cQz/CaX6+MpxDKkJyyYj4P8Jq5QJ3BwyE4qHZk" +
                "ESBoFsiqJxtJYg99iur6CmsO4nn53HGv4kbeGFVFcbAoBBAaIKKBB4ucqpwuRq7ieyEuDWwApUJiudgCcB8JKvtlXpT5kbBtHaZtKdAgVBfMhmgA9F7brQ9wKLAi+iHyWv0aHi+OjZ5w25IS9YRm+4rJTrzjMqVtuDypThceiWBwEv" +
                "znPIxSY7FCTZJDmGNVT5VYa3OCR8oVklaL5VgnqU5BxoPEEJIC/3NGgr6TMs9PZYbeLtA/dWJBXDEkrzWU8IzRT0cvj5qCKCzgUtPKinH0yC3SreKpTdMrhgjXkSWUC2S1X3ri16cMWXfi3ZhenB+qj2Md6MzGjYl6qNelLS+Ak66t" +
                "r3HMZ62L4Oz49L8lxAMtVQ2MwwQe0lNPvA0h0Ij42NtTbaLQuieZlRTNUZX5kiDa8Q4EGPajwcxVDtciBoF5SLhVz/x/lJOBq77lGozhEYjqAfeU2BEhzFi7rjTZujvqYpOwQbdgaLfRTuW5zOxDCMg+aQd8Hi7mDQrFyAD93FGnfH" +
                "QraKY6Nj+nqwBB6HA47zpsIO7i7atDsa8N75hh4DCzoTpjCGGHgB5PEYWvcIiCEC3tcVDuKO4HUPgxjC4FPG9R04ZKOTiT+aQlvSwnePiniKh6fQ0R2RtnNcmNnTOLDpq8DByjcs3XYTlqtjMjtG74/ktnuP/WBSvi9R4HxpeD8hu/" +
                "d0r26hMyRvxVI+mljnUqQcRzHkkmlI568ExXPi3CLlynxfUMznVJkWG1RScVkrojRke+W1A6mC1GVLVa++QW36bGmflnkNVaEhrz1Zz5nU12yD4jb0g+5Y8ZrrTCI7bk3Etbc/2IY2X8ORBuQtcs8FThFUe9bUg7cVvoHwhOKUQ5E1" +
                "pJdBT0lf3ZYgF4gBOT1DPKNNVO+OSGGt3Penq2YV6M0Oc3ESIghDldpXir6Es9hKY7+a/sh0TEYKWZrGAxIWu7WPdWVvmJGI2Ml51WCBjEY4ILLUSGdViuPI5nw9RX0BoV6XCqdBrHEWNBSttQSewRMTqwtqmmxwPSPEnnthT1WmyF" +
                "R2M+tcUjvZIctKFo2qm33+fx99yRb1irxjosbGB0dR2+aFQ2RWeArYbk8Us+IBQSDY6iRDj6SJ5jes9dN+E+99uvdwBzs3u9cVbUa2bbcJhu7MZM94ymzLa1e3WDdccZNgl0tif0RrfyD5KLL9NUei5QJ0JnBUrmVzp0FwrC8eUSX+" +
                "BJPKtYeOisOjq0MhRCvDw9NjuEpNPqbmZ4MF5OsJUKvSTrOm/nA6jGFVFE2D0I9DM9mRRu2qaaGXl7aFhjumxfZCUJtzg93aIII/Y+effwHu+aieeBwAAA==");
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
