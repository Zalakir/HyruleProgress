using System.Reflection;
using System.Text.Json;

namespace HyruleProgress;

internal static class RegressionChecks
{
    private static int checks;
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { checks++; return; }
        throw new InvalidOperationException(message);
    }

    private static byte[] Play()
    {
        var ram = new byte[0x800];
        ram[0x11] = 1;
        ram[0x12] = 5;
        ram[0x66F] = 0x20;
        return ram;
    }

    [STAThread]
    private static void Main()
    {
        CheckStorage();
        CheckBridge();
        CheckTimer();
        CheckInventory();
        Console.WriteLine($"PASS: {checks} regression checks.");
    }

    private static void CheckStorage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"HyruleProgress-checks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "save.json");
        try
        {
            Check(TrackerStore.Load(path).Items.Count > 0, "Missing save defaults");
            foreach (string invalid in new[]
            {
                "{", "null", """{"Items":null}""", """{"Items":[null]}""",
                """{"Locations":[{"Name":"Level 1","Type":null}]}""",
                """{"CurrentPotion":99}""", """{"RunElapsedTicks":-1}""",
                """{"HeartUpgradeFlags":4096}"""
            })
            {
                File.WriteAllText(path, invalid);
                Throws<JsonException>(() => TrackerStore.Load(path), "Invalid save was accepted");
                Check(File.ReadAllText(path) == invalid, "Loading modified invalid save");
            }
            File.WriteAllText(path,
                """{"BombUpgrade":true,"HeartUpgradeFlags":15,"Items":[{"Name":"Letter","Obtained":true,"Notes":"Keep me"},{"Name":"Bombs","Obtained":true}]}""");
            var state = TrackerStore.Load(path);
            Check(state.Items.Single(x => x.Name == "Letter").Notes == "Keep me", "Migration lost notes");
            Check(state.Items.Single(x => x.Name == "Letter").Obtained, "Migration lost ownership");
            Check(state.Items.Any(x => x.Name == "Bombs"), "Migration removed legacy item");
            Check(state.Items.Any(x => x.Name == "Food") && state.Locations.Count == 24, "Missing defaults not migrated");
            Check(state.BombUpgrade && !state.SecondBombUpgrade && state.HeartUpgradeFlags == 15, "Legacy upgrades lost");
            state.CurrentPotion = Potion.Red;
            state.RunTimerCompleted = true;
            TrackerStore.Save(path, state);
            var restored = TrackerStore.Load(path);
            Check(restored.CurrentPotion == Potion.Red && restored.RunTimerCompleted, "Save round trip");
            Check(!File.Exists(path + ".tmp"), "Successful save left temporary file");
            string original = File.ReadAllText(path);
            using (var locked = new FileStream(path + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                Throws<IOException>(() => TrackerStore.Save(path, TrackerState.CreateDefault()), "Locked write succeeded");
            Check(File.ReadAllText(path) == original, "Failed save damaged original");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Throws<UnauthorizedAccessException>(() => TrackerStore.Save(path, TrackerState.CreateDefault()), "Locked replacement succeeded");
            Check(File.ReadAllText(path) == original, "Failed replacement damaged original");
            TrackerStore.Save(path, state);
            Check(!File.Exists(path + ".tmp"), "Retry did not consume temporary file");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void CheckBridge()
    {
        Throws<ArgumentException>(() => MesenBridge.Apply(TrackerState.CreateDefault(), new byte[20]), "Short RAM accepted");
        Throws<ArgumentException>(() => MesenBridge.ApplyRunTimer(TrackerState.CreateDefault(), [], Now), "Short timer RAM accepted");
        foreach (byte mode in new byte[] { 0, 1, 2, 3, 0x0D, 0x0E, 0x0F, 0x14 })
        {
            var state = TrackerState.CreateDefault();
            state.Items.Single(x => x.Name == "Food").Obtained = true;
            state.CurrentPotion = Potion.Red;
            var ram = Play();
            ram[0x12] = mode;
            Check(!MesenBridge.Apply(state, ram), $"Non-gameplay mode {mode} updated progress");
            Check(state.Items.Single(x => x.Name == "Food").Obtained && state.CurrentPotion == Potion.Red, "Menu consumed items");
        }
        for (int total = 3; total <= 16; total++)
        {
            var state = TrackerState.CreateDefault();
            var ram = Play();
            ram[0x66F] = (byte)((total - 1) << 4);
            MesenBridge.Apply(state, ram);
            int gained = total - 3;
            Check(state.HeartUpgradeFlags == (1 << Math.Max(0, gained - 1)) - 1, "Heart count");
            Check(state.Items.Single(x => x.Name == "Heart Container").Obtained == (gained > 0), "First heart");
        }
        foreach (byte capacity in new byte[] { 8, 11, 12, 15, 16 })
        {
            var state = TrackerState.CreateDefault();
            var ram = Play();
            ram[0x67C] = capacity;
            MesenBridge.Apply(state, ram);
            Check(state.BombUpgrade == (capacity >= 12) && state.SecondBombUpgrade == (capacity >= 16), "Bag thresholds");
            ram[0x658] = 8;
            Check(!MesenBridge.Apply(state, ram), "Bomb count affected upgrades");
        }
        var current = TrackerState.CreateDefault();
        var live = Play();
        var food = current.Items.Single(x => x.Name == "Food");
        foreach (byte owned in new byte[] { 1, 0, 1, 0 })
        {
            live[0x65D] = owned;
            Check(MesenBridge.Apply(current, live) && food.Obtained == (owned != 0), "Food lifecycle");
        }
        foreach (byte potion in new byte[] { 2, 1, 0 })
        {
            live[0x65E] = potion;
            Check(MesenBridge.Apply(current, live) && (int)current.CurrentPotion == potion, "Potion lifecycle");
        }
        var ganon = TrackerState.CreateDefault();
        var level9 = ganon.Locations.Single(x => x.Name == "Level 9");
        var pickup = Play();
        pickup[0x10] = 9;
        pickup[0x671] = 0xFF;
        MesenBridge.Apply(ganon, pickup);
        Check(!level9.Checked, "Eight Triforce pieces incorrectly completed Level 9");
        Check(!MesenBridge.Apply(ganon, pickup) && !level9.Checked, "Ganon room before pickup completed Level 9");
        pickup[0x509] = 1;
        pickup[0x10] = 8;
        Check(!MesenBridge.Apply(ganon, pickup) && !level9.Checked, "Fanfare outside Level 9 completed it");
        pickup[0x10] = 9;
        pickup[0x12] = 1;
        Check(!MesenBridge.Apply(ganon, pickup) && !level9.Checked, "Menu snapshot completed Level 9");
        pickup[0x12] = 5;
        Check(MesenBridge.Apply(ganon, pickup) && level9.Checked, "Ganon Triforce pickup not detected");
        Check(!MesenBridge.Apply(ganon, pickup), "Repeated pickup caused extra updates");
        pickup[0x509] = 0;
        Check(!MesenBridge.Apply(ganon, pickup) && level9.Checked, "Ending fanfare cleared Level 9");
        var lateConnection = TrackerState.CreateDefault();
        pickup[0x672] = 1;
        Check(MesenBridge.Apply(lateConnection, pickup)
            && lateConnection.Locations.Single(x => x.Name == "Level 9").Checked, "Post-fanfare pickup not detected");
        var savedGanon = JsonSerializer.Deserialize<TrackerState>(JsonSerializer.Serialize(ganon))!;
        Check(savedGanon.Locations.Single(x => x.Name == "Level 9").Checked, "Level 9 completion not saved");
    }

    private static void CheckTimer()
    {
        var state = TrackerState.CreateDefault();
        var ram = Play();
        ram[0x11] = 0;
        Check(!MesenBridge.ApplyRunTimer(state, ram, Now), "Opening transition started timer");
        ram[0x11] = 1;
        Check(MesenBridge.ApplyRunTimer(state, ram, Now) && state.RunStartedAtUtc == Now, "Playable start");
        Check(!MesenBridge.ApplyRunTimer(state, ram, Now.AddSeconds(1)), "Repeated start");
        Check(state.GetElapsedTicks(Now.AddMinutes(1)) == TimeSpan.FromMinutes(1).Ticks, "Running time");
        state.StopRunTimer(Now.AddMinutes(1));
        Check(!MesenBridge.ApplyRunTimer(state, ram, Now.AddMinutes(2)) && state.RunStartedAtUtc == null, "Manual pause overridden");
        state.RunStartedAtUtc = Now.AddMinutes(2);
        ram[0x10] = 9;
        ram[0x350] = 0x37;
        Check(!MesenBridge.ApplyRunTimer(state, ram, Now.AddMinutes(3)), "Seeing Zelda ended run");
        ram[0xAD] = 1;
        ram[0xAC] = 0x40;
        Check(MesenBridge.ApplyRunTimer(state, ram, Now.AddMinutes(3)) && state.RunTimerCompleted, "Rescue finish");
        Check(state.RunElapsedTicks == TimeSpan.FromMinutes(2).Ticks && state.RunStartedAtUtc == null, "Rescue elapsed time");
        Check(!MesenBridge.ApplyRunTimer(state, Play(), Now.AddMinutes(4)), "Completed auto-restart");
        var legacy = new TrackerState { RunElapsedTicks = 123 };
        Check(MesenBridge.ApplyRunTimer(legacy, Play(), Now) && legacy.RunStartedAtUtc == null, "Legacy manual timer restarted");
        var fallback = new TrackerState { RunStartedAtUtc = Now };
        ram[0x12] = 0x13;
        Check(MesenBridge.ApplyRunTimer(fallback, ram, Now.AddSeconds(10)) && fallback.RunTimerCompleted, "Ending fallback");
        Check(fallback.RunElapsedTicks == TimeSpan.FromSeconds(10).Ticks, "Fallback time");
        var extreme = new TrackerState { RunElapsedTicks = long.MaxValue, RunStartedAtUtc = Now };
        Check(extreme.GetElapsedTicks(Now.AddSeconds(1)) == long.MaxValue, "Elapsed time overflow");
        extreme.StopRunTimer(Now.AddSeconds(1));
        Check(extreme.RunElapsedTicks == long.MaxValue && extreme.RunStartedAtUtc == null, "Stop overflow");
        var rollback = new TrackerState { RunElapsedTicks = 42, RunStartedAtUtc = Now.AddSeconds(5) };
        Check(rollback.GetElapsedTicks(Now) == 42, "Clock rollback reduced elapsed time");
    }

    private static IEnumerable<ItemTile> Tiles(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is ItemTile tile) yield return tile;
            foreach (var descendant in Tiles(child)) yield return descendant;
        }
    }

    private static void CheckInventory()
    {
        var state = TrackerState.CreateDefault();
        using var view = new InventoryView(state) { Size = new Size(1060, 690) };
        var tiles = Tiles(view).ToArray();
        var sprites = (Dictionary<string, Image>)typeof(InventoryView)
            .GetField("sprites", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        var imageField = typeof(ItemTile).GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(ReferenceEquals(imageField.GetValue(tiles.Single(x => x.AccessibleName == "Level 9 Complete")),
            sprites["Triforcelv9"]), "Level 9 Triforce icon");
        Check(tiles.Where(x => x.AccessibleName!.EndsWith(" Complete") && x.AccessibleName != "Level 9 Complete")
            .All(x => ReferenceEquals(imageField.GetValue(x), sprites["Triforce"])), "Other Triforce icons changed");
        var hearts = tiles.Where(x => x.AccessibleName!.StartsWith("Heart")).ToArray();
        var bags = tiles.Where(x => x.AccessibleName!.StartsWith("Bomb Bag")).ToArray();
        Check(hearts.Length == 13 && bags.Length == 2, "Slot counts");
        var grid = (TableLayoutPanel)hearts[0].Parent!;
        for (int i = 0; i < hearts.Length; i++)
            Check(grid.GetColumn(hearts[i]) == i && grid.GetRow(hearts[i]) == i % 2, "W layout");
        Check(grid.GetColumn(bags[0]) == 14 && grid.GetRow(bags[0]) == 0
            && grid.GetColumn(bags[1]) == 14 && grid.GetRow(bags[1]) == 1, "Stacked bags");
        var food = tiles.Single(x => x.AccessibleName == "Food");
        Check(!food.AccessibilityObject.State.HasFlag(AccessibleStates.Checked), "Unchecked accessibility");
        state.Items.Single(x => x.Name == "Food").Obtained = true;
        view.RefreshProgress();
        Check(food.AccessibilityObject.State.HasFlag(AccessibleStates.Checked), "Checked accessibility");
        var letter = tiles.Single(x => x.AccessibleName == "Letter");
        foreach (var expected in new[] { "Letter", "Blue Potion", "Red Potion", "Letter" })
        {
            typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(letter, [EventArgs.Empty]);
            Check(letter.AccessibleName == expected, "Manual potion cycle");
        }
        foreach (var size in new[] { new Size(840, 570), new Size(1060, 690) })
        {
            view.Size = size;
            view.PerformLayout();
            using var bitmap = new Bitmap(size.Width, size.Height);
            view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            Check(hearts.All(x => grid.ClientRectangle.Contains(x.Bounds)), "Heart bounds");
        }
        view.SetState(TrackerState.CreateDefault());
        Check(hearts.Concat(bags).All(x => !x.Obtained) && !food.Obtained, "Reset progress");
    }
}
