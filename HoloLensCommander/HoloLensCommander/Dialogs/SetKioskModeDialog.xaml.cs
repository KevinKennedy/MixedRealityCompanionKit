using System.Collections.Generic;
using Microsoft.Tools.WindowsDevicePortal;
using Windows.UI.Xaml.Controls;

namespace HoloLensCommander
{
    public sealed partial class SetKioskModeDialog : ContentDialog
    {
        private SetKioskModeDialogViewModel viewModel;

        public bool KioskModeEnabled { get; private set; }
        public DevicePortal.PackageInfo StartupAppPackageInfo { get; private set; }

        public SetKioskModeDialog(bool kioskModeEnabled, List<DevicePortal.PackageInfo> installedApps, DevicePortal.PackageInfo startupAppPackageInfo)
        {
            this.InitializeComponent();

            this.viewModel = new SetKioskModeDialogViewModel(kioskModeEnabled, installedApps, startupAppPackageInfo);
            this.DataContext = this.viewModel;
        }

        private void OkButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            this.KioskModeEnabled = this.viewModel.KioskModeEnabled;
            this.StartupAppPackageInfo = this.viewModel.StartupAppPackageInfo;
        }
    }
}
