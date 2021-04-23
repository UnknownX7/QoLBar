using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Plugin;
using ImGuiScene;

namespace QoLBar
{
    public class TextureDictionary : ConcurrentDictionary<int, TextureWrap>, IDisposable
    {
        public const int FrameIconID = 10_000_000;
        public const int SafeIconID = 10_000_100;
        public static int GetSafeIconID(ushort i) => SafeIconID + i;

        private readonly Dictionary<int, string> userIcons = new Dictionary<int, string>();
        private readonly Dictionary<int, string> textureOverrides = new Dictionary<int, string>();
        private int loadingTasks = 0;
        private static readonly TextureWrap disposedTexture = new GLTextureWrap(0, 0, 0);
        private readonly ConcurrentQueue<(bool, Task)> loadQueue = new ConcurrentQueue<(bool, Task)>();
        private Task loadTask;
        private readonly bool useHR = false;
        public bool IsEmptying { get; private set; } = false;

        public TextureDictionary(bool hr) => useHR = hr;

        public new TextureWrap this[int k]
        {
            get
            {
                if (IsEmptying)
                    return null;
                else if (TryGetValue(k, out var tex) && tex?.ImGuiHandle != IntPtr.Zero)
                    return tex;
                else
                {
                    if (LoadTexture(k))
                        return ((ConcurrentDictionary<int, TextureWrap>)this)[k];
                    else
                        return null;
                }
            }

            set => ((ConcurrentDictionary<int, TextureWrap>)this)[k] = value;
        }

        public void TryDispose(int k)
        {
            if (TryGetValue(k, out var tex))
            {
                tex?.Dispose();
                TryUpdate(k, disposedTexture, null);
            }
        }

        public bool IsTextureLoading() => loadingTasks > 0 || !loadQueue.IsEmpty;

        private async void DoLoadQueueAsync()
        {
            while (!IsEmptying && loadQueue.TryDequeue(out var t))
            {
                //while (loadingTasks > 100) ;
                if (!t.Item1)
                {
                    Interlocked.Increment(ref loadingTasks);
                    _ = t.Item2.ContinueWith((_) => Interlocked.Decrement(ref loadingTasks));
                    t.Item2.Start();
                }
                else
                {
                    while (loadingTasks > 0)
                        await Task.Yield();
                    t.Item2.RunSynchronously();
                }
            }
        }

        private void LoadTextureWrap(int i, bool overwrite, bool doSync, Func<TextureWrap> loadFunc)
        {
            var contains = TryGetValue(i, out var _tex);
            if (!contains || overwrite || _tex?.ImGuiHandle == IntPtr.Zero)
            {
                _tex?.Dispose();
                this[i] = null;

                var t = new Task(() =>
                {
                    try
                    {
                        if (IsEmptying) { TryUpdate(i, disposedTexture, null); return; }

                        var tex = loadFunc();
                        if (tex != null && tex.ImGuiHandle != IntPtr.Zero)
                            TryUpdate(i, tex, null);
                    }
                    catch { }
                });

                loadQueue.Enqueue((doSync, t));

                if (!doSync)
                {
                    if (loadTask?.IsCompleted != false)
                        loadTask = Task.Run(DoLoadQueueAsync);
                }
                else
                    DoLoadQueueAsync(); // Temporary fix to reduce nvwgf2umx.dll crashing (this wont actually run sync if any tasks are waiting/loading)
            }
        }

        public bool LoadTexture(int k, bool overwrite = false)
        {
            if (k < 0 && userIcons.TryGetValue(k, out var path))
            {
                LoadImage(k, path, overwrite);
                return true;
            }
            else if (textureOverrides.TryGetValue(k, out var texPath))
            {
                LoadTex(k, texPath, overwrite);
                return false;
            }
            else if (k >= 0)
            {
                LoadIcon(k, overwrite);
                return false;
            }
            else
                return false;
        }

        private void LoadIcon(int icon, bool overwrite) => LoadTextureWrap(icon, overwrite, false, () =>
        {
            var iconTex = useHR ? GetHRIcon(icon) : QoLBar.Interface.Data.GetIcon(icon);
            return (iconTex == null) ? null : LoadTextureWrapSquare(iconTex);
        });

        public void AddTex(int iconSlot, string path, bool overwrite = false)
        {
            TryDispose(iconSlot);
            if (overwrite)
                textureOverrides[iconSlot] = path;
            else
                textureOverrides.Add(iconSlot, path);
        }

        private void LoadTex(int iconSlot, string path, bool overwrite) => LoadTextureWrap(iconSlot, overwrite, false, () =>
        {
            var iconTex = QoLBar.Interface.Data.GetFile<Lumina.Data.Files.TexFile>(path);
            return (iconTex == null) ? null : LoadTextureWrapSquare(iconTex);
        });

        public void AddImage(int iconSlot, string path)
        {
            TryDispose(iconSlot);
            userIcons.Add(iconSlot, path);
        }

        // Seems to cause a nvwgf2umx.dll crash (System Access Violation Exception) if used async
        private void LoadImage(int iconSlot, string path, bool overwrite) => LoadTextureWrap(iconSlot, overwrite, true, () => QoLBar.Interface.UiBuilder.LoadImage(path));

        private Lumina.Data.Files.TexFile GetHRIcon(int icon)
        {
            var path = $"ui/icon/{icon / 1000 * 1000:000000}/{icon:000000}_hr1.tex";
            return QoLBar.Interface.Data.GetFile<Lumina.Data.Files.TexFile>(path) ?? QoLBar.Interface.Data.GetIcon(icon);
        }

        private TextureWrap LoadTextureWrapSquare(Lumina.Data.Files.TexFile tex)
        {
            if (tex.Header.Width > tex.Header.Height)
            {
                var imageData = tex.GetRgbaImageData();
                var newData = new byte[tex.Header.Width * tex.Header.Width * 4];
                var diff = (int)Math.Floor((tex.Header.Width - tex.Header.Height) / 2f);
                imageData.CopyTo(newData, diff * tex.Header.Width * 4);
                return QoLBar.Interface.UiBuilder.LoadImageRaw(newData, tex.Header.Width, tex.Header.Width, 4);
            }
            else if (tex.Header.Width < tex.Header.Height)
            {
                var imageData = tex.GetRgbaImageData();
                var newData = new byte[tex.Header.Height * tex.Header.Height * 4];
                var length = newData.Length / 4;
                var imageDataPos = 0;
                var diff = (tex.Header.Height - tex.Header.Width) / 2f;
                for (int i = 0; i < length; i++)
                {
                    var column = i % tex.Header.Height;
                    if (Math.Floor(diff) <= column && column < tex.Header.Height - Math.Ceiling(diff))
                    {
                        var pixel = i * 4;
                        newData[pixel] = imageData[imageDataPos++];
                        newData[pixel + 1] = imageData[imageDataPos++];
                        newData[pixel + 2] = imageData[imageDataPos++];
                        newData[pixel + 3] = imageData[imageDataPos++];
                    }
                }
                return QoLBar.Interface.UiBuilder.LoadImageRaw(newData, tex.Header.Height, tex.Header.Height, 4);
            }
            else
            {
                return QoLBar.Interface.UiBuilder.LoadImageRaw(tex.GetRgbaImageData(), tex.Header.Width, tex.Header.Height, 4);
            }
        }

        public Dictionary<int, string> GetUserIcons() => userIcons;

        public bool AddUserIcons(string path)
        {
            if (IsTextureLoading()) return false;

            foreach (var kv in userIcons)
                TryDispose(kv.Key);

            userIcons.Clear();
            if (!string.IsNullOrEmpty(path))
            {
                var directory = new DirectoryInfo(path);
                foreach (var file in directory.GetFiles())
                {
                    int.TryParse(Path.GetFileNameWithoutExtension(file.Name), out int i);
                    if (i > 0)
                    {
                        if (userIcons.ContainsKey(-i))
                            PluginLog.LogError($"Attempted to load {file.Name} into index {-i} but it already exists!");
                        else
                            AddImage(-i, directory.FullName + "\\" + file.Name);
                    }
                }
            }

            return true;
        }

        public void TryEmpty()
        {
            if (IsEmptying) return;

            IsEmptying = true;
            Task.Run(async () => {
                while (IsTextureLoading() || loadTask?.IsCompleted == false)
                    await Task.Delay(1000);
                Dispose();
                IsEmptying = false;
            });
        }

        public void Dispose()
        {
            foreach (var t in this)
                t.Value?.Dispose();
        }

        public static void AddExtraTextures(TextureDictionary lr, TextureDictionary hr)
        {
            void AddTexSheet(int id, string path, bool noHR = false)
            {
                lr.AddTex(id, path + ".tex");
                hr.AddTex(id, path + (!noHR ? "_hr1.tex" : ".tex"));
            }

            AddTexSheet(FrameIconID, "ui/uld/icona_frame");
            AddTexSheet(FrameIconID + 1, "ui/uld/icona_recast");
            AddTexSheet(FrameIconID + 2, "ui/uld/icona_recast2");
            lr.LoadTexture(FrameIconID);
            hr.LoadTexture(FrameIconID);

            AddTexSheet(GetSafeIconID(0), "ui/uld/achievement");
            AddTexSheet(GetSafeIconID(1), "ui/uld/actionbar");
            AddTexSheet(GetSafeIconID(2), "ui/uld/actioncross");
            AddTexSheet(GetSafeIconID(3), "ui/uld/actionmenu");
            AddTexSheet(GetSafeIconID(4), "ui/uld/adventurenotebook");
            AddTexSheet(GetSafeIconID(5), "ui/uld/alarm");
            AddTexSheet(GetSafeIconID(6), "ui/uld/aozbriefing");
            AddTexSheet(GetSafeIconID(7), "ui/uld/aoznotebook");
            AddTexSheet(GetSafeIconID(8), "ui/uld/aquariumsetting");
            AddTexSheet(GetSafeIconID(9), "ui/uld/areamap");
            AddTexSheet(GetSafeIconID(10), "ui/uld/armouryboard");
            AddTexSheet(GetSafeIconID(11), "ui/uld/camerasettings");
            AddTexSheet(GetSafeIconID(12), "ui/uld/cardtripletriad");
            AddTexSheet(GetSafeIconID(13), "ui/uld/character");
            AddTexSheet(GetSafeIconID(14), "ui/uld/charactergearset");
            AddTexSheet(GetSafeIconID(15), "ui/uld/charamake");
            AddTexSheet(GetSafeIconID(16), "ui/uld/charamake_dataimport");
            AddTexSheet(GetSafeIconID(17), "ui/uld/charaselect");
            AddTexSheet(GetSafeIconID(18), "ui/uld/circlebuttons");
            AddTexSheet(GetSafeIconID(19), "ui/uld/circlefinder");
            AddTexSheet(GetSafeIconID(20), "ui/uld/colosseumresult");
            AddTexSheet(GetSafeIconID(21), "ui/uld/companycraftrecipe");
            AddTexSheet(GetSafeIconID(22), "ui/uld/concentration");
            AddTexSheet(GetSafeIconID(23), "ui/uld/configbackup");
            AddTexSheet(GetSafeIconID(24), "ui/uld/contentsfinder");
            AddTexSheet(GetSafeIconID(25), "ui/uld/contentsinfo");
            AddTexSheet(GetSafeIconID(26), "ui/uld/contentsnotebook");
            AddTexSheet(GetSafeIconID(27), "ui/uld/contentsreplayplayer");
            AddTexSheet(GetSafeIconID(28), "ui/uld/contentsreplaysetting");
            AddTexSheet(GetSafeIconID(29), "ui/uld/creditplayer");
            AddTexSheet(GetSafeIconID(30), "ui/uld/cursor");
            AddTexSheet(GetSafeIconID(31), "ui/uld/deepdungeonclassjob");
            AddTexSheet(GetSafeIconID(32), "ui/uld/deepdungeonnavimap_ankh");
            AddTexSheet(GetSafeIconID(33), "ui/uld/deepdungeonnavimap_key");
            AddTexSheet(GetSafeIconID(34), "ui/uld/deepdungeonresult");
            AddTexSheet(GetSafeIconID(35), "ui/uld/deepdungeonsavedata");
            AddTexSheet(GetSafeIconID(36), "ui/uld/deepdungeontopmenu");
            AddTexSheet(GetSafeIconID(37), "ui/uld/description");
            AddTexSheet(GetSafeIconID(38), "ui/uld/dtr");
            AddTexSheet(GetSafeIconID(39), "ui/uld/emjicon");
            AddTexSheet(GetSafeIconID(40), "ui/uld/emjicon2");
            AddTexSheet(GetSafeIconID(41), "ui/uld/emjicon3");
            AddTexSheet(GetSafeIconID(42), "ui/uld/emjparts");
            AddTexSheet(GetSafeIconID(43), "ui/uld/emote");
            AddTexSheet(GetSafeIconID(44), "ui/uld/enemylist");
            AddTexSheet(GetSafeIconID(45), "ui/uld/eurekaelementaledit");
            AddTexSheet(GetSafeIconID(46), "ui/uld/eurekaelementalhud");
            AddTexSheet(GetSafeIconID(47), "ui/uld/eurekalogosshardlist");
            AddTexSheet(GetSafeIconID(48), "ui/uld/exp_gauge");
            AddTexSheet(GetSafeIconID(49), "ui/uld/explorationdetail");
            AddTexSheet(GetSafeIconID(50), "ui/uld/explorationship");
            AddTexSheet(GetSafeIconID(51), "ui/uld/fashioncheck");
            AddTexSheet(GetSafeIconID(52), "ui/uld/fashioncheckscoregauge");
            AddTexSheet(GetSafeIconID(53), "ui/uld/fashioncheckscoregaugenum");
            AddTexSheet(GetSafeIconID(54), "ui/uld/fate");
            AddTexSheet(GetSafeIconID(55), "ui/uld/fishingnotebook");
            AddTexSheet(GetSafeIconID(56), "ui/uld/freecompany");
            AddTexSheet(GetSafeIconID(57), "ui/uld/gateresult");
            AddTexSheet(GetSafeIconID(58), "ui/uld/gatherercraftericon");
            AddTexSheet(GetSafeIconID(59), "ui/uld/gcarmy");
            AddTexSheet(GetSafeIconID(60), "ui/uld/gcarmychangeclass");
            AddTexSheet(GetSafeIconID(61), "ui/uld/gcarmychangemirageprism");
            AddTexSheet(GetSafeIconID(62), "ui/uld/gcarmyclass");
            AddTexSheet(GetSafeIconID(63), "ui/uld/gcarmyexpedition");
            AddTexSheet(GetSafeIconID(64), "ui/uld/gcarmyexpeditionforecast");
            AddTexSheet(GetSafeIconID(65), "ui/uld/gcarmyexpeditionresult");
            AddTexSheet(GetSafeIconID(66), "ui/uld/gcarmymemberprofile");
            AddTexSheet(GetSafeIconID(67), "ui/uld/goldsaucercarddeckedit");
            AddTexSheet(GetSafeIconID(68), "ui/uld/housing");
            AddTexSheet(GetSafeIconID(69), "ui/uld/housinggoods");
            AddTexSheet(GetSafeIconID(70), "ui/uld/housingguestbook");
            AddTexSheet(GetSafeIconID(71), "ui/uld/housingguestbook2");
            AddTexSheet(GetSafeIconID(72), "ui/uld/howto");
            AddTexSheet(GetSafeIconID(73), "ui/uld/iconverminion");
            AddTexSheet(GetSafeIconID(74), "ui/uld/image2");
            AddTexSheet(GetSafeIconID(75), "ui/uld/inventory");
            AddTexSheet(GetSafeIconID(76), "ui/uld/itemdetail");
            AddTexSheet(GetSafeIconID(77), "ui/uld/jobhudacn0");
            AddTexSheet(GetSafeIconID(78), "ui/uld/jobhudast0");
            AddTexSheet(GetSafeIconID(79), "ui/uld/jobhudblm0");
            AddTexSheet(GetSafeIconID(80), "ui/uld/jobhudbrd0");
            AddTexSheet(GetSafeIconID(81), "ui/uld/jobhuddnc0");
            AddTexSheet(GetSafeIconID(82), "ui/uld/jobhuddrg0");
            AddTexSheet(GetSafeIconID(83), "ui/uld/jobhuddrk0");
            AddTexSheet(GetSafeIconID(84), "ui/uld/jobhuddrk1");
            AddTexSheet(GetSafeIconID(85), "ui/uld/jobhudgnb");
            AddTexSheet(GetSafeIconID(86), "ui/uld/jobhudmch0");
            AddTexSheet(GetSafeIconID(87), "ui/uld/jobhudmnk1");
            AddTexSheet(GetSafeIconID(88), "ui/uld/jobhudnin1");
            AddTexSheet(GetSafeIconID(89), "ui/uld/jobhudpld");
            AddTexSheet(GetSafeIconID(90), "ui/uld/jobhudsam1");
            AddTexSheet(GetSafeIconID(91), "ui/uld/jobhudsch0");
            AddTexSheet(GetSafeIconID(92), "ui/uld/jobhudsimple_stacka");
            AddTexSheet(GetSafeIconID(93), "ui/uld/jobhudsimple_stackb");
            AddTexSheet(GetSafeIconID(94), "ui/uld/jobhudsmn0");
            AddTexSheet(GetSafeIconID(95), "ui/uld/jobhudsmn1");
            AddTexSheet(GetSafeIconID(96), "ui/uld/jobhudwar");
            AddTexSheet(GetSafeIconID(97), "ui/uld/jobhudwhm");
            AddTexSheet(GetSafeIconID(98), "ui/uld/journal");
            AddTexSheet(GetSafeIconID(99), "ui/uld/journal_detail");
            AddTexSheet(GetSafeIconID(100), "ui/uld/letterlist", true);
            AddTexSheet(GetSafeIconID(101), "ui/uld/letterlist2");
            AddTexSheet(GetSafeIconID(102), "ui/uld/letterlist3");
            AddTexSheet(GetSafeIconID(103), "ui/uld/letterviewer");
            AddTexSheet(GetSafeIconID(104), "ui/uld/levelup2");
            AddTexSheet(GetSafeIconID(105), "ui/uld/lfg");
            AddTexSheet(GetSafeIconID(106), "ui/uld/linkshell");
            AddTexSheet(GetSafeIconID(107), "ui/uld/lotterydaily");
            AddTexSheet(GetSafeIconID(108), "ui/uld/lotteryweekly");
            AddTexSheet(GetSafeIconID(109), "ui/uld/lovmheader");
            AddTexSheet(GetSafeIconID(110), "ui/uld/lovmheadernum");
            AddTexSheet(GetSafeIconID(111), "ui/uld/lovmpalette");
            AddTexSheet(GetSafeIconID(112), "ui/uld/maincommand_icon");
            AddTexSheet(GetSafeIconID(113), "ui/uld/minerbotanist");
            AddTexSheet(GetSafeIconID(114), "ui/uld/minionnotebook");
            AddTexSheet(GetSafeIconID(115), "ui/uld/minionnotebookykw");
            AddTexSheet(GetSafeIconID(116), "ui/uld/mirageprismplate2");
            AddTexSheet(GetSafeIconID(117), "ui/uld/navimap");
            AddTexSheet(GetSafeIconID(118), "ui/uld/negotiation");
            AddTexSheet(GetSafeIconID(119), "ui/uld/nikuaccepted");
            AddTexSheet(GetSafeIconID(120), "ui/uld/numericstepperb");
            AddTexSheet(GetSafeIconID(121), "ui/uld/orchestrionplaylist");
            AddTexSheet(GetSafeIconID(122), "ui/uld/partyfinder");
            AddTexSheet(GetSafeIconID(123), "ui/uld/performance");
            AddTexSheet(GetSafeIconID(124), "ui/uld/puzzle");
            AddTexSheet(GetSafeIconID(125), "ui/uld/pvpduelrequest");
            AddTexSheet(GetSafeIconID(126), "ui/uld/pvprankpromotionqualifier");
            AddTexSheet(GetSafeIconID(127), "ui/uld/pvpscreeninformation");
            AddTexSheet(GetSafeIconID(128), "ui/uld/pvpsimulationheader2");
            AddTexSheet(GetSafeIconID(129), "ui/uld/pvpsimulationmachineselect");
            AddTexSheet(GetSafeIconID(130), "ui/uld/pvpteam");
            AddTexSheet(GetSafeIconID(131), "ui/uld/racechocoboranking");
            AddTexSheet(GetSafeIconID(132), "ui/uld/racechocoboresult");
            AddTexSheet(GetSafeIconID(133), "ui/uld/readycheck");
            AddTexSheet(GetSafeIconID(134), "ui/uld/recipenotebook");
            AddTexSheet(GetSafeIconID(135), "ui/uld/relic2growth");
            AddTexSheet(GetSafeIconID(136), "ui/uld/retainer");
            AddTexSheet(GetSafeIconID(137), "ui/uld/rhythmaction");
            AddTexSheet(GetSafeIconID(138), "ui/uld/rhythmactionstatus");
            AddTexSheet(GetSafeIconID(139), "ui/uld/roadstone");
            AddTexSheet(GetSafeIconID(140), "ui/uld/satisfactionsupplyicon");
            AddTexSheet(GetSafeIconID(141), "ui/uld/teleport");
            AddTexSheet(GetSafeIconID(142), "ui/uld/todolist");
            AddTexSheet(GetSafeIconID(143), "ui/uld/togglebutton");
            AddTexSheet(GetSafeIconID(144), "ui/uld/weeklybingo");
            AddTexSheet(GetSafeIconID(145), "ui/uld/worldtransrate");
        }
    }
}