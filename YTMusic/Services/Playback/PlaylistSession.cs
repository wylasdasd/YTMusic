using CommonHelp;

namespace YTMusic.Services.Playback;

/// <summary>
/// Owns playlist items, shuffle order, and next/previous index math.
/// </summary>
public sealed class PlaylistSession
{
    public List<PlayingItem> Items { get; private set; } = new();
    public int CurrentIndex { get; set; } = -1;
    public string? Name { get; set; }
    public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;

    private readonly List<int> _shuffleIndices = new();

    public void Clear()
    {
        Items.Clear();
        _shuffleIndices.Clear();
        CurrentIndex = -1;
        Name = null;
    }

    public void Replace(List<PlayingItem> items, int currentIndex, string? name, PlaybackMode mode)
    {
        Items = items;
        CurrentIndex = currentIndex;
        Name = name;
        Mode = mode;
        GenerateShuffleIndices();
    }

    public void GenerateShuffleIndices()
    {
        _shuffleIndices.Clear();

        var indices = Enumerable.Range(0, Items.Count).ToList();

        if (CurrentIndex >= 0 && CurrentIndex < Items.Count)
        {
            indices.RemoveAt(CurrentIndex);
            _shuffleIndices.Add(CurrentIndex);
        }

        indices.Shuffle();
        _shuffleIndices.AddRange(indices);
    }

    public PlaybackMode ToggleMode()
    {
        if (Items.Count == 0)
        {
            return Mode;
        }

        Mode = Mode switch
        {
            PlaybackMode.Sequential => PlaybackMode.Random,
            PlaybackMode.Random => PlaybackMode.SingleLoop,
            PlaybackMode.SingleLoop => PlaybackMode.Sequential,
            _ => PlaybackMode.Sequential
        };

        if (Mode == PlaybackMode.Random && Items.Count > 0)
        {
            GenerateShuffleIndices();
        }

        return Mode;
    }

    public int GetNextIndex()
    {
        if (Items.Count <= 1)
        {
            return CurrentIndex;
        }

        if (Mode == PlaybackMode.Random)
        {
            var currentShufflePosition = _shuffleIndices.IndexOf(CurrentIndex);
            if (currentShufflePosition == -1 || currentShufflePosition == _shuffleIndices.Count - 1)
            {
                var previousTrackIndex = CurrentIndex;
                GenerateShuffleIndices();

                if (_shuffleIndices.Count > 1 && _shuffleIndices[0] == previousTrackIndex)
                {
                    return _shuffleIndices[1];
                }

                return _shuffleIndices[0];
            }

            return _shuffleIndices[currentShufflePosition + 1];
        }

        return (CurrentIndex + 1) % Items.Count;
    }

    public int GetPreviousIndex()
    {
        if (Items.Count <= 1)
        {
            return CurrentIndex;
        }

        if (Mode == PlaybackMode.Random)
        {
            var currentShufflePosition = _shuffleIndices.IndexOf(CurrentIndex);
            if (currentShufflePosition > 0)
            {
                return _shuffleIndices[currentShufflePosition - 1];
            }

            return _shuffleIndices[^1];
        }

        var previousIndex = CurrentIndex - 1;
        if (previousIndex < 0)
        {
            previousIndex = Items.Count - 1;
        }

        return previousIndex;
    }

    public static bool IsMissingLocalFile(PlayingItem item)
        => !string.IsNullOrWhiteSpace(item.LocalFilePath) && !File.Exists(item.LocalFilePath);
}
