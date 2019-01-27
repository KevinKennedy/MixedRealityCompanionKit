// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Tools.WindowsDevicePortal;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.UI.Core;

namespace HoloLensCommander
{
    /// <summary>
    /// Application install status event handler.
    /// </summary>
    /// <param name="sender">The DeviceMonitor sending the event.</param>
    /// <param name="args">Install status information.</param>
    public delegate void DeviceMonitorAppInstallStatusEventHandler(
        DeviceMonitor sender, 
        ApplicationInstallStatusEventArgs args);

    /// <summary>
    /// File upload status event handler
    /// </summary>
    /// <param name="sender">The DeviceMonitor sending the event.</param>
    /// <param name="message">The status</param>
    public delegate void DeviceMonitorFileUploadStatusEventHandler(
        DeviceMonitor sender,
        string message);

    /// <summary>
    /// Delegate defining the method signature for handling the DeviceMonitorUpdated event.
    /// </summary>
    /// <param name="sender">The object sending the event.</param>
    public delegate void DeviceMonitorUpdatedEventHandler(
        DeviceMonitor sender);

    /// <summary>
    /// Class that provides the relevant functionality of the Windows Device Portal.
    /// </summary>
    public partial class DeviceMonitor : IDisposable
    {
        /// <summary>
        /// The default address used when connecting to a device. This address assumes
        /// a USB connection.
        /// </summary>
        public static readonly string DefaultConnectionAddress = "localhost:10080";
        public static readonly string DefaultConnectionAddressAsIp = "127.0.0.1:10080";
        
        /// <summary>
        /// The file names of applications that will remain running after the user
        /// requests all apps to be closed.
        /// </summary>
        private static readonly string[] DoNotCloseApps = new string[] 
            {
                "HoloShellApp.exe",
                "MixedRealityPortal.exe"
            };

        /// <summary>
        /// Options used to connect to this device.
        /// </summary>
        /// <remarks>
        /// This data is displayed until an initial connection is made to the device.
        /// </remarks>
        private ConnectOptions connectOptions;

        /// <summary>
        /// Dispatcher that allows heartbeats to be marshaled appropriately.
        /// </summary>
        private CoreDispatcher dispatcher;

        /// <summary>
        /// Instance of the IDevicePortalConnection used to connect to this device.
        /// </summary>
        private IDevicePortalConnection devicePortalConnection;

        /// <summary>
        /// Instance of the DevicePortal used to communicate with this device.
        /// </summary>
        private DevicePortal devicePortal;

        /// <summary>
        /// Set to true when we are trying to connect.  Prevents multiple
        /// simultanious connection attempts.
        /// </summary>
        private bool isConnecting = false;

        /// <summary>
        /// Used to cancel the heartbeat
        /// </summary>
        private CancellationTokenSource heartbeatCancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Event that is sent when the application install status has changed.
        /// </summary>
        public event DeviceMonitorAppInstallStatusEventHandler AppInstallStatus;

        /// <summary>
        /// Event that is sent when file upload status changed
        /// </summary>
        public event DeviceMonitorFileUploadStatusEventHandler FileUploadStatus;

        /// <summary>
        /// Event that is sent when the heartbeat has been received.
        /// </summary>
        public event DeviceMonitorUpdatedEventHandler Updated;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceMonitor" /> class.
        /// </summary>
        public DeviceMonitor(CoreDispatcher dispatcher, ConnectOptions connectOptions)
        {
            if (dispatcher == null)
            {
                throw new NullReferenceException("The argument dispatcher cannot be null.");
            }

            if(connectOptions == null)
            {
                throw new NullReferenceException("The argument connectOptions cannot be null.");
            }

            this.dispatcher = dispatcher;
            this.connectOptions = connectOptions;

            var heartbeatTask = this.dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => { await HeartbeatAsync(); });
        }

        private async Task HeartbeatAsync()
        {
            CancellationToken cancellationToken = this.heartbeatCancellationTokenSource.Token;

            while(true)
            {
                try
                {
                    await this.EnsureConnectionAsync(cancellationToken);

                    this.MachineName = await this.GetMachineNameAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.UpdateBatteryStatus();
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.UpdateIpd();
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.UpdateThermalStage();
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.UpdateKioskModeStatus();
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.UpdateRunningProcessList();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (TaskCanceledException)
                {
                    // Only bail if we were canceled explicitly
                    throw;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"DeviceMonitor.HeartbeatAsync - ate exception: {e.ToString()}");
                }

                Debug.Assert(this.dispatcher.HasThreadAccess); // we should always be on the UI thread for sending events
                this.Updated?.Invoke(this);

                await Task.Delay(TimeSpan.FromSeconds(5.0), cancellationToken); // TODO: Make this timeout configurable
            }
        }

        /// <summary>
        /// Finalizer so that we are assured we clean up all encapsulated resources.
        /// </summary>
        /// <remarks>Call Dispose on this object to avoid running the finalizer.</remarks>
        ~DeviceMonitor()
        {
            this.Dispose();
        }

        /// <summary>
        /// Cleans up encapsulated resources.
        /// </summary>
        public void Dispose()
        {
            this.heartbeatCancellationTokenSource.Cancel();

            if (this.devicePortal != null)
            {
                this.devicePortal.ConnectionStatus -= this.DevicePortalConnectionStatus;
                this.devicePortal.AppInstallStatus -= this.DevicePortalAppInstallStatus;
                this.devicePortal = null;
            }

            // Suppress finalization to avoid attempting to clean up twice.
            GC.SuppressFinalize(this);
        }

        private string FixupMachineAddress(string address)
        {
            address = address.ToLower();

            // Insert http if needed
            if (!address.StartsWith("http"))
            {
                string scheme = "https";

                if (string.Equals(address, DefaultConnectionAddress) ||
                    string.Equals(address, DefaultConnectionAddressAsIp))
                {
                    scheme = "http";
                }

                address = string.Format(
                    "{0}://{1}",
                    scheme,
                    address);
            }

            if (this.connectOptions.ConnectingToDesktopPC)
            {
                string s = address.Substring(address.IndexOf("//"));
                if (!s.Contains(":"))
                {
                    // Append the default Windows Device Portal port for Desktop PC connections.
                    address += ":50443";
                }
            }

            return address;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>Note that we let exceptions leave this function.  We depend on caller for any retry</remarks>
        private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
        {
            // We should always be on the UI thread
            Debug.Assert(this.dispatcher.HasThreadAccess);

            // Make sure we're not trying multiple connections.
            // This logic assumes one thread.  There will be a race condition
            // with the isConnecting field if multiple threads are executing this.
            if(this.isConnecting)
            {
                // we're already trying to connect on another task.  Wait
                await WaitForCondition(TimeSpan.FromSeconds(2.0), cancellationToken, () => !this.isConnecting); // make this timeout configurable
                if(this.isConnecting)
                {
                    // Someone else is still connecting so bail
                    throw new TimeoutException("DeviceMonitor.EnsureConnectionAsync: someone else is taking too long to connect");
                }
            }
            this.isConnecting = true;

            try
            {
                if (this.devicePortalConnection == null)
                {
                    var address = FixupMachineAddress(this.connectOptions.Address);

                    this.devicePortalConnection = new DefaultDevicePortalConnection(
                            address,
                            this.connectOptions.UserName,
                            this.connectOptions.Password);
                    this.devicePortal = new DevicePortal(this.devicePortalConnection);
                    this.devicePortal.ConnectionStatus += DevicePortalConnectionStatus;
                    this.devicePortal.AppInstallStatus += DevicePortalAppInstallStatus;
                }

                if (this.DeviceConnectionStatus != DeviceConnectionStatus.Connected || this.DeviceConnectionStatus != DeviceConnectionStatus.Connecting)
                {
                    Certificate certificate = null;

                    if (!this.connectOptions.UseInstalledCertificate)
                    {
                        // Get the device certificate
                        certificate = await this.devicePortal.GetRootDeviceCertificateAsync(true);
                    }

                    // Establish the connection to the device.
                    // No way to cancel this :-|
                    await this.devicePortal.ConnectAsync(
                        ssid: this.connectOptions.Ssid,
                        ssidKey: this.connectOptions.NetworkKey,
                        updateConnection: this.connectOptions.UpdateConnection,
                        manualCertificate: certificate);
                }
            }
            finally
            {
                this.isConnecting = false;
            }
        }

        private void DevicePortalConnectionStatus(DevicePortal sender, DeviceConnectionStatusEventArgs args)
        {
            // This can get called back on random threads.  Marshal it back to the UI thread if needed

            if (this.dispatcher.HasThreadAccess)
            {
                this.DeviceConnectionStatus = args.Status;
                if(!this.isConnecting)
                {
                    this.Updated?.Invoke(this);
                }
            }
            else
            {
                var _ = this.dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.DevicePortalConnectionStatus(sender, args); });
            }
        }

        private static async Task WaitForCondition(TimeSpan timeout, CancellationToken cancellationToken, Func<bool> condition)
        {
            DateTime giveUpTime = DateTime.UtcNow;
            while(!condition())
            {
                await Task.Delay(200, cancellationToken);
                if(DateTime.UtcNow >= giveUpTime)
                {
                    // Maybe throw an exception instead>
                    return;
                }
            }
        }

        /// <summary>
        /// Updates the cached battery data.
        /// </summary>
        /// <returns>Task object used for tracking method completion.</returns>
        private async Task UpdateBatteryStatus()
        {
            this.BatteryState = await this.devicePortal.GetBatteryStateAsync();
        }
        
        /// <summary>
        /// Updates the cached interpupilary distance data.
        /// </summary>
        /// <returns>Task object used for tracking method completion.</returns>
        private async Task UpdateIpd()
        {
            try
            {
                this.Ipd = await this.devicePortal.GetInterPupilaryDistanceAsync();
            }
            catch(NotSupportedException)
            {
                // Not supported on this type of device.
            }
        }

        /// <summary>
        /// Updates the cached kiosk mode state
        /// </summary>
        /// <returns>Task object used for tracking method completion.</returns>
        private async Task UpdateKioskModeStatus()
        {
            try
            {
                this.KioskModeStatus = await this.devicePortal.GetKioskModeStatusAsync();
            }
            catch (NotSupportedException)
            {
                // Not supported on this type of device.
            }
        }

        /// <summary>
        /// Updates the cached list of running processes
        /// </summary>
        /// <returns></returns>
        private async Task UpdateRunningProcessList()
        {
            try
            {
                if (this.RetrieveRunningProcesses)
                {
                    this.RunningProcesses = await this.devicePortal.GetRunningProcessesAsync();
                }
            }
            catch(Exception e)
            {
                string message = e.Message;
                var dpe = e as DevicePortalException;
                if(dpe != null)
                {
                    message = dpe.Reason + " - " + e.Message;
                }

                Debug.WriteLine("UpdateRunningProcessList: Failed:" + message);
            }
        }


        /// <summary>
        /// Updates the cached thermal data.
        /// </summary>
        /// <returns>Task object used for tracking method completion.</returns>
        private async Task UpdateThermalStage()
        {
            try
            {
                this.ThermalStage = await this.devicePortal.GetThermalStageAsync();
            }
            catch(NotSupportedException)
            {
                // Not supported on this type of device.
            }
        }
    }
}
