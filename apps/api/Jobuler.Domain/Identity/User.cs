using Jobuler.Domain.Common;

namespace Jobuler.Domain.Identity;

public class User : AuditableEntity
{
    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public string PreferredLocale { get; private set; } = "he";
    public string? ProfileImageUrl { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? EmailLookupHash { get; private set; }
    public string? PhoneLookupHash { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public bool EmailVerified { get; private set; } = false;
    public string? CountryCode { get; private set; }  // ISO 3166-1 alpha-2
    public string? StateCode { get; private set; }    // ISO 3166-2 subdivision

    // EF Core constructor
    private User() { }

    public static User Create(string email, string displayName, string passwordHash, string locale = "he", string? phoneNumber = null, string? profileImageUrl = null, DateOnly? birthday = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Email = email.ToLowerInvariant().Trim(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            PreferredLocale = locale,
            PhoneNumber = phoneNumber?.Trim(),
            ProfileImageUrl = profileImageUrl,
            Birthday = birthday
        };
    }

    public DateOnly? Birthday { get; private set; }

    public void UpdatePhone(string? phoneNumber) { PhoneNumber = phoneNumber?.Trim(); Touch(); }

    public void UpdateContactLookupHashes(string emailLookupHash, string? phoneLookupHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailLookupHash);
        EmailLookupHash = emailLookupHash;
        PhoneLookupHash = phoneLookupHash;
        Touch();
    }

    public void SetPasswordHash(string hash) { PasswordHash = hash; Touch(); }

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;

    public void UpdateProfile(string displayName, string? profileImageUrl, string locale)
    {
        DisplayName = displayName.Trim();
        ProfileImageUrl = profileImageUrl;
        PreferredLocale = locale;
        Touch();
    }

    public void UpdateProfileFull(string displayName, string? profileImageUrl, string? phoneNumber, DateOnly? birthday)
    {
        DisplayName = displayName.Trim();
        ProfileImageUrl = profileImageUrl;
        PhoneNumber = phoneNumber?.Trim();
        Birthday = birthday;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }

    public void MarkEmailVerified()
    {
        EmailVerified = true;
        Touch();
    }

    public void UpdateLocation(string? countryCode, string? stateCode)
    {
        CountryCode = countryCode?.ToUpperInvariant().Trim();
        StateCode = stateCode?.ToUpperInvariant().Trim();
        Touch();
    }
}
