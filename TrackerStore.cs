using System.Text.Json;

namespace HyruleProgress;

internal static class TrackerStore
{
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    public static TrackerState Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            return TrackerState.CreateDefault();
        }
        catch (DirectoryNotFoundException)
        {
            return TrackerState.CreateDefault();
        }

        var state = JsonSerializer.Deserialize<TrackerState>(json)
            ?? throw new JsonException("The saved run is null.");
        if (state.Items == null || state.Items.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name))
            || state.Locations == null || state.Locations.Any(x => x == null
                || string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Type)
                || x.FirstQuestRoomFlags is < 0 or > 7 || x.SecondQuestRoomFlags is < 0 or > 7)
            || !Enum.IsDefined(state.CurrentPotion) || state.RunElapsedTicks < 0
            || state.HeartUpgradeFlags < 0 || state.HeartUpgradeFlags >= 1 << (TrackerState.HeartContainerCount - 1))
            throw new JsonException("The saved run contains invalid inventory, location, or timer data.");

        var defaults = TrackerState.CreateDefault();
        foreach (var item in defaults.Items)
            if (!state.Items.Any(x => x.Name == item.Name))
                state.Items.Add(item);
        foreach (var location in defaults.Locations)
            if (!state.Locations.Any(x => x.Name == location.Name
                && x.Type.Equals(location.Type, StringComparison.OrdinalIgnoreCase)))
                state.Locations.Add(location);
        return state;
    }

    public static void Save(string path, TrackerState state)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporaryPath = fullPath + ".tmp";
        // Finish writing before replacing the last complete save.
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, state, SaveOptions);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
