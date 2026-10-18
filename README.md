# Hyrule Progress - Zelda 1 Randomizer Tracker

A lightweight Windows desktop tracker prototype for Zelda 1 Randomizer.

## Features
- Item checklist with notes
- Location/check list with visited and check-done status
- Loot and notes per location
- Overview counters for items, checks, visited locations, and dungeon checks
- Add/remove locations
- Automatic local JSON save

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

## Save location
`%LOCALAPPDATA%\Zelda1RandomizerTracker\save.json`

## Important scope note
This is a first functional prototype. The included locations are a starter checklist, not a complete or seed-aware Zelda 1 Randomizer logic database. It does not read the randomizer seed or connect to a Lua script yet. Checks and items can be tracked manually.
