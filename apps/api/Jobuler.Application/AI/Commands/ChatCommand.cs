using MediatR;

namespace Jobuler.Application.AI.Commands;

public record ChatCommand(
    string Message,
    string Locale,
    string? UserDisplayName,
    string? CurrentPath,
    bool IsAuthenticated,
    bool IsAdminMode,
    IReadOnlyList<AiChatMessageDto> RecentMessages) : IRequest<AiChatResponseDto>;

public class ChatCommandHandler : IRequestHandler<ChatCommand, AiChatResponseDto>
{
    private readonly IAiAssistant _ai;

    public ChatCommandHandler(IAiAssistant ai) => _ai = ai;

    public Task<AiChatResponseDto> Handle(ChatCommand request, CancellationToken ct)
    {
        var safeMessage = request.Message.Trim();
        if (safeMessage.Length == 0)
        {
            return Task.FromResult(new AiChatResponseDto("", []));
        }

        if (safeMessage.Length > 2000)
        {
            safeMessage = safeMessage[..2000];
        }

        var recentMessages = request.RecentMessages
            .Where(m => (m.Role is "user" or "assistant") && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(12)
            .Select(m =>
            {
                var content = m.Content.Trim();
                return new AiChatMessageDto(m.Role, content.Length > 2000 ? content[..2000] : content);
            })
            .ToList();

        return _ai.ChatAsync(new AiChatRequestDto(
            safeMessage,
            request.Locale,
            request.UserDisplayName,
            request.CurrentPath,
            request.IsAuthenticated,
            request.IsAdminMode,
            recentMessages), ct);
    }
}
