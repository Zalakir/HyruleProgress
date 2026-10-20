-- Mesen 2 - Zelda 1 Randomizer
-- Export en lecture seule de la RAM pour le suivi automatique de Hyrule Progress

local outputPath =
    os.getenv("LOCALAPPDATA")
    .. "\\HyruleProgress\\bridge.json"
local tempPath = outputPath .. ".tmp"

local frameCounter = 0
local pickupSession = tostring(os.time()) .. "-" .. tostring({}):gsub("[^%w]", "")
local roomPickups = {}
local previousInventory = nil

local majorItemAddresses = {
    0x0657, 0x0659, 0x065A, 0x065B, 0x065C, 0x065F,
    0x0660, 0x0661, 0x0662, 0x0663, 0x0664, 0x0665,
    0x0666, 0x0674, 0x0675, 0x0676
}

local importantRoomItems = {
    [0x01] = true, [0x02] = true, [0x03] = true, -- Swords
    [0x05] = true, -- Recorder
    [0x06] = true, [0x07] = true, -- Candles
    [0x08] = true, [0x09] = true, [0x0A] = true, -- Arrows, Bow
    [0x0B] = true, [0x0C] = true, [0x0D] = true, -- Magic Key, Raft, Stepladder
    [0x10] = true, [0x11] = true, -- Rod, Book
    [0x12] = true, [0x13] = true, -- Rings
    [0x14] = true, [0x15] = true, -- Bracelet, Letter
    [0x1A] = true, -- Heart Container
    [0x1C] = true, [0x1D] = true, [0x1E] = true -- Boomerangs, Shield
}

local function readRam(address)
    return emu.read(address, emu.memType.nesInternalRam)
end

local function readCpu(address)
    -- Bit 0x100 disables side effects in Mesen 2's CPU address space.
    return emu.read(address, emu.memType.nesMemory + 0x100)
end

local function captureRoomPickup()
    local level = readRam(0x0010)
    local mode = readRam(0x0012)
    local slot = readRam(0x0016)
    if level < 1 or level > 9 or slot > 2
        or (mode ~= 0x05 and mode ~= 0x09)
        or readRam(0x0011) == 0 or readRam(0x00E0) ~= 0
        or readRam(0x00E1) ~= 0 then
        previousInventory = nil
        return
    end

    local quest = readRam(0x062D + slot)
    local room = readRam(0x00EB)
    if quest > 1 or room > 0x7F then
        previousInventory = nil
        return
    end
    local current = { level = level, mode = mode, slot = slot, quest = quest, room = room }
    -- The loaded level points at its own world-flags block. Bit 0x10 records
    -- collection even when a duplicate or lower-tier item leaves inventory unchanged.
    local flagsAddress = readCpu(0x6BAF) + 256 * readCpu(0x6BB0)
    if flagsAddress >= 0x067F and flagsAddress + room <= 0x07FF then
        current.roomItemTaken = math.floor(readCpu(flagsAddress + room) / 16) % 2 == 1
    end
    current.roomItem = readRam(0x00AB)
    for _, address in ipairs(majorItemAddresses) do
        current[address] = readRam(address)
    end
    -- The high nibble is maximum hearts; the low nibble is current health.
    current[0x066F] = math.floor(readRam(0x066F) / 16)

    local previous = previousInventory
    previousInventory = current
    if not previous or previous.level ~= level or previous.mode ~= mode
        or previous.slot ~= slot or previous.quest ~= quest or previous.room ~= room then
        return
    end

    local gainedItem = previous.roomItemTaken == false and current.roomItemTaken == true
        and previous.roomItem == current.roomItem and importantRoomItems[current.roomItem] == true
    for _, address in ipairs(majorItemAddresses) do
        if current[address] > previous[address] then gainedItem = true end
    end
    if current[0x066F] > previous[0x066F] then gainedItem = true end
    if not gainedItem then return end

    local roomType = 1 -- Mob / ordinary dungeon room
    local roomIndex = 1
    if mode == 0x09 then
        -- Attribute D's low six bits: 0x3E is a passage, 0x3F is an item cellar.
        if readCpu(0x69FE + room) % 64 ~= 0x3F then
            emu.log("Tracker: pickup in an unrecognized cellar; mark the room manually.")
            return
        end
        roomType = 2
        local seen = {}
        local ordinal = 0
        local found = false
        for index = 0, 9 do
            local cellar = readCpu(0x6BB2 + index)
            if cellar <= 0x7F and not seen[cellar] then
                seen[cellar] = true
                if readCpu(0x69FE + cellar) % 64 == 0x3F then
                    ordinal = ordinal + 1
                    if cellar == room then
                        roomIndex = ordinal
                        found = true
                        break
                    end
                end
            end
        end
        if not found or roomIndex > 2 then
            emu.log("Tracker: item cellar is outside the supported layout; mark the room manually.")
            return
        end
    elseif room == readCpu(0x6BBC) then
        roomType = 0
    end

    -- Retain events so slow polls and app restarts cannot lose the pickup's original location.
    roomPickups[#roomPickups + 1] = string.format(
        '{"Id":"%s-%d","Level":%d,"Quest":%d,"RoomType":%d,"RoomIndex":%d}',
        pickupSession, frameCounter, level, quest, roomType, roomIndex
    )
end

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

    local file = io.open(tempPath, "w")
    if not file then
        emu.log("Tracker : impossible d'ecrire le fichier JSON")
        return
    end

    file:write("{")
    file:write('"protocolVersion":2,')
    file:write('"emulator":"Mesen",')
    file:write('"frame":' .. tostring(frameCounter) .. ",")
    file:write('"rom":' .. '"' ..
        tostring(romInfo.name or "unknown")
            :gsub('[%z\1-\31\\"]', "?") .. '",')
    file:write('"ramStart":0,')
    file:write('"ramLength":2048,')
    file:write('"ramHex":"' .. table.concat(ram) .. '",')
    file:write('"roomPickups":[' .. table.concat(roomPickups, ",") .. ']')
    file:write("}")
    file:close()

    -- Fichier temporaire puis renommage : l'application ne lit jamais un JSON a moitie ecrit
    os.remove(outputPath)
    os.rename(tempPath, outputPath)
end

emu.addEventCallback(function()
    frameCounter = frameCounter + 1
    captureRoomPickup()

    -- Environ deux exports par seconde à 60 images/s
    if frameCounter % 30 == 0 then
        exportMemory()
    end
end, emu.eventType.endFrame)

-- A loaded state or reset is not an item pickup.
emu.addEventCallback(function() previousInventory = nil end, emu.eventType.stateLoaded)
emu.addEventCallback(function() previousInventory = nil end, emu.eventType.reset)