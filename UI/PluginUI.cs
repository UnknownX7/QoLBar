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
            ImGuiEx.SetItemTooltip("This option will invert the ' f ' argument for all icons.");
            ImGui.SameLine(halfWidth);
            var _ = QoLBar.Config.UseHRIcons;
            if (ImGui.Checkbox("Use HR Icons", ref _))
            {
                if (IconBrowserUI.cleaningIconsOnClose)
                    QoLBar.PrintError("Please close the Icon Browser to change this option.");
                else
                {
                    QoLBar.Config.UseHRIcons = _;
                    QoLBar.Config.Save();
                }
            }
            ImGuiEx.SetItemTooltip("Loads the high resolution icons instead. Be aware that the Icon Browser will use\n" +
                "up to 5GB of memory until closed if you open the \"Spoilers\" tab!\n" +
                "The Icon Browser may need to be closed in some cases to toggle it.");

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
            ImGui.TextUnformatted($"{QoLBar.Plugin.uiModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.NextColumn();

            ImGui.TextUnformatted("Rapture Shell Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.Plugin.raptureShellModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.NextColumn();

            ImGui.TextUnformatted("Rapture Macro Module");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.Plugin.raptureMacroModule.ToString("X")}");
            ImGui.NextColumn();
            ImGui.NextColumn();

            ImGui.TextUnformatted("Game Text Input Active");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.textActiveBoolPtr.ToString("X")}");
            ImGui.NextColumn();
            ImGui.TextUnformatted($"{QoLBar.GameTextInputActive}");

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
            ImportBar("H4sIAAAAAAAEAK1ZbZPbthH+KzDVGSdTWSYpiSfpm322c+7UtefsTCbteTIQCUmoSYAhwJPVTP57nwVIvdi6F7L94LMIAruLfX12+UfwF7srRbAIRDAMlnGw+GO/gqdhoPDj2avX794/w4P5e7D412GDaTfQe/b" +
                "yxTUebbCIh0GavwgW0TCQ73EgHIVD/PuM5Y8lnmfDyec/h2fIvGW8YJxZ8dWylFux1tXOk4zu5n2pc12JzJ0aDD75s4otBUubN/+ujWW5/CIY1mWq1ZGYwWISzyfzaHwx7yzuh0oYwwrBVrpiRZ1bWeaCiXSjDXak2PGc" +
                "HtiVyHN9o/yD3YhKPAl687LacWDRkMVDNmZSMV1lojrhGLXc4vbHGO+LHiY5uSNnFVeZLpwEJwx/1TXbSvWkZUfPuTZiNBp5xnFXxoNB4w3LnKsvbFlb6wzXy6NMvWz9aTC40reikmrNdCkU3W3IdhCXnIbnRrPSXfnSV" +
                "jn7K/sIhesHfXCxSMJwFn3jVxezeDodJ2FXoX/SGk6rl0/29O4+//n7l78EC7BML/1/+634fRUsbFWLs0xffxVpbQX8KZO3Mqt5zgqeVpoNwtbSz9mnjTSwc54zcfd2+MDzIuxsqlYAs+EUtI9h/s1WxzgK72d9XmFx6H" +
                "T13ivokW7lkwk7eNbPhtyKFg0UY6zgGdMrn88gu+Bm94T9jbKREZYyAVPch/QNXCi6CRDKzGgsaUoTTNXF0kX2Q963imaDwXu4syO6qqRQGTKesa32/FKz0s0uoJ5MwigaDF5xlYqWYkYPrNBW9ghLkphEfqdrZVml61x" +
                "YuyfNU+jDv7puXt0EPXgk0XgCsf+hvZ14ta4Loexpcn6hTt+ytRYG1cNuBdRJdoFZkPW8uZxByGCFzuRqxzZ66144EjBxJk2Z853IeqkZ8saDwVO2Yk8P8vxg9Xqdk1XhKz/2oPsb0R0T3d9O6Mq1Qnk8qAUum+Z15ly4" +
                "KHOZSsu0EqYPz2fQu4tXClmqeCyF26NyOEUNIraFYTPGy1LwismVy8A8yxDNYHlioMuNSL8clLys9NaI6qmBbS4dTdjH8iUZJQcx5W2iiRqdKvo4DrJ5RAWoARJc7TyYcGJuuXJxdcjxSTTtzeQ93AlV1ZRIZbAKqs2Th" +
                "ngcjsNwMo7GfYiHUzL5r01d+48m1SsHO/4ZLKajzjVpsZiHcTQ9kDQbufI5TBZ8fYxo4hlIxvGjxJwBfR1o1kYQRfqr18KlQBiyEZ6ssN2Dp5NbPAtH0RB/wtljmEbTOKa88E77RGzh8aY1dabrJYINpk4JAKQIgy8ELh" +
                "HxXKHK5TuIUtaW3fK8FqYRZXak0GjiREkep9NJAjN91HsEkhFClGbvX8CmYXRBGMJB6gvPLxxND1efzIjhNHpcjknG0cR7XcsGZBNP9lnUxzHCR8CU8ycvEqQmKpso5SVPRWVc4ObITCQemYVQJwLZFUXnaG1H0INdX0F" +
                "dYT1OPgePdYBR+8KLVFc7AEBBAVEVCgQ9rnNuKLm69VRXCm4NVGA2urKAMVhSK7neV6Xfde4QRGubWXc0E0XJKZoBFhy35+JwGHkq2Eh+lL4hh0qSs3e/bMANeyk2/FbqygwGlxuu1vuL6kzQdfgeB3mMlnuEU7B0l+YU" +
                "1qjy6w0tSSR8ZUWlgBlbJZgHQc6JxlOSAHm5p0FbSZ9SoffXahNvH3JvVVoJKqE8X/QkYYVB80p/H1RE1LmgxSf19JNLsHvFe4W2iNqbJ9UFsl1murep44ewd58e9No1nX1U+2DXBmQ06QvVpj0hafIIHXXt+U5jPW634" +
                "/15SV4oJFOLwuamJ+YoofkXHu4gPHY+1tpo9yiI5+WGU6reyLUr0tjCgIMe1Hg8TVDtcgC0ay7VUm//x4FRPL0YDM6S6hyB8RT9yBsOSHCW3rg7vXlz1Tc8E6fEJp2JJSEJ9y2dzsAwTqLmkt8TS7oTQ7NyDR/6ntZFd1" +
                "qEVmlOdk5f95bA8+SAcX6qqIP7ntq8OzXg3ssdP0cs6gyY4gQx8BLg8Ry17hGQIAI+1BVNHs/Q6x4GCcLgl42035EjNDqbhdM52pKWfPeoSOZ0eY6O7oy0nePCzd0uIp++ChqsfIPSfTfhsTols3Pw/kxuu/Pa9ybluxI" +
                "FzZcmdwOyeyZi6AzZW7V6eAB5qVUmaRTDPgqLdP5acbonzS0yadzvJad8zo1rsaGSSuraMGOR7c2wHUgVrC5bqHrzDdWmz9b+bZnXqAoNeO2Jeq60/SJ2JG4DP/gBFW+l3WhCx62JpB0eT/LR5ltcacTeEvZc0hTBtHfN" +
                "hthtaAeRZ5ymHIZtkV5GPSV9/bWEXBADOZ2Gjl6bpN4DkKJaeexPN80p6M1Pr2kSopgglfotRV/AWeyl8T9df+Q6JieFLl3jgYQlvvrXtvILbiSiDnLeNLQgoxMOQJY76bxKaRzZ3K+nqC8R6nVpaBokGmchQ/HaauAMm" +
                "bpYXXLXZMP1nBBH7kU9VZkRUjkM6XPN/WSHrSpdNKpu+Pz/ffSVWNZr9k6omhofGkXtmxeJyKzoFmB3JIo7cY8gCLY63ZBH8tTKW9H6aU8BP0iktudALZmETCSoOZ2oeFMSO1ZKNGzYQV6Cttob2rOnqKegaQci/aVh14" +
                "L8LSW9QBQnn2sJm+Eo2RnYMfFe6HLTIYhKwEny1aFzVr9z7FYM+yFue0wimQlR/tjvC8kxRL6/6790Fqsr3oy52w4dGu2M/q9kJvyYwJ9uad1KI11RWq2Y/9LafkX7WW2Oz5zJMNfwM0WfF6xuVhoKgY/fM+5H3+kyvR1" +
                "ScJN73JwKoVoZ7p+44ylzNYy7z0xL1LgZ4GjpJ4DzcDKfJDg1Hs+jOExiNw3TTu2mGTusPvqxA1bcWGIYQ23BLYGEEUiMwuDP/wKRVHCHnR4AAA==");
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
