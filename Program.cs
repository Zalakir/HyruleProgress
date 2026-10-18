using System.Text.Json;
using System.Drawing;
using System.Windows.Forms;

namespace Zelda1RandomizerTracker;

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
    public string SeedName { get; set; } = "";
    public List<TrackItem> Items { get; set; } = [];
    public List<TrackLocation> Locations { get; set; } = [];

    public static TrackerState CreateDefault()
    {
        var s = new TrackerState();
        string[] items =
        [
            "Wooden Sword", "White Sword", "Magical Sword", "Wooden Boomerang",
            "Magical Boomerang", "Bow", "Silver Arrows", "Bombs", "Blue Candle",
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
    public string Loot { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class TrackerForm : Form
{
    private readonly string savePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zelda1RandomizerTracker", "save.json");
    private TrackerState state;
    private readonly DataGridView itemGrid = new();
    private readonly DataGridView locationGrid = new();
    private readonly TextBox seedBox = new();
    private readonly Label summary = new();
    private readonly Label status = new();

    public TrackerForm()
    {
        Text = "Zelda 1 Randomizer Tracker";
        MinimumSize = new Size(900, 600);
        Size = new Size(1120, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 242, 226);

        state = LoadState();
        BuildUi();
        RefreshGrids();
        UpdateSummary();
    }

    private TrackerState LoadState()
    {
        try
        {
            if (File.Exists(savePath))
            {
                var loaded = JsonSerializer.Deserialize<TrackerState>(File.ReadAllText(savePath));
                if (loaded != null) return loaded;
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
            status.Text = $"Saved locally · {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            status.Text = "Save failed: " + ex.Message;
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "ZELDA 1 · RANDOMIZER TRACKER", Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) };
        header.Controls.Add(title);
        header.Controls.Add(new Label { Text = "Seed / run:", AutoSize = true, Location = new Point(2, 39) });
        seedBox.SetBounds(90, 35, 280, 28);
        seedBox.Text = state.SeedName;
        seedBox.TextChanged += (_, _) => { state.SeedName = seedBox.Text; SaveState(); };
        header.Controls.Add(seedBox);
        var reset = new Button { Text = "New run / reset", Width = 140, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(Width - 200, 35) };
        reset.Click += (_, _) => ResetRun();
        header.Controls.Add(reset);
        root.Controls.Add(header, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var dash = new TabPage("Overview");
        var items = new TabPage("Items");
        var locations = new TabPage("Locations & checks");
        tabs.TabPages.AddRange([dash, items, locations]);
        root.Controls.Add(tabs, 0, 1);

        var dashPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22) };
        summary.SetBounds(20, 20, 700, 150);
        summary.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        dashPanel.Controls.Add(summary);
        var hint = new Label
        {
            Text = "Quick workflow\r\n1. Mark an item when you pick it up.\r\n2. Mark a location as visited and a check as completed.\r\n3. Add the found item in the Loot column to keep a simple history.\r\n\r\nThis starter list is editable and is not a seed-aware logic solver.",
            AutoSize = true, Location = new Point(20, 190), Font = new Font("Segoe UI", 11)
        };
        dashPanel.Controls.Add(hint);
        dash.Controls.Add(dashPanel);

        ConfigureGrid(itemGrid);
        itemGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Obtained", HeaderText = "Got it", DataPropertyName = "Obtained", Width = 75 });
        itemGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Item", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        itemGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", DataPropertyName = "Notes", Width = 260 });
        itemGrid.CellValueChanged += (_, _) => { PullItemGrid(); SaveState(); UpdateSummary(); };
        itemGrid.CurrentCellDirtyStateChanged += (_, _) => { if (itemGrid.IsCurrentCellDirty) itemGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        itemGrid.DataError += (_, e) => { e.ThrowException = false; };
        var itemPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        itemPanel.Controls.Add(itemGrid);
        items.Controls.Add(itemPanel);

        ConfigureGrid(locationGrid);
        locationGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Visited", HeaderText = "Visited", DataPropertyName = "Visited", Width = 75 });
        locationGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Checked", HeaderText = "Check done", DataPropertyName = "Checked", Width = 90 });
        locationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Location", DataPropertyName = "Name", Width = 230, ReadOnly = true });
        locationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", DataPropertyName = "Type", Width = 110 });
        locationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Loot", HeaderText = "Loot found", DataPropertyName = "Loot", Width = 210 });
        locationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", DataPropertyName = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        locationGrid.CellValueChanged += (_, _) => { PullLocationGrid(); SaveState(); UpdateSummary(); };
        locationGrid.CurrentCellDirtyStateChanged += (_, _) => { if (locationGrid.IsCurrentCellDirty) locationGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        locationGrid.DataError += (_, e) => { e.ThrowException = false; };
        var locPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 42 };
        var add = new Button { Text = "+ Add location/check", Width = 170, Height = 30, Location = new Point(0, 4) };
        add.Click += (_, _) => AddLocation();
        var remove = new Button { Text = "Remove selected", Width = 140, Height = 30, Location = new Point(180, 4) };
        remove.Click += (_, _) => RemoveLocation();
        toolbar.Controls.Add(add); toolbar.Controls.Add(remove);
        locPanel.Controls.Add(locationGrid); locPanel.Controls.Add(toolbar);
        locationGrid.Dock = DockStyle.Fill;
        locations.Controls.Add(locPanel);

        status.Text = "Ready";
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(status, 0, 2);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private void RefreshGrids()
    {
        itemGrid.DataSource = null;
        itemGrid.DataSource = state.Items;
        locationGrid.DataSource = null;
        locationGrid.DataSource = state.Locations;
    }

    private void PullItemGrid()
    {
        if (itemGrid.IsCurrentCellInEditMode) itemGrid.EndEdit();
        itemGrid.Refresh();
    }

    private void PullLocationGrid()
    {
        if (locationGrid.IsCurrentCellInEditMode) locationGrid.EndEdit();
        locationGrid.Refresh();
    }

    private void UpdateSummary()
    {
        int got = state.Items.Count(x => x.Obtained);
        int checks = state.Locations.Count(x => x.Checked);
        int visited = state.Locations.Count(x => x.Visited);
        int dungeons = state.Locations.Count(x => x.Type.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) && x.Checked);
        double itemPct = state.Items.Count == 0 ? 0 : got * 100.0 / state.Items.Count;
        double checkPct = state.Locations.Count == 0 ? 0 : checks * 100.0 / state.Locations.Count;
        summary.Text = $"ITEMS  {got}/{state.Items.Count}  ({itemPct:0}%)\r\n" +
                       $"CHECKS  {checks}/{state.Locations.Count}  ({checkPct:0}%)\r\n" +
                       $"LOCATIONS VISITED  {visited}    ·    DUNGEON CHECKS  {dungeons}/9";
    }

    private void AddLocation()
    {
        using var dialog = new AddLocationDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        state.Locations.Add(new TrackLocation { Name = dialog.LocationName, Type = dialog.LocationType });
        RefreshGrids(); SaveState(); UpdateSummary();
    }

    private void RemoveLocation()
    {
        if (locationGrid.CurrentRow?.DataBoundItem is not TrackLocation location) return;
        if (MessageBox.Show($"Remove '{location.Name}' from this run?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        state.Locations.Remove(location);
        RefreshGrids(); SaveState(); UpdateSummary();
    }

    private void ResetRun()
    {
        if (MessageBox.Show("Start a new run? This replaces the current local save.", "New run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        state = TrackerState.CreateDefault();
        state.SeedName = "";
        seedBox.Text = "";
        RefreshGrids(); SaveState(); UpdateSummary();
    }

}

public sealed class AddLocationDialog : Form
{
    private readonly TextBox nameBox = new();
    private readonly ComboBox typeBox = new();
    public string LocationName => nameBox.Text.Trim();
    public string LocationType => typeBox.Text;

    public AddLocationDialog()
    {
        Text = "Add location/check";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(380, 165);
        Controls.Add(new Label { Text = "Location name", AutoSize = true, Location = new Point(15, 18) });
        nameBox.SetBounds(15, 42, 345, 28); Controls.Add(nameBox);
        Controls.Add(new Label { Text = "Type", AutoSize = true, Location = new Point(15, 78) });
        typeBox.SetBounds(100, 74, 180, 28);
        typeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeBox.Items.AddRange(["Overworld", "Dungeon", "Shop", "Cave", "Other"]);
        typeBox.SelectedIndex = 0; Controls.Add(typeBox);
        var ok = new Button { Text = "Add", DialogResult = DialogResult.OK, Location = new Point(190, 118), Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, 118), Width = 80 };
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, e) => { if (string.IsNullOrWhiteSpace(nameBox.Text)) { MessageBox.Show("Enter a location name."); DialogResult = DialogResult.None; } };
    }
}
