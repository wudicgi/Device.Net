﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Device.Net.Windows
{
    /// <summary>
    /// TODO: Merge this factory class with other factory classes. I.e. create a DeviceFactoryBase class
    /// </summary>
    public abstract class WindowsDeviceFactoryBase
    {
        #region Public Properties
        public ILogger Logger { get;  }
        public ITracer Tracer { get; }
        #endregion

        #region Public Abstract Properties
        public abstract DeviceType DeviceType { get; }
        #endregion

        #region Protected Abstract Methods
        protected abstract ConnectedDeviceDefinition GetDeviceDefinition(string deviceId);
        protected abstract Guid GetClassGuid();
        #endregion

        #region Constructor
        protected WindowsDeviceFactoryBase(ILogger logger, ITracer tracer)
        {
            Logger = logger;
            Tracer = tracer;
        }
        #endregion

        #region Public Methods
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502")]
        public async Task<IEnumerable<ConnectedDeviceDefinition>> GetConnectedDeviceDefinitionsAsync(FilterDeviceDefinition filterDeviceDefinition)
        {
            return await Task.Run<IEnumerable<ConnectedDeviceDefinition>>(() =>
            {
                var deviceDefinitions = new Collection<ConnectedDeviceDefinition>();
                var spDeviceInterfaceData = new SpDeviceInterfaceData();
                var spDeviceInfoData = new SpDeviceInfoData();
                var spDeviceInterfaceDetailData = new SpDeviceInterfaceDetailData();
                spDeviceInterfaceData.CbSize = (uint)Marshal.SizeOf(spDeviceInterfaceData);
                spDeviceInfoData.CbSize = (uint)Marshal.SizeOf(spDeviceInfoData);
                string productIdHex = null;
                string vendorHex = null;
                string displayName = null;

                var guidString = GetClassGuid().ToString();
                var copyOfClassGuid = new Guid(guidString);
                const int flags = APICalls.DigcfDeviceinterface | APICalls.DigcfPresent;

                Log($"About to call {nameof(APICalls.SetupDiGetClassDevs)} for class Guid {guidString}. Flags: {flags}", null, LogLevel.Information);

                var devicesHandle = APICalls.SetupDiGetClassDevs(ref copyOfClassGuid, IntPtr.Zero, IntPtr.Zero, flags);

                spDeviceInterfaceDetailData.CbSize = IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize;

                var i = -1;

                if (filterDeviceDefinition != null)
                {
                    if (filterDeviceDefinition.ProductId.HasValue) productIdHex = Helpers.GetHex(filterDeviceDefinition.ProductId);
                    if (filterDeviceDefinition.VendorId.HasValue) vendorHex = Helpers.GetHex(filterDeviceDefinition.VendorId);
                }

                while (true)
                {
                    try
                    {
                        i++;

                        var isSuccess = APICalls.SetupDiEnumDeviceInterfaces(devicesHandle, IntPtr.Zero, ref copyOfClassGuid, (uint)i, ref spDeviceInterfaceData);
                        if (!isSuccess)
                        {
                            var errorCode = Marshal.GetLastWin32Error();

                            if (errorCode == APICalls.ERROR_NO_MORE_ITEMS)
                            {
                                Log($"The call to {nameof(APICalls.SetupDiEnumDeviceInterfaces)} returned ERROR_NO_MORE_ITEMS", null, LogLevel.Information);
                                break;
                            }

                            if (errorCode > 0)
                            {
                                Log($"{nameof(APICalls.SetupDiEnumDeviceInterfaces)} called successfully but a device was skipped while enumerating because something went wrong. The device was at index {i}. The error code was {errorCode}.", null, LogLevel.Warning);
                            }
                        }

                        isSuccess = APICalls.SetupDiGetDeviceInterfaceDetail(devicesHandle, ref spDeviceInterfaceData, ref spDeviceInterfaceDetailData, 256, out _, ref spDeviceInfoData);
                        if (!isSuccess)
                        {
                            var errorCode = Marshal.GetLastWin32Error();

                            if (errorCode == APICalls.ERROR_NO_MORE_ITEMS)
                            {
                                Log($"The call to {nameof(APICalls.SetupDiEnumDeviceInterfaces)} returned ERROR_NO_MORE_ITEMS", null, LogLevel.Information);
                                //TODO: This probably can't happen but leaving this here because there was some strange behaviour
                                break;
                            }

                            if (errorCode > 0)
                            {
                                Log($"{nameof(APICalls.SetupDiGetDeviceInterfaceDetail)} called successfully but a device was skipped while enumerating because something went wrong. The device was at index {i}. The error code was {errorCode}.", null, LogLevel.Warning);
                            }
                        }

                        //Note this is a bit nasty but we can filter Vid and Pid this way I think...
                        if (filterDeviceDefinition != null)
                        {
                            if (filterDeviceDefinition.VendorId.HasValue && !spDeviceInterfaceDetailData.DevicePath.ContainsIgnoreCase(vendorHex)) continue;
                            if (filterDeviceDefinition.ProductId.HasValue && !spDeviceInterfaceDetailData.DevicePath.ContainsIgnoreCase(productIdHex)) continue;
                        }

                        uint[] tryPropertyList = new uint[] { APICalls.SPDRP_FRIENDLYNAME, APICalls.SPDRP_DEVICEDESC };
                        foreach (uint tryProperty in tryPropertyList)
                        {
                            byte[] propertyValueBuffer = new byte[256];
                            isSuccess = APICalls.SetupDiGetDeviceRegistryProperty(devicesHandle, ref spDeviceInfoData, tryProperty, out uint dwRegType, propertyValueBuffer, (uint)propertyValueBuffer.Length, out uint dwRegSize);
                            if (!isSuccess)
                            {
                                var errorCode = Marshal.GetLastWin32Error();

                                if (errorCode == APICalls.ERROR_NO_MORE_ITEMS)
                                {
                                    Log($"The call to {nameof(APICalls.SetupDiGetDeviceRegistryProperty)} returned ERROR_NO_MORE_ITEMS", null, LogLevel.Information);
                                    break;
                                }

                                if (errorCode > 0)
                                {
                                    Log($"{nameof(APICalls.SetupDiGetDeviceRegistryProperty)} called successfully but a device was skipped while enumerating because something went wrong. The device was at index {i}. The error code was {errorCode}.", null, LogLevel.Warning);
                                }
                            }
                            else
                            {
                                displayName = System.Text.Encoding.Unicode.GetString(propertyValueBuffer).TrimEnd('\0');
                                break;
                            }
                        }

                        if ((filterDeviceDefinition != null) && (filterDeviceDefinition.DisplayName != null))
                        {
                            if (displayName == null)
                            {
                                continue;
                            }

                            if (!displayName.StartsWithIgnoreCase(filterDeviceDefinition.DisplayName))
                            {
                                continue;
                            }
                        }

                        var connectedDeviceDefinition = GetDeviceDefinition(spDeviceInterfaceDetailData.DevicePath);

                        if (connectedDeviceDefinition == null)
                        {
                            Logger.Log($"Device with path {spDeviceInterfaceDetailData.DevicePath} was skipped. See previous logs.", GetType().Name, null, LogLevel.Warning);
                            continue;
                        }

                        if (!DeviceManager.IsDefinitionMatch(filterDeviceDefinition, connectedDeviceDefinition)) continue;

                        deviceDefinitions.Add(connectedDeviceDefinition);
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                    }
                }

                APICalls.SetupDiDestroyDeviceInfoList(devicesHandle);

                return deviceDefinitions;
            });
        }
        #endregion

        #region Protected Methods
        protected void Log(Exception ex, [CallerMemberName] string callMemberName = null)
        {
            Log(null, $"{GetType().Name} - {callMemberName}", ex, LogLevel.Error);
        }

        protected void Log(string message, Exception ex, LogLevel logLevel, [CallerMemberName] string callMemberName = null)
        {
            Log(message, $"{GetType().Name} - {callMemberName}", ex, logLevel);
        }

        protected void Log(string message, string region, Exception ex, LogLevel logLevel)
        {
            Logger?.Log(message, region, ex, logLevel);
        }
        #endregion

        #region Private Static Methods
        private static uint GetNumberFromDeviceId(string deviceId, string searchString)
        {
            if (deviceId == null) throw new ArgumentNullException(nameof(deviceId));

            var indexOfSearchString = deviceId.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);
            string hexString = null;
            if (indexOfSearchString > -1)
            {
                hexString = deviceId.Substring(indexOfSearchString + searchString.Length, 4);
            }
            var numberAsInteger = uint.Parse(hexString, NumberStyles.HexNumber);
            return numberAsInteger;
        }
        #endregion

        #region Public Static Methods
        public static ConnectedDeviceDefinition GetDeviceDefinitionFromWindowsDeviceId(string deviceId, DeviceType deviceType, ILogger logger)
        {
            uint? vid = null;
            uint? pid = null;
            try
            {
                vid = GetNumberFromDeviceId(deviceId, "vid_");
                pid = GetNumberFromDeviceId(deviceId, "pid_");
            }
            catch (Exception ex)
            {
                logger?.Log($"Error {ex.Message}", nameof(GetDeviceDefinitionFromWindowsDeviceId), ex, LogLevel.Error);
            }

            return new ConnectedDeviceDefinition(deviceId) { DeviceType = deviceType, VendorId = vid, ProductId = pid };
        }
        #endregion
    }
}
