using System;

namespace LexiVocab.Infrastructure.Persistence;

/// <summary>
/// Utility to parse database connection strings, specifically converting
/// postgres:// URLs to Npgsql-compatible connection strings.
/// </summary>
public static class ConnectionStringParser
{
    public static string ParseDatabaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return url;

        try 
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            // Building Npgsql compatible connection string
            return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse DATABASE_URL: {url}", ex);
        }
    }
}
