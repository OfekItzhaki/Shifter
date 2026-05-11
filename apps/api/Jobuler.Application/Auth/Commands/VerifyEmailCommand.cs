using MediatR;

namespace Jobuler.Application.Auth.Commands;

public record VerifyEmailCommand(string Token) : IRequest;
