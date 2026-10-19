using System.Text.Json;

namespace HyruleProgress;

// Reads the RAM dump written by Integration.lua and marks progress found in Zelda 1 memory.
// Addresses follow the vanilla Zelda 1 inventory layout used by Zelda Randomizer.
internal static class MesenBridge
{
    public static readonly string BridgePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HyruleProgress", "bridge.json");

    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(3);

    private const int CurrentLevel = 0x0010;
    private const int IsUpdatingMode = 0x0011;
    private const int GameMode = 0x0012;
    private const int LinkState = 0x00AC;
    private const int ZeldaState = 0x00AD;
    private const int Paused = 0x00E0;
    private const int MenuState = 0x00E1;
    private const int FirstObjectType = 0x0350;
    private const int TriforceFanfareActive = 0x0509;
    private const int Sword = 0x0657;
    private const int Arrows = 0x0659;
    private const int Bow = 0x065A;
    private const int Candle = 0x065B;
    private const int Recorder = 0x065C;
    private const int Food = 0x065D;
    private const int PotionSlot = 0x065E;
    private const int MagicalRod = 0x065F;
    private const int Raft = 0x0660;
    private const int BookOfMagic = 0x0661;
    private const int Ring = 0x0662;
    private const int Stepladder = 0x0663;
    private const int MagicKey = 0x0664;
    private const int PowerBracelet = 0x0665;
    private const int Letter = 0x0666;
    private const int Compasses = 0x0667;
    private const int Maps = 0x0668;
    private const int Level9Compass = 0x0669;
    private const int Level9Map = 0x066A;
    private const int HeartContainers = 0x066F;
    private const int Triforce = 0x0671;
    private const int LastBossDefeated = 0x0672;
    private const int Boomerang = 0x0674;
    private const int MagicalBoomerang = 0x0675;
    private const int MagicalShield = 0x0676;
    private const int MaxBombs = 0x067C;

    private const int StartingHearts = 3;
    private const int DefaultMaxBombs = 8;
    private const int BombUpgradeSize = 4;

    public static byte[]? TryReadRam()
    {
        try
        {
            var info = new FileInfo(BridgePath);
            if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc > StaleAfter)
                return null;
            using var stream = new FileStream(BridgePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (!root.TryGetProperty("ramHex", out var hex) || hex.GetString() is not { } text)
                return null;
            var ram = Convert.FromHexString(text);
            return ram.Length >= 0x0800 ? ram : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException)
        {
            // The script rewrites the file twice a second; a partial read is retried on the next poll.
            return null;
        }
    }

    private static void ValidateRam(byte[] ram)
    {
        ArgumentNullException.ThrowIfNull(ram);
        if (ram.Length < 0x0800)
            throw new ArgumentException("A complete 2 KiB NES RAM snapshot is required.", nameof(ram));
    }

    public static bool IsInGame(byte[] ram)
    {
        ValidateRam(ram);
        return ram[GameMode] is >= 0x04 and <= 0x0C or >= 0x10 and <= 0x13;
    }

    public static bool ApplyRunTimer(TrackerState state, byte[] ram, DateTime utcNow)
    {
        ValidateRam(ram);
        if (state.RunTimerCompleted) return false;
        bool changed = false;
        // Older saves already using the manual stopwatch must not auto-start again.
        if (!state.RunTimerHasStarted && (state.RunStartedAtUtc.HasValue || state.RunElapsedTicks > 0))
        {
            state.RunTimerHasStarted = true;
            changed = true;
        }

        // UpdateZelda sets slot 1 to state 1 and halts Link on contact,
        // about two seconds before switching to the win-game mode ($13).
        bool rescuedZelda = ram[GameMode] == 0x13
            || (ram[GameMode] == 0x05 && ram[IsUpdatingMode] != 0
                && ram[CurrentLevel] == 9 && ram[FirstObjectType] == 0x37
                && ram[ZeldaState] == 1 && ram[LinkState] == 0x40);
        if (rescuedZelda)
        {
            state.StopRunTimer(utcNow);
            state.RunTimerCompleted = true;
            return true;
        }

        bool playable = ram[GameMode] == 0x05 && ram[IsUpdatingMode] != 0
            && ram[Paused] == 0 && ram[MenuState] == 0 && ram[LinkState] == 0;
        if (!state.RunTimerHasStarted && playable)
        {
            state.RunStartedAtUtc = utcNow;
            state.RunTimerHasStarted = true;
            return true;
        }
        return changed;
    }

    // Progress is only ever added, except for food and the current potion, which follow consumption.
    public static bool Apply(TrackerState state, byte[] ram)
    {
        if (!IsInGame(ram)) return false;
        bool changed = false;

        void Item(string name, bool owned, bool trackCurrent = false)
        {
            if (!owned && !trackCurrent) return;
            var item = state.Items.FirstOrDefault(x => x.Name == name);
            if (item != null && item.Obtained != owned)
            {
                item.Obtained = owned;
                changed = true;
            }
        }

        Item("Wooden Sword", ram[Sword] == 1);
        Item("White Sword", ram[Sword] == 2);
        Item("Magical Sword", ram[Sword] == 3);
        Item("Arrows", ram[Arrows] == 1);
        Item("Silver Arrows", ram[Arrows] == 2);
        Item("Bow", ram[Bow] != 0);
        Item("Blue Candle", ram[Candle] == 1);
        Item("Red Candle", ram[Candle] == 2);
        Item("Recorder", ram[Recorder] != 0);
        Item("Food", ram[Food] != 0, trackCurrent: true);
        Item("Magical Rod", ram[MagicalRod] != 0);
        Item("Raft", ram[Raft] != 0);
        Item("Book of Magic", ram[BookOfMagic] != 0);
        Item("Blue Ring", ram[Ring] == 1);
        Item("Red Ring", ram[Ring] == 2);
        Item("Stepladder", ram[Stepladder] != 0);
        Item("Magic Key", ram[MagicKey] != 0);
        Item("Power Bracelet", ram[PowerBracelet] != 0);
        Item("Letter", ram[Letter] != 0);
        var potion = ram[PotionSlot] switch
        {
            1 => Potion.Blue,
            2 => Potion.Red,
            _ => Potion.None
        };
        if (state.CurrentPotion != potion)
        {
            state.CurrentPotion = potion;
            changed = true;
        }
        Item("Wooden Boomerang", ram[Boomerang] != 0);
        Item("Magical Boomerang", ram[MagicalBoomerang] != 0);
        Item("Magical Shield", ram[MagicalShield] != 0);

        int containers = (ram[HeartContainers] >> 4) + 1;
        int gained = Math.Clamp(containers - StartingHearts, 0, TrackerState.HeartContainerCount);
        Item("Heart Container", gained >= 1);
        int heartFlags = state.HeartUpgradeFlags | ((1 << Math.Max(0, gained - 1)) - 1);
        if (heartFlags != state.HeartUpgradeFlags)
        {
            state.HeartUpgradeFlags = heartFlags;
            changed = true;
        }

        if (ram[MaxBombs] >= DefaultMaxBombs + BombUpgradeSize && !state.BombUpgrade)
        {
            state.BombUpgrade = true;
            changed = true;
        }
        if (ram[MaxBombs] >= DefaultMaxBombs + 2 * BombUpgradeSize && !state.SecondBombUpgrade)
        {
            state.SecondBombUpgrade = true;
            changed = true;
        }

        for (int level = 1; level <= 9; level++)
        {
            var location = state.Locations.FirstOrDefault(x => x.Name == $"Level {level}" && x.Type.Equals("Dungeon", StringComparison.OrdinalIgnoreCase));
            if (location == null) continue;
            int bit = 1 << (level - 1);
            bool map = level == 9 ? ram[Level9Map] != 0 : (ram[Maps] & bit) != 0;
            bool compass = level == 9 ? ram[Level9Compass] != 0 : (ram[Compasses] & bit) != 0;
            // The Ganon flag is set after the pickup fanfare, not at the boss's death.
            bool triforce = level == 9
                ? ram[LastBossDefeated] != 0
                    || (ram[CurrentLevel] == 9 && ram[TriforceFanfareActive] != 0)
                : (ram[Triforce] & bit) != 0;
            if (map && !location.HasMap) { location.HasMap = true; changed = true; }
            if (compass && !location.HasCompass) { location.HasCompass = true; changed = true; }
            if (triforce && !location.Checked) { location.Checked = true; changed = true; }
        }

        return changed;
    }
}
