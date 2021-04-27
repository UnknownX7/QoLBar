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
        public static int GetSafeIconID(ushort i) => FrameIconID + i;

        private readonly Dictionary<int, string> userIcons = new Dictionary<int, string>();
        private readonly Dictionary<int, string> textureOverrides = new Dictionary<int, string>();
        private int loadingTasks = 0;
        private static readonly TextureWrap disposedTexture = new GLTextureWrap(0, 0, 0);
        private readonly ConcurrentQueue<(bool, Task)> loadQueue = new ConcurrentQueue<(bool, Task)>();
        private Task loadTask;
        private readonly bool useHR = false;
        private readonly bool useGrayscale = false;
        public bool IsEmptying { get; private set; } = false;

        public TextureDictionary(bool hr, bool gs)
        {
            useHR = hr;
            useGrayscale = gs;
        }

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
            var imageData = !useGrayscale ? tex.GetRgbaImageData() : tex.GetGrayscaleImageData();
            if (tex.Header.Width > tex.Header.Height)
            {
                var newData = new byte[tex.Header.Width * tex.Header.Width * 4];
                var diff = (int)Math.Floor((tex.Header.Width - tex.Header.Height) / 2f);
                imageData.CopyTo(newData, diff * tex.Header.Width * 4);
                return QoLBar.Interface.UiBuilder.LoadImageRaw(newData, tex.Header.Width, tex.Header.Width, 4);
            }
            else if (tex.Header.Width < tex.Header.Height)
            {
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
                return QoLBar.Interface.UiBuilder.LoadImageRaw(imageData, tex.Header.Width, tex.Header.Height, 4);
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

            AddTexSheet(FrameIconID, "ui/uld/icona_frame"); // GetSafeIconID(0)
            lr.LoadTexture(FrameIconID);
            hr.LoadTexture(FrameIconID);
            AddTexSheet(GetSafeIconID(1), "ui/uld/icona_recast");
            AddTexSheet(GetSafeIconID(2), "ui/uld/icona_recast2");

            AddTexSheet(GetSafeIconID(100), "ui/uld/achievement");
            AddTexSheet(GetSafeIconID(101), "ui/uld/actionbar");
            AddTexSheet(GetSafeIconID(102), "ui/uld/actioncross");
            AddTexSheet(GetSafeIconID(103), "ui/uld/actionmenu");
            AddTexSheet(GetSafeIconID(104), "ui/uld/adventurenotebook");
            AddTexSheet(GetSafeIconID(105), "ui/uld/alarm");
            AddTexSheet(GetSafeIconID(106), "ui/uld/aozbriefing");
            AddTexSheet(GetSafeIconID(107), "ui/uld/aoznotebook");
            AddTexSheet(GetSafeIconID(108), "ui/uld/aquariumsetting");
            AddTexSheet(GetSafeIconID(109), "ui/uld/areamap");
            AddTexSheet(GetSafeIconID(110), "ui/uld/armouryboard");

            // B 200

            AddTexSheet(GetSafeIconID(300), "ui/uld/camerasettings");
            AddTexSheet(GetSafeIconID(301), "ui/uld/cardtripletriad");
            AddTexSheet(GetSafeIconID(302), "ui/uld/character");
            AddTexSheet(GetSafeIconID(303), "ui/uld/charactergearset");
            AddTexSheet(GetSafeIconID(304), "ui/uld/charamake");
            AddTexSheet(GetSafeIconID(305), "ui/uld/charamake_dataimport");
            AddTexSheet(GetSafeIconID(306), "ui/uld/charaselect");
            AddTexSheet(GetSafeIconID(307), "ui/uld/circlebuttons");
            AddTexSheet(GetSafeIconID(308), "ui/uld/circlefinder");
            AddTexSheet(GetSafeIconID(309), "ui/uld/colosseumresult");
            AddTexSheet(GetSafeIconID(310), "ui/uld/companycraftrecipe");
            AddTexSheet(GetSafeIconID(311), "ui/uld/concentration");
            AddTexSheet(GetSafeIconID(312), "ui/uld/configbackup");
            AddTexSheet(GetSafeIconID(313), "ui/uld/contentsfinder");
            AddTexSheet(GetSafeIconID(314), "ui/uld/contentsinfo");
            AddTexSheet(GetSafeIconID(315), "ui/uld/contentsnotebook");
            AddTexSheet(GetSafeIconID(316), "ui/uld/contentsreplayplayer");
            AddTexSheet(GetSafeIconID(317), "ui/uld/contentsreplaysetting");
            AddTexSheet(GetSafeIconID(318), "ui/uld/creditplayer");
            AddTexSheet(GetSafeIconID(319), "ui/uld/cursor");

            AddTexSheet(GetSafeIconID(400), "ui/uld/deepdungeonclassjob");
            AddTexSheet(GetSafeIconID(401), "ui/uld/deepdungeonnavimap_ankh");
            AddTexSheet(GetSafeIconID(402), "ui/uld/deepdungeonnavimap_key");
            AddTexSheet(GetSafeIconID(403), "ui/uld/deepdungeonresult");
            AddTexSheet(GetSafeIconID(404), "ui/uld/deepdungeonsavedata");
            AddTexSheet(GetSafeIconID(405), "ui/uld/deepdungeontopmenu");
            AddTexSheet(GetSafeIconID(406), "ui/uld/description");
            AddTexSheet(GetSafeIconID(407), "ui/uld/dtr");

            AddTexSheet(GetSafeIconID(500), "ui/uld/emjicon");
            AddTexSheet(GetSafeIconID(501), "ui/uld/emjicon2");
            AddTexSheet(GetSafeIconID(502), "ui/uld/emjicon3");
            AddTexSheet(GetSafeIconID(503), "ui/uld/emjparts");
            AddTexSheet(GetSafeIconID(504), "ui/uld/emote");
            AddTexSheet(GetSafeIconID(505), "ui/uld/enemylist");
            AddTexSheet(GetSafeIconID(506), "ui/uld/eurekaelementaledit");
            AddTexSheet(GetSafeIconID(507), "ui/uld/eurekaelementalhud");
            AddTexSheet(GetSafeIconID(508), "ui/uld/eurekalogosshardlist");
            AddTexSheet(GetSafeIconID(509), "ui/uld/exp_gauge");
            AddTexSheet(GetSafeIconID(510), "ui/uld/explorationdetail");
            AddTexSheet(GetSafeIconID(511), "ui/uld/explorationship");

            AddTexSheet(GetSafeIconID(600), "ui/uld/fashioncheck");
            AddTexSheet(GetSafeIconID(601), "ui/uld/fashioncheckscoregauge");
            AddTexSheet(GetSafeIconID(602), "ui/uld/fashioncheckscoregaugenum");
            AddTexSheet(GetSafeIconID(603), "ui/uld/fate");
            AddTexSheet(GetSafeIconID(604), "ui/uld/fishingnotebook");
            AddTexSheet(GetSafeIconID(605), "ui/uld/freecompany");

            AddTexSheet(GetSafeIconID(700), "ui/uld/gateresult");
            AddTexSheet(GetSafeIconID(701), "ui/uld/gatherercraftericon");
            AddTexSheet(GetSafeIconID(702), "ui/uld/gcarmy");
            AddTexSheet(GetSafeIconID(703), "ui/uld/gcarmychangeclass");
            AddTexSheet(GetSafeIconID(704), "ui/uld/gcarmychangemirageprism");
            AddTexSheet(GetSafeIconID(705), "ui/uld/gcarmyclass");
            AddTexSheet(GetSafeIconID(706), "ui/uld/gcarmyexpedition");
            AddTexSheet(GetSafeIconID(707), "ui/uld/gcarmyexpeditionforecast");
            AddTexSheet(GetSafeIconID(708), "ui/uld/gcarmyexpeditionresult");
            AddTexSheet(GetSafeIconID(709), "ui/uld/gcarmymemberprofile");
            AddTexSheet(GetSafeIconID(710), "ui/uld/goldsaucercarddeckedit");

            AddTexSheet(GetSafeIconID(800), "ui/uld/housing");
            AddTexSheet(GetSafeIconID(801), "ui/uld/housinggoods");
            AddTexSheet(GetSafeIconID(802), "ui/uld/housingguestbook");
            AddTexSheet(GetSafeIconID(803), "ui/uld/housingguestbook2");
            AddTexSheet(GetSafeIconID(804), "ui/uld/howto");

            AddTexSheet(GetSafeIconID(900), "ui/uld/iconverminion");
            AddTexSheet(GetSafeIconID(901), "ui/uld/image2");
            AddTexSheet(GetSafeIconID(902), "ui/uld/inventory");
            AddTexSheet(GetSafeIconID(903), "ui/uld/itemdetail");

            AddTexSheet(GetSafeIconID(1000), "ui/uld/jobhudacn0");
            AddTexSheet(GetSafeIconID(1001), "ui/uld/jobhudast0");
            AddTexSheet(GetSafeIconID(1002), "ui/uld/jobhudblm0");
            AddTexSheet(GetSafeIconID(1003), "ui/uld/jobhudbrd0");
            AddTexSheet(GetSafeIconID(1004), "ui/uld/jobhuddnc0");
            AddTexSheet(GetSafeIconID(1005), "ui/uld/jobhuddrg0");
            AddTexSheet(GetSafeIconID(1006), "ui/uld/jobhuddrk0");
            AddTexSheet(GetSafeIconID(1007), "ui/uld/jobhuddrk1");
            AddTexSheet(GetSafeIconID(1008), "ui/uld/jobhudgnb");
            AddTexSheet(GetSafeIconID(1009), "ui/uld/jobhudmch0");
            AddTexSheet(GetSafeIconID(1010), "ui/uld/jobhudmnk1");
            AddTexSheet(GetSafeIconID(1011), "ui/uld/jobhudnin1");
            AddTexSheet(GetSafeIconID(1012), "ui/uld/jobhudpld");
            AddTexSheet(GetSafeIconID(1013), "ui/uld/jobhudsam1");
            AddTexSheet(GetSafeIconID(1014), "ui/uld/jobhudsch0");
            AddTexSheet(GetSafeIconID(1015), "ui/uld/jobhudsimple_stacka");
            AddTexSheet(GetSafeIconID(1016), "ui/uld/jobhudsimple_stackb");
            AddTexSheet(GetSafeIconID(1017), "ui/uld/jobhudsmn0");
            AddTexSheet(GetSafeIconID(1018), "ui/uld/jobhudsmn1");
            AddTexSheet(GetSafeIconID(1019), "ui/uld/jobhudwar");
            AddTexSheet(GetSafeIconID(1020), "ui/uld/jobhudwhm");
            AddTexSheet(GetSafeIconID(1021), "ui/uld/journal");
            AddTexSheet(GetSafeIconID(1022), "ui/uld/journal_detail");

            // K 1100

            AddTexSheet(GetSafeIconID(1200), "ui/uld/letterlist", true);
            AddTexSheet(GetSafeIconID(1201), "ui/uld/letterlist2");
            AddTexSheet(GetSafeIconID(1202), "ui/uld/letterlist3");
            AddTexSheet(GetSafeIconID(1203), "ui/uld/letterviewer");
            AddTexSheet(GetSafeIconID(1204), "ui/uld/levelup2");
            AddTexSheet(GetSafeIconID(1205), "ui/uld/lfg");
            AddTexSheet(GetSafeIconID(1206), "ui/uld/linkshell");
            AddTexSheet(GetSafeIconID(1207), "ui/uld/lotterydaily");
            AddTexSheet(GetSafeIconID(1208), "ui/uld/lotteryweekly");
            AddTexSheet(GetSafeIconID(1209), "ui/uld/lovmheader");
            AddTexSheet(GetSafeIconID(1210), "ui/uld/lovmheadernum");
            AddTexSheet(GetSafeIconID(1211), "ui/uld/lovmpalette");

            AddTexSheet(GetSafeIconID(1300), "ui/uld/maincommand_icon");
            AddTexSheet(GetSafeIconID(1301), "ui/uld/minerbotanist");
            AddTexSheet(GetSafeIconID(1302), "ui/uld/minionnotebook");
            AddTexSheet(GetSafeIconID(1303), "ui/uld/minionnotebookykw");
            AddTexSheet(GetSafeIconID(1304), "ui/uld/mirageprismplate2");

            AddTexSheet(GetSafeIconID(1400), "ui/uld/navimap");
            AddTexSheet(GetSafeIconID(1401), "ui/uld/negotiation");
            AddTexSheet(GetSafeIconID(1402), "ui/uld/nikuaccepted");
            AddTexSheet(GetSafeIconID(1403), "ui/uld/numericstepperb");

            AddTexSheet(GetSafeIconID(1500), "ui/uld/orchestrionplaylist");

            AddTexSheet(GetSafeIconID(1600), "ui/uld/partyfinder");
            AddTexSheet(GetSafeIconID(1601), "ui/uld/performance");
            AddTexSheet(GetSafeIconID(1602), "ui/uld/puzzle");
            AddTexSheet(GetSafeIconID(1603), "ui/uld/pvpduelrequest");
            AddTexSheet(GetSafeIconID(1604), "ui/uld/pvprankpromotionqualifier");
            AddTexSheet(GetSafeIconID(1605), "ui/uld/pvpscreeninformation");
            AddTexSheet(GetSafeIconID(1606), "ui/uld/pvpsimulationheader2");
            AddTexSheet(GetSafeIconID(1607), "ui/uld/pvpsimulationmachineselect");
            AddTexSheet(GetSafeIconID(1608), "ui/uld/pvpteam");

            // Q 1700

            AddTexSheet(GetSafeIconID(1800), "ui/uld/racechocoboranking");
            AddTexSheet(GetSafeIconID(1801), "ui/uld/racechocoboresult");
            AddTexSheet(GetSafeIconID(1802), "ui/uld/readycheck");
            AddTexSheet(GetSafeIconID(1803), "ui/uld/recipenotebook");
            AddTexSheet(GetSafeIconID(1804), "ui/uld/relic2growth");
            AddTexSheet(GetSafeIconID(1805), "ui/uld/retainer");
            AddTexSheet(GetSafeIconID(1806), "ui/uld/rhythmaction");
            AddTexSheet(GetSafeIconID(1807), "ui/uld/rhythmactionstatus");
            AddTexSheet(GetSafeIconID(1808), "ui/uld/roadstone");

            AddTexSheet(GetSafeIconID(1900), "ui/uld/satisfactionsupplyicon");

            AddTexSheet(GetSafeIconID(2000), "ui/uld/teleport");
            AddTexSheet(GetSafeIconID(2001), "ui/uld/todolist");
            AddTexSheet(GetSafeIconID(2002), "ui/uld/togglebutton");

            // U 2100

            // V 2200

            AddTexSheet(GetSafeIconID(2300), "ui/uld/weeklybingo");
            AddTexSheet(GetSafeIconID(2301), "ui/uld/worldtransrate");

            // X 2400

            // Y 2500

            // Z 2600
        }
    }
}