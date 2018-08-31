using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Tools.WindowsDevicePortal;

namespace HoloLensCommander
{
    class SetKioskModeDialogViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Event that is fired when a property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        public List<DevicePortal.PackageInfo> InstalledApps { get; private set; }

        private bool kioskModeEnabled;
        public bool KioskModeEnabled
        {
            get { return this.kioskModeEnabled; }
            set { this.PropertyChangedHelper(value, ref this.kioskModeEnabled); }
        }

        private DevicePortal.PackageInfo startupAppPackageInfo;
        public DevicePortal.PackageInfo StartupAppPackageInfo
        {
            get { return this.startupAppPackageInfo; }
            set { this.PropertyChangedHelper(value, ref this.startupAppPackageInfo); }
        }

        public SetKioskModeDialogViewModel(bool kioskModeEnabled, List<DevicePortal.PackageInfo> installedApps, DevicePortal.PackageInfo startupAppPackageInfo)
        {
            this.kioskModeEnabled = kioskModeEnabled;
            this.InstalledApps = installedApps;
            this.startupAppPackageInfo = startupAppPackageInfo;
        }

        private bool PropertyChangedHelper<T>(T newValue, ref T storage, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (IEquatable<T>.Equals(newValue, storage))
            {
                return false;
            }

            storage = newValue;

            this.SendPropertyChanged(propertyName);

            return true;
        }

        private void SendPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
