namespace HyruleProgress;

internal enum DungeonRoomType { Boss, Mob, Basement }

internal sealed record DungeonRoomPickup(string Id, int Level, int Quest, DungeonRoomType RoomType, int RoomIndex);

internal static class DungeonRoomTracker
{
    private static readonly (int BossRooms, int MobRooms, int Basements)[] FirstQuestRooms =
    [
        (1, 1, 1), (1, 1, 0), (1, 0, 1),
        (1, 0, 1), (1, 0, 1), (1, 0, 1),
        (1, 0, 1), (1, 0, 2), (0, 0, 2)
    ];

    private static readonly (int BossRooms, int MobRooms, int Basements)[] SecondQuestRooms =
    [
        (1, 1, 0), (1, 0, 1), (1, 1, 0),
        (1, 0, 2), (1, 0, 1), (1, 0, 1),
        (1, 0, 1), (1, 0, 2), (0, 0, 2)
    ];

    public static IReadOnlyList<(int BossRooms, int MobRooms, int Basements)> GetRooms(bool secondQuest) =>
        secondQuest ? SecondQuestRooms : FirstQuestRooms;

    public static bool IsValid(DungeonRoomPickup pickup) =>
        !string.IsNullOrWhiteSpace(pickup.Id) && pickup.Level is >= 1 and <= 9
        && pickup.Quest is 0 or 1 && Enum.IsDefined(pickup.RoomType)
        && (pickup.RoomIndex is >= 1 and <= 2
            || pickup.RoomType == DungeonRoomType.Basement && pickup.RoomIndex == 0);

    public static bool Apply(TrackerState state, IEnumerable<DungeonRoomPickup> pickups, bool baselineOnly = false)
    {
        bool changed = false;
        foreach (var pickup in pickups)
        {
            if (!IsValid(pickup))
                throw new ArgumentException("Invalid dungeon room pickup.", nameof(pickups));
            if (!state.ProcessedRoomPickupIds.Add(pickup.Id)) continue;
            changed = true;
            if (baselineOnly) continue;

            var (bossRooms, mobRooms, basements) = GetRooms(pickup.Quest == 1)[pickup.Level - 1];
            var (offset, count) = pickup.RoomType switch
            {
                DungeonRoomType.Boss => (0, bossRooms),
                DungeonRoomType.Mob => (bossRooms, mobRooms),
                _ => (bossRooms + mobRooms, basements)
            };
            int roomIndex = pickup.RoomType == DungeonRoomType.Basement && pickup.RoomIndex == 0 && count == 1
                ? 1 : pickup.RoomIndex;
            if (roomIndex < 1 || roomIndex > count)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"Pickup {pickup.Id}: Level {pickup.Level}, quest {pickup.Quest + 1}, {pickup.RoomType} {pickup.RoomIndex} is not in the supported layout; use a manual room check.");
                continue;
            }
            var location = state.Locations.FirstOrDefault(x => x.Name == $"Level {pickup.Level}"
                && x.Type.Equals("Dungeon", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Dungeon is not in this run: Level {pickup.Level}");
            int flag = 1 << (offset + roomIndex - 1);
            if (pickup.Quest == 1)
                location.SecondQuestRoomFlags |= flag;
            else
                location.FirstQuestRoomFlags |= flag;
        }
        return changed;
    }
}
