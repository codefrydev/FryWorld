using System;
using System.IO;
using Material.Icons;
using SkiaSharp;

namespace FryWorld.Tools.UiSnapshots;

public static class IconGenerator
{
    private const int CanvasSize = 5064;
    private const int OutputSize = 512;

    // Official Fry Brand Paths from app-logo.svg
    private const string DragonBody = "M2474 751C2532.5 786.5 2609.5 835.5 2682.5 859.5C2768.74 897.828 2854.61 905.811 2884 925.5C2935.5 960 2964.5 1038.74 3038 1085C3111.5 1131.26 3226 1156 3267 1156.5C3308 1157 3322.5 1124 3356 1128C3389.5 1132 3422.5 1199.5 3431 1251C3439.5 1302.5 3449.5 1442 3417.5 1450.5C3385.5 1459 3252 1403 3164 1391.5C3076 1380 2823 1391.5 2823 1391.5C2823 1391.5 2973 1587.5 3068.5 1626C3164 1664.5 3244.5 1651.5 3257.5 1664.5C3270.5 1677.5 3226.5 1772 3164 1813.5C3101.5 1855 3011 1874.5 2995.5 1864.5C2980 1854.5 2903 1640.5 2742 1630.5C2581 1620.5 2531 1626 2481.5 1607C2432 1588 2370.5 1526 2345.5 1479.5C2320.5 1433 2325 1310 2287.5 1311C2250 1312 1948 1522.5 1927.5 1743C1907 1963.5 1943 2117.5 1968 2220.5C1993 2323.5 2133 2584 2133 2584C2133 2584 1945.5 2439.5 1887 2353C1828.5 2266.5 1747.5 2037 1747.5 2037L1645 2220.5C1645 2220.5 1619 2037 1619 1916C1619 1795 1667 1626 1667 1626C1667 1626 1633 1639 1589.5 1662C1546 1685 1493 1736.5 1493 1736.5C1493 1736.5 1547.5 1549 1615 1457C1682.5 1365 1855.5 1222.5 1855.5 1222.5C1855.5 1222.5 1751.5 1217 1717.5 1215C1683.5 1213 1575 1222.5 1575 1222.5C1575 1222.5 1727.5 1086 1832.5 1045C1937.5 1004 2133 973 2133 973C2133 973 1961 878 1832.5 753C1704 628 1667 353 1667 353C1667 353 1824.5 563.337 1958.5 664C2092.5 764.663 2290.5 822.5 2290.5 822.5C2290.5 822.5 2245.5 757 2203 650C2160.5 543 2158.5 411.5 2158.5 411.5C2158.5 411.5 2415.5 715.5 2474 751Z";
    private const string DragonShadow = "M2291.5 1633C2291.5 1633 2268.5 1913.5 2645.5 2143C3022.5 2372.5 3176.5 2469.5 3337 2716C3497.5 2962.5 3276.5 3592.5 3276.5 3592.5C3276.5 3592.5 3305 3376 3276.5 3111C3248 2846 3028 2641.5 2916 2545C2804 2448.5 2527 2287.5 2437.5 2205.5C2348 2123.5 2273.5 2027.5 2263.5 1917.5C2253.5 1807.5 2291.5 1633 2291.5 1633Z";
    private const string BowlPath = "M4213 2989C4213 3215.14 4168.46 3439.06 4081.92 3647.98C3995.38 3856.9 3868.54 4046.74 3708.64 4206.64C3548.74 4366.54 3358.9 4493.38 3149.98 4579.92C2941.06 4666.46 2717.14 4711 2491 4711C2264.86 4711 2040.94 4666.46 1832.02 4579.92C1623.1 4493.38 1433.26 4366.54 1273.36 4206.64C1113.46 4046.74 986.618 3856.9 900.079 3647.98C813.541 3439.06 769 3215.14 769 2989H1299C1299 3142.65 1329.25 3294.79 1388.02 3436.74C1446.8 3578.69 1532.94 3707.67 1641.54 3816.31C1750.14 3924.96 1879.06 4011.14 2020.95 4069.94C2162.84 4128.74 2314.92 4159 2468.5 4159C2622.08 4159 2774.16 4128.74 2916.05 4069.94C3057.94 4011.14 3186.86 3924.96 3295.46 3816.32C3404.06 3707.67 3490.2 3578.69 3548.98 3436.74C3607.75 3294.79 3638 3142.65 3638 2989H4213Z";
    private const string DragonEye = "M2654.5 1023.5C2654.5 1023.5 2753.5 1021 2810 1033C2866.5 1045 2920 1126 2920 1126C2920 1126 2872 1139 2847 1140C2822 1141 2757.5 1132.5 2718.5 1111.5C2679.5 1090.5 2654.5 1023.5 2654.5 1023.5Z";

    private static readonly SKColor NavyColor = new(0x0D, 0x2B, 0x45);
    private static readonly SKColor ShadowColor = new(0x1A, 0x3D, 0x5C);
    private static readonly SKColor EyeColor = new(0x7C, 0xA2, 0xC2);

    public static void GenerateAllBrandIcons(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        var apps = new[]
        {
            ("com.fryworld.csharpstudio", "#", "#2563EB", (string?)null),
            ("com.fryworld.chess", (string?)null, "#059669", MaterialIconDataProvider.GetData(MaterialIconKind.ChessKnight)),
            ("com.fryworld.imageeditor", (string?)null, "#DC2626", MaterialIconDataProvider.GetData(MaterialIconKind.Palette)),
            ("com.fryworld.snake", (string?)null, "#D97706", MaterialIconDataProvider.GetData(MaterialIconKind.GamepadVariant)),
            ("com.fryworld.tictactoe", (string?)null, "#DB2777", MaterialIconDataProvider.GetData(MaterialIconKind.Grid)),
            ("com.fryworld.telemetry", (string?)null, "#0891B2", MaterialIconDataProvider.GetData(MaterialIconKind.Speedometer)),
            ("com.fryworld.scratchpad", (string?)null, "#EA580C", MaterialIconDataProvider.GetData(MaterialIconKind.NotebookEdit))
        };

        foreach (var (id, textSymbol, accentHex, svgPath) in apps)
        {
            var outFile = Path.Combine(targetDirectory, $"{id}.png");
            RenderAppLogo(outFile, textSymbol, accentHex, svgPath);
            Console.WriteLine($"[BrandIcon] Generated official Fry brand icon -> {outFile}");
        }
    }

    private static void RenderAppLogo(string outputPath, string? textSymbol, string accentHex, string? svgPathData)
    {
        using var bitmap = new SKBitmap(OutputSize, OutputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Scale from master 5064x5064 to OutputSize (512x512)
        float scale = (float)OutputSize / CanvasSize;
        canvas.Scale(scale);

        // 1. Crisp Circular White Card Background
        using (var bgPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        {
            canvas.DrawCircle(CanvasSize / 2f, CanvasSize / 2f, CanvasSize / 2f, bgPaint);
        }

        // 2. Dragon Body
        using (var bodyPaint = new SKPaint
        {
            Color = NavyColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        using (var path = SKPath.ParseSvgPathData(DragonBody))
        {
            canvas.DrawPath(path, bodyPaint);
        }

        // 3. Dragon Neck Shadow
        using (var shadowPaint = new SKPaint
        {
            Color = ShadowColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        using (var path = SKPath.ParseSvgPathData(DragonShadow))
        {
            canvas.DrawPath(path, shadowPaint);
        }

        // 4. U-Bowl Crescent
        using (var bowlPaint = new SKPaint
        {
            Color = NavyColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        using (var path = SKPath.ParseSvgPathData(BowlPath))
        {
            canvas.DrawPath(path, bowlPaint);
        }

        // 5. Dragon Eye
        using (var eyePaint = new SKPaint
        {
            Color = EyeColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        using (var path = SKPath.ParseSvgPathData(DragonEye))
        {
            canvas.DrawPath(path, eyePaint);
        }

        // 6. Brand Symbol in Front of Dragon Mouth (at approx x = 3850, y = 1450)
        // Parsed accent color or official navy
        var symbolColor = SKColor.TryParse(accentHex, out var col) ? col : NavyColor;

        if (!string.IsNullOrWhiteSpace(textSymbol))
        {
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 1050);
            using var textPaint = new SKPaint
            {
                Color = NavyColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawText(textSymbol, 3600, 1550, font, textPaint);
        }
        else if (!string.IsNullOrWhiteSpace(svgPathData))
        {
            using var iconPath = SKPath.ParseSvgPathData(svgPathData);
            if (iconPath != null)
            {
                var bounds = iconPath.Bounds;
                // Target bounding box at mouth: x: 3450..4450, y: 950..1950 (target width/height ~1000px)
                float targetDim = 1050f;
                float pathScale = targetDim / Math.Max(bounds.Width, bounds.Height);

                var matrix = SKMatrix.CreateIdentity();
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top));
                matrix = matrix.PostConcat(SKMatrix.CreateScale(pathScale, pathScale));
                // Center in target box (3400 + 525, 950 + 525)
                float targetCenterX = 3925f;
                float targetCenterY = 1475f;
                float finalX = targetCenterX - (bounds.Width * pathScale) / 2f;
                float finalY = targetCenterY - (bounds.Height * pathScale) / 2f;
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(finalX, finalY));

                iconPath.Transform(matrix);

                using var iconPaint = new SKPaint
                {
                    Color = NavyColor, // Using canonical Fry dark navy #0D2B45
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawPath(iconPath, iconPaint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }
}
