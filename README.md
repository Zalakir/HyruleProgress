# Hyrule Progress - Zelda 1 Randomizer Tracker

A lightweight Windows desktop tracker prototype for Zelda 1 Randomizer.

## Features
- Zelda-style inventory with a black background, green/gold frames, and pixel-art sprites from `Resources/ItemImage`
- 5-by-3 item grid, separate hearts/bombs strip, nine-column dungeon panel, and equipment panel
- Clickable item/equipment icons and independent dungeon maps, compasses, and completion indicators
- Run stopwatch in the header with start/stop controls; elapsed time is saved across app restarts and resets with a new run
- Automatic tracking from the Mesen 2 emulator through `Integration.lua`
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

## Automatic tracking with Mesen 2
Hyrule Progress can update itself while you play by using `Integration.lua`, a read-only Lua script that runs inside the Mesen 2 emulator. Tested with Mesen 2.1.1 and Zelda Randomizer 3.6.6.

### Set up the Lua script in Mesen
1. **Start Hyrule Progress** at least once so the `%LOCALAPPDATA%\HyruleProgress` folder exists. Leave the app open while you play.
2. **Load your ROM** in Mesen 2 and start the game.
3. **Open the script window:** in Mesen's main menu, choose **Debug > Script Window**.
4. **Open the script:** in the script window, choose **File > Open** and select `Integration.lua` from the Hyrule Progress source folder (copy it anywhere convenient if you use a published build).
5. **Allow file access** (first time only): the script must write a file to disk. If the log at the bottom of the script window reports an error about `io` or about writing the JSON file, open Mesen's script settings and enable **Allow access to I/O and OS functions**, then run the script again.
6. **Run the script:** press **F5** (or click the **Run** button). Within a second, the Hyrule Progress header changes from gray **○ AUTO** to green **● AUTO**.
7. **Keep the script window open** while you play. Closing it or pressing **Escape** stops the script, and the indicator returns to gray after about 3 seconds.

Repeat steps 3-6 each time you restart Mesen. Your tracker progress is saved, so restarting the script resumes where you left off.

### Troubleshooting
- **Indicator stays gray:** make sure the script is running (no error in the script window log) and that `%LOCALAPPDATA%\HyruleProgress\bridge.json` is being updated. Press **Win + R** and enter `%LOCALAPPDATA%\HyruleProgress` to open the folder.
- **Indicator is green but nothing updates:** the tracker ignores the title and file-select screens; progress appears once you are playing a save file.
- **Starting a new seed:** click **New run / reset** before playing, because auto-tracking only adds progress and never clears it.

### What is tracked
The script writes a dump of the NES RAM to `%LOCALAPPDATA%\HyruleProgress\bridge.json` about twice per second. The tracker reads it and automatically marks:
- Every inventory and equipment item, including swords, candles, rings, arrows, boomerangs, and Magical Shield
- Heart containers gained since the 3 starting hearts (up to 5 slots), Bombs, and bomb capacity upgrades
- Maps and compasses for Levels 1-9, plus the Triforce for Levels 1-8

Auto-tracking only adds progress and never clears it, so manual clicks still work and consumed items such as Food or Letter remain marked. RAM is ignored on the title and file-select screens. Level 9 completion and overworld location checks remain manual. Because the game keeps only the best sword, candle, ring, and arrows, picking up a lower tier after a higher one cannot be detected.

## Save location
`%LOCALAPPDATA%\HyruleProgress\save.json`

## Important scope note
This is a first functional prototype, not a complete or seed-aware Zelda 1 Randomizer logic database. It does not read the randomizer seed. Inventory and dungeon progress can be tracked automatically from Mesen 2 (verified with Zelda Randomizer 3.6.6) or manually.
