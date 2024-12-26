using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace VRCX
{
    public partial class AppApiCef
    {
        private static bool dialogOpen;

        /// <summary>
        /// Adds metadata to a PNG screenshot file and optionally renames the file to include the specified world ID.
        /// </summary>
        /// <param name="path">The path to the PNG screenshot file.</param>
        /// <param name="metadataString">The metadata to add to the screenshot file.</param>
        /// <param name="worldId">The ID of the world to associate with the screenshot.</param>
        /// <param name="changeFilename">Whether to rename the screenshot file to include the world ID.</param>
        public override string AddScreenshotMetadata(string path, string metadataString, string worldId, bool changeFilename = false)
        {
#if LINUX
            string winePrefix = LogWatcher.GetVrcPrefixPath() + "/drive_c/";
            string winePath = path.Substring(3).Replace("\\", "/");
            path = winePrefix + winePath;
#endif
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path) || !path.EndsWith(".png") || !fileName.StartsWith("VRChat_"))
                return string.Empty;

            if (changeFilename)
            {
                var newFileName = $"{fileName}_{worldId}";
                var newPath = Path.Combine(Path.GetDirectoryName(path), newFileName + Path.GetExtension(path));
                File.Move(path, newPath);
                path = newPath;
            }

            ScreenshotHelper.WritePNGDescription(path, metadataString);
            return path;
        }

        public override void OpenScreenshotFileDialog()
        {
            if (dialogOpen) return;
            dialogOpen = true;

            var thread = new Thread(() =>
            {
                using var openFileDialog = new OpenFileDialog();
                openFileDialog.DefaultExt = ".png";
                openFileDialog.Filter = "PNG Files (*.png)|*.png";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                var initialPath = GetVRChatPhotosLocation();
                if (Directory.Exists(initialPath))
                {
                    openFileDialog.InitialDirectory = initialPath;
                }

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    dialogOpen = false;
                    return;
                }

                dialogOpen = false;

                var path = openFileDialog.FileName;
                if (string.IsNullOrEmpty(path))
                    return;

                ExecuteAppFunction("screenshotMetadataResetSearch", null);
                ExecuteAppFunction("getAndDisplayScreenshot", path);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}