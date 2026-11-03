using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Flow.Launcher.Helper
{
    /// <summary>
    /// Native APIs used by <see cref="SingleInstance{TApplication}"/>.
    /// Isolated because DllImport cannot be applied inside a generic type.
    /// </summary>
    internal static class SingleInstanceNativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeServerProcessId(
            SafePipeHandle pipe,
            out uint serverProcessId);

        private const uint PipeAccessInbound = 0x00000001;
        private const uint FileFlagOverlapped = 0x40000000;
        private const uint PipeRejectRemoteClients = 0x00000008;
        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint SePrivilegeEnabled = 0x00000002;
        private const int DaclSecurityInformation = 0x00000004;
        private const int LabelSecurityInformation = 0x00000010;
        private const int SeKernelObject = 6;
        private const int MandatoryLabelAceType = 0x11;
        private static readonly IntPtr InvalidHandleValue = new(-1);

        /// <summary>
        /// Medium mandatory label. No-Write-Up still blocks low-integrity callers.
        /// </summary>
        internal const string MediumIntegritySid = "S-1-16-8192";

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateNamedPipeW(
            string name,
            uint openMode,
            uint pipeMode,
            uint maxInstances,
            uint outBufferSize,
            uint inBufferSize,
            uint defaultTimeout,
            ref SecurityAttributes securityAttributes);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetKernelObjectSecurity(
            SafeHandle handle,
            int securityInformation,
            byte[] securityDescriptor);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int GetSecurityInfo(
            SafeHandle handle,
            int objectType,
            int securityInfo,
            out IntPtr owner,
            out IntPtr group,
            out IntPtr dacl,
            out IntPtr sacl,
            out IntPtr securityDescriptor);

        [DllImport("advapi32.dll")]
        private static extern uint GetSecurityDescriptorLength(IntPtr securityDescriptor);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out Luid luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle,
            bool disableAllPrivileges,
            ref TokenPrivileges newState,
            uint bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        /// <summary>
        /// Inbound async pipe whose label is medium, so a normal-integrity process can connect
        /// to an elevated owner. Access stays limited to the current user.
        /// </summary>
        internal static NamedPipeServerStream CreateInboundPipe(string channelName)
        {
            var descriptor = CreateUserMediumDescriptor();
            if (descriptor == null)
            {
                return new NamedPipeServerStream(
                    channelName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
            }

            // Lowering the label below the creator's integrity requires this privilege.
            // A normal-integrity creator already matches the label and can ignore a failed enable.
            TryEnablePrivilege("SeRelabelPrivilege");

            var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
            try
            {
                var attributes = new SecurityAttributes
                {
                    Length = Marshal.SizeOf<SecurityAttributes>(),
                    SecurityDescriptor = pinned.AddrOfPinnedObject(),
                    InheritHandle = 0
                };
                var raw = CreateNamedPipeW(
                    @"\\.\pipe\" + channelName,
                    PipeAccessInbound | FileFlagOverlapped,
                    PipeRejectRemoteClients,
                    1,
                    4096,
                    4096,
                    0,
                    ref attributes);
                if (raw == InvalidHandleValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var safeHandle = new SafePipeHandle(raw, ownsHandle: true);
                try
                {
                    return new NamedPipeServerStream(PipeDirection.In, isAsync: true, isConnected: false, safeHandle);
                }
                catch
                {
                    safeHandle.Dispose();
                    throw;
                }
            }
            finally
            {
                pinned.Free();
            }
        }

        /// <summary>
        /// Lets a normal-integrity process open an object created by an elevated instance.
        /// The DACL stays limited to the current user. No-Write-Up still blocks low-integrity callers.
        /// </summary>
        internal static bool AllowMediumIntegrityClients(SafeHandle handle)
        {
            var descriptor = CreateUserMediumDescriptor();
            if (descriptor == null)
                return false;

            TryEnablePrivilege("SeRelabelPrivilege");
            return SetKernelObjectSecurity(
                handle,
                DaclSecurityInformation | LabelSecurityInformation,
                descriptor);
        }

        internal static string GetIntegrityLabelSid(SafeHandle handle)
        {
            if (GetSecurityInfo(
                    handle,
                    SeKernelObject,
                    LabelSecurityInformation,
                    out _,
                    out _,
                    out _,
                    out _,
                    out var securityDescriptor) != 0
                || securityDescriptor == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var length = GetSecurityDescriptorLength(securityDescriptor);
                var bytes = new byte[length];
                Marshal.Copy(securityDescriptor, bytes, 0, (int)length);
                var parsed = new RawSecurityDescriptor(bytes, 0);
                if (parsed.SystemAcl == null)
                    return null;

                foreach (GenericAce ace in parsed.SystemAcl)
                {
                    if ((int)ace.AceType != MandatoryLabelAceType)
                        continue;

                    var aceBytes = new byte[ace.BinaryLength];
                    ace.GetBinaryForm(aceBytes, 0);
                    return new SecurityIdentifier(aceBytes, 8).Value;
                }

                return null;
            }
            finally
            {
                LocalFree(securityDescriptor);
            }
        }

        private static byte[] CreateUserMediumDescriptor()
        {
            var user = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrEmpty(user))
                return null;

            var descriptor = new RawSecurityDescriptor($"D:(A;;GA;;;{user})S:(ML;;NW;;;ME)");
            var bytes = new byte[descriptor.BinaryLength];
            descriptor.GetBinaryForm(bytes, 0);
            return bytes;
        }

        private static void TryEnablePrivilege(string privilege)
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
                return;

            try
            {
                if (!LookupPrivilegeValue(null, privilege, out var luid))
                    return;

                var privileges = new TokenPrivileges
                {
                    PrivilegeCount = 1,
                    Privileges = new LuidAndAttributes
                    {
                        Luid = luid,
                        Attributes = SePrivilegeEnabled
                    }
                };
                AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                CloseHandle(token);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr SecurityDescriptor;
            public int InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LuidAndAttributes
        {
            public Luid Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public LuidAndAttributes Privileges;
        }
    }
}
