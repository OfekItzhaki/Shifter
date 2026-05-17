using Jobuler.Domain.Common;

namespace Jobuler.Domain.Platform;

/// <summary>
/// System-level key-value settings (not tenant-scoped).
/// Used for platform-wide configuration such as super-admin session timeout.
/// </summary>
public class PlatformSettings : AuditableEntity
{
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;

    private PlatformSettings() { }

    public static PlatformSettings Create(string key, string value) =>
        new()
        {
            Key = key,
            Value = value
        };

    public void UpdateValue(string value)
    {
        Value = value;
        Touch();
    }
}
