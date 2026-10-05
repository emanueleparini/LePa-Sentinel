using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LePaSentinel;

/// <summary>
/// Adds/removes LePa Sentinel's hooks in Claude Code's user settings (~/.claude/settings.json),
/// leaving everything else in the file untouched.
/// </summary>
public static class HookInstaller
{
    static readonly string[] Events =
        { "SessionStart", "UserPromptSubmit", "Notification", "PostToolUse", "Stop", "SessionEnd" };

    public static string SettingsPath => Path.Combine(AppInfo.ClaudeDir, "settings.json");
    static string BackupPath => SettingsPath + ".bak-lepa-sentinel";

    /// <summary>Forward slashes and quotes work both in Git Bash and in cmd, which Claude Code may use to run hooks.</summary>
    public static string Command => $"\"{AppInfo.HookExePath.Replace('\\', '/')}\" --hook";

    /// <summary>Hooks written by this app, including the ones from its pre-release name (Sentinella).</summary>
    static bool IsOurs(string? command) =>
        command != null && command.Contains("--hook")
        && (command.Contains(AppInfo.ExeName, StringComparison.OrdinalIgnoreCase)
            || command.Contains("Sentinella.exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when every event has our hook with the current executable path.</summary>
    public static bool IsInstalled()
    {
        try
        {
            if (Load()?["hooks"] is not JsonObject hooks) return false;
            return Events.All(ev => hooks[ev] is JsonArray groups && groups
                .OfType<JsonObject>()
                .SelectMany(g => g["hooks"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Any(h => (string?)h["command"] == Command));
        }
        catch { return false; }
    }

    /// <summary>True when the settings contain any hook of ours, even with an outdated path.</summary>
    public static bool HasAnyHook()
    {
        try { return Load()?["hooks"] is JsonObject hooks && OurHooks(hooks).Any(); }
        catch { return false; }
    }

    public static void Install()
    {
        var root = Load() ?? new JsonObject();
        if (File.Exists(SettingsPath) && !File.Exists(BackupPath))
            File.Copy(SettingsPath, BackupPath);

        RemoveOurs(root);
        if (root["hooks"] is not JsonObject hooks)
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }
        foreach (var ev in Events)
        {
            if (hooks[ev] is not JsonArray groups)
            {
                groups = new JsonArray();
                hooks[ev] = groups;
            }
            var group = new JsonObject();
            if (ev == "PostToolUse") group["matcher"] = "*";
            group["hooks"] = new JsonArray(new JsonObject
            {
                ["type"] = "command",
                ["command"] = Command,
                ["timeout"] = 5,
            });
            groups.Add(group);
        }
        Save(root);
    }

    public static void Uninstall()
    {
        var root = Load();
        if (root == null) return;
        RemoveOurs(root);
        Save(root);
    }

    static System.Collections.Generic.IEnumerable<JsonObject> OurHooks(JsonObject hooks) =>
        hooks.Select(p => p.Value).OfType<JsonArray>()
             .SelectMany(groups => groups.OfType<JsonObject>())
             .SelectMany(g => g["hooks"] as JsonArray ?? new JsonArray())
             .OfType<JsonObject>()
             .Where(h => IsOurs((string?)h["command"]));

    static void RemoveOurs(JsonObject root)
    {
        if (root["hooks"] is not JsonObject hooks) return;
        foreach (var (eventName, value) in hooks.ToList())
        {
            if (value is not JsonArray groups) continue;
            foreach (var group in groups.OfType<JsonObject>().ToList())
            {
                if (group["hooks"] is not JsonArray inner) continue;
                foreach (var hook in inner.OfType<JsonObject>().Where(h => IsOurs((string?)h["command"])).ToList())
                    inner.Remove(hook);
                if (inner.Count == 0) groups.Remove(group);
            }
            if (groups.Count == 0) hooks.Remove(eventName);
        }
        if (hooks.Count == 0) root.Remove("hooks");
    }

    static JsonObject? Load()
    {
        if (!File.Exists(SettingsPath)) return null;
        var text = File.ReadAllText(SettingsPath);
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        return node as JsonObject ?? throw new InvalidDataException("settings.json is not a JSON object");
    }

    static void Save(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var tmp = SettingsPath + ".lepa-sentinel.tmp";
        File.WriteAllText(tmp, json + Environment.NewLine, new UTF8Encoding(false));
        File.Move(tmp, SettingsPath, true);
    }
}
