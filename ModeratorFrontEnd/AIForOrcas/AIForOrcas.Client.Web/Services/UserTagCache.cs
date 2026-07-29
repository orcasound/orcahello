using System.Collections.Concurrent;

namespace AIForOrcas.Client.Web.Services;

public class UserTagCache
{
    private readonly ConcurrentDictionary<string, List<string>> _cache = new();

    public List<string> GetTags(string userId)
    {
        return _cache.TryGetValue(userId, out var tags)
            ? tags.ToList()
            : new List<string>();
    }

    public void SetTags(string userId, List<string> tags)
    {
        _cache[userId] = tags.ToList();
    }
}
