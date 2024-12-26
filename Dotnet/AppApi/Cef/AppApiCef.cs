// Copyright(c) 2019-2022 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
#if LINUX
#else
using CefSharp;
#endif
using librsync.net;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using NLog;

namespace VRCX
{
    public partial class AppApiCef : AppApiCommon
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Shows the developer tools for the main browser window.
        /// </summary>
        public override void ShowDevTools()
        {
#if LINUX
#else
            MainForm.Instance.Browser.ShowDevTools();
#endif
        }

        /// <summary>
        /// Deletes all cookies from the global cef cookie manager.
        /// </summary>
        public override void DeleteAllCookies()
        {
#if LINUX
#else
            Cef.GetGlobalCookieManager().DeleteCookies();
#endif
        }

        public override void SetVR(bool active, bool hmdOverlay, bool wristOverlay, bool menuButton, int overlayHand)
        {
#if LINUX
#else
            Program.VRCXVRInstance.SetActive(active, hmdOverlay, wristOverlay, menuButton, overlayHand);
#endif
        }

        public override void RefreshVR()
        {
#if LINUX
#else
            Program.VRCXVRInstance.Restart();
#endif
        }

        public override void RestartVR()
        {
#if LINUX
#else
            Program.VRCXVRInstance.Restart();
#endif
        }

        public override void SetZoom(double zoomLevel)
        {
#if LINUX
#else
            MainForm.Instance.Browser.SetZoomLevel(zoomLevel);
#endif
        }

        public override async Task<double> GetZoom()
        {
#if LINUX
            return -1.0f;
#else
            return await MainForm.Instance.Browser.GetZoomLevelAsync();
#endif
        }

        public override void DesktopNotification(string BoldText, string Text = "", string Image = "")
        {
            try
            {
                ToastContentBuilder builder = new ToastContentBuilder();

                if (Uri.TryCreate(Image, UriKind.Absolute, out Uri uri))
                    builder.AddAppLogoOverride(uri);

                if (!string.IsNullOrEmpty(BoldText))
                    builder.AddText(BoldText);

                if (!string.IsNullOrEmpty(Text))
                    builder.AddText(Text);
#if LINUX
#else
                builder.Show();
#endif
            }
            catch (System.AccessViolationException ex)
            {
                logger.Warn(ex, "Unable to send desktop notification");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unknown error when sending desktop notification");
            }
        }
        
        public override void RestartApplication(bool isUpgrade)
        {
            var args = new List<string>();

            if (isUpgrade)
                args.Add(StartupArgs.VrcxLaunchArguments.IsUpgradePrefix);

            if (StartupArgs.LaunchArguments.IsDebug)
                args.Add(StartupArgs.VrcxLaunchArguments.IsDebugPrefix);

            if (!string.IsNullOrWhiteSpace(StartupArgs.LaunchArguments.ConfigDirectory))
                args.Add($"{StartupArgs.VrcxLaunchArguments.ConfigDirectoryPrefix}={StartupArgs.LaunchArguments.ConfigDirectory}");

            if (!string.IsNullOrWhiteSpace(StartupArgs.LaunchArguments.ProxyUrl))
                args.Add($"{StartupArgs.VrcxLaunchArguments.ProxyUrlPrefix}={StartupArgs.LaunchArguments.ProxyUrl}");

            var vrcxProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Program.BaseDirectory, "VRCX.exe"),
                    Arguments = string.Join(' ', args),
                    UseShellExecute = true,
                    WorkingDirectory = Program.BaseDirectory
                }
            };
            vrcxProcess.Start();
            Environment.Exit(0);
        }
        
        public override bool CheckForUpdateExe()
        {
            return File.Exists(Path.Combine(Program.AppDataDirectory, "update.exe"));
        }
        
        public override void ExecuteAppFunction(string function, string json)
        {
#if LINUX
#else
            if (MainForm.Instance?.Browser != null && !MainForm.Instance.Browser.IsLoading && MainForm.Instance.Browser.CanExecuteJavascriptInMainFrame)
                MainForm.Instance.Browser.ExecuteScriptAsync($"$app.{function}", json);
#endif
        }

        public override void ExecuteVrFeedFunction(string function, string json)
        {
#if LINUX
#else
            Program.VRCXVRInstance.ExecuteVrFeedFunction(function, json);
#endif
        }

        public override void ExecuteVrOverlayFunction(string function, string json)
        {
#if LINUX
#else
            Program.VRCXVRInstance.ExecuteVrOverlayFunction(function, json);
#endif
        }

        public override string GetLaunchCommand()
        {
            var command = StartupArgs.LaunchArguments.LaunchCommand;
            StartupArgs.LaunchArguments.LaunchCommand = string.Empty;
            return command;
        }
        
        public override void FocusWindow()
        {
#if LINUX
#else
            MainForm.Instance.Invoke(new Action(() => { MainForm.Instance.Focus_Window(); }));
#endif
        }

        public override void ChangeTheme(int value)
        {
#if LINUX
#else
            WinformThemer.SetGlobalTheme(value);
#endif
        }

        public override void DoFunny()
        {
#if LINUX
#else
            WinformThemer.DoFunny();
#endif
        }
        
        public override string GetClipboard()
        {
            var clipboard = string.Empty;
#if LINUX
#else
            var thread = new Thread(() => clipboard = Clipboard.GetText());
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
#endif
            return clipboard;
        }

        public override void SetStartup(bool enabled)
        {
#if LINUX
#else
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key == null)
                {
                    logger.Warn("Failed to open startup registry key");
                    return;
                }
                
                if (enabled)
                {
                    var path = Application.ExecutablePath;
                    key.SetValue("VRCX", $"\"{path}\" --startup");
                }
                else
                {
                    key.DeleteValue("VRCX", false);
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "Failed to set startup");
            }
#endif
        }

        public override void CopyImageToClipboard(string path)
        {
            if (!File.Exists(path) ||
                (!path.EndsWith(".png") &&
                 !path.EndsWith(".jpg") &&
                 !path.EndsWith(".jpeg") &&
                 !path.EndsWith(".gif") &&
                 !path.EndsWith(".bmp") &&
                 !path.EndsWith(".webp")))
                return;
            
            MainForm.Instance.BeginInvoke(new MethodInvoker(() =>
            {
                var image = Image.FromFile(path);
                // Clipboard.SetImage(image);
                var data = new DataObject();
                data.SetData(DataFormats.Bitmap, image);
                data.SetFileDropList(new StringCollection { path });
                Clipboard.SetDataObject(data, true);
            }));
        }
        
        public override void FlashWindow()
        {
#if LINUX
#else
            MainForm.Instance.BeginInvoke(new MethodInvoker(() => { WinformThemer.Flash(MainForm.Instance); }));
#endif
        }
        
        public override void SetUserAgent()
        {
            using var client = MainForm.Instance.Browser.GetDevToolsClient();
            _ = client.Network.SetUserAgentOverrideAsync(Program.Version);
        }

        public override bool IsRunningUnderWine()
        {
#if LINUX
            return false;
#else
            return Wine.GetIfWine();
#endif
        }
    }
}