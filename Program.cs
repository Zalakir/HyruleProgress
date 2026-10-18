using System.Text.Json;
using System.Drawing;
using System.Windows.Forms;

namespace HyruleProgress;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrackerForm());
    }
}

public sealed class TrackerState
{
    public List<TrackItem> Items { get; set; } = [];
    public List<TrackLocation> Locations { get; set; } = [];
    public int HeartUpgradeFlags { get; set; }
    public bool BombUpgrade { get; set; }

    public static TrackerState CreateDefault()
    {
        var s = new TrackerState();
        string[] items =
        [
            "Wooden Sword", "White Sword", "Magical Sword", "Wooden Boomerang",
            "Magical Boomerang", "Bow", "Arrows", "Silver Arrows", "Bombs", "Blue Candle",
            "Red Candle", "Recorder", "Food", "Letter",
            "Power Bracelet", "Raft", "Stepladder", "Magic Key", "Blue Ring",
            "Red Ring", "Magical Shield", "Book of Magic", "Magical Rod",
            "Bait", "Map", "Compass", "Heart Container"
        ];
        foreach (var name in items) s.Items.Add(new TrackItem { Name = name });
        for (int i = 1; i <= 9; i++)
            s.Locations.Add(new TrackLocation { Name = $"Level {i}", Type = "Dungeon" });
        string[] other =
        [
            "Starting cave", "Overworld cave / item shop", "Overworld secret",
            "Take-any cave", "Potion shop", "Recorder staircase", "White Sword cave",
            "Magical Sword cave", "Armos item", "Graveyard secret", "Coast / raft check",
            "Waterfall area", "Lost Hills", "Lost Woods", "Spectacle Rock"
        ];
        foreach (var name in other) s.Locations.Add(new TrackLocation { Name = name, Type = "Overworld" });
        return s;
    }
}

public sealed class TrackItem
{
    public string Name { get; set; } = "";
    public bool Obtained { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class TrackLocation
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Overworld";
    public bool Visited { get; set; }
    public bool Checked { get; set; }
    public bool HasMap { get; set; }
    public bool HasCompass { get; set; }
    public string Loot { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class TrackerForm : Form
{
    private readonly string savePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HyruleProgress", "save.json");
    private TrackerState state;
    private InventoryView inventory = null!;

    public TrackerForm()
    {
        string? versionWarning = null;
        try
        {
            var version = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "version.txt")).Trim();
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidDataException("The version file is empty.");
            Text = $"Hyrule Progress v{version}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Text = "Hyrule Progress";
            versionWarning = $"Version information unavailable: {ex.Message}";
        }

        MinimumSize = new Size(900, 680);
        Size = new Size(1120, 800);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.Black;
        ForeColor = Color.White;

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon", "HyruleProgress.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        state = LoadState();
        BuildUi();
        if (versionWarning != null)
            Shown += (_, _) => MessageBox.Show(this, versionWarning, "App version", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private TrackerState LoadState()
    {
        try
        {
            if (File.Exists(savePath))
            {
                var loaded = JsonSerializer.Deserialize<TrackerState>(File.ReadAllText(savePath));
                if (loaded != null)
                {
                    if (!loaded.Items.Any(x => x.Name == "Arrows"))
                        loaded.Items.Add(new TrackItem { Name = "Arrows" });
                    return loaded;
                }
            }
        }
        catch { }
        return TrackerState.CreateDefault();
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            File.WriteAllText(savePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Save failed: " + ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(14), BackColor = Color.Black };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "HYRULE PROGRESS", Font = new Font("Consolas", 20, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(0, 0) };
        header.Controls.Add(title);
        var reset = new Button { Text = "New run / reset", Width = 140, Height = 30, Dock = DockStyle.Right };
        reset.FlatStyle = FlatStyle.Flat;
        reset.BackColor = Color.FromArgb(24, 24, 24);
        reset.ForeColor = Color.White;
        reset.FlatAppearance.BorderColor = Color.FromArgb(112, 112, 112);
        reset.Click += (_, _) => ResetRun();
        header.Controls.Add(reset);
        root.Controls.Add(header, 0, 0);

        inventory = new InventoryView(state);
        inventory.ProgressChanged += (_, _) => SaveState();
        root.Controls.Add(inventory, 0, 1);
    }

    private void ResetRun()
    {
        if (MessageBox.Show("Start a new run? This replaces the current local save.", "New run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        state = TrackerState.CreateDefault();
        inventory.SetState(state);
        SaveState();
    }
}
