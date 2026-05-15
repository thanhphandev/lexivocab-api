namespace LexiVocab.Domain.Enums;

/// <summary>
/// Centralized catalog of all error codes in the LexiVocab system.
/// Each code follows the pattern: {DOMAIN}_{ERROR_TYPE}_{SPECIFIC_CONDITION}
/// </summary>
public enum ErrorCode
{
    // ─── Generic Errors ───────────────────────────────────────
    UNKNOWN_ERROR,
    VALIDATION_FAILED,
    INTERNAL_SERVER_ERROR,
    SERVICE_UNAVAILABLE,
    
    // ─── Authentication Errors (AUTH_*) ──────────────────────
    AUTH_INVALID_CREDENTIALS,
    AUTH_EMAIL_ALREADY_EXISTS,
    AUTH_EMAIL_NOT_VERIFIED,
    AUTH_ACCOUNT_LOCKED,
    AUTH_ACCOUNT_DISABLED,
    AUTH_SOCIAL_LOGIN_CONFLICT,
    AUTH_INVALID_TOKEN,
    AUTH_TOKEN_EXPIRED,
    AUTH_REFRESH_TOKEN_INVALID,
    AUTH_GOOGLE_TOKEN_INVALID,
    AUTH_PASSWORD_TOO_WEAK,
    AUTH_VERIFICATION_CODE_INVALID,
    AUTH_VERIFICATION_CODE_EXPIRED,
    AUTH_SESSION_EXPIRED,
    
    // ─── Authorization Errors (AUTHZ_*) ──────────────────────
    AUTHZ_INSUFFICIENT_PERMISSIONS,
    AUTHZ_RESOURCE_FORBIDDEN,
    AUTHZ_ADMIN_ONLY,
    
    // ─── Vocabulary Errors (VOCAB_*) ─────────────────────────
    VOCAB_NOT_FOUND,
    VOCAB_ALREADY_EXISTS,
    VOCAB_QUOTA_EXCEEDED,
    VOCAB_INVALID_IMPORT_FORMAT,
    VOCAB_BATCH_IMPORT_FAILED,
    VOCAB_EXPORT_FAILED,
    
    // ─── Tag Errors (TAG_*) ──────────────────────────────────
    TAG_NOT_FOUND,
    TAG_NAME_ALREADY_EXISTS,
    TAG_CANNOT_DELETE_WITH_WORDS,
    
    // ─── Review Errors (REVIEW_*) ────────────────────────────
    REVIEW_SESSION_NOT_FOUND,
    REVIEW_INVALID_QUALITY_SCORE,
    REVIEW_NO_CARDS_DUE,
    REVIEW_DUPLICATE_SUBMISSION,
    
    // ─── Payment Errors (PAYMENT_*) ──────────────────────────
    PAYMENT_DECLINED,
    PAYMENT_INVALID_COUPON,
    PAYMENT_COUPON_EXPIRED,
    PAYMENT_ORDER_NOT_FOUND,
    PAYMENT_ORDER_EXPIRED,
    PAYMENT_PROVIDER_ERROR,
    PAYMENT_ALREADY_PROCESSED,
    
    // ─── Subscription Errors (SUB_*) ─────────────────────────
    SUB_NOT_FOUND,
    SUB_ALREADY_ACTIVE,
    SUB_EXPIRED,
    SUB_CANCELLED,
    SUB_PLAN_NOT_FOUND,
    
    // ─── AI Service Errors (AI_*) ────────────────────────────
    AI_QUOTA_EXCEEDED,
    AI_SERVICE_UNAVAILABLE,
    AI_INVALID_REQUEST,
    AI_PROVIDER_ERROR,
    AI_MODEL_NOT_AVAILABLE,
    
    // ─── Rate Limiting Errors (RATE_*) ───────────────────────
    RATE_LIMIT_EXCEEDED,
    
    // ─── Resource Errors (RESOURCE_*) ────────────────────────
    RESOURCE_NOT_FOUND,
    RESOURCE_CONFLICT,
}
