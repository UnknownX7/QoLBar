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
    }
}