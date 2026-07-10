using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ACEOptimizer.Services
{
    internal static class AuthenticodeVerifier
    {
        private static readonly Guid GenericVerifyAction = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

        public static bool IsTrusted(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            IntPtr filePathPointer = IntPtr.Zero;
            IntPtr fileInfoPointer = IntPtr.Zero;
            IntPtr trustDataPointer = IntPtr.Zero;

            try
            {
                filePathPointer = Marshal.StringToCoTaskMemUni(filePath);
                WinTrustFileInfo fileInfo = new()
                {
                    StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    FilePath = filePathPointer
                };

                fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);

                WinTrustData trustData = new()
                {
                    StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                    UIChoice = 2,
                    UnionChoice = 1,
                    FileInfo = fileInfoPointer,
                    StateAction = 0,
                    ProviderFlags = 0x00000080
                };

                trustDataPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustData>());
                Marshal.StructureToPtr(trustData, trustDataPointer, false);

                uint result = WinVerifyTrust(IntPtr.Zero, GenericVerifyAction, trustDataPointer);
                return result == 0;
            }
            finally
            {
                if (trustDataPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(trustDataPointer);
                if (fileInfoPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileInfoPointer);
                if (filePathPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePathPointer);
            }
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint WinVerifyTrust(
            IntPtr windowHandle,
            [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
            IntPtr trustData);

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustFileInfo
        {
            public uint StructSize;
            public IntPtr FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public uint StructSize;
            public IntPtr PolicyCallbackData;
            public IntPtr SIPClientData;
            public uint UIChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr URLReference;
            public uint ProviderFlags;
            public uint UIContext;
        }
    }
}
