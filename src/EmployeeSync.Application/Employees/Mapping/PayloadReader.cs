using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>
/// Small helper that reads typed fields from a JSON object and collects validation errors keyed
/// by the JSON path the caller sent (e.g. <c>personal_info.email</c>), so the 400 response points
/// at the provider's own field names.
/// </summary>
internal sealed partial class PayloadReader
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool HasErrors => _errors.Count > 0;

    public IReadOnlyDictionary<string, string[]> Errors =>
        _errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);

    public void AddError(string path, string message)
    {
        if (!_errors.TryGetValue(path, out var list))
        {
            list = [];
            _errors[path] = list;
        }

        list.Add(message);
    }

    public bool RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        AddError(path, "Must be a JSON object.");
        return false;
    }

    /// <summary>Returns the child object, or null (with an error when required) if missing or not an object.</summary>
    public JsonElement? ReadObject(JsonElement parent, string name, string path, bool required)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                AddError(path, "Is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            AddError(path, "Must be a JSON object.");
            return null;
        }

        return value;
    }

    public string? ReadString(JsonElement? parent, string name, string path, bool required, int maxLength)
    {
        if (parent is not { } obj || !obj.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                AddError(path, "Is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            AddError(path, "Must be a string.");
            return null;
        }

        var text = value.GetString()!.Trim();
        if (text.Length == 0)
        {
            if (required)
            {
                AddError(path, "Must not be empty.");
            }

            return null;
        }

        if (text.Length > maxLength)
        {
            AddError(path, $"Must be at most {maxLength} characters.");
            return null;
        }

        return text;
    }

    public string? ReadEmail(JsonElement? parent, string name, string path, bool required)
    {
        var value = ReadString(parent, name, path, required, maxLength: 254);
        if (value is null)
        {
            return null;
        }

        if (!EmailRegex().IsMatch(value))
        {
            AddError(path, "Must be a valid email address.");
            return null;
        }

        return value.ToLowerInvariant();
    }

    public DateOnly? ReadDate(JsonElement? parent, string name, string path)
    {
        var value = ReadString(parent, name, path, required: false, maxLength: 10);
        if (value is null)
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            AddError(path, "Must be a date in yyyy-MM-dd format.");
            return null;
        }

        return date;
    }

    /// <summary>Reads an enum-like string and translates it with <paramref name="allowed"/> (case-insensitive).</summary>
    public T? ReadEnum<T>(JsonElement? parent, string name, string path, IReadOnlyDictionary<string, T> allowed)
        where T : struct
    {
        var value = ReadString(parent, name, path, required: false, maxLength: 50);
        if (value is null)
        {
            return null;
        }

        if (allowed.TryGetValue(value.ToLowerInvariant(), out var mapped))
        {
            return mapped;
        }

        AddError(path, $"Must be one of: {string.Join(", ", allowed.Keys)}.");
        return null;
    }

    // Pragmatic check (something@something.tld); full RFC 5322 validation is not worth it here.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
