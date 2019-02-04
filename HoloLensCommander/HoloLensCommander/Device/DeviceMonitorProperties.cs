// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using HoloLensCommander.Device;
using Microsoft.Tools.WindowsDevicePortal;
using static Microsoft.Tools.WindowsDevicePortal.DevicePortal;

namespace HoloLensCommander
{
    /// <summary>
    /// Class that provides the relevant functionality of the Windows Device Portal.
    /// </summary>
    public partial class DeviceMonitor
    {
        /// <summary>
        /// Returns the address (and port if explicitly specified) used to communicate with the device.
        /// </summary>
        public string Address
        {
            get
            {
                return (this.devicePortal == null) ? string.Empty : this.devicePortal.Address;
            }
        }

        public Uri DevicePortalUri
        {
            get 
            {
                return (this.devicePortal == null) ? null : this.devicePortalConnection.Connection;
            }
        }

        /// <summary>
        /// Most recently reported status of the device portal connection
        /// </summary>
        public DeviceConnectionStatus DeviceConnectionStatus { get; private set; } = DeviceConnectionStatus.None;

        /// <summary>
        /// Returns the most recently cached battery state data.
        /// </summary>
        public BatteryState BatteryState { get; private set; }

        /// <summary>
        /// Returns the family of the attached device.
        /// </summary>
        public string DeviceFamily
        {
            get
            {
                return (this.devicePortal == null) ? string.Empty : this.devicePortal.DeviceFamily;
            }
        }

        /// <summary>
        /// Returns the most recently cached value of the user's interpupilary distance.
        /// </summary>
        public float Ipd { get; private set; }

        /// <summary>
        /// Returns the status of Kiosk mode on this device
        /// </summary>
        public KioskModeStatus KioskModeStatus
            { get; private set; } = new DevicePortal.KioskModeStatus(); // default to null object that says it's not supported.

        public RunningProcesses RunningProcesses
            { get; private set; } = new DevicePortal.RunningProcesses(); // default to empty list

        private bool retrieveRunningProcesses = false;
        public bool RetrieveRunningProcesses
        {
            get
            {
                return retrieveRunningProcesses;
            }
            set
            {
                this.retrieveRunningProcesses = value;
                if (!retrieveRunningProcesses)
                {
                    // Empty list
                    this.RunningProcesses = new DevicePortal.RunningProcesses();
                }
            }
        }

        /// <summary>
        /// Get or set the cached name of the connected device.
        /// </summary>
        public string MachineName { get; private set; } = string.Empty;

        /// <summary>
        /// Returns the version of the operating system.
        /// </summary>
        public string OperatingSystemVersion
        {
            get
            {
                return (this.devicePortal == null) ? string.Empty : this.devicePortal.OperatingSystemVersion;
            }
        }

        /// <summary>
        /// Returns the most recently cached device name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the most recently cached device network (ssid) name.
        /// </summary>
        public string NetworkName { get; private set; }

        public DevicePortalPlatforms Platform
        {
            get
            {
                return (this.devicePortal == null) ? DevicePortalPlatforms.Unknown : this.devicePortal.Platform;
            }
        }

        public string PlatformName
        {
            get
            {
                return (this.devicePortal == null) ? string.Empty : this.devicePortal.PlatformName;
            }
        }

        /// <summary>
        /// Returns the most recently cached thermal stage.
        /// </summary>
        public ThermalStages ThermalStage { get; private set; }

        public JobQueue JobQueue { get { return this.jobQueue; } }

    }
}
