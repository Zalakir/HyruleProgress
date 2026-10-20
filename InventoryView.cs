using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HyruleProgress;

internal sealed class InventoryView : UserControl
{
    private static readonly (string Name, string? Sprite)[] InventoryItems =
    [
        ("Bow", "Bow"), ("Silver Arrows", "MagicalArrow"), ("Arrows", "Arrow"),
        ("Wooden Boomerang", "Boomerang"), ("Magical Boomerang", "MagicalBoomerang"),
        ("Blue Candle", "BlueCandle"), ("Red Candle", "RedCandle"), ("Recorder", "Recorder"),
        ("Food", "Food"), ("Letter", "Letter"), ("Raft", "Raft"), ("Stepladder", "Stepladder"),
        ("Magic Key", "MagicalKey"), ("Magical Rod", "MagicalRod"), ("Book of Magic", "Book")
    ];

    private static readonly (string Name, string Sprite)[] EquipmentItems =
    [
        ("Wooden Sword", "Sword"), ("White Sword", "WhiteSword"), ("Magical Sword", "MagicalSword"),
        ("Power Bracelet", "PowerBracelet"), ("Blue Ring", "BlueRing"), ("Red Ring", "RedRing")
    ];

    private readonly List<(ItemTile Tile, Func<bool?> Read, Func<string>? Notes)> tiles = [];
    private readonly Dictionary<string, Image> sprites = [];
    private readonly ToolTip toolTip = new();
    private readonly TableLayoutPanel dungeonRooms;
    private readonly Label questLabel;
    private readonly Button questToggle;
    private bool secondQuest;
    private ItemTile letterPotionTile = null!;
    private TrackerState state;

    public event EventHandler? ProgressChanged;

    public InventoryView(TrackerState state)
    {
        this.state = state;
        Dock = DockStyle.Fill;
        BackColor = Color.Black;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var left = CreateGrid(1, 2);
        left.Margin = new Padding(0, 0, 10, 0);
        left.RowStyles[0].Height = 70;
        left.RowStyles[1].Height = 30;
        layout.Controls.Add(left, 0, 0);
        var right = CreateGrid(1, 2);
        right.Margin = Padding.Empty;
        right.RowStyles[0].SizeType = SizeType.Absolute;
        right.RowStyles[0].Height = 350;
        right.RowStyles[1].Height = 100;
        layout.Controls.Add(right, 1, 0);

        var items = new RetroFrame("ITEMS", Color.FromArgb(32, 200, 48)) { Margin = new Padding(0, 0, 0, 8) };
        left.Controls.Add(items, 0, 0);
        var itemLayout = CreateGrid(5, 3);
        items.Controls.Add(itemLayout);
        foreach (var (name, sprite) in InventoryItems)
        {
            if (name == "Letter")
                AddLetterPotion(itemLayout);
            else
                AddItem(itemLayout, name, sprite);
        }

        var upgrades = new RetroFrame("", Color.FromArgb(32, 200, 48)) { Margin = Padding.Empty };
        left.Controls.Add(upgrades, 0, 1);
        var upgradeLayout = CreateGrid(16, 2);
        upgrades.Controls.Add(upgradeLayout);
        for (int index = 0; index < TrackerState.HeartContainerCount; index++)
        {
            int flag = index == 0 ? 0 : 1 << (index - 1);
            var heart = index == 0
                ? AddItem(upgradeLayout, "Heart Container", "HeartContainer")
                : AddTile(upgradeLayout, $"Heart container upgrade {index}", "HeartContainer",
                    () => (this.state.HeartUpgradeFlags & flag) != 0,
                    () => this.state.HeartUpgradeFlags ^= flag);
            upgradeLayout.SetCellPosition(heart, new TableLayoutPanelCellPosition(index, index % 2));
            upgradeLayout.SetColumnSpan(heart, 2);
        }
        var firstBag = AddTile(upgradeLayout, "Bomb Bag upgrade 1", "BombBag",
            () => this.state.BombUpgrade, () => this.state.BombUpgrade = !this.state.BombUpgrade);
        upgradeLayout.SetCellPosition(firstBag, new TableLayoutPanelCellPosition(14, 0));
        upgradeLayout.SetColumnSpan(firstBag, 2);
        var secondBag = AddTile(upgradeLayout, "Bomb Bag upgrade 2", "BombBag",
            () => this.state.SecondBombUpgrade, () => this.state.SecondBombUpgrade = !this.state.SecondBombUpgrade);
        upgradeLayout.SetCellPosition(secondBag, new TableLayoutPanelCellPosition(14, 1));
        upgradeLayout.SetColumnSpan(secondBag, 2);

        var dungeons = new RetroFrame("DUNGEONS", Color.FromArgb(232, 184, 0)) { Margin = new Padding(0, 0, 0, 8) };
        right.Controls.Add(dungeons, 0, 0);
        var dungeonContent = CreateGrid(1, 3);
        dungeonContent.RowStyles[0].SizeType = SizeType.Absolute;
        dungeonContent.RowStyles[0].Height = 166;
        dungeonContent.RowStyles[1].SizeType = SizeType.Absolute;
        dungeonContent.RowStyles[1].Height = 30;
        dungeonContent.RowStyles[2].SizeType = SizeType.Absolute;
        dungeonContent.RowStyles[2].Height = 108;
        dungeons.Controls.Add(dungeonContent);
        var dungeonLayout = CreateGrid(9, 4);
        dungeonLayout.RowStyles[0].SizeType = SizeType.Absolute;
        dungeonLayout.RowStyles[0].Height = 22;
        dungeonContent.Controls.Add(dungeonLayout, 0, 0);
        for (int level = 1; level <= 9; level++)
            dungeonLayout.Controls.Add(new Label
            {
                Text = level.ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Yellow, Font = new Font("Consolas", 11, FontStyle.Bold), Margin = Padding.Empty
            });
        foreach (var progress in Enum.GetValues<DungeonProgress>())
            for (int level = 1; level <= 9; level++)
                AddDungeonTile(dungeonLayout, level, progress);

        var questHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false
        };
        questLabel = new Label
        {
            AutoSize = true, ForeColor = Color.White, Font = new Font("Consolas", 12, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 0)
        };
        questToggle = new Button
        {
            Size = new Size(40, 24), Margin = new Padding(8, 0, 0, 0),
            Padding = Padding.Empty, BackColor = Color.Black, ForeColor = Color.Yellow,
            FlatStyle = FlatStyle.Flat, Font = new Font("Consolas", 10, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        questToggle.FlatAppearance.BorderColor = Color.Yellow;
        questToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 40, 0);
        questToggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 64, 0);
        questToggle.Click += (_, _) =>
        {
            secondQuest = !secondQuest;
            UpdateDungeonRooms();
        };
        questHeader.Controls.Add(questLabel);
        questHeader.Controls.Add(questToggle);
        dungeonContent.Controls.Add(questHeader, 0, 1);
        dungeonRooms = CreateGrid(9, 3);
        foreach (RowStyle row in dungeonRooms.RowStyles)
        {
            row.SizeType = SizeType.Absolute;
            row.Height = 36;
        }
        // Keep spare panel height in an empty row rather than stretching the last icon row.
        dungeonRooms.RowCount = 4;
        dungeonRooms.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.SizeChanged += (_, _) =>
        {
            // Match the pixel-art size instead of spreading room rows over the panel height.
            int available = Math.Max(1, (right.ClientSize.Width - dungeons.Padding.Horizontal) / 9 - 4);
            int iconSize = Math.Min(80, available >= 16 ? available / 16 * 16 : available);
            foreach (RowStyle row in dungeonRooms.RowStyles)
                if (row.SizeType == SizeType.Absolute)
                    row.Height = iconSize + 4;
            dungeonContent.RowStyles[0].Height = 22 + 3 * (iconSize + 16);
            dungeonContent.RowStyles[2].Height = 3 * (iconSize + 4) + 6;
            right.RowStyles[0].Height = dungeonContent.RowStyles.Cast<RowStyle>().Sum(row => row.Height)
                + dungeons.Padding.Vertical + dungeons.Margin.Vertical;
        };
        dungeonContent.Controls.Add(dungeonRooms, 0, 2);
        UpdateDungeonRooms();

        var equipment = new RetroFrame("EQUIPMENT", Color.FromArgb(232, 184, 0)) { Margin = Padding.Empty };
        right.Controls.Add(equipment, 0, 1);
        var equipmentLayout = CreateGrid(3, 2);
        equipment.Controls.Add(equipmentLayout);
        foreach (var (name, sprite) in EquipmentItems)
            AddItem(equipmentLayout, name, sprite);

        RefreshProgress();
    }

    private void UpdateDungeonRooms()
    {
        questLabel.Text = secondQuest ? "Second Quest" : "First Quest";
        questToggle.Text = secondQuest ? "Q1" : "Q2";
        questToggle.AccessibleName = secondQuest ? "Switch to First Quest" : "Switch to Second Quest";
        questToggle.AccessibleDescription = $"{questLabel.Text} room layout selected. Changes only the room display.";
        toolTip.SetToolTip(questToggle, questToggle.AccessibleName);

        dungeonRooms.SuspendLayout();
        try
        {
            while (dungeonRooms.Controls.Count > 0)
            {
                var control = dungeonRooms.Controls[0];
                tiles.RemoveAll(x => x.Tile == control);
                toolTip.SetToolTip(control, null);
                dungeonRooms.Controls.Remove(control);
                control.Dispose();
            }

            var rooms = DungeonRoomTracker.GetRooms(secondQuest);
            bool isSecondQuest = secondQuest;
            for (int dungeon = 0; dungeon < rooms.Count; dungeon++)
            {
                int level = dungeon + 1;
                int row = 0;
                var (bossRooms, mobRooms, basements) = rooms[dungeon];
                AddRooms("Boss Room", "BossBlock", bossRooms);
                AddRooms("Mob Room", "RoomBlock", mobRooms);
                AddRooms("Basement", "Basement", basements);

                void AddRooms(string roomType, string sprite, int count)
                {
                    for (int index = 1; index <= count; index++)
                    {
                        int flag = 1 << row;
                        TrackLocation? Find() => state.Locations.FirstOrDefault(x => x.Name == $"Level {level}"
                            && x.Type.Equals("Dungeon", StringComparison.OrdinalIgnoreCase));
                        var indicator = AddTile(dungeonRooms, $"Level {level} {roomType} {index}", sprite,
                            () => Find() is { } location
                                ? ((isSecondQuest ? location.SecondQuestRoomFlags : location.FirstQuestRoomFlags) & flag) != 0
                                : null,
                            () =>
                            {
                                var location = Find() ?? throw new InvalidOperationException($"Dungeon is not in this run: Level {level}");
                                if (isSecondQuest)
                                    location.SecondQuestRoomFlags ^= flag;
                                else
                                    location.FirstQuestRoomFlags ^= flag;
                            }, () => $"{(isSecondQuest ? "Second Quest" : "First Quest")} room. Checked means looted. Manual checks and automatic major-item pickups are supported.");
                        dungeonRooms.SetCellPosition(indicator, new TableLayoutPanelCellPosition(dungeon, row++));
                    }
                }
            }
        }
        finally
        {
            dungeonRooms.ResumeLayout();
        }
        RefreshProgress();
    }

    public void SetState(TrackerState value)
    {
        state = value;
        RefreshProgress();
    }

    public void RefreshProgress()
    {
        var (name, sprite) = state.CurrentPotion switch
        {
            Potion.Blue => ("Blue Potion", "BluePotion"),
            Potion.Red => ("Red Potion", "RedPotion"),
            _ => ("Letter", "Letter")
        };
        letterPotionTile.SetContent(GetSprite(sprite), name);
        foreach (var (tile, read, notes) in tiles)
        {
            bool? value = read();
            tile.Enabled = value.HasValue;
            tile.Obtained = value == true;
            string description = !tile.Enabled ? $"{tile.AccessibleName} - not in this run"
                : $"{tile.AccessibleName} - {(tile.Obtained ? "obtained / complete" : "not obtained / incomplete")}. "
                    + (tile == letterPotionTile
                        ? "Click or press Space/Enter to cycle: uncollected letter, letter, blue potion, red potion."
                        : "Click or press Space/Enter to toggle.");
            string? note = notes?.Invoke();
            if (!string.IsNullOrWhiteSpace(note))
                description += $"\n{note}";
            tile.AccessibleDescription = description;
            toolTip.SetToolTip(tile, description);
        }
    }

    private static TableLayoutPanel CreateGrid(int columns, int rows)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty };
        for (int i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        return grid;
    }

    private ItemTile AddItem(TableLayoutPanel grid, string name, string? sprite)
    {
        TrackItem? Find() => state.Items.FirstOrDefault(x => x.Name == name);
        return AddTile(grid, name, sprite, () => Find()?.Obtained,
            () =>
            {
                var item = Find() ?? throw new InvalidOperationException($"Item is not in this run: {name}");
                item.Obtained = !item.Obtained;
            }, () => Find()?.Notes ?? "");
    }

    private enum DungeonProgress { Map, Compass, Complete }

    private void AddLetterPotion(TableLayoutPanel grid)
    {
        TrackItem? Find() => state.Items.FirstOrDefault(x => x.Name == "Letter");
        letterPotionTile = AddTile(grid, "Letter", "Letter",
            () => state.CurrentPotion != Potion.None ? true : Find()?.Obtained,
            () =>
            {
                var letter = Find() ?? throw new InvalidOperationException("Item is not in this run: Letter");
                switch (state.CurrentPotion)
                {
                    case Potion.None when !letter.Obtained: letter.Obtained = true; break;
                    case Potion.None: state.CurrentPotion = Potion.Blue; break;
                    case Potion.Blue: state.CurrentPotion = Potion.Red; break;
                    case Potion.Red:
                        state.CurrentPotion = Potion.None;
                        letter.Obtained = false;
                        break;
                }
            }, () => Find()?.Notes ?? "");
    }

    private void AddDungeonTile(TableLayoutPanel grid, int level, DungeonProgress progress)
    {
        TrackLocation? Find() => state.Locations.FirstOrDefault(x => x.Name == $"Level {level}" && x.Type.Equals("Dungeon", StringComparison.OrdinalIgnoreCase));
        string sprite = progress == DungeonProgress.Complete
            ? (level == 9 ? "Triforcelv9" : "Triforce")
            : progress.ToString();
        AddTile(grid, $"Level {level} {progress}", sprite,
            () =>
            {
                var location = Find();
                if (location == null) return null;
                return progress switch
                {
                    DungeonProgress.Map => location.HasMap,
                    DungeonProgress.Compass => location.HasCompass,
                    _ => location.Checked
                };
            },
            () =>
            {
                var location = Find() ?? throw new InvalidOperationException($"Dungeon is not in this run: Level {level}");
                switch (progress)
                {
                    case DungeonProgress.Map: location.HasMap = !location.HasMap; break;
                    case DungeonProgress.Compass: location.HasCompass = !location.HasCompass; break;
                    case DungeonProgress.Complete: location.Checked = !location.Checked; break;
                }
            });
    }

    private Image? GetSprite(string? sprite)
    {
        Image? image = null;
        if (sprite != null && !sprites.TryGetValue(sprite, out image))
        {
            string resourceName = $"HyruleProgress.Resources.ItemImage.{sprite}.png";
            using var stream = typeof(InventoryView).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing item image: {resourceName}");
            using var source = Image.FromStream(stream);
            image = new Bitmap(source);
            sprites.Add(sprite, image);
        }
        return image;
    }

    private ItemTile AddTile(TableLayoutPanel grid, string name, string? sprite, Func<bool?> read, Action toggle, Func<string>? notes = null)
    {
        var tile = new ItemTile(GetSprite(sprite), name)
        {
            Dock = DockStyle.Fill, Margin = new Padding(1), AccessibleName = name
        };
        tile.Click += (_, _) =>
        {
            toggle();
            RefreshProgress();
            ProgressChanged?.Invoke(this, EventArgs.Empty);
        };
        tiles.Add((tile, read, notes));
        grid.Controls.Add(tile);
        return tile;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            toolTip.Dispose();
            foreach (var image in sprites.Values) image.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class RetroFrame : Panel
{
    private readonly string title;
    private readonly Color borderColor;

    public RetroFrame(string title, Color borderColor)
    {
        this.title = title;
        this.borderColor = borderColor;
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        Padding = new Padding(14, title.Length == 0 ? 12 : 26, 14, 12);
        DoubleBuffered = true;
        ResizeRedraw = true;
        Font = new Font("Consolas", 14, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int inset = 5;
        int right = Width - inset - 1;
        int bottom = Height - inset - 1;
        if (right <= 20 || bottom <= 20) return;
        Point[] outline =
        [
            new(13, inset), new(right - 8, inset), new(right, 13), new(right, bottom - 8),
            new(right - 8, bottom), new(13, bottom), new(inset, bottom - 8), new(inset, 13)
        ];
        using var outer = new Pen(Color.White, 7);
        using var inner = new Pen(borderColor, 4);
        e.Graphics.DrawPolygon(outer, outline);
        e.Graphics.DrawPolygon(inner, outline);
        if (title.Length > 0)
        {
            var titleSize = TextRenderer.MeasureText(title, Font);
            e.Graphics.FillRectangle(Brushes.Black, 24, 0, titleSize.Width + 8, 24);
            TextRenderer.DrawText(e.Graphics, title, Font, new Point(28, 0), Color.White);
        }
    }
}

internal sealed class ItemTile : Control
{
    private readonly bool interactive;
    private Image? image;
    private string itemName;
    private bool obtained;

    public bool Obtained
    {
        get => obtained;
        set
        {
            if (obtained == value) return;
            obtained = value;
            Invalidate();
            if (IsHandleCreated)
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ItemTileAccessibleObject(this);

    private sealed class ItemTileAccessibleObject(ItemTile owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State
            | (owner.interactive && owner.Obtained ? AccessibleStates.Checked : AccessibleStates.None);
    }

    public ItemTile(Image? image, string itemName, bool interactive = true)
    {
        this.interactive = interactive;
        this.image = image;
        this.itemName = itemName;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, interactive);
        BackColor = Color.Black;
        ForeColor = Color.White;
        Cursor = interactive ? Cursors.Hand : Cursors.Default;
        TabStop = interactive;
        AccessibleRole = interactive ? AccessibleRole.CheckButton : AccessibleRole.Graphic;
        Font = new Font("Consolas", 10, FontStyle.Bold);
    }

    public void SetContent(Image? value, string name)
    {
        if (image == value && itemName == name) return;
        image = value;
        itemName = name;
        AccessibleName = name;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int available = Math.Max(1, Math.Min(Width - 2, Height - 2));
        // Sprites are 16x16 pixel art exported at 10x; integer scaling keeps their edges crisp.
        int side = Math.Min(80, available >= 16 ? available / 16 * 16 : available);
        var bounds = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
        if (image != null)
        {
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using var attributes = new ImageAttributes();
            if ((interactive && !Obtained) || !Enabled)
            {
                var matrix = new ColorMatrix { Matrix00 = .3f, Matrix11 = .3f, Matrix22 = .3f };
                attributes.SetColorMatrix(matrix);
            }
            e.Graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        else
        {
            TextRenderer.DrawText(e.Graphics, itemName.ToUpperInvariant(), Font, ClientRectangle,
                Obtained ? Color.White : Color.DimGray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
        if (interactive && Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(3, 3, Math.Max(0, Width - 7), Math.Max(0, Height - 7)));
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { if (interactive && e.Button == MouseButtons.Left) Focus(); base.OnMouseDown(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (interactive && (e.KeyCode is Keys.Space or Keys.Enter))
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }
}
