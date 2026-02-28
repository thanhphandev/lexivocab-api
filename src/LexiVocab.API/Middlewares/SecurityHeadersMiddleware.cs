namespace LexiVocab.API.Middlewares;

/// <summary>
/// Applies production-grade security headers to every HTTP response.
/// Equivalent to Helmet.js in the Node.js ecosystem.
///
/// Headers applied:
///  - X-Content-Type-Options: Prevents MIME-type sniffing attacks.
///  - X-Frame-Options: Prevents clickjacking by disallowing iframe embedding.
///  - X-XSS-Protection: Legacy XSS filter for older browsers.
///  - Referrer-Policy: Controls how much referrer information is sent.
///  - Permissions-Policy: Restricts access to browser APIs (camera, mic, etc.).
///  - Content-Security-Policy: Controls resource loading origins.
///  - Cache-Control: Prevents caching of sensitive API responses.
///  - X-Permitted-Cross-Domain-Policies: Blocks Flash/PDF cross-domain requests.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // ─── Anti-MIME Sniffing ──────────────────────────────
        headers["X-Content-Type-Options"] = "nosniff";

        // ─── Anti-Clickjacking ──────────────────────────────
        headers["X-Frame-Options"] = "DENY";

        // ─── Legacy XSS Filter ──────────────────────────────
        headers["X-XSS-Protection"] = "1; mode=block";

        // ─── Referrer Policy ────────────────────────────────
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // ─── Browser Feature Restrictions ────────────────────
        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";

        var path = context.Request.Path;
        var isDocsEndpoint = path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi") || path.Value == "/";

        if (!isDocsEndpoint)
        {
            // ─── Content Security Policy (API-focused) ──────────
            // For API-only endpoints: restrict everything heavily.
            // (Note: default-src 'none' blocks all HTML/JS/CSS rendering)
            headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'";

            // ─── Prevent Caching of Sensitive Data ──────────────
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
            headers["Pragma"] = "no-cache";
        }
        else
        {
            // Relaxed CSP for API Documentation UI (Scalar needs inline scripts, eval, data URIs, and external connections)
            headers["Content-Security-Policy"] =
                "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: https: http:; connect-src 'self' ws: wss: *; frame-ancestors 'none'";
        }

        // ─── Flash/PDF Cross-Domain ─────────────────────────
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        await _next(context);
    }
}
