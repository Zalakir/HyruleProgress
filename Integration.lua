-- Mesen 2 - Zelda 1 Randomizer
-- Diagnostic en lecture seule

local outputPath =
    os.getenv("LOCALAPPDATA")
    .. "\\HyruleProgress\\bridge.json"

local frameCounter = 0

local function exportMemory()
    local ram = {}

    -- 2 Ko de RAM interne de la NES
    for address = 0x0000, 0x07FF do
        ram[#ram + 1] =
            string.format("%02X", emu.read(
                address,
                emu.memType.nesInternalRam
            ))
    end

    local romInfo = emu.getRomInfo()

    local file = io.open(outputPath, "w")
    if not file then
        emu.log("Tracker : impossible d'ecrire le fichier JSON")
        return
    end

    file:write("{")
    file:write('"protocolVersion":1,')
    file:write('"emulator":"Mesen",')
    file:write('"frame":' .. tostring(frameCounter) .. ",")
    file:write('"rom":' .. '"' ..
        tostring(romInfo.name or "unknown")
            :gsub('[%z\1-\31\\"]', "?") .. '",')
    file:write('"ramStart":0,')
    file:write('"ramLength":2048,')
    file:write('"ramHex":"' .. table.concat(ram) .. '"')
    file:write("}")
    file:close()
end

emu.addEventCallback(function()
    frameCounter = frameCounter + 1

    -- Environ deux exports par seconde à 60 images/s
    if frameCounter % 30 == 0 then
        exportMemory()
    end
end, emu.eventType.endFrame)