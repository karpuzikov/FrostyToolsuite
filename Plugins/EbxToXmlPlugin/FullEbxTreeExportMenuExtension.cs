using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;

namespace EbxToXmlPlugin
{
    public class FullEbxTreeExportMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Export Full EBX Tree";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Choose where the full EBX tree export will be created.";

                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                string exportRoot = Path.Combine(
                    fbd.SelectedPath,
                    "Frosty_EBX_Export_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));

                string xmlRoot = Path.Combine(exportRoot, "XML");
                Directory.CreateDirectory(xmlRoot);

                int exportedCount = 0;
                int failedCount = 0;

                FrostyTaskWindow.Show("Exporting Full EBX Tree", "", (task) =>
                {
                    List<EbxAssetEntry> entries = App.AssetManager.EnumerateEbx()
                        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    Dictionary<Guid, string> namesByGuid = new Dictionary<Guid, string>();
                    foreach (EbxAssetEntry entry in entries)
                    {
                        if (entry.Guid != Guid.Empty && !namesByGuid.ContainsKey(entry.Guid))
                            namesByGuid.Add(entry.Guid, entry.Name);
                    }

                    string treePath = Path.Combine(exportRoot, "tree.txt");
                    string manifestPath = Path.Combine(exportRoot, "assets.tsv");
                    string errorsPath = Path.Combine(exportRoot, "errors.txt");
                    string summaryPath = Path.Combine(exportRoot, "summary.txt");

                    using (StreamWriter treeWriter = CreateWriter(treePath))
                    using (StreamWriter manifestWriter = CreateWriter(manifestPath))
                    using (StreamWriter errorWriter = CreateWriter(errorsPath))
                    {
                        manifestWriter.WriteLine(
                            "Name\tType\tGuid\tSize\tOriginalSize\tLocation\tSha1\tDependencyCount\tDependencies");

                        int totalCount = entries.Count;

                        for (int index = 0; index < totalCount; index++)
                        {
                            EbxAssetEntry entry = entries[index];
                            double progress = totalCount == 0
                                ? 100.0d
                                : ((index + 1) / (double)totalCount) * 100.0d;

                            task.Update(entry.Name, progress);
                            treeWriter.WriteLine(entry.Name);

                            List<string> dependencyDescriptions = new List<string>();
                            foreach (Guid dependencyGuid in entry.EnumerateDependencies())
                            {
                                string dependencyName;
                                if (namesByGuid.TryGetValue(dependencyGuid, out dependencyName))
                                    dependencyDescriptions.Add(dependencyGuid + "=" + dependencyName);
                                else
                                    dependencyDescriptions.Add(dependencyGuid.ToString());
                            }

                            manifestWriter.WriteLine(string.Join("\t", new[]
                            {
                                ToTsv(entry.Name),
                                ToTsv(entry.Type),
                                entry.Guid.ToString(),
                                entry.Size.ToString(),
                                entry.OriginalSize.ToString(),
                                entry.Location.ToString(),
                                ToTsv(entry.Sha1.ToString()),
                                dependencyDescriptions.Count.ToString(),
                                ToTsv(string.Join(" | ", dependencyDescriptions))
                            }));

                            try
                            {
                                string relativeDirectory = MakeSafeRelativePath(entry.Path);
                                string outputDirectory = string.IsNullOrEmpty(relativeDirectory)
                                    ? xmlRoot
                                    : Path.Combine(xmlRoot, relativeDirectory);

                                Directory.CreateDirectory(outputDirectory);

                                string safeFilename = MakeSafeFileName(entry.Filename);
                                if (string.IsNullOrWhiteSpace(safeFilename))
                                    safeFilename = entry.Guid.ToString();

                                string outputPath = Path.Combine(outputDirectory, safeFilename + ".xml");

                                EbxAsset asset = App.AssetManager.GetEbx(entry);
                                if (asset == null)
                                    throw new InvalidDataException("AssetManager.GetEbx returned null.");

                                using (EbxXmlWriter writer = new EbxXmlWriter(
                                    new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read),
                                    App.AssetManager))
                                {
                                    writer.WriteObjects(asset.Objects);
                                }

                                exportedCount++;
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                errorWriter.WriteLine(entry.Name);
                                errorWriter.WriteLine(ex.ToString());
                                errorWriter.WriteLine(new string('-', 80));
                                App.Logger.Log("Failed to export {0}: {1}", entry.Name, ex.Message);
                            }
                        }
                    }

                    using (StreamWriter summaryWriter = CreateWriter(summaryPath))
                    {
                        summaryWriter.WriteLine("Frosty Full EBX Tree Export");
                        summaryWriter.WriteLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        summaryWriter.WriteLine("Total EBX assets: " + (exportedCount + failedCount));
                        summaryWriter.WriteLine("Exported XML assets: " + exportedCount);
                        summaryWriter.WriteLine("Failed XML assets: " + failedCount);
                        summaryWriter.WriteLine();
                        summaryWriter.WriteLine("Files:");
                        summaryWriter.WriteLine("  XML\\       Full EBX object graphs, preserving the Frostbite asset path.");
                        summaryWriter.WriteLine("  assets.tsv   Searchable metadata, GUIDs and resolved EBX dependencies.");
                        summaryWriter.WriteLine("  tree.txt     Complete Frostbite EBX asset path list.");
                        summaryWriter.WriteLine("  errors.txt   Assets that could not be decoded/exported.");
                    }
                });

                FrostyMessageBox.Show(
                    "Full EBX tree export complete.\n\n" +
                    "Exported: " + exportedCount + "\n" +
                    "Failed: " + failedCount + "\n\n" +
                    exportRoot,
                    "Frosty Editor");
            }
        });

        private static StreamWriter CreateWriter(string path)
        {
            return new StreamWriter(path, false, new UTF8Encoding(false));
        }

        private static string ToTsv(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\t", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string MakeSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string[] segments = path
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            return Path.Combine(segments.Select(MakeSafeFileName).ToArray());
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);

            foreach (char c in name)
                builder.Append(invalidChars.Contains(c) ? '_' : c);

            string result = builder.ToString().Trim().TrimEnd('.');

            if (IsReservedWindowsName(result))
                result = "_" + result;

            return result;
        }

        private static bool IsReservedWindowsName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string baseName = name.Split('.')[0].ToUpperInvariant();

            if (baseName == "CON" || baseName == "PRN" || baseName == "AUX" || baseName == "NUL")
                return true;

            if (baseName.Length == 4)
            {
                string prefix = baseName.Substring(0, 3);
                char suffix = baseName[3];

                if ((prefix == "COM" || prefix == "LPT") && suffix >= '1' && suffix <= '9')
                    return true;
            }

            return false;
        }
    }
}
