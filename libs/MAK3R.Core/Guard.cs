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
}