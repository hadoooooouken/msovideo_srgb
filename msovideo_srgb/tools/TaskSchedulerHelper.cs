using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace msovideo_srgb
{
    public static class TaskSchedulerHelper
    {
        public static void EnsureCalibrationLoaderTrigger()
        {
            try
            {
                string taskName = @"\Microsoft\Windows\WindowsColorSystem\Calibration Loader";

                // Export current task XML
                var exportProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/query /tn \"{taskName}\" /xml",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                exportProcess.Start();
                string xmlContent = exportProcess.StandardOutput.ReadToEnd();
                exportProcess.WaitForExit();

                if (exportProcess.ExitCode != 0 || string.IsNullOrWhiteSpace(xmlContent))
                {
                    // Failed to export task, maybe task doesn't exist
                    return;
                }

                // Parse XML
                XDocument doc = XDocument.Parse(xmlContent);
                XNamespace ns = doc.Root.GetDefaultNamespace();

                var triggersNode = doc.Descendants(ns + "Triggers").FirstOrDefault();
                if (triggersNode == null) return;

                bool hasTrigger507 = false;

                // Check specifically for our Kernel-Power 507 trigger content
                foreach (var eventTrigger in triggersNode.Elements(ns + "EventTrigger"))
                {
                    var subscription = eventTrigger.Element(ns + "Subscription")?.Value;
                    if (subscription != null && subscription.Contains("Microsoft-Windows-Kernel-Power") && subscription.Contains("EventID=507"))
                    {
                        hasTrigger507 = true;
                        break;
                    }
                }

                if (!hasTrigger507)
                {
                    // Inject the trigger
                    var newTrigger = new XElement(ns + "EventTrigger",
                        new XElement(ns + "Enabled", "true"),
                        new XElement(ns + "Subscription", "<QueryList><Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=507]]</Select></Query></QueryList>")
                    );

                    triggersNode.Add(newTrigger);

                    string tempXmlFile = Path.Combine(Path.GetTempPath(), "msovideo_srgb_calibration_loader.xml");
                    doc.Save(tempXmlFile);

                    // Import the new XML with elevation
                    var importProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/create /tn \"{taskName}\" /xml \"{tempXmlFile}\" /f",
                            UseShellExecute = true,
                            Verb = "runas" // Request elevation
                        }
                    };

                    try
                    {
                        importProcess.Start();
                        importProcess.WaitForExit();
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // User cancelled UAC prompt
                    }
                    finally
                    {
                        if (File.Exists(tempXmlFile))
                        {
                            File.Delete(tempXmlFile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently fail if something goes wrong, we don't want to crash the main app
                Debug.WriteLine($"Failed to ensure Calibration Loader trigger: {ex.Message}");
            }
        }
    }
}
