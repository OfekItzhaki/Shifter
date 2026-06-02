using Jobuler.Application.Auth.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Jobuler.Api.Controllers;

internal static class AuthCookieHelper
{
    private const string RefreshTokenCookieName = "refresh_token";

    public static void SetRefreshTokenCookie(
        HttpContext httpContext,
        IConfiguration configuration,
        IHostEnvironment environment,
        LoginResult result)
    {
        var expiryDays = int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        httpContext.Response.Cookies.Append(
            RefreshTokenCookieName,
            result.RefreshToken,
            BuildRefreshCookieOptions(httpContext, environment, TimeSpan.FromDays(expiryDays)));
    }

    public static void ClearRefreshTokenCookie(HttpContext httpContext, IHostEnvironment environment)
    {
        httpContext.Response.Cookies.Delete(
            RefreshTokenCookieName,
            BuildRefreshCookieOptions(httpContext, environment, TimeSpan.Zero));
    }

    public static string? GetRefreshToken(HttpContext httpContext, string? requestToken)
    {
        if (!string.IsNullOrWhiteSpace(requestToken))
        {
            return requestToken;
        }

        return httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken)
            ? cookieToken
            : null;
    }

    public static object ToClientLoginResult(LoginResult result) => new
    {
        result.AccessToken,
        result.AccessTokenExpiresAt,
        result.UserId,
        result.DisplayName,
        result.PreferredLocale,
        result.IsPlatformAdmin,
        result.TimezoneId,
        result.TimezoneOffsetMinutes
    };

    private static CookieOptions BuildRefreshCookieOptions(
        HttpContext httpContext,
        IHostEnvironment environment,
        TimeSpan maxAge)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/auth",
            MaxAge = maxAge
        };
    }
}
