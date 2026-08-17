using System.Collections.Concurrent;

namespace AIForOrcas.Client.Web.Services;

public class UserTagCache
{
    private readonly ConcurrentDictionary<string, List<string>> _cache = new();

    public List<string> GetTags(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<string>();
        }

        return _cache.TryGetValue(userId, out var tags)
            ? tags.ToList()
            : new List<string>();
    }

    public void SetTags(string userId, List<string> tags)
    {
        if (string.IsNullOrWhiteSpace(userId) || tags == null)
        {
            return;
        }

        _cache[userId] = tags.ToList();
    }
}
