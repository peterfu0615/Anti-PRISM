using System.Collections.Concurrent;
using AntiPrism.Core.Models;
using Serilog;

namespace AntiPrism.Core.Storage;

public class BlacklistStore
{
    private readonly ConcurrentDictionary<string, BlacklistEntry> _entriesById = new();
    private readonly ConcurrentDictionary<string, BlacklistEntry> _entriesByNormalizedPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BlacklistEntry> _entriesByFileName = new(StringComparer.OrdinalIgnoreCase);

    public void LoadEntries(IEnumerable<BlacklistEntry> entries)
    {
        _entriesById.Clear();
        _entriesByNormalizedPath.Clear();
        _entriesByFileName.Clear();

        foreach (var entry in entries)
        {
            AddOrUpdate(entry);
        }
    }

    public void AddOrUpdate(BlacklistEntry entry)
    {
        _entriesById[entry.Id] = entry;

        if (!string.IsNullOrWhiteSpace(entry.ExePath))
        {
            var normalized = Path.GetFullPath(entry.ExePath).Trim();
            _entriesByNormalizedPath[normalized] = entry;
        }

        if (!string.IsNullOrWhiteSpace(entry.FileName))
        {
            _entriesByFileName[entry.FileName.Trim()] = entry;
        }
    }

    public bool Remove(string id)
    {
        if (_entriesById.TryRemove(id, out var entry))
        {
            if (!string.IsNullOrWhiteSpace(entry.ExePath))
            {
                var normalized = Path.GetFullPath(entry.ExePath).Trim();
                _entriesByNormalizedPath.TryRemove(normalized, out _);
            }

            if (!string.IsNullOrWhiteSpace(entry.FileName))
            {
                _entriesByFileName.TryRemove(entry.FileName.Trim(), out _);
            }

            return true;
        }

        return false;
    }

    public List<BlacklistEntry> GetAll() => _entriesById.Values.ToList();

    public bool IsBlacklisted(string? fullPath, string? fileName, out BlacklistEntry? matchedEntry)
    {
        matchedEntry = null;

        // 1. Exact full path match
        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            try
            {
                var normalized = Path.GetFullPath(fullPath).Trim();
                if (_entriesByNormalizedPath.TryGetValue(normalized, out var entry) && entry.IsEnabled)
                {
                    matchedEntry = entry;
                    return true;
                }
            }
            catch { }
        }

        // 2. Filename match
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var cleanedFileName = fileName.Trim();
            if (_entriesByFileName.TryGetValue(cleanedFileName, out var entry) && entry.IsEnabled)
            {
                matchedEntry = entry;
                return true;
            }
        }

        return false;
    }
}
