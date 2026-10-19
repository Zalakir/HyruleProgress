# Hyrule Progress - Zelda 1 Randomizer Tracker

A lightweight Windows desktop tracker prototype for Zelda 1 Randomizer.

## Features
- Zelda-style inventory with a black background, green/gold frames, and pixel-art sprites from `Resources/ItemImage`
- 5-by-3 item grid, separate hearts/Bomb Bags strip, nine-column dungeon panel, and equipment panel
- Clickable item/equipment icons and independent dungeon maps, compasses, and completion indicators
- Run stopwatch in the header with automatic Mesen start/finish detection and manual start/stop controls; elapsed time is saved across app restarts and resets with a new run
- Automatic tracking from the Mesen 2 emulator through `Integration.lua`
- Inventory-only interface with the title/header and new-run reset button
- Automatic local JSON save

## Inventory controls
- Click an icon (or focus it with Tab and press Space/Enter) to toggle an item. Dim icons are missing; bright icons are obtained.
- The letter/potion slot shows the letter when no potion is held, the blue potion for one dose, or the red potion for two doses. Clicking it (or pressing Space/Enter) cycles through uncollected letter, collected letter, blue potion, red potion, then back to uncollected letter. Its current potion is saved and cleared by a new-run reset.
- Hover over an icon for its name, status, and item notes.
- Each dungeon column has its number, map, compass, and Triforce. Maps and compasses are saved per dungeon; Triforces toggle the existing Level 1-9 completed checks, not separate collectible pieces.
- The bottom-left panel contains 13 independent collectible Heart Containers (excluding the 3 starting hearts, for 16 hearts in game). They alternate top/bottom from left to right in a staggered W pattern: seven on top, six below. The two Bomb Bag upgrades sit at the right, upgrade 1 above upgrade 2, both using `BombBag.png`. The first heart matches its checklist entry; the remaining hearts and both Bomb Bag upgrades are saved separately. Click each heart or bag to toggle it independently. Individual bombs and current bomb count are not tracked.
- Existing saves retain their first five heart slots; the eight additional slots start uncollected until marked manually or detected in Mesen.
- Existing saves keep their previous bomb capacity upgrade as Bomb Bag upgrade 1; upgrade 2 starts uncollected. Legacy Bombs entries remain preserved but are no longer displayed or updated, and bomb possession does not count as a bag upgrade.
- Tabs, checklist/location editors, counters, and status text have been removed. Existing notes, locations, and items not displayed in the inventory remain preserved in saved data.
- Use **New run / reset** in the header to clear progress after confirmation. Save errors and version-file warnings appear in dialogs.
- With Mesen connected, the stopwatch starts once when the opening gameplay screen is ready and Link becomes controllable, not during the title, file select, or opening transition. It stops when Link reaches Zelda after Ganon, before the ending curtain/credits. If the tracker first connects during gameplay, it starts from that point; it cannot recover time played before connecting.
- The stopwatch keeps running through menus, deaths, emulator pauses/disconnections, and app restarts. Manual Start/Stop remains available; a manual stop is not undone by auto-start. Completed runs do not auto-start again. Use **New run / reset** to arm automatic timing for another run. Timer start/completion flags are saved. Detection uses the existing bridge updates and polling, so it can lag the game by about a second; it is not frame-accurate speedrun timing.
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
- **Starting a new seed:** click **New run / reset** before playing, because auto-tracking preserves collected progress (only Food and the current potion follow consumption).

### What is tracked
The script writes a dump of the NES RAM to `%LOCALAPPDATA%\HyruleProgress\bridge.json` about twice per second. The tracker reads it and automatically marks:
- Every inventory and equipment item, including swords, candles, rings, arrows, boomerangs, and Magical Shield
- Heart containers gained since the 3 starting hearts (all 13 collectible containers, up to 16 hearts in game) and the two Bomb Bag upgrades (maximum bomb capacities of 12 and 16, respectively, up from the starting capacity of 8). Spending bombs does not clear bag upgrades.
- Maps and compasses for Levels 1-9, plus the Triforce for Levels 1-8
- Stopwatch start when Link becomes playable and finish on Zelda rescue (with the win-game mode as a fallback if the initial contact was missed)

Auto-tracking only adds collected progress, except for Food and the current potion. Food checks when acquired and unchecks when used; buying it again checks it again. The letter/potion slot follows the current potion, including red-to-blue and blue-to-letter transitions when potions are used. Letter ownership stays tracked separately and remains marked after use, so the letter is dim if it has never been collected. Manual clicks still work, but while connected, the next in-game update overrides manual Food and potion changes. Older saves start with no potion. RAM is ignored on the title and file-select screens. Level 9 completion and overworld location checks remain manual. Because the game keeps only the best sword, candle, ring, and arrows, picking up a lower tier after a higher one cannot be detected.

## Save location
`%LOCALAPPDATA%\HyruleProgress\save.json`

## Important scope note
This is a first functional prototype, not a complete or seed-aware Zelda 1 Randomizer logic database. It does not read the randomizer seed. Inventory and dungeon progress can be tracked automatically from Mesen 2 (verified with Zelda Randomizer 3.6.6) or manually.
