using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;

namespace msovideo_srgb
{
    public partial class MainWindow
    {
        private static readonly Guid GUID_CONSOLE_DISPLAY_STATE =
            new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(
            IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        private readonly MainViewModel _viewModel;

        private ContextMenu _contextMenu;
        private NotifyIcon _notifyIcon;
        private IntPtr _powerNotificationHandle;

        public MainWindow()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Already running!");
                Close();
                return;
            }

            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;
            SystemEvents.DisplaySettingsChanged += _viewModel.OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += _viewModel.OnPowerModeChanged;
            SystemEvents.SessionSwitch += _viewModel.OnSessionSwitch;

            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0);

            if (args.Contains("-minimize"))
            {
                WindowState = WindowState.Minimized;
                Hide();
            }

            InitializeTrayIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource?.AddHook(WndProc);

            var guid = GUID_CONSOLE_DISPLAY_STATE;
            _powerNotificationHandle = RegisterPowerSettingNotification(
                new WindowInteropHelper(this).Handle, ref guid, 0);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_POWERBROADCAST && (int)wParam == PBT_POWERSETTINGCHANGE)
            {
                var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
                if (setting.PowerSetting == GUID_CONSOLE_DISPLAY_STATE && setting.Data == 1)
                {
                    // Display turned on — reapply calibration
                    ReapplyMonitorSettings();
                }
            }

            return IntPtr.Zero;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs o)
        {
            var window = new AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.Cast<Window>().Any(x => x is AdvancedWindow)) return;
            var monitor = ((FrameworkElement)sender).DataContext as MonitorData;
            var window = new AdvancedWindow(monitor)
            {
                Owner = this
            };

            void CloseWindow(object o, EventArgs e2) => window.Close();

            SystemEvents.DisplaySettingsChanged += CloseWindow;
            try
            {
                if (window.ShowDialog() == false) return;
            }
            finally
            {
                SystemEvents.DisplaySettingsChanged -= CloseWindow;
            }

            if (window.ChangedCalibration)
            {
                _viewModel.SaveConfig();
                monitor?.ReapplyClamp();
            }
        }

        private void ReapplyButton_Click(object sender, RoutedEventArgs e)
        {
            ReapplyMonitorSettings();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "Msovideo sRGB",
                Icon = Properties.Resources.icon,
                Visible = true
            };

            _notifyIcon.MouseDoubleClick +=
                delegate
                {
                    Show();
                    WindowState = WindowState.Normal;
                };

            _contextMenu = new ContextMenu();

            _contextMenu.Popup += delegate { UpdateContextMenu(); };

            _notifyIcon.ContextMenu = _contextMenu;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (_powerNotificationHandle != IntPtr.Zero)
                {
                    UnregisterPowerSettingNotification(_powerNotificationHandle);
                }

                var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                hwndSource?.RemoveHook(WndProc);
            }
            catch { }

            SystemEvents.DisplaySettingsChanged -= _viewModel.OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= _viewModel.OnPowerModeChanged;
            SystemEvents.SessionSwitch -= _viewModel.OnSessionSwitch;

            _notifyIcon?.Dispose();
            base.OnClosed(e);
        }

        private void UpdateContextMenu()
        {
            while (_contextMenu.MenuItems.Count > 0)
            {
                _contextMenu.MenuItems[0].Dispose();
            }

            foreach (var monitor in _viewModel.Monitors)
            {
                var item = new MenuItem();
                _contextMenu.MenuItems.Add(item);
                item.Text = monitor.Name;
                item.Checked = monitor.Clamped;
                item.Enabled = monitor.CanClamp;
                item.Click += (sender, args) => monitor.Clamped = !monitor.Clamped;
            }

            _contextMenu.MenuItems.Add("-");

            var reapplyItem = new MenuItem();
            _contextMenu.MenuItems.Add(reapplyItem);
            reapplyItem.Text = "Reapply";
            reapplyItem.Click += delegate { ReapplyMonitorSettings(); };

            var exitItem = new MenuItem();
            _contextMenu.MenuItems.Add(exitItem);
            exitItem.Text = "Exit";
            exitItem.Click += delegate { Close(); };
        }

        private void ReapplyMonitorSettings()
        {
            foreach (var monitor in _viewModel.Monitors)
            {
                monitor.ReapplyClamp();
            }
        }
    }
}