using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Device.Net.Windows
{
    public static class APICalls
    {
        #region Constants
        public const int DigcfDeviceinterface = 16;
        public const int DigcfPresent = 2;
        public const uint FileShareRead = 1;
        public const uint FileShareWrite = 2;

        public const uint OpenExisting = 3;
        public const int FileAttributeNormal = 128;
        public const int FileFlagOverlapped = 1073741824;

        public const int ERROR_NO_MORE_ITEMS = 259;

        public const int PURGE_TXCLEAR = 0x0004;
        public const int PURGE_RXCLEAR = 0x0008;
        #endregion

        #region Methods

        #region Kernel32
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string lpFileName, FileAccessRights dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        // Used to read bytes from the serial connection. 
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out int lpNumberOfBytesRead, int lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, int lpOverlapped);
        #endregion

        #region SetupAPI
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdienumdeviceinterfaces
        /// </summary>
        [DllImport(@"setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SpDeviceInterfaceData deviceInterfaceData, ref SpDeviceInterfaceDetailData deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, ref SpDeviceInfoData deviceInfoData);
        #endregion

        #endregion

        #region Extended

        // Reference: https://github.com/jorticus/zeitgeber-tools/blob/527b8b8b5afdeacba8d8adfe7fe0c437c8dc0b3a/Zeitlib/WinAPI/SetupAPI.cs

        public const int SPDRP_DEVICEDESC = 0x00000000;
        public const int SPDRP_HARDWAREID = 0x00000001;
        public const int SPDRP_FRIENDLYNAME = 0x0000000C;
        public const int SPDRP_LOCATION_INFORMATION = 0x0000000D;

        //SetupDiGetDeviceRegistryProperty() gives us the hardware ID, which we use to check to see if it has matching VID/PID
        //http://www.pinvoke.net/default.aspx/setupapi/SetupDiGetDeviceRegistryProperty.html
        [DllImport(@"setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SpDeviceInfoData deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize
        );

        #endregion
    }
}
