using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flow.Launcher.Core.WebDavSync;

internal static class WebDavSyncSettingsStamp
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void MarkAppliedNow(string settingsDirectory)
    {
        var settingsPath = Path.Combine(settingsDirectory, "Settings.json");
        if (!File.Exists(settingsPath))
        {
            return;
        }

        JsonNode root = JsonNode.Parse(File.ReadAllText(settingsPath)) ?? new JsonObject();
        if (root is not JsonObject rootObject)
        {
            return;
        }

        var webDav = rootObject["WebDavSync"] as JsonObject ?? new JsonObject();
        webDav["LastSuccessfulSyncUtc"] = DateTime.UtcNow.ToString("O");
        rootObject["WebDavSync"] = webDav;
        File.WriteAllText(settingsPath, rootObject.ToJsonString(JsonOptions));
    }
}
