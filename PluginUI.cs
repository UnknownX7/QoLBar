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
                bars[i].BarConfigPopup();
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
            ImportBar("H4sIAAAAAAAEALVZbW/bOBL+K4y9QPdwjk+SJfnlW5O22xwumyLJoptuDgtaoi1uJFInUnF8i/73nSElv6SWlNy5QNFYFDkzfDiceWb0Z+8Hvc5Zb9ab9wa9W65T/H367v3l1SkM3CSy0FGp/8WV7s1++3MzW8HLn2mGP3E" +
                "uOXt7jevNy9GgdxFJccsFrNku8WHCr72ZO3QGvbvq75fq72fz1xlNPTccff06OKjogtCMUKLZkyYR1Wwpi/VGqQfGlvM2O89lKgsWm/X9/q2VIsickah680epNEn5AyMwzmELr9ySM3Qndg/u86017elTwZQiGSMLWZCsTDXPU0ZY" +
                "lEicdC6zjIoY5v0Dh8hHlqbyXtgHnbCCnfS6BGtpxBF3QLwBGREuiCxiVhwQ79aivfrHCGZdyhgEui/ZACUFyJOZ0XhAwZ0syYqLk1o8PqdSseFwuFHkNSjq96vzn6dUPJB5qTUcULurqHJeO0q//1E+soKLJZE5E2jygKxBP/oAT" +
                "ZUkudnJuS5S8ndyA7jJXef6KPUDWwMOI9cNgk5nm81Cx5m4L3Gguxc5yk9SgoPK+clW5Nd/g1k8Zm9jQFgXJQPAq91+5rFOwCG3I+D9ZSYU4tu5zHN2Ft5oulZXAJmd3Aq4vTVki/kvCgHHQQWOpzSjMZELe4W5Ioyq9Qn5J147xT" +
                "R6NBHU+uw9QOje98BXiZIwJNHdiSizuXHdl1762WzhTvp9tN+IXxSciRguOSzZ80/7wow3bBFEhb7juv3+Oyoitr88xiGSSc2bnRJtQWMuZSk0KWSZMq2fyaER7NxOuK4m3PeaBYbuyAeDfpYWeFosy4wJfSh2vBX7c8hSMgXRT68" +
                "YYINwA9qwwJ6CwRnPIZMxX6xJIlfmhREBJxdzlad0zeI2tMA4r99/QxbkzVbtj1oulymeBEz8W/Py33H5CJf/vrecLwUE6+1Wwa+itIyNn2V5yiOuiRRMtYg+BchuE9gF/FthFCURuCCELbO7vktWAH1MaJ4zWhC+MHGCxjGkCJB8" +
                "ANvzhEUPW3zmhVwpVrxRAOu5kQzQajpHPFMQKSycEmXiqqzlgCGMuBj6qlRFxdqmK2PTigp045dm2/YIs1F2BQcPcVzltEDEIRCevELJTt5zhv7YCSYTbzRuUeoEeMx3VSj+r8RzELXGL/DYmwVgddP6qeO5wXa9SvjCRhKe0SWr5" +
                "VwtFhBhdm33rO3O0JtUydvzWqyc+N50q6VUDHXg/3LJTGiC46xsx0Na2eT8bBMdtpwCgTC2wA9n0myMG3geXvtLaQOnBudXtYfEspzD9QIPiTCVRXAjHpD1wFWmoqRpugYT81KTR5qWTO2ZOOk0ESz0NyaGzSZOHT+EU72RmxwbI2" +
                "Ph+/qcYdANiT+pFQZAQhr9cJ9veWMfZrfEpnDk+tbVnxt16tYovJ7BeqPAm/otip2d9N00ZxxC6MPcyQjcwYgVysSKFCIfWotnjVwLYofJjMbPd5hws+Bu5SZP7sawrcsbjiRtHoW4WZrMjvcM1AtIEfi4TKnCgGzGI1kIuBeQ7lV" +
                "VRcCQWPDls+z0H5kagvC/xRj4O3JHYy8IvnYTG9cNG4nNAarUhFI9kZyxhD5yWah+/zyhYrmBBJgsbpxuqBDQ3jSF4G9CU0aidZRiBAESsExwiEOeEZoVgqYbuNQreM7eKUVoC+SKzuOurXuDRMBupQ737QsvRFQwTL80nXVO1kxB" +
                "wYX/v2JDblNO9A7k31sTije4WRTYE4MHVqEb2SWqsWAa9dB/2ouda1PdtOz45bQU2Y3fQZ6CduIX7ljcxuq7Hb2p5norILhoyB6mdlY7wcC+sFQEnGdtfa72f8tQaJonFENXwpcmQ8IUAhf1FTh5QQhJJAW2dE25mMvVd28yeMG43" +
                "z+esvHEh0TTrAzKgQ8UsvjRNLruOPTHjZnZC6YVoB9ozI6gLggnoddI77zQwQ0eRdM0dMbTZkVuheRxtHUkci+EwuYaLsaRtLWxFQ+J1B12fo51aJuE2aQQmNFPBZaFR9JnsGwktF4I/P18TY+mDi44uGWzOogpZ8B9j6TORzhHLc" +
                "cHAeVTWWBf70gKbVHVrBCCyueE61fpA3aNhVowqoqhzdOXvSdrQeBMJ47f4kBTRJhCTXycHQc+VHltJaQzdm1yyrCX9KzQsYWaLXcwVR2qkA5kru8B3As46qi593aIon6nPPH+SReUXIjFXhO0s8MuYo4dMHLDNLCF94Ii0NhUirk" +
                "yv+cU6QJVppUCZ1JwWSqiNOxLDeomYEbKvK4i7p9JrTop0r7N0xJIR1VXdBJR28NFwypqSLelyYrrRGKJUnsD14PdDwIkkRqMH5ILpPVz7AupelfxAGYrnIHiCcX2lCIrCNnDTpveP+VgASiEtJzgSoMQQrals0ivdp30vloFWNg2" +
                "OHaxBGEIk52SdTP4bKPX/jTFpCkvjT6Zm4oOgjJ7sq91YQdMy0psLbqvZIE1xgyoDKixw8KEDdxqJ51GnUHIKHOF3TlWHSrCTEstgW7yyFzqOTUNDXARo27HDbAszWMkrNvWfiqp7bGRRSGzCr5Kz//jS+/YvFySSyZKrP2w/bep3" +
                "zjcmgLtBcE7Ss2KFpXg/mWUoOfQSPNHVvvTjinNwaOTypvK/twoKgtj2zdVOJxWU8GBWm3dj5OeL3zkipuIulgQ++mw/sDzi0h2Vx7w5GtARODnAC2rkUpCz3rPAaDwE1IsVwN0LWzE3e8bIWobeqY+eiejhxuONVIw6J2Z61JF2k" +
                "nwDZyfpDKh5kAnyg9HVTT1PNOP7N2AP2KxiE2sTcTeGfsgha6enWG403n45sVNTiOAZO+DzWbsG0s8pzbEMXbg0d5svnRxpq6E+dZVfbD5C5JpTb3jHQAA");
            Importing.allowImportHotkeys = prev;
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
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
    }
}
