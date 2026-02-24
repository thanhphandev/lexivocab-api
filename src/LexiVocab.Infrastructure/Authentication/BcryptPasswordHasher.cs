using LexiVocab.Application.Common.Interfaces;

namespace LexiVocab.Infrastructure.Authentication;

/// <summary>
/// BCrypt password hashing implementation.
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
