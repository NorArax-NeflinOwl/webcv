namespace CvAspNet.Web;

/// <summary>
/// Response headers that limit what a page may load and who may frame it. The gateway adds HSTS, nosniff and
/// Referrer-Policy for the public site; these come from the application so that they hold behind any proxy.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// The pages load their own scripts and styles only (no inline script, style element or style attribute),
    /// Google Fonts' stylesheet and fonts, images from the site and data: URLs (the favicon), and talk only to
    /// their own origin: REST and the poker WebSocket (ws:/wss: on the same host).
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XFrameOptions = "DENY";  // frame-ancestors for browsers without CSP 2
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return next(context);
    });
}
