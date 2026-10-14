"""dmgbuild settings for the FryWorld disk image.

Invoked by packaging/macos/make-dmg.sh:

    dmgbuild -s packaging/macos/dmg/settings.py \
             -D app=/path/to/FryWorld.app -D assets=packaging/macos/dmg \
             "FryWorld" FryWorld-1.0.0-arm64.dmg

dmgbuild writes the Finder layout straight into the .DS_Store via ds_store /
mac_alias. It never drives Finder over AppleScript, which is what makes this
deterministic on a headless GitHub runner.
"""

import os.path

# dmgbuild exec()s this file, so there is no __file__ here - make-dmg.sh passes
# every path in with -D instead.
application = defines["app"]  # noqa: F821 - injected by dmgbuild
here = defines["assets"]  # noqa: F821
macos_dir = os.path.dirname(os.path.abspath(here))
app_name = os.path.basename(application)

format = "UDZO"
compression_level = 9

# HFS+ rather than APFS for maximum compatibility
filesystem = "HFS+"

files = [application]
symlinks = {"Applications": "/Applications"}

# Becomes the mounted volume's icon, so the disk shows the FryWorld logo on the
# Desktop and in the Finder sidebar instead of a generic drive.
icon = os.path.join(macos_dir, "AppIcon.icns")

background = os.path.join(here, "background.png")  # @2x sibling is picked up automatically

window_rect = ((200, 120), (660, 428))
default_view = "icon-view"
show_icon_preview = False
include_icon_view_settings = True
include_list_view_settings = False

arrange_by = None
grid_offset = (0, 0)
label_pos = "bottom"
text_size = 13
icon_size = 128
icon_locations = {
    app_name: (170, 196),
    "Applications": (490, 196),
}

show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = False
show_sidebar = False
