namespace LexiVocab.Application.Common.Helpers;

public record RefreshTokenMetadata(
    Guid UserId,
    string DeviceInfo,
    string IpAddress,
    DateTime CreatedAt
);
