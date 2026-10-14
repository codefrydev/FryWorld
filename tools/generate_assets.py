#!/usr/bin/env python3
"""Master asset generator for FryWorld.

Renders the master SVG (src/FryWorld/Assets/app-logo.svg) and generates
all application icons, platform package branding, and media:
1. Master 1024x1024 PNG logo: src/FryWorld/Assets/app-logo.png
2. Windows multi-res ICO:      src/FryWorld/Assets/app-logo.ico
3. macOS ICNS & Iconset:       packaging/macos/AppIcon.icns
4. Windows MSIX assets:        packaging/windows/msix/Assets/*.png
5. Windows Inno branding:      packaging/windows/branding/*.png
6. macOS DMG background:       packaging/macos/dmg/background*.png
7. Linux desktop & icon:       packaging/linux/fryworld*.png
"""

from __future__ import annotations

import os
import pathlib
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

REPO_ROOT = pathlib.Path(__file__).resolve().parents[1]
ASSETS_DIR = REPO_ROOT / "src" / "FryWorld" / "Assets"
MACOS_DIR = REPO_ROOT / "packaging" / "macos"
WINDOWS_DIR = REPO_ROOT / "packaging" / "windows"
LINUX_DIR = REPO_ROOT / "packaging" / "linux"

# Brand Palette (matching the FryWorld dragon logo & theme)
BRAND_NAVY = (13, 43, 69)
BRAND_NAVY_DEEP = (8, 28, 46)
BRAND_CYAN = (124, 162, 194)
BRAND_MUTED = (159, 179, 200)
BRAND_WHITE = (255, 255, 255)
BG_SURFACE_TOP = (238, 242, 247)
BG_SURFACE_BOTTOM = (220, 228, 238)


def get_font(size: int, bold: bool = True) -> ImageFont.FreeTypeFont:
    for font_path in [
        "/System/Library/Fonts/SFNS.ttf",
        "/System/Library/Fonts/HelveticaNeue.ttc",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "C:\\Windows\\Fonts\\segoeui.ttf",
    ]:
        if os.path.exists(font_path):
            try:
                return ImageFont.truetype(font_path, size)
            except Exception:
                continue
    return ImageFont.load_default(size)


def render_svg_to_png(svg_path: pathlib.Path, png_path: pathlib.Path, size: int = 1024) -> Image.Image:
    """Renders the master SVG to high-resolution PNG using native macOS renderer."""
    if sys.platform == "darwin":
        swift_code = f"""
import AppKit

let svgUrl = URL(fileURLWithPath: "{svg_path}")
guard let img = NSImage(contentsOf: svgUrl) else {{
    exit(1)
}}

let targetSize = NSSize(width: {size}, height: {size})
let rep = NSBitmapImageRep(
    bitmapDataPlanes: nil, pixelsWide: {size}, pixelsHigh: {size},
    bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true,
    isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0
)!
rep.size = targetSize

NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
img.draw(in: NSRect(origin: .zero, size: targetSize), from: NSRect(origin: .zero, size: img.size), operation: .copy, fraction: 1.0)
NSGraphicsContext.restoreGraphicsState()

let data = rep.representation(using: .png, properties: [:])!
try data.write(to: URL(fileURLWithPath: "{png_path}"))
"""
        cmd = ["swift", "-e", swift_code]
        res = subprocess.run(cmd, capture_output=True, text=True)
        if res.returncode == 0 and png_path.exists():
            return Image.open(png_path).convert("RGBA")
        print(f"Warning: swift rendering failed: {res.stderr}")

    # Fallback if swift failed or not on macOS
    if png_path.exists():
        return Image.open(png_path).convert("RGBA")
    raise RuntimeError(f"Could not render SVG {svg_path} to PNG")


def generate_ico(master: Image.Image, output_path: pathlib.Path):
    """Generates a multi-resolution Windows .ico file."""
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master.save(output_path, format="ICO", sizes=sizes)
    print(f"  ✓ Windows ICO: {output_path}")


def generate_icns(master: Image.Image, iconset_dir: pathlib.Path, icns_path: pathlib.Path):
    """Generates macOS AppIcon.iconset and compiles AppIcon.icns."""
    iconset_dir.mkdir(parents=True, exist_ok=True)
    specs = [
        (16, "icon_16x16.png"),
        (32, "icon_16x16@2x.png"),
        (32, "icon_32x32.png"),
        (64, "icon_32x32@2x.png"),
        (128, "icon_128x128.png"),
        (256, "icon_128x128@2x.png"),
        (256, "icon_256x256.png"),
        (512, "icon_256x256@2x.png"),
        (512, "icon_512x512.png"),
        (1024, "icon_512x512@2x.png"),
    ]

    for px, filename in specs:
        resized = master.resize((px, px), Image.LANCZOS)
        resized.save(iconset_dir / filename, "PNG")

    if sys.platform == "darwin":
        try:
            subprocess.run(["iconutil", "-c", "icns", str(iconset_dir), "-o", str(icns_path)], check=True)
            print(f"  ✓ macOS ICNS: {icns_path}")
            return
        except Exception as ex:
            print(f"  ! iconutil failed: {ex}")

    master.resize((512, 512), Image.LANCZOS).save(icns_path, "PNG")
    print(f"  ✓ macOS fallback icon: {icns_path}")


def generate_msix_assets(master: Image.Image, out_dir: pathlib.Path):
    """Generates all required assets for Windows MSIX / AppxManifest."""
    out_dir.mkdir(parents=True, exist_ok=True)
    specs = {
        "Square44x44Logo.png": (44, 44),
        "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24),
        "Square150x150Logo.png": (150, 150),
        "Wide310x150Logo.png": (310, 150),
        "StoreLogo.png": (50, 50),
        "SplashScreen.png": (620, 300),
    }

    for name, (w, h) in specs.items():
        if w == h:
            img = master.resize((w, h), Image.LANCZOS)
        else:
            # Letterbox inside dark navy splash
            img = Image.new("RGBA", (w, h), (10, 12, 16, 255))
            icon_size = int(min(w, h) * 0.75)
            icon_resized = master.resize((icon_size, icon_size), Image.LANCZOS)
            img.alpha_composite(icon_resized, ((w - icon_size) // 2, (h - icon_size) // 2))
        img.save(out_dir / name, "PNG")

    print(f"  ✓ Windows MSIX assets: {out_dir}")


def generate_inno_branding(master: Image.Image, out_dir: pathlib.Path):
    """Generates Inno Setup wizard artwork."""
    out_dir.mkdir(parents=True, exist_ok=True)

    # Large wizard banner (164x314)
    w, h = 164, 314
    large = Image.new("RGB", (w, h), BRAND_NAVY_DEEP)
    draw = ImageDraw.Draw(large)

    logo_size = 90
    logo = master.resize((logo_size, logo_size), Image.LANCZOS)
    large.paste(logo, ((w - logo_size) // 2, 35), logo)

    f_title = get_font(18)
    draw.text((w // 2, 142), "FryWorld", font=f_title, fill=BRAND_WHITE, anchor="mm")

    draw.rectangle([(w - 30) // 2, 160, (w + 30) // 2, 162], fill=BRAND_CYAN)

    f_sub = get_font(9)
    draw.text((w // 2, 178), "Creative & Developer Suite", font=f_sub, fill=BRAND_MUTED, anchor="mm")
    draw.text((w // 2, h - 20), "codefrydev.in", font=f_sub, fill=BRAND_MUTED, anchor="mm")
    large.save(out_dir / "wizard-large.png", "PNG")

    # Small header logo (55x55)
    small = master.resize((55, 55), Image.LANCZOS)
    small.save(out_dir / "wizard-small.png", "PNG")
    print(f"  ✓ Inno Setup branding: {out_dir}")


def generate_dmg_background(master: Image.Image, out_dir: pathlib.Path):
    """Generates macOS DMG Finder background with drag-to-Applications arrow."""
    out_dir.mkdir(parents=True, exist_ok=True)
    w, h = 660, 428
    header_h = 84

    def draw_bg(scale: int = 1) -> Image.Image:
        bw, bh = w * scale, h * scale
        hs = header_h * scale
        img = Image.new("RGBA", (bw, bh), BG_SURFACE_TOP)
        draw = ImageDraw.Draw(img)

        # Header background
        draw.rectangle((0, 0, bw, hs), fill=BRAND_NAVY)
        # Header accent line
        draw.rectangle((0, hs - 3 * scale, bw, hs), fill=BRAND_CYAN)

        # Header logo badge (56x56 scaled)
        badge_size = 56 * scale
        badge = master.resize((badge_size, badge_size), Image.LANCZOS)
        img.alpha_composite(badge, (28 * scale, 14 * scale))

        # Header Text
        f_title = get_font(24 * scale)
        draw.text((100 * scale, 18 * scale), "FryWorld", font=f_title, fill=BRAND_WHITE)

        f_sub = get_font(12 * scale)
        draw.text((101 * scale, 52 * scale), "Creative & Developer Suite", font=f_sub, fill=BRAND_MUTED)

        # Middle Drag Arrow between app (x=170) and Applications (x=490)
        cy = 196 * scale
        ax1, ax2 = 290 * scale, 370 * scale
        shaft_h = 10 * scale
        head_w, head_h = 24 * scale, 28 * scale
        draw.rounded_rectangle(
            [ax1, cy - shaft_h // 2, ax2 - head_w, cy + shaft_h // 2],
            radius=shaft_h // 2,
            fill=BRAND_NAVY
        )
        draw.polygon(
            [(ax2, cy), (ax2 - head_w, cy - head_h // 2), (ax2 - head_w, cy + head_h // 2)],
            fill=BRAND_NAVY
        )

        # Caption text
        f_caption = get_font(14 * scale)
        text = "Drag FryWorld to Applications"
        draw.text((bw // 2, 316 * scale), text, font=f_caption, fill=BRAND_NAVY, anchor="mm")

        return img.convert("RGB")

    draw_bg(1).save(out_dir / "background.png", "PNG", optimize=True)
    draw_bg(2).save(out_dir / "background@2x.png", "PNG", optimize=True)
    print(f"  ✓ macOS DMG background: {out_dir}")


def generate_linux_assets(master: Image.Image, out_dir: pathlib.Path):
    """Generates Linux desktop entry and icon."""
    out_dir.mkdir(parents=True, exist_ok=True)
    master.resize((256, 256), Image.LANCZOS).save(out_dir / "fryworld-256x256.png", "PNG")

    desktop_content = """[Desktop Entry]
Name=FryWorld
Comment=Creative & Developer Suite Launcher
Exec=FryWorld %u
Icon=fryworld
Terminal=false
Type=Application
Categories=Development;Graphics;AudioVideo;Utility;
Keywords=fryworld;launcher;sdk;csharp;code;studio;
"""
    (out_dir / "fryworld.desktop").write_text(desktop_content)
    print(f"  ✓ Linux assets: {out_dir}")


def main():
    print("==================================================")
    print(" Generating FryWorld Cross-Platform Assets")
    print("==================================================")

    ASSETS_DIR.mkdir(parents=True, exist_ok=True)
    master_svg = ASSETS_DIR / "app-logo.svg"
    master_png = ASSETS_DIR / "app-logo.png"

    print(f"  Rendering {master_svg} -> {master_png}...")
    master = render_svg_to_png(master_svg, master_png, 1024)
    print(f"  ✓ Master 1024x1024 PNG: {master_png}")

    generate_ico(master, ASSETS_DIR / "app-logo.ico")
    generate_icns(master, MACOS_DIR / "AppIcon.iconset", MACOS_DIR / "AppIcon.icns")
    generate_msix_assets(master, WINDOWS_DIR / "msix" / "Assets")
    generate_inno_branding(master, WINDOWS_DIR / "branding")
    generate_dmg_background(master, MACOS_DIR / "dmg")
    generate_linux_assets(master, LINUX_DIR)

    print("==================================================")
    print(" All assets successfully generated!")
    print("==================================================")


if __name__ == "__main__":
    main()
