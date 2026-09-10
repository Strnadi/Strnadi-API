using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Tenant.Application.Maps;

public sealed record ClusterSnapshotEntry(Guid Token, int[] MemberRecordingIds, ClusterItem[] ItemsBeyondPreview);

public readonly record struct DecodedClusterCursor(string ClusterId, Guid Token, int Offset);

public class ClusterSnapshotStore(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private static string CacheKey(string clusterId) => $"map-cluster-snapshot:{clusterId}";

    public string? StoreAndGetFirstCursor(string clusterId, int[] memberRecordingIds, ClusterItem[] itemsBeyondPreview)
    {
        if (itemsBeyondPreview.Length == 0)
            return null;

        var token = Guid.NewGuid();
        cache.Set(CacheKey(clusterId), new ClusterSnapshotEntry(token, memberRecordingIds, itemsBeyondPreview), Ttl);
        return EncodeCursor(clusterId, token, 0);
    }

    public ClusterSnapshotEntry? TryGet(string clusterId) => cache.Get<ClusterSnapshotEntry>(CacheKey(clusterId));

    public string EncodeNextCursor(string clusterId, Guid token, int offset) => EncodeCursor(clusterId, token, offset);

    public static bool TryDecodeCursor(string cursor, out DecodedClusterCursor decoded)
    {
        decoded = default;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 3)
                return false;
            if (parts[0].Length == 0 || !Guid.TryParse(parts[1], out var token) || !int.TryParse(parts[2], out var offset) || offset < 0)
                return false;

            decoded = new DecodedClusterCursor(parts[0], token, offset);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string EncodeCursor(string clusterId, Guid token, int offset) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clusterId}|{token}|{offset}"));
}
