namespace LexiVocab.Domain.Enums;

/// <summary>
/// Categorizes the type of audited user action.
/// Stored as VARCHAR(50) in the database for readability in direct queries.
/// </summary>
public enum AuditAction
{
    // ─── Authentication ──────────────────────────────────
    Login = 0,
    LoginFailed = 1,
    Register = 2,
    Logout = 3,
    TokenRefresh = 4,
    GoogleLogin = 5,

    // ─── Vocabulary CRUD ─────────────────────────────────
    VocabularyCreated = 10,
    VocabularyUpdated = 11,
    VocabularyDeleted = 12,
    VocabularyBulkImported = 13,
    VocabularyExported = 14,

    // ─── Reviews / SRS ───────────────────────────────────
    ReviewCompleted = 20,

    // ─── User Settings ───────────────────────────────────
    SettingsUpdated = 30,
    ProfileUpdated = 31,
    PasswordChanged = 32,

    // ─── Subscription & Payment ──────────────────────────
    SubscriptionCreated = 40,
    SubscriptionCancelled = 41,
    PaymentCompleted = 42,
    PaymentFailed = 43,

    // ─── Admin Actions ───────────────────────────────────
    AdminUserDeactivated = 50,
    AdminUserActivated = 51,
    AdminRoleChanged = 52,

    // ─── System ──────────────────────────────────────────
    RateLimited = 90,
    SuspiciousActivity = 91
}
