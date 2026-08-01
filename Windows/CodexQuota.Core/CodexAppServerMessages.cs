using System.Text.Json;

namespace CodexQuota.Core;

public static class CodexAppServerMessages
{
    public static string Initialize(int id, string clientName, string title, string version) =>
        JsonSerializer.Serialize(new
        {
            method = "initialize",
            id,
            @params = new
            {
                clientInfo = new { name = clientName, title, version }
            }
        });

    public static string Initialized() =>
        JsonSerializer.Serialize(new { method = "initialized", @params = new { } });

    public static string AccountRead(int id) =>
        JsonSerializer.Serialize(new
        {
            method = "account/read",
            id,
            @params = new { refreshToken = false }
        });

    public static string RateLimitsRead(int id) =>
        JsonSerializer.Serialize(new { method = "account/rateLimits/read", id });
}
