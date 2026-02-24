namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Password hashing abstraction — decouples Application layer from BCrypt implementation.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
