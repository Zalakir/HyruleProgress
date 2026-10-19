# Hyrule Progress - Zelda 1 Randomizer Tracker

A lightweight Windows desktop tracker prototype for Zelda 1 Randomizer.

## Features
- Zelda-style inventory with a black background, green/gold frames, and pixel-art sprites from `Resources/ItemImage`
- 5-by-3 item grid, separate hearts/bombs strip, nine-column dungeon panel, and equipment panel
- Clickable item/equipment icons and independent dungeon maps, compasses, and completion indicators
- Run stopwatch in the header with start/stop controls; elapsed time is saved across app restarts and resets with a new run
- Inventory-only interface with the title/header and new-run reset button
- Automatic local JSON save

## Inventory controls
- Click an icon (or focus it with Tab and press Space/Enter) to toggle an item. Dim icons are missing; bright icons with a green corner marker are obtained.
- Hover over an icon for its name, status, and item notes.
- Each dungeon column has its number, map, compass, and Triforce. Maps and compasses are saved per dungeon; Triforces toggle the existing Level 1-9 completed checks, not separate collectible pieces.
- The bottom-left strip contains five independent heart slots and two bomb slots. The first heart and first bomb match their checklist entries; the remaining hearts and bomb capacity upgrade are saved separately.
- Tabs, checklist/location editors, counters, and status text have been removed. Existing notes, locations, and items not displayed in the inventory remain preserved in saved data.
- Use **New run / reset** in the header to clear progress after confirmation. Save errors and version-file warnings appear in dialogs.
- Item images are embedded in the app, including single-file published builds. Existing saves and reset behavior are preserved.
- Older saves gain an uncollected Arrows entry; new upgrade and per-dungeon map/compass fields start uncollected.

## Requirements
- Windows 10/11
- .NET 8 SDK to build from source

## Run from source
1. Install the .NET 8 SDK from Microsoft.
2. Open a terminal in this folder.
3. Run: `dotnet run`

To publish a standalone Windows build:
`dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`

The published executable will be under `bin/Release/net8.0-windows/win-x64/publish/`.

## App version
The app version is stored in `version.txt` and displayed in the window title. Update the file's version text to change the displayed version; the file is copied alongside the app during build and publish, so it can also be updated without rebuilding.

## Save location
`%LOCALAPPDATA%\HyruleProgress\save.json`

## Important scope note
This is a first functional prototype, not a complete or seed-aware Zelda 1 Randomizer logic database. It does not read the randomizer seed or connect to a Lua script yet. Inventory and dungeon progress are tracked manually.
