using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
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
        private static readonly Dictionary<string, ScreenResolutionRule> ScreenRules =
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

        public static PatchResult Apply()
        {
            return Patch(false);
        }

        public static PatchResult Restore()
        {
            return Patch(true);
        }

        private static PatchResult Patch(bool restore)
        {
            PatchResult result = new PatchResult();

            List<EbxAssetEntry> entries = App.AssetManager.EnumerateEbx()
                .Where(entry => IsUiPath(entry.Name))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FrostyTaskWindow.Show(
                restore ? "Restoring NFS Heat 4K UI Patch" : "Applying NFS Heat 4K UI Patch",
                "",
                (task) =>
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

                                if (typeName == "MenuWidgetData")
                                {
                                    if (restore)
                                    {
                                        if (TrySetDimensions(obj, 3840, 2160, 1920, 1080))
                                        {
                                            changed = true;
                                            result.MenuWidgets++;
                                        }
                                    }
                                    else
                                    {
                                        if (TrySetDimensions(obj, 1920, 1080, 3840, 2160))
                                        {
                                            changed = true;
                                            result.MenuWidgets++;
                                        }
                                    }
                                }
                                else if (typeName == "RimeWidgetReferenceElementData")
                                {
                                    bool useWidgetWidth;
                                    bool useWidgetHeight;

                                    if (TryGetBool(obj, "UseWidgetWidth", out useWidgetWidth) &&
                                        TryGetBool(obj, "UseWidgetHeight", out useWidgetHeight) &&
                                        useWidgetWidth && useWidgetHeight)
                                    {
                                        if (restore)
                                        {
                                            if (TrySetDimensions(obj, 512, 512, 256, 256))
                                            {
                                                changed = true;
                                                result.WidgetReferences++;
                                            }
                                        }
                                        else
                                        {
                                            if (TrySetDimensions(obj, 256, 256, 512, 512))
                                            {
                                                changed = true;
                                                result.WidgetReferences++;
                                            }
                                        }
                                    }
                                }
                                else if (typeName == "RimeFontConfiguration" &&
                                         entry.Name.Equals("UI/Fonts/FontConfiguration", StringComparison.OrdinalIgnoreCase))
                                {
                                    bool fontChanged = false;

                                    if (restore)
                                    {
                                        fontChanged |= TrySetNumber(obj, "FontDpiScale", 2, 1);
                                        fontChanged |= TrySetNumber(obj, "GlyphCacheSize", 2048, 1024);
                                        fontChanged |= TrySetNumber(obj, "GlyphCacheSizeLowEnd", 512, 256);
                                    }
                                    else
                                    {
                                        fontChanged |= TrySetNumber(obj, "FontDpiScale", 1, 2);
                                        fontChanged |= TrySetNumber(obj, "GlyphCacheSize", 1024, 2048);
                                        fontChanged |= TrySetNumber(obj, "GlyphCacheSizeLowEnd", 256, 512);
                                    }

                                    if (fontChanged)
                                    {
                                        changed = true;
                                        result.FontConfigurations++;
                                    }
                                }
                            }

                            ScreenResolutionRule screenRule;
                            if (ScreenRules.TryGetValue(entry.Name, out screenRule))
                            {
                                foreach (object obj in asset.Objects)
                                {
                                    if (obj == null || obj.GetType().Name != "RimeScreenData")
                                        continue;

                                    bool screenChanged;

                                    if (restore)
                                    {
                                        screenChanged = TrySetDimensions(
                                            obj,
                                            screenRule.Width * 2,
                                            screenRule.Height * 2,
                                            screenRule.Width,
                                            screenRule.Height);
                                    }
                                    else
                                    {
                                        screenChanged = TrySetDimensions(
                                            obj,
                                            screenRule.Width,
                                            screenRule.Height,
                                            screenRule.Width * 2,
                                            screenRule.Height * 2);
                                    }

                                    if (screenChanged)
                                    {
                                        changed = true;
                                        result.RimeScreens++;
                                    }
                                }
                            }

                            if (changed)
                            {
                                asset.Update();
                                App.AssetManager.ModifyEbx(entry.Name, asset);
                                result.AssetsModified++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors++;
                            App.Logger.Log(
                                "{0} failed for {1}: {2}",
                                restore ? "4K UI restore" : "4K UI patch",
                                entry.Name,
                                ex.Message);
                        }
                    }
                });

            return result;
        }

        private static bool IsUiPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.Equals("UI", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UI/", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UI\\", StringComparison.OrdinalIgnoreCase);
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

        private static bool TrySetNumber(object obj, string propertyName, double expectedValue, double newValue)
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

        private static bool TryConvertNumber(double value, Type propertyType, out object convertedValue)
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
        public int Errors;
    }

    public class ApplyNfsHeat4KUiPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Apply NFS Heat 4K UI Patch";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.Apply();

            FrostyMessageBox.Show(
                "NFS Heat 4K UI patch applied.\n\n" +
                "Assets modified: " + result.AssetsModified + "\n" +
                "1920x1080 menu canvases: " + result.MenuWidgets + "\n" +
                "256x256 widget references: " + result.WidgetReferences + "\n" +
                "Rime screens/render targets: " + result.RimeScreens + "\n" +
                "Font configuration: " + result.FontConfigurations + "\n" +
                "Errors: " + result.Errors + "\n\n" +
                "Save/export the Frosty project as a mod, then test in-game.",
                "NFS Heat 4K UI Patch");
        });
    }

    public class RestoreNfsHeat4KUiPatchMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Restore NFS Heat UI Patch Values";
        public override ImageSource Icon => EbxToXmlMenuExtension.imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            PatchResult result = NfsHeat4KUiPatcher.Restore();

            FrostyMessageBox.Show(
                "NFS Heat UI patch values restored.\n\n" +
                "Assets modified: " + result.AssetsModified + "\n" +
                "Menu canvases restored: " + result.MenuWidgets + "\n" +
                "Widget references restored: " + result.WidgetReferences + "\n" +
                "Rime screens/render targets restored: " + result.RimeScreens + "\n" +
                "Font configuration restored: " + result.FontConfigurations + "\n" +
                "Errors: " + result.Errors,
                "NFS Heat 4K UI Patch");
        });
    }
}
