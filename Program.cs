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

public enum Potion { None, Blue, Red }

public sealed class TrackerState
{
    public const int HeartContainerCount = 13;

    public List<TrackItem> Items { get; set; } = [];
    public List<TrackLocation> Locations { get; set; } = [];
    public int HeartUpgradeFlags { get; set; }
    public bool BombUpgrade { get; set; }
    public bool SecondBombUpgrade { get; set; }
    public Potion CurrentPotion { get; set; }
    public long RunElapsedTicks { get; set; }
    public DateTime? RunStartedAtUtc { get; set; }
    public bool RunTimerInitialized { get; set; }
    public bool RunTimerHasStarted { get; set; }
    public bool RunTimerCompleted { get; set; }

    public long GetElapsedTicks(DateTime utcNow)
    {
        long elapsed = Math.Max(0, RunElapsedTicks);
        long running = RunStartedAtUtc is DateTime startedAt
            ? Math.Max(0, (utcNow - startedAt).Ticks) : 0;
        return Math.Min(elapsed, TimeSpan.MaxValue.Ticks - running) + running;
    }

    public void StopRunTimer(DateTime utcNow)
    {
        RunElapsedTicks = GetElapsedTicks(utcNow);
        RunStartedAtUtc = null;
    }

    public string GetElapsedText(DateTime utcNow)
    {
        var elapsed = TimeSpan.FromTicks(GetElapsedTicks(utcNow));
        return FormattableString.Invariant(
            $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}");
    }

    public static TrackerState CreateDefault()
    {
        var s = new TrackerState { RunTimerInitialized = true };
        string[] items =
        [
            "Wooden Sword", "White Sword", "Magical Sword", "Wooden Boomerang",
            "Magical Boomerang", "Bow", "Arrows", "Silver Arrows", "Blue Candle",
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
    private readonly System.Windows.Forms.Timer runTimer = new() { Interval = 33 };
    private RunTimerDisplay timerDisplay = null!;
    private Button timerToggle = null!;
    private readonly System.Windows.Forms.Timer bridgeTimer = new() { Interval = 500 };
    private Label bridgeStatus = null!;
    private bool saveBlocked;

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
        if (!state.RunTimerInitialized)
            state.RunTimerInitialized = true;
        BuildUi();
        runTimer.Tick += (_, _) => UpdateTimerDisplay();
        runTimer.Start();
        bridgeTimer.Tick += (_, _) => PollBridge();
        bridgeTimer.Start();
        FormClosing += (_, _) => SaveState();
        Disposed += (_, _) => { runTimer.Dispose(); bridgeTimer.Dispose(); };
        if (versionWarning != null)
            Shown += (_, _) => MessageBox.Show(this, versionWarning, "App version", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private TrackerState LoadState()
    {
        try
        {
            return TrackerStore.Load(savePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            saveBlocked = true;
            Shown += (_, _) => MessageBox.Show(this,
                $"The saved run could not be loaded: {ex.Message}\n\nThe original save will not be overwritten. "
                + "Restore it and restart the app, or use New run / reset to replace it. "
                + "Changes in this session will not be saved until then.",
                "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return TrackerState.CreateDefault();
        }
    }

    private void SaveState()
    {
        if (saveBlocked) return;
        try
        {
            TrackerStore.Save(savePath, state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
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

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Black, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        var title = new Label { Text = "HYRULE PROGRESS", Font = new Font("Consolas", 20, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        header.Controls.Add(title, 0, 0);
        bridgeStatus = new Label
        {
            Font = new Font("Consolas", 11, FontStyle.Bold), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        header.Controls.Add(bridgeStatus, 1, 0);
        timerDisplay = new RunTimerDisplay
        {
            Font = new Font("Consolas", 17, FontStyle.Bold),
            ForeColor = Color.FromArgb(232, 184, 0), Dock = DockStyle.Fill,
            AccessibleName = "Run timer"
        };
        header.Controls.Add(timerDisplay, 2, 0);
        timerToggle = new Button { Width = 82, Height = 30, Dock = DockStyle.Fill, Margin = new Padding(3, 7, 3, 7) };
        timerToggle.FlatStyle = FlatStyle.Flat;
        timerToggle.BackColor = Color.FromArgb(24, 24, 24);
        timerToggle.ForeColor = Color.White;
        timerToggle.FlatAppearance.BorderColor = Color.FromArgb(112, 112, 112);
        timerToggle.Click += (_, _) => ToggleRunTimer();
        header.Controls.Add(timerToggle, 3, 0);
        var reset = new Button { Text = "New run / reset", Width = 140, Height = 30, Dock = DockStyle.Right };
        reset.FlatStyle = FlatStyle.Flat;
        reset.BackColor = Color.FromArgb(24, 24, 24);
        reset.ForeColor = Color.White;
        reset.FlatAppearance.BorderColor = Color.FromArgb(112, 112, 112);
        reset.Click += (_, _) => ResetRun();
        header.Controls.Add(reset, 4, 0);
        root.Controls.Add(header, 0, 0);

        inventory = new InventoryView(state);
        inventory.ProgressChanged += (_, _) => SaveState();
        root.Controls.Add(inventory, 0, 1);
        UpdateTimerDisplay();
        SetBridgeStatus(false);
    }

    private void PollBridge()
    {
        var ram = MesenBridge.TryReadRam();
        SetBridgeStatus(ram != null);
        if (ram == null) return;
        bool timerChanged = MesenBridge.ApplyRunTimer(state, ram, DateTime.UtcNow);
        bool progressChanged = MesenBridge.Apply(state, ram);
        if (timerChanged || progressChanged)
        {
            if (timerChanged) UpdateTimerDisplay();
            if (progressChanged) inventory.RefreshProgress();
            SaveState();
        }
    }

    private void SetBridgeStatus(bool connected)
    {
        bridgeStatus.Text = connected ? "● AUTO" : "○ AUTO";
        bridgeStatus.ForeColor = connected ? Color.LimeGreen : Color.DimGray;
        bridgeStatus.AccessibleName = connected ? "Mesen auto-tracking connected" : "Mesen auto-tracking not connected";
    }

    private void UpdateTimerDisplay()
    {
        timerDisplay.DisplayText = $"RUN {state.GetElapsedText(DateTime.UtcNow)}";
        string toggleText = state.RunStartedAtUtc.HasValue ? "Stop" : "Start";
        if (timerToggle.Text != toggleText)
        {
            timerToggle.Text = toggleText;
            timerToggle.AccessibleName = $"{toggleText} run timer";
        }
    }

    internal sealed class RunTimerDisplay : Control
    {
        private string displayText = "RUN 00:00:00.000";

        public string DisplayText
        {
            get => displayText;
            set
            {
                if (displayText == value) return;
                displayText = value;
                Invalidate();
            }
        }

        public RunTimerDisplay()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Black;
            AccessibleRole = AccessibleRole.StaticText;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics, displayText, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new TimerAccessibleObject(this);

        private sealed class TimerAccessibleObject(RunTimerDisplay owner) : ControlAccessibleObject(owner)
        {
            public override string? Value => owner.DisplayText;
        }
    }

    private void ToggleRunTimer()
    {
        state.RunTimerHasStarted = true;
        if (state.RunStartedAtUtc.HasValue)
        {
            state.StopRunTimer(DateTime.UtcNow);
        }
        else
        {
            state.RunStartedAtUtc = DateTime.UtcNow;
        }
        UpdateTimerDisplay();
        SaveState();
    }

    private void ResetRun()
    {
        if (MessageBox.Show("Start a new run? This replaces the current local save.", "New run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        state = TrackerState.CreateDefault();
        saveBlocked = false;
        inventory.SetState(state);
        UpdateTimerDisplay();
        SaveState();
    }
}
