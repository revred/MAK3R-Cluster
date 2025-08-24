using System.Security.Cryptography;
using System.Text;

namespace MAK3R.Core;

/// <summary>
/// ULID (Universally Unique Lexicographically Sortable Identifier) generator
/// Used in DigitalTwin2 for time-ordered, distributed identifiers
/// </summary>
public static class UlidGenerator
{
    private static readonly char[] Base32Chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();
    private static readonly Random _random = new();

    /// <summary>
    /// Generate a new ULID with current timestamp
    /// </summary>
    public static string NewId()
    {
        return NewId(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Generate a new ULID with specific timestamp
    /// </summary>
    public static string NewId(DateTimeOffset timestamp)
    {
        var timestampMs = timestamp.ToUnixTimeMilliseconds();
        var randomBytes = new byte[10];
        _random.NextBytes(randomBytes);

        return EncodeUlid(timestampMs, randomBytes);
    }

    /// <summary>
    /// Extract timestamp from ULID
    /// </summary>
    public static DateTimeOffset GetTimestamp(string ulid)
    {
        Guard.ValidUlid(ulid);

        var timestampPart = ulid[..10]; // First 10 chars are timestamp
        var timestampMs = DecodeTimestamp(timestampPart);
        return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
    }

    /// <summary>
    /// Validate ULID format
    /// </summary>
    public static bool IsValid(string ulid)
    {
        if (string.IsNullOrEmpty(ulid) || ulid.Length != 26)
            return false;

        return ulid.All(c => Base32Chars.Contains(c));
    }

    private static string EncodeUlid(long timestampMs, byte[] randomBytes)
    {
        var result = new StringBuilder(26);

        // Encode timestamp (48 bits = 10 base32 chars)
        result.Append(EncodeTimestamp(timestampMs));

        // Encode random part (80 bits = 16 base32 chars)
        result.Append(EncodeRandom(randomBytes));

        return result.ToString();
    }

    private static string EncodeTimestamp(long timestampMs)
    {
        var chars = new char[10];
        var value = timestampMs;

        for (int i = 9; i >= 0; i--)
        {
            chars[i] = Base32Chars[value % 32];
            value /= 32;
        }

        return new string(chars);
    }

    private static string EncodeRandom(byte[] randomBytes)
    {
        var chars = new char[16];
        
        // Convert 10 bytes (80 bits) to 16 base32 chars
        ulong value = 0;
        for (int i = 0; i < randomBytes.Length; i++)
        {
            value = (value << 8) | randomBytes[i];
        }

        for (int i = 15; i >= 0; i--)
        {
            chars[i] = Base32Chars[value % 32];
            value /= 32;
        }

        return new string(chars);
    }

    private static long DecodeTimestamp(string timestampPart)
    {
        long value = 0;
        foreach (char c in timestampPart)
        {
            var index = Array.IndexOf(Base32Chars, c);
            if (index == -1)
                throw new ArgumentException($"Invalid character in ULID: {c}");
            
            value = value * 32 + index;
        }
        return value;
    }
}

/// <summary>
/// DigitalTwin2 specific ID generators with semantic prefixes
/// </summary>
public static class DigitalTwinIds
{
    public static string NewFactId() => $"FACT_{UlidGenerator.NewId()}";
    public static string NewEvidenceId() => $"EVD_{UlidGenerator.NewId()}";
    public static string NewEventId() => $"EVT_{UlidGenerator.NewId()}";
    public static string NewQuestionId() => $"Q_{UlidGenerator.NewId()}";
    public static string NewInsightId() => $"INS_{UlidGenerator.NewId()}";
    public static string NewAnomalyId() => $"ANO_{UlidGenerator.NewId()}";
    public static string NewSessionId() => $"SES_{UlidGenerator.NewId()}";

    /// <summary>
    /// Generate correlation ID for request tracking
    /// </summary>
    public static string NewCorrelationId() => UlidGenerator.NewId();

    /// <summary>
    /// Extract entity type from prefixed ID
    /// </summary>
    public static string GetEntityType(string id)
    {
        Guard.NotNullOrWhiteSpace(id);
        
        var parts = id.Split('_', 2);
        return parts.Length > 1 ? parts[0] : "UNKNOWN";
    }

    /// <summary>
    /// Extract ULID from prefixed ID
    /// </summary>
    public static string GetUlid(string id)
    {
        Guard.NotNullOrWhiteSpace(id);
        
        var parts = id.Split('_', 2);
        return parts.Length > 1 ? parts[1] : id;
    }
}