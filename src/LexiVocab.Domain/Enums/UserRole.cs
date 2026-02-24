namespace LexiVocab.Domain.Enums;

/// <summary>
/// User roles for authorization. Maps to VARCHAR(20) "Role" column.
/// </summary>
public enum UserRole
{
    User = 0,
    Premium = 1,
    Admin = 2
}
