using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;
namespace msovideo_srgb
{
    public class MainViewModel
    {
        public ObservableCollection<MonitorData> Monitors { get; }

        private static int _stateId = 0;
        private string _configPath;

        private string _startupName;
        private RegistryKey _startupKey;
        private string _startupValue;

        public MainViewModel()
        {
            Monitors = new ObservableCollection<MonitorData>();
            _configPath = AppDomain.CurrentDomain.BaseDirectory + "config.xml";

            _startupName = "msovideo_srgb";
            _startupKey = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            _startupValue = Application.ExecutablePath + " -minimize";

            UpdateMonitors();
        }

        public bool? RunAtStartup
        {
            get
            {
                if (_startupKey == null) return false;
                var keyValue = _startupKey.GetValue(_startupName);

                if (keyValue == null)
                {
                    return false;
                }

                if (keyValue as string == _startupValue)
                {
                    return true;
                }

                return null;
            }
            set
            {
                if (_startupKey == null) return;
                if (value == true)
                {
                    _startupKey.SetValue(_startupName, _startupValue);
                }
                else
                {
                    _startupKey.DeleteValue(_startupName);
                }
            }
        }

        private void UpdateMonitors()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => Monitors.Clear());
            List<XElement> config = null;
            try
            {
                if (File.Exists(_configPath))
                {
                    config = XElement.Load(_configPath).Descendants("monitor").ToList();
                }
            }
            catch
            {
                config = null;
            }

            var hdrPaths = DisplayConfigManager.GetHdrDisplayPaths();

            var number = 1;
            foreach (var display in WindowsDisplayAPI.Display.GetDisplays())
            {
                var path = display.DevicePath;

                var hdrActive = hdrPaths.Contains(path);

                var settings = config?.FirstOrDefault(x => (string)x.Attribute("path") == path);
                MonitorData monitor;
                if (settings != null)
                {
                    monitor = new MonitorData(this, number++, display, path, hdrActive,
                        (bool?)settings.Attribute("clamp_sdr") ?? false,
                        (bool?)settings.Attribute("use_icc") ?? false,
                        (string)settings.Attribute("icc_path") ?? "",
                        (bool?)settings.Attribute("calibrate_gamma") ?? false,
                        (int?)settings.Attribute("selected_gamma") ?? 0,
                        (double?)settings.Attribute("custom_gamma") ?? 2.2,
                        (double?)settings.Attribute("custom_percentage") ?? 100,
                        (int?)settings.Attribute("target_white") ?? 0,
                        (double?)settings.Attribute("custom_white_x") ?? Colorimetry.D65.X,
                        (double?)settings.Attribute("custom_white_y") ?? Colorimetry.D65.Y,
                        (bool?)settings.Attribute("report_white_d65") ?? false,
                        (bool?)settings.Attribute("report_color_space_srgb") ?? false,
                        (bool?)settings.Attribute("report_gamma_srgb") ?? false,
                        (int?)settings.Attribute("target") ?? 0,
                        (int?)settings.Attribute("resolution") ?? 2,
                        (bool?)settings.Attribute("use_icc_hdr") ?? false,
                        (string)settings.Attribute("icc_path_hdr") ?? "",
                        (bool?)settings.Attribute("calibrate_gamma_hdr") ?? false,
                        (int?)settings.Attribute("target_peak") ?? 10000,
                        (double?)settings.Attribute("bpc_threshold") ?? 80,
                        (int?)settings.Attribute("target_white_hdr") ?? 0,
                        (double?)settings.Attribute("custom_white_hdr_x") ?? Colorimetry.D65.X,
                        (double?)settings.Attribute("custom_white_hdr_y") ?? Colorimetry.D65.Y);
                }
                else
                {
                    monitor = new MonitorData(this, number++, display, path, hdrActive, false);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => Monitors.Add(monitor));
            }

            foreach (var monitor in Monitors.ToList())
            {
                monitor.ReapplyClamp();
            }
        }

        public void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            int currentId = ++_stateId;
            Thread.Sleep(100);
            if (_stateId == currentId)
            {
                UpdateMonitors();
            }
        }

        public void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            OnDisplaySettingsChanged(null, null);
        }

        public void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                OnDisplaySettingsChanged(null, null);
            }
        }

        public void SaveConfig()
        {
            try
            {
                List<XElement> monitors = new List<XElement>();
                if (File.Exists(_configPath))
                {
                    monitors = XElement.Load(_configPath).Descendants("monitor").ToList();
                }

                foreach (var m in Monitors)
                {
                    XElement monitor = new XElement("monitor", 
                            new XAttribute("path", m.Path),
                            new XAttribute("clamp_sdr", m.ClampSdr),
                            new XAttribute("use_icc", m.UseIcc),
                            new XAttribute("icc_path", m.ProfilePath),
                            new XAttribute("calibrate_gamma", m.CalibrateGamma),
                            new XAttribute("selected_gamma", m.SelectedGamma),
                            new XAttribute("custom_gamma", m.CustomGamma),
                            new XAttribute("custom_percentage", m.CustomPercentage),
                            new XAttribute("target_white", m.TargetWhite),
                            new XAttribute("custom_white_x", m.CustomWhiteX),
                            new XAttribute("custom_white_y", m.CustomWhiteY),
                            new XAttribute("report_white_d65", m.ReportWhiteD65),
                            new XAttribute("report_color_space_srgb", m.ReportColorSpaceSRGB),
                            new XAttribute("report_gamma_srgb", m.ReportGammaSRGB),
                            new XAttribute("target", m.Target),
                            new XAttribute("resolution", m.Resolution),
                            new XAttribute("use_icc_hdr", m.UseIccHDR),
                            new XAttribute("icc_path_hdr", m.ProfilePathHDR),
                            new XAttribute("calibrate_gamma_hdr", m.CalibrateGammaHDR),
                            new XAttribute("target_peak", m.TargetPeak),
                            new XAttribute("bpc_threshold", m.BPCThreshold),
                            new XAttribute("target_white_hdr", m.TargetWhiteHDR),
                            new XAttribute("custom_white_hdr_x", m.CustomWhiteHdrX),
                            new XAttribute("custom_white_hdr_y", m.CustomWhiteHdrY));

                    var existing = monitors.FirstOrDefault(x => (string)x.Attribute("path") == m.Path);
                    if (existing != null)
                    {
                        int index = monitors.IndexOf(existing);
                        monitors[index] = monitor;
                    }
                    else
                    {
                        monitors.Add(monitor);
                    }
                }

                var xElem = new XElement("monitors", monitors);
                xElem.Save(_configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\nCould not save configuration. Try making sure the folder is writable and running the program as an administrator if needed.");
            }
        }
    }
}