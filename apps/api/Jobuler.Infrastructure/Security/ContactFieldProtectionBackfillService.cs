using Jobuler.Application.Common;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Security;

public sealed class ContactFieldProtectionBackfillService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContactFieldProtectionBackfillService> _logger;

    public ContactFieldProtectionBackfillService(
        IServiceProvider serviceProvider,
        ILogger<ContactFieldProtectionBackfillService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contactLookup = scope.ServiceProvider.GetRequiredService<IContactLookupProtector>();

        var users = await db.Users
            .Where(u => u.EmailLookupHash == null)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var normalizedEmail = contactLookup.NormalizeEmail(user.Email);
            var normalizedPhone = string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? null
                : contactLookup.NormalizePhone(user.PhoneNumber);

            user.UpdateProfileFull(user.DisplayName, user.ProfileImageUrl, normalizedPhone, user.Birthday);
            user.UpdateContactLookupHashes(
                contactLookup.HashEmail(normalizedEmail),
                normalizedPhone is null ? null : contactLookup.HashPhone(normalizedPhone));

            db.Entry(user).Property(u => u.Email).IsModified = true;
            db.Entry(user).Property(u => u.PhoneNumber).IsModified = normalizedPhone is not null;
        }

        if (users.Count == 0)
            return;

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Backfilled contact field protection for {UserCount} users.", users.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
