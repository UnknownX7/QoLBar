using System;

#pragma warning disable CS0649 // Field is unassigned

namespace QoLBar.Wrappers
{
    public class UIModule
    {
        private unsafe class VirtualTable
        {
            public delegate*<IntPtr, void> vf4; // Client__UI__UIModule_Abort
            public delegate*<IntPtr, IntPtr> vf5; // Client__UI__UIModule_GetExcelModule
            public delegate*<IntPtr, IntPtr> vf6; // Client__UI__UIModule_GetRaptureTextModule
            public delegate*<IntPtr, IntPtr> vf7; // Client__UI__UIModule_GetRaptureAtkModule
            public delegate*<IntPtr, IntPtr> vf8; // Client__UI__UIModule_GetRaptureAtkModule2
            public delegate*<IntPtr, IntPtr> vf9; // Client__UI__UIModule_GetRaptureShellModule
            public delegate*<IntPtr, IntPtr> vf10; // Client__UI__UIModule_GetPronounModule
            public delegate*<IntPtr, IntPtr> vf11; // Client__UI__UIModule_GetRaptureLogModule
            public delegate*<IntPtr, IntPtr> vf12; // Client__UI__UIModule_GetRaptureMacroModule
            public delegate*<IntPtr, IntPtr> vf13; // Client__UI__UIModule_GetRaptureHotbarModule
            public delegate*<IntPtr, IntPtr> vf14; // Client__UI__UIModule_GetRaptureGearsetModule
            public delegate*<IntPtr, IntPtr> vf15; // Client__UI__UIModule_GetAcquaintanceModule
            public delegate*<IntPtr, IntPtr> vf16; // Client__UI__UIModule_GetItemOrderModule
            public delegate*<IntPtr, IntPtr> vf17; // Client__UI__UIModule_GetItemFinderModule
            public delegate*<IntPtr, IntPtr> vf18; // Client__UI__UIModule_GetConfigModule
            public delegate*<IntPtr, IntPtr> vf19; // Client__UI__UIModule_GetAddonConfig
            public delegate*<IntPtr, IntPtr> vf20; // Client__UI__UIModule_GetUiSavePackModule
            public delegate*<IntPtr, IntPtr> vf21; // Client__UI__UIModule_GetLetterDataModule
            public delegate*<IntPtr, IntPtr> vf22; // Client__UI__UIModule_GetRetainerTaskDataModule
            public delegate*<IntPtr, IntPtr> vf23; // Client__UI__UIModule_GetFlagStatusModule

            public delegate*<IntPtr, IntPtr> vf29; // Client__UI__UIModule_GetRaptureTeleportHistory

            public delegate*<IntPtr, IntPtr> vf34; // Client__UI__UIModule_GetAgentModule

            public delegate*<IntPtr, IntPtr> vf36; // Client__UI__UIModule_GetUI3DModule

            public delegate*<IntPtr, IntPtr> vf54; // Client__UI__UIModule_GetUIInputModule

            public delegate*<IntPtr, IntPtr> vf56; // Client__UI__UIModule_GetLogFilterConfig

            public VirtualTable(IntPtr* address)
            {
                foreach (var f in GetType().GetFields())
                {
                    var i = ushort.Parse(f.Name.Substring(2));
                    var vfunc = *(address + i);
                    f.SetValue(this, f.FieldType.Cast(vfunc));
                }
            }
        }

        public readonly IntPtr Address;
        private readonly VirtualTable vtbl;

        public unsafe void Abort() => vtbl.vf4(Address);
        public unsafe IntPtr GetExcelModule() => vtbl.vf5(Address);
        public unsafe IntPtr GetRaptureTextModule() => vtbl.vf6(Address);
        public unsafe IntPtr GetRaptureAtkModule() => vtbl.vf7(Address);
        public unsafe IntPtr GetRaptureAtkModule2() => vtbl.vf8(Address);
        public unsafe IntPtr GetRaptureShellModule() => vtbl.vf9(Address);
        public unsafe IntPtr GetPronounModule() => vtbl.vf10(Address);
        public unsafe IntPtr GetRaptureLogModule() => vtbl.vf11(Address);
        public unsafe IntPtr GetRaptureMacroModule() => vtbl.vf12(Address);
        public unsafe IntPtr GetRaptureHotbarModule() => vtbl.vf13(Address);
        public unsafe IntPtr GetRaptureGearsetModule() => vtbl.vf14(Address);
        public unsafe IntPtr GetAcquaintanceModule() => vtbl.vf15(Address);
        public unsafe IntPtr GetItemOrderModule() => vtbl.vf16(Address);
        public unsafe IntPtr GetItemFinderModule() => vtbl.vf17(Address);
        public unsafe IntPtr GetConfigModule() => vtbl.vf18(Address);
        public unsafe IntPtr GetAddonConfig() => vtbl.vf19(Address);
        public unsafe IntPtr GetUiSavePackModule() => vtbl.vf20(Address);
        public unsafe IntPtr GetLetterDataModule() => vtbl.vf21(Address);
        public unsafe IntPtr GetRetainerTaskDataModule() => vtbl.vf22(Address);
        public unsafe IntPtr GetFlagStatusModule() => vtbl.vf23(Address);
        public unsafe IntPtr GetRaptureTeleportHistory() => vtbl.vf29(Address);
        public unsafe IntPtr GetAgentModule() => vtbl.vf34(Address);
        public unsafe IntPtr GetUI3DModule() => vtbl.vf36(Address);
        public unsafe IntPtr GetUIInputModule() => vtbl.vf54(Address);
        public unsafe IntPtr GetLogFilterConfig() => vtbl.vf56(Address);

        public unsafe UIModule(IntPtr address)
        {
            Address = address;
            vtbl = new VirtualTable((IntPtr*)(*(IntPtr*)address));
        }

        public string ToString(string format = null) => Address.ToString(format);
    }
}