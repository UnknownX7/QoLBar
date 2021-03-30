using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Dalamud.Plugin;

namespace QoLBar
{
    public class QoLSerializer : DefaultSerializationBinder
    {
        private readonly static Type barType = typeof(BarConfig);
        private readonly static Type shortcutType = typeof(Shortcut);
        private readonly static Type barType2 = typeof(BarCfg);
        private readonly static Type shortcutType2 = typeof(ShCfg);
        private readonly static Type vector2Type = typeof(Vector2);
        private readonly static Type vector4Type = typeof(Vector4);
        private readonly static string barShortName = "b";
        private readonly static string shortcutShortName = "s";
        private readonly static string barShortName2 = "b2";
        private readonly static string shortcutShortName2 = "s2";
        private readonly static string vector2ShortName = "2";
        private readonly static string vector4ShortName = "4";
        private readonly static Dictionary<string, Type> types = new Dictionary<string, Type>
        {
            [barType.FullName] = barType,
            [barShortName] = barType,
            [shortcutType.FullName] = shortcutType,
            [shortcutShortName] = shortcutType,
            [barType2.FullName] = barType2,
            [barShortName2] = barType2,
            [shortcutType2.FullName] = shortcutType2,
            [shortcutShortName2] = shortcutType2,
            [vector2Type.FullName] = vector2Type,
            [vector2ShortName] = vector2Type,
            [vector4Type.FullName] = vector4Type,
            [vector4ShortName] = vector4Type
        };
        private readonly static Dictionary<Type, string> typeNames = new Dictionary<Type, string>
        {
            [barType] = barShortName,
            [shortcutType] = shortcutShortName,
            [barType2] = barShortName2,
            [shortcutType2] = shortcutShortName2,
            [vector2Type] = vector2ShortName,
            [vector4Type] = vector4ShortName
        };

        public override Type BindToType(string assemblyName, string typeName)
        {
            if (types.ContainsKey(typeName))
                return types[typeName];
            else
                return base.BindToType(assemblyName, typeName);
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            if (typeNames.ContainsKey(serializedType))
            {
                assemblyName = null;
                typeName = typeNames[serializedType];
            }
            else
                base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    public static class Importing
    {
        public class ImportInfo
        {
            public BarCfg bar;
            public ShCfg shortcut;
        }

        public class ExportInfo
        {
            public BarConfig b1;
            public BarCfg b2;
            public Shortcut s1;
            public ShCfg s2;
            //public string v = QoLBar.Config.PluginVersion;
        }

        private static readonly QoLSerializer qolSerializer = new QoLSerializer();

        private static void CleanBarConfig(BarCfg bar)
        {
            if (bar.DockSide == BarCfg.BarDock.UndockedH || bar.DockSide == BarCfg.BarDock.UndockedV)
            {
                bar.Alignment = bar.GetDefaultValue(x => x.Alignment);
                bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
                bar.Offset = bar.GetDefaultValue(x => x.Offset);
                bar.Hint = bar.GetDefaultValue(x => x.Hint);
            }
            else
            {
                bar.LockedPosition = bar.GetDefaultValue(x => x.LockedPosition);
                bar.Position = bar.GetDefaultValue(x => x.Position);
                bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);

                if (bar.Visibility == BarCfg.BarVisibility.Always)
                {
                    bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
                    bar.Hint = bar.GetDefaultValue(x => x.Hint);
                }
            }

            CleanShortcut(bar.ShortcutList);
        }

        private static void CleanShortcut(List<ShCfg> shortcuts)
        {
            foreach (var sh in shortcuts)
                CleanShortcut(sh);
        }

        private static void CleanShortcut(ShCfg sh)
        {
            if (sh.Type != ShCfg.ShortcutType.Category)
            {
                sh.SubList = sh.GetDefaultValue(x => x.SubList);
                sh.CategoryColumns = sh.GetDefaultValue(x => x.CategoryColumns);
                sh.CategoryStaysOpen = sh.GetDefaultValue(x => x.CategoryStaysOpen);
                sh.CategoryWidth = sh.GetDefaultValue(x => x.CategoryWidth);
                sh.CategorySpacing = sh.GetDefaultValue(x => x.CategorySpacing);
                sh.CategoryScale = sh.GetDefaultValue(x => x.CategoryScale);
                sh.CategoryFontScale = sh.GetDefaultValue(x => x.CategoryFontScale);
                sh.CategoryNoBackground = sh.GetDefaultValue(x => x.CategoryNoBackground);
                sh.CategoryOnHover = sh.GetDefaultValue(x => x.CategoryOnHover);
            }
            else
            {
                if (sh.Mode != ShCfg.ShortcutMode.Default)
                    sh.Command = sh.GetDefaultValue(x => x.Command);
                sh.CategoryColumns = Math.Max(sh.CategoryColumns, 1);
                CleanShortcut(sh.SubList);
            }

            if (sh.Type == ShCfg.ShortcutType.Spacer)
            {
                sh.Command = sh.GetDefaultValue(x => x.Command);
                sh.Mode = sh.GetDefaultValue(x => x.Mode);
            }

            if (!sh.Name.StartsWith("::"))
            {
                sh.IconZoom = sh.GetDefaultValue(x => x.IconZoom);
                sh.IconOffset = sh.GetDefaultValue(x => x.IconOffset);
            }
            //else if (sh.ColorAnimation == 0)
            //    sh.ColorBg = sh.GetDefaultValue(x => x.ColorBg);
        }

        public static T CopyObject<T>(T o)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects, SerializationBinder = qolSerializer };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(o, settings), settings);
        }

        public static string SerializeObject(object o, bool saveAllValues) => !saveAllValues
            ? JsonConvert.SerializeObject(o, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                SerializationBinder = qolSerializer
            })
            : JsonConvert.SerializeObject(o, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

        public static T DeserializeObject<T>(string o) => JsonConvert.DeserializeObject<T>(o, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = qolSerializer
        });

        public static string CompressString(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                gs.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(mso.ToArray());
        }

        public static string DecompressString(string s)
        {
            var data = Convert.FromBase64String(s);
            byte[] lengthBuffer = new byte[4];
            Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
            int uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

            var buffer = new byte[uncompressedSize];
            using (var ms = new MemoryStream(data))
            {
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                gzip.Read(buffer, 0, uncompressedSize);
            }
            return Encoding.UTF8.GetString(buffer);
        }

        public static string ExportObject(object o, bool saveAllValues) => CompressString(SerializeObject(o, saveAllValues));

        public static T ImportObject<T>(string import) => DeserializeObject<T>(DecompressString(import));

        public static string ExportBar(BarCfg bar, bool saveAllValues)
        {
            if (!saveAllValues)
            {
                bar = CopyObject(bar);
                CleanBarConfig(bar);
            }
            return ExportObject(new ExportInfo { b2 = bar }, saveAllValues);
        }

        public static bool allowImportConditions = false;
        public static bool allowImportHotkeys = false;
        public static BarConfig ImportBar(string import)
        {
            var bar = ImportObject<BarConfig>(import);

            if (!allowImportConditions)
                bar.ConditionSet = bar.GetDefaultValue(x => x.ConditionSet);

            if (!allowImportHotkeys)
            {
                static void removeHotkeys(List<Shortcut> shortcuts)
                {
                    foreach (var sh in shortcuts)
                    {
                        sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                        sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
                        if (sh.SubList != null && sh.SubList.Count > 0)
                            removeHotkeys(sh.SubList);
                    }
                }
                removeHotkeys(bar.ShortcutList);
            }

            return bar;
        }

        public static string ExportShortcut(ShCfg sh, bool saveAllValues)
        {
            if (!saveAllValues)
            {
                sh = CopyObject(sh);
                CleanShortcut(sh);
            }
            return ExportObject(new ExportInfo { s2 = sh }, saveAllValues);
        }

        public static Shortcut ImportShortcut(string import)
        {
            var sh = ImportObject<Shortcut>(import);

            if (!allowImportHotkeys)
            {
                sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
            }

            return sh;
        }

        public static ImportInfo TryImport(string import, bool printError = false)
        {
            ExportInfo imported;
            try
            {
                imported = ImportObject<ExportInfo>(import);
            }
            catch (Exception e)
            {
                // If we failed to import the ExportInfo then this is an old version
                imported = ImportLegacy(import);
                if (imported == null && printError)
                {
                    PluginLog.LogError("Invalid import string!");
                    PluginLog.LogError($"{e.GetType()}\n{e.Message}");
                }
            }

            if (imported?.b1 != null)
                imported.b2 = imported.b1.Upgrade();
            else if (imported?.s1 != null)
                imported.s2 = imported.s1.Upgrade(new BarConfig(), false);

            return new ImportInfo
            {
                bar = imported?.b2,
                shortcut = imported?.s2
            };
        }

        private static ExportInfo ImportLegacy(string import)
        {
            var imported = new ExportInfo();
            try { imported.b1 = ImportBar(import); }
            catch
            {
                try { imported.s1 = ImportShortcut(import); }
                catch { return null; }
            }
            return imported;
        }
    }
}
