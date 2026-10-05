using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace LePaSentinel;

public sealed class SessionState
{
    public string SessionId { get; set; } = "";
    public string Cwd { get; set; } = "";
    /// <summary>working | waiting | done | idle</summary>
    public string Status { get; set; } = "idle";
    public string? Prompt { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? TranscriptPath { get; set; }
    /// <summary>The claude.exe process running this session (null if it could not be found).</summary>
    public int? ClaudePid { get; set; }
    public DateTime? ClaudeStartedAt { get; set; }
}

/// <summary>One JSON file per Claude Code session, shared between hook processes and the GUI.</summary>
public static class Store
{
    public static readonly string Dir = Path.Combine(AppInfo.ClaudeDir, "lepa-sentinel");

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    static string PathFor(string id)
    {
        var safe = new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return Path.Combine(Dir, (safe.Length == 0 ? "unknown" : safe) + ".json");
    }

    public static SessionState? Load(string id) => Read(PathFor(id));

    static SessionState? Read(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(path), Json);
            }
            catch (IOException) { Thread.Sleep(15); }
            catch (UnauthorizedAccessException) { Thread.Sleep(15); }
            catch (JsonException) { return null; }
        }
        return null;
    }

    public static void Save(SessionState state)
    {
        Directory.CreateDirectory(Dir);
        var path = PathFor(state.SessionId);
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json), new UTF8Encoding(false));
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { File.Move(tmp, path, true); return; }
            catch (IOException) { Thread.Sleep(20); }
            catch (UnauthorizedAccessException) { Thread.Sleep(20); }
        }
        try { File.Delete(tmp); } catch { }
    }

    public static void Delete(string id)
    {
        try { File.Delete(PathFor(id)); } catch { }
    }

    public static List<SessionState> LoadAll()
    {
        var list = new List<SessionState>();
        if (!Directory.Exists(Dir)) return list;
        foreach (var file in Directory.EnumerateFiles(Dir, "*.json"))
        {
            var state = Read(file);
            if (state != null && state.SessionId.Length > 0) list.Add(state);
        }
        return list;
    }

    /// <summary>Removes temp files left behind by a hook process that was killed mid-write.</summary>
    public static void CleanOrphans()
    {
        if (!Directory.Exists(Dir)) return;
        foreach (var file in Directory.EnumerateFiles(Dir, "*.tmp"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromMinutes(1))
                    File.Delete(file);
            }
            catch { }
        }
    }
}
