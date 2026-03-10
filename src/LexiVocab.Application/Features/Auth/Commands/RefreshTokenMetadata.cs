namespace LexiVocab.Application.Features.Auth.Commands;

public record RefreshTokenMetadata(
    Guid UserId,
    string DeviceInfo,
    string IpAddress,
    DateTime CreatedAt
);
