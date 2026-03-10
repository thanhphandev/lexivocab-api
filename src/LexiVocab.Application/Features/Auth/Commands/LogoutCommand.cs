using LexiVocab.Application.Common;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IDistributedCache _cache;

    public LogoutCommandHandler(IDistributedCache cache) => _cache = cache;

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
            await _cache.RemoveAsync($"rf_token:{request.RefreshToken}", ct);
            
        return Result.Success();
    }
}
