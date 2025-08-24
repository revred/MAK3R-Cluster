using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MAK3R.Core;

public static class Guard
{
    public static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    public static string NotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null, empty, or whitespace", paramName);
        return value;
    }

    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Value cannot be empty", paramName);
        return value;
    }

    public static T NotDefault<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            throw new ArgumentException($"Value cannot be default({typeof(T).Name})", paramName);
        return value;
    }

    public static int GreaterThan(int value, int minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= minimum)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than {minimum}");
        return value;
    }

    public static int GreaterThanOrEqual(int value, int minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < minimum)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than or equal to {minimum}");
        return value;
    }

    // DigitalTwin2 specific validation methods
    public static double ValidConfidence(double confidence, [CallerArgumentExpression(nameof(confidence))] string? paramName = null)
    {
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(paramName, confidence, "Confidence must be between 0.0 and 1.0");
        return confidence;
    }

    public static string ValidEntityId(string entityId, [CallerArgumentExpression(nameof(entityId))] string? paramName = null)
    {
        NotNullOrWhiteSpace(entityId, paramName);
        if (entityId.Length > 100)
            throw new ArgumentException("Entity ID cannot exceed 100 characters", paramName);
        return entityId;
    }

    public static string ValidDataRoomId(string dataRoomId, [CallerArgumentExpression(nameof(dataRoomId))] string? paramName = null)
    {
        NotNullOrWhiteSpace(dataRoomId, paramName);
        if (!dataRoomId.StartsWith("DR-") || dataRoomId.Length < 5)
            throw new ArgumentException("Data room ID must start with 'DR-' and be at least 5 characters", paramName);
        return dataRoomId;
    }

    public static double PositiveValue(double value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0.0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive");
        return value;
    }

    public static string ValidUlid(string ulid, [CallerArgumentExpression(nameof(ulid))] string? paramName = null)
    {
        NotNullOrWhiteSpace(ulid, paramName);
        if (ulid.Length != 26)
            throw new ArgumentException("ULID must be exactly 26 characters", paramName);
        return ulid;
    }
}