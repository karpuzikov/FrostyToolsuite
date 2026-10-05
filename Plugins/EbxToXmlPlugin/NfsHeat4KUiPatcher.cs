using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace EbxToXmlPlugin
{
    internal struct ScreenResolutionRule
    {
        public int Width;
        public int Height;

        public ScreenResolutionRule(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    internal static class NfsHeat4KUiPatcher
    {
        // 1.4.0 touched these. Keep the complete list so Restore can undo that build.
        private static readonly Dictionary<string, ScreenResolutionRule> LegacyScreenRules =
            new Dictionary<string, ScreenResolutionRule>(StringComparer.OrdinalIgnoreCase)
            {
                { "UI/InteractionPointScreenData", new ScreenResolutionRule(1920, 1080) },
                { "UI/MiniMapPoiScreen", new ScreenResolutionRule(512, 512) },
                { "UI/MiniMapScreen", new ScreenResolutionRule(225, 225) },
                { "UI/StretchedFullScreen", new ScreenResolutionRule(1920, 1080) },
                { "UI/StretchedFullScreenNoSafeZone", new ScreenResolutionRule(1920, 1080) },
                { "UI/AR/OffscreenIndicatorScreenData", new ScreenResolutionRule(268, 268) },
                { "UI/HUD/ARScreenData", new ScreenResolutionRule(256, 256) },
                { "UI/HUD/CarTags/HUD_AiCopCarTag_RenderTargetScreen", new ScreenResolutionRule(160, 128) },
                { "UI/HUD/CarTags/HUD_CarTag_RenderTargetScreen", new ScreenResolutionRule(650, 78) },
                { "UI/HUD/Instruments/InstrumentsScreen", new ScreenResolutionRule(512, 512) },
                { "UI/Static/StaticScreen", new ScreenResolutionRule(1920, 1080) },
                { "UI/Static/StaticScreenWithSafeZone", new ScreenResolutionRule(1920, 1080) },
                { "UI/Test/TempGarageMenuScreenData", new ScreenResolutionRule(1920, 1080) }
            };

        public static PatchResult ApplyMenuSafe()
        {
            PatchResult result = new PatchResult();

            List<EbxAssetEntry> entries = App.AssetManager.EnumerateEbx()
                .Where(entry => IsMenuPath(entry.Name))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FrostyTaskWindow.Show("Applying NFS Heat Menu 4K Patch", "", (task) =>
            {
                int total = Math.Max(1, entries.Count);

                for (int index = 0; index < entries.Count; index++)
                {
                    EbxAssetEntry entry = entries[index];
                    task.Update(entry.Name, ((index + 1) / (double)total) * 100.0d);

                    try
                    {
                        EbxAsset asset = App.AssetManager.GetEbx(entry);
                        if (asset == null || !asset.IsValid)
                            continue;

                        bool changed = false;

                        foreach (object obj in asset.Objects)
                        {
                            if (obj == null)
                                continue;

                            string typeName = obj.GetType().Name;

                            if (typeName == "MenuWidgetData" &&
                                TrySetDimensions(obj, 1920, 1080, 3840, 2160))
                            {
                                changed = true;
                                result.MenuWidgets++;
                            }
                            else if (typeName == "RimeWidgetReferenceElementData")
                            {
                                bool useWidgetWidth;
                                bool useWidgetHeight;

                                if (TryGetBool(obj, "UseWidgetWidth", out useWidgetWidth) &&
                                    TryGetBool(obj, "UseWidgetHeight", out useWidgetHeight) &&
                                    useWidgetWidth && useWidgetHeight &&
                                    TrySetDimensions(obj, 256, 256, 512, 512))
                                {
                                    changed = true;
                                    result.WidgetReferences++;
                                }
                            }
                        }

                        CommitIfChanged(entry, asset, changed, result);
                    }
                    catch (Exception ex)
                    {
                        LogError("Menu 4K patch", entry.Name, ex, result);
                    }
                }
            });

            return result;
        }

        public static PatchResult ApplyPoiIconsHd()
        {
            PatchResult result = new PatchResult();

            FrostyTaskWindow.Show("Applying NFS Heat POI Icons HD Patch", "", (task) =>
            {
                PatchScreenAsset(
                    "UI/MiniMapScreen",
                    225, 225,
                    450, 450,
                    task,
                    0.0d,
                    result);
            });

            return result;
        }

        public static PatchResult ApplyRadarMapHd()
        {
            PatchResult result = new PatchResult();

            FrostyTaskWindow.Show("Applying NFS Heat Radar/Map HD Patch", "", (task) =>
            {
                PatchScreenAsset(
                    "UI/MiniMapPoiScreen",
                    512, 512,
                    1024, 1024,
                    task,
                    0.0d,
                    result);
            });

            return result;
        }

        public static PatchResult ApplySpeedometerHd()
        {
            PatchResult result = new PatchResult();

            FrostyTaskWindow.Show("Applying NFS Heat Speedometer HD Patch", "", (task) =>
            {
                PatchScreenAsset(
                    "UI/HUD/Instruments/InstrumentsScreen",
                    512, 512,
                    1024, 1024,
                    task,
                    0.0d,
                    result);
            });

            return result;
        }

        public static PatchResult ApplyButtonIconSmoothing()
        {
            PatchResult result = new PatchResult();
            const string assetName = "UI/ButtonPromptIcon";

            FrostyTaskWindow.Show("Applying NFS Heat Button Icon Smoothing", "", (task) =>
            {
                task.Update(assetName, 50.0d);

                try
                {
                    EbxAssetEntry entry = App.AssetManager.GetEbxEntry(assetName);
                    if (entry == null)
                        return;

                    EbxAsset asset = App.AssetManager.GetEbx(entry);
                    if (asset == null || !asset.IsValid)
                        return;

                    bool changed = false;

                    foreach (object obj in asset.Objects)
                    {
                        if (obj == null || obj.GetType().Name != "RimeTextureElementData")
                            continue;

                        if (TrySetBool(obj, "SmoothEdges", false, true))
                        {
                            changed = true;
                            result.ButtonIconElements++;
                        }
                    }

                    CommitIfChanged(entry, asset, changed, result);
                    task.Update(assetName, 100.0d);
                }
                catch (Exception ex)
                {
                    LogError("Button icon smoothing", assetName, ex, result);
                }
            });

            return result;
        }

        public static PatchResult Restore()
        {
            PatchResult result = new PatchResult();

            List<EbxAssetEntry> entries = App.AssetManager.EnumerateEbx()
                .Where(entry => IsUiPath(entry.Name))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FrostyTaskWindow.Show("Restoring NFS Heat UI Patch Values", "", (task) =>
            {
                int total = Math.Max(1, entries.Count);

                for (int index = 0; index < entries.Count; index++)
                {
                    EbxAssetEntry entry = entries[index];
                    task.Update(entry.Name, ((index + 1) / (double)total) * 100.0d);

                    try
                    {
                        EbxAsset asset = App.AssetManager.GetEbx(entry);
                        if (asset == null || !asset.IsValid)
                            continue;

                        bool changed = false;

                        foreach (object obj in asset.Objects)
                        {
                            if (obj == null)
                                continue;

                            string typeName = obj.GetType().Name;

                            if (typeName == "MenuWidgetData" &&
                                TrySetDimensions(obj, 3840, 2160, 1920, 1080))
                            {
                                changed = true;
                                result.MenuWidgets++;
                            }
                            else if (typeName == "RimeWidgetReferenceElementData" &&
                                     TrySetDimensions(obj, 512, 512, 256, 256))
                            {
                                changed = true;
                                result.WidgetReferences++;
                            }
                            else if (typeName == "RimeFontConfiguration" &&
                                     entry.Name.Equals("UI/Fonts/FontConfiguration", StringComparison.OrdinalIgnoreCase))
                            {
                                bool fontChanged = false;
                                fontChanged |= TrySetNumber(obj, "FontDpiScale", 2, 1);
                                fontChanged |= TrySetNumber(obj, "GlyphCacheSize", 2048, 1024);
                                fontChanged |= TrySetNumber(obj, "GlyphCacheSizeLowEnd", 512, 256);

                                if (fontChanged)
                                {
                                    changed = true;
                                    result.FontConfigurations++;
                                }
                            }
                            else if (entry.Name.Equals("UI/ButtonPromptIcon", StringComparison.OrdinalIgnoreCase) &&
                                     typeName == "RimeTextureElementData" &&
                                     TrySetBool(obj, "SmoothEdges", true, false))
                            {
                                changed = true;
                                result.ButtonIconElements++;
                            }
                        }

                        ScreenResolutionRule rule;
                        if (LegacyScreenRules.TryGetValue(entry.Name, out rule))
                        {
                            foreach (object obj in asset.Objects)
                            {
                                if (obj == null || obj.GetType().Name != "RimeScreenData")
                                    continue;

                                if (TrySetScreenLayoutDimensions(
                                    obj,
                                    rule.Width * 2,
                                    rule.Height * 2,
                                    rule.Width,
                                    rule.Height))
                                {
                                    changed = true;
                                    result.RimeScreens++;
                                }
                            }
                        }

                        CommitIfChanged(entry, asset, changed, result);
                    }
                    catch (Exception ex)
                    {
                        LogError("UI restore", entry.Name, ex, result);
                    }
                }
            });

            return result;
        }

        private static void PatchScreenAsset(
            string assetName,
            double expectedWidth,
            double expectedHeight,
            double newWidth,
            double newHeight,
            dynamic task,
            double progress,
            PatchResult result)
        {
            task.Update(assetName, progress);

            try
            {
                EbxAssetEntry entry = App.AssetManager.GetEbxEntry(assetName);
                if (entry == null)
                    return;

                EbxAsset asset = App.AssetManager.GetEbx(entry);
                if (asset == null || !asset.IsValid)
                    return;

                bool changed = false;

                foreach (object obj in asset.Objects)
                {
                    if (obj == null || obj.GetType().Name != "RimeScreenData")
                        continue;

                    if (TrySetScreenLayoutDimensions(
                        obj,
                        expectedWidth,
                        expectedHeight,
                        newWidth,
                        newHeight))
                    {
                        changed = true;
                        result.RimeScreens++;
                    }
                }

                CommitIfChanged(entry, asset, changed, result);
            }
            catch (Exception ex)
            {
                LogError("HD render-target patch", assetName, ex, result);
            }
        }

        private static void CommitIfChanged(
            EbxAssetEntry entry,
            EbxAsset asset,
            bool changed,
            PatchResult result)
        {
            if (!changed)
                return;

            asset.Update();
            App.AssetManager.ModifyEbx(entry.Name, asset);
            result.AssetsModified++;
        }

        private static void LogError(
            string operation,
            string assetName,
            Exception ex,
            PatchResult result)
        {
            result.Errors++;
            App.Logger.Log("{0} failed for {1}: {2}", operation, assetName, ex.Message);
        }

        private static bool IsMenuPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.StartsWith("UI/Menu/", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UI\\Menu\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUiPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.Equals("UI", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UI/", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UI\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySetScreenLayoutDimensions(
            object obj,
            double expectedWidth,
            double expectedHeight,
            double newWidth,
            double newHeight)
        {
            PropertyInfo widthProperty = obj.GetType().GetProperty("ScreenLayoutWidth");
            PropertyInfo heightProperty = obj.GetType().GetProperty("ScreenLayoutHeight");

            if (widthProperty == null || heightProperty == null ||
                !widthProperty.CanRead || !widthProperty.CanWrite ||
                !heightProperty.CanRead || !heightProperty.CanWrite)
                return false;

            double currentWidth;
            double currentHeight;

            if (!TryConvertToDouble(widthProperty.GetValue(obj), out currentWidth) ||
                !TryConvertToDouble(heightProperty.GetValue(obj), out currentHeight))
                return false;

            if (!NearlyEqual(currentWidth, expectedWidth) || !NearlyEqual(currentHeight, expectedHeight))
                return false;

            object convertedWidth;
            object convertedHeight;

            if (!TryConvertNumber(newWidth, widthProperty.PropertyType, out convertedWidth) ||
                !TryConvertNumber(newHeight, heightProperty.PropertyType, out convertedHeight))
                return false;

            widthProperty.SetValue(obj, convertedWidth);
            heightProperty.SetValue(obj, convertedHeight);
            return true;
        }

        private static bool TrySetDimensions(
            object obj,
            double expectedWidth,
            double expectedHeight,
            double newWidth,
            double newHeight)
        {
            PropertyInfo widthProperty = obj.GetType().GetProperty("Width");
            PropertyInfo heightProperty = obj.GetType().GetProperty("Height");

            if (widthProperty == null || heightProperty == null ||
                !widthProperty.CanRead || !widthProperty.CanWrite ||
                !heightProperty.CanRead || !heightProperty.CanWrite)
                return false;

            double currentWidth;
            double currentHeight;

            if (!TryConvertToDouble(widthProperty.GetValue(obj), out currentWidth) ||
                !TryConvertToDouble(heightProperty.GetValue(obj), out currentHeight))
                return false;

            if (!NearlyEqual(currentWidth, expectedWidth) || !NearlyEqual(currentHeight, expectedHeight))
                return false;

            object convertedWidth;
            object convertedHeight;

            if (!TryConvertNumber(newWidth, widthProperty.PropertyType, out convertedWidth) ||
                !TryConvertNumber(newHeight, heightProperty.PropertyType, out convertedHeight))
                return false;

            widthProperty.SetValue(obj, convertedWidth);
            heightProperty.SetValue(obj, convertedHeight);
            return true;
        }

        private static bool TrySetNumber(
            object obj,
            string propertyName,
            double expectedValue,
            double newValue)
        {
            PropertyInfo property = obj.GetType().GetProperty(propertyName);

            if (property == null || !property.CanRead || !property.CanWrite)
                return false;

            double currentValue;
            if (!TryConvertToDouble(property.GetValue(obj), out currentValue))
                return false;

            if (!NearlyEqual(currentValue, expectedValue))
                return false;

            object convertedValue;
            if (!TryConvertNumber(newValue, property.PropertyType, out convertedValue))
                return false;

            property.SetValue(obj, convertedValue);
            return true;
        }

        private static bool TrySetBool(
            object obj,
            string propertyName,
            bool expectedValue,
            bool newValue)
        {
            PropertyInfo property = obj.GetType().GetProperty(propertyName);

            if (property == null || !property.CanRead || !property.CanWrite)
                return false;

            object rawValue = property.GetValue(obj);
            if (!(rawValue is bool) || (bool)rawValue != expectedValue)
                return false;

            property.SetValue(obj, newValue);
            return true;
        }

        private static bool TryGetBool(object obj, string propertyName, out bool value)
        {
            value = false;

            PropertyInfo property = obj.GetType().GetProperty(propertyName);
            if (property == null || !property.CanRead)
                return false;

            object raw = property.GetValue(obj);
            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertToDouble(object value, out double result)
        {
            result = 0;

            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertNumber(
            double value,
            Type propertyType,
            out object convertedValue)
        {
            convertedValue = null;

            try
            {
                Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

                if (targetType.IsEnum)
                    return false;

                convertedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001d;
        }
    }

    internal class PatchResult
    {
        public int AssetsModified;
        public int MenuWidgets;
        public int WidgetReferences;
        public int RimeScreens;
        public int FontConfigurations;
        public int ButtonIconElements;
        public int Errors;
    }

    public class ApplyNfsHeat4KUiPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Apply Menu 4K Patch (Known Pattern)";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.ApplyMenuSafe();

            FrostyMessageBox.Show(
                "Menu 4K patch applied.\n\n" +
                "Assets modified: " + result.AssetsModified + "\n" +
                "1920x1080 menu canvases: " + result.MenuWidgets + "\n" +
                "256x256 widget references: " + result.WidgetReferences + "\n" +
                "Errors: " + result.Errors,
                "NFS Heat UI Tools");
        });
    }

    public class ApplyNfsHeatPoiIconsHdPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Apply POI Icons HD Test";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.ApplyPoiIconsHd();

            FrostyMessageBox.Show(
                "POI Icons HD test applied.\n\n" +
                "Screen layouts modified: " + result.RimeScreens + "\n" +
                "Errors: " + result.Errors + "\n\n" +
                "UI/MiniMapScreen: 225x225 -> 450x450",
                "NFS Heat UI Tools");
        });
    }

    public class ApplyNfsHeatRadarMapHdPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Apply Radar/Map HD Test";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.ApplyRadarMapHd();

            FrostyMessageBox.Show(
                "Radar/Map HD test applied.\n\n" +
                "Screen layouts modified: " + result.RimeScreens + "\n" +
                "Errors: " + result.Errors + "\n\n" +
                "UI/MiniMapPoiScreen: 512x512 -> 1024x1024",
                "NFS Heat UI Tools");
        });
    }

    public class ApplyNfsHeatSpeedometerHdPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Apply Speedometer HD Test";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.ApplySpeedometerHd();

            FrostyMessageBox.Show(
                "Speedometer HD test applied.\n\n" +
                "Screen layouts modified: " + result.RimeScreens + "\n" +
                "Errors: " + result.Errors + "\n\n" +
                "This only touches UI/HUD/Instruments/InstrumentsScreen.",
                "NFS Heat UI Tools");
        });
    }

    public class RestoreNfsHeat4KUiPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Restore All NFS Heat UI Patch Values";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.Restore();

            FrostyMessageBox.Show(
                "NFS Heat UI patch values restored.\n\n" +
                "Assets modified: " + result.AssetsModified + "\n" +
                "Menu canvases: " + result.MenuWidgets + "\n" +
                "Widget references: " + result.WidgetReferences + "\n" +
                "Render screens: " + result.RimeScreens + "\n" +
                "Font configuration: " + result.FontConfigurations + "\n" +
                "Button icon elements: " + result.ButtonIconElements + "\n" +
                "Errors: " + result.Errors,
                "NFS Heat UI Tools");
        });
    }
}
