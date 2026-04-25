using Jobuler.Application.Common;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Development implementation — logs the invitation URL to the console instead of sending.
/// </summary>
public class NoOpInvitationSender : IInvitationSender
{
    private readonly ILogger<NoOpInvitationSender> _logger;

    public NoOpInvitationSender(ILogger<NoOpInvitationSender> logger)
    {
        _logger = logger;
    }

    public Task SendInvitationAsync(
        string contact, string channel, string inviteUrl, string personName,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOp Invitation] Channel={Channel} To={Contact} Person={PersonName} URL={InviteUrl}",
            channel, contact, personName, inviteUrl);
        return Task.CompletedTask;
    }
}
