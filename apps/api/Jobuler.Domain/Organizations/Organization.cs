using Jobuler.Domain.Common;

namespace Jobuler.Domain.Organizations;

public class Organization : AuditableEntity
{
    public string DisplayName { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public Guid PrimaryOwnerUserId { get; private set; }
    public string? CountryCode { get; private set; }
    public string? SetupTemplate { get; private set; }
    public string? DefaultLocale { get; private set; }
    public string? DefaultTimezoneId { get; private set; }
    public OrganizationStatus Status { get; private set; } = OrganizationStatus.Active;
    public DateTime? RelocatedAt { get; private set; }
    public DateTime? DisabledAt { get; private set; }
    public DateTime? PurgeEligibleAt { get; private set; }
    public string? DedicatedDeploymentKey { get; private set; }

    private Organization() { }

    public static Organization Create(
        string displayName,
        Guid primaryOwnerUserId,
        string? countryCode,
        string? setupTemplate,
        string? defaultLocale,
        string? defaultTimezoneId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new Organization
        {
            DisplayName = displayName.Trim(),
            NormalizedName = NormalizeName(displayName),
            PrimaryOwnerUserId = primaryOwnerUserId,
            CountryCode = NormalizeOptionalCode(countryCode),
            SetupTemplate = NormalizeOptionalSlug(setupTemplate),
            DefaultLocale = NormalizeOptionalSlug(defaultLocale),
            DefaultTimezoneId = defaultTimezoneId?.Trim()
        };
    }

    public void UpdateIdentity(
        string displayName,
        string? countryCode,
        string? setupTemplate,
        string? defaultLocale,
        string? defaultTimezoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName.Trim();
        NormalizedName = NormalizeName(displayName);
        CountryCode = NormalizeOptionalCode(countryCode);
        SetupTemplate = NormalizeOptionalSlug(setupTemplate);
        DefaultLocale = NormalizeOptionalSlug(defaultLocale);
        DefaultTimezoneId = defaultTimezoneId?.Trim();
        Touch();
    }

    public void TransferPrimaryOwnership(Guid newOwnerUserId)
    {
        PrimaryOwnerUserId = newOwnerUserId;
        Touch();
    }

    public void MarkRelocated(string dedicatedDeploymentKey, DateTime relocatedAt, int disabledRetentionDays = 90)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dedicatedDeploymentKey);
        if (disabledRetentionDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(disabledRetentionDays), "Retention must be positive.");

        Status = OrganizationStatus.RelocatedDisabled;
        DedicatedDeploymentKey = dedicatedDeploymentKey.Trim();
        RelocatedAt = relocatedAt;
        DisabledAt = relocatedAt;
        PurgeEligibleAt = relocatedAt.AddDays(disabledRetentionDays);
        Touch();
    }

    public void MarkPurgePending(DateTime now)
    {
        if (Status != OrganizationStatus.RelocatedDisabled)
            throw new InvalidOperationException("Only relocated organizations can be marked purge pending.");

        if (!PurgeEligibleAt.HasValue || PurgeEligibleAt.Value > now)
            throw new InvalidOperationException("Organization is not eligible for purge yet.");

        Status = OrganizationStatus.PurgePending;
        Touch();
    }

    public void RestoreAfterRelocationReview()
    {
        Status = OrganizationStatus.Active;
        RelocatedAt = null;
        DisabledAt = null;
        PurgeEligibleAt = null;
        DedicatedDeploymentKey = null;
        Touch();
    }

    public bool IsAccessEnabled => Status == OrganizationStatus.Active;

    public static string BuildDefaultName(string? countryCode, string? setupTemplate, string? fallbackName)
    {
        var countryPart = string.IsNullOrWhiteSpace(countryCode)
            ? "Global"
            : countryCode.Trim().ToUpperInvariant();
        var templatePart = string.IsNullOrWhiteSpace(setupTemplate)
            ? "General"
            : setupTemplate.Trim().Replace('_', ' ').Replace('-', ' ');

        var generated = $"{countryPart} {ToTitleCase(templatePart)}";
        return string.IsNullOrWhiteSpace(generated) ? fallbackName?.Trim() ?? "Default Organization" : generated;
    }

    private static string NormalizeName(string name) =>
        name.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalSlug(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string ToTitleCase(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts.Select(part =>
            part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }
}
