using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LePaSentinel;

/// <summary>
/// Handles one Claude Code hook event (JSON on stdin) and updates the session's state file.
/// Never throws and never writes to stdout, so it cannot interfere with Claude Code.
/// </summary>
public static class HookHandler
{
    public static void Run()
    {
        try
        {
            string input;
            using (var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
                input = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(input)) return;

            using var doc = JsonDocument.Parse(input);
            Apply(doc.RootElement);
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("LEPA_SENTINEL_DEBUG") == "1")
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "lepa-sentinel-hook.log"), ex + Environment.NewLine);
        }
    }

    static void Apply(JsonElement r)
    {
        var id = Str(r, "session_id");
        if (id.Length == 0) return;

        var now = DateTime.UtcNow;
        var existing = Store.Load(id);
        SessionState state;

        switch (Str(r, "hook_event_name"))
        {
            case "SessionEnd":
                Store.Delete(id);
                return;

            case "SessionStart":
                state = existing ?? new SessionState { SessionId = id, Status = "idle", StartedAt = now };
                if (existing != null && Str(r, "source") == "clear")
                {
                    state.Status = "idle";
                    state.Prompt = null;
                    state.Message = null;
                    state.FinishedAt = null;
                }
                break;

            case "UserPromptSubmit":
                state = existing ?? new SessionState { SessionId = id };
                state.Status = "working";
                state.Prompt = Clean(Str(r, "prompt"));
                state.Message = null;
                state.StartedAt = now;
                state.FinishedAt = null;
                break;

            case "Notification":
                if (existing == null) return;
                state = existing;
                var type = Str(r, "notification_type");
                var message = Str(r, "message");
                if (type == "auth_success") return;
                bool needsUser = type is "permission_prompt" or "elicitation_dialog"
                    || (type.Length == 0 && message.Contains("permission", StringComparison.OrdinalIgnoreCase));
                // The idle reminder arrives after Stop too: only surface it while a task is running.
                if (!needsUser && state.Status != "working") return;
                state.Status = "waiting";
                state.Message = needsUser ? Translate(message) : L.WaitingForYourInput;
                break;

            case "PostToolUse":
                // A tool ran, so a pending permission was granted and Claude is working again.
                if (existing == null || existing.Status != "waiting") return;
                state = existing;
                state.Status = "working";
                state.Message = null;
                break;

            case "Stop":
                if (existing == null) return;
                state = existing;
                state.Status = "done";
                state.Message = null;
                state.FinishedAt = now;
                break;

            default:
                return;
        }

        // Sessions can be resumed by a new process: refresh the link when a session (re)starts or gets a prompt.
        if (state.ClaudePid == null || Str(r, "hook_event_name") is "SessionStart" or "UserPromptSubmit")
        {
            if (ClaudeProcess.FindAncestor() is { } claude)
            {
                state.ClaudePid = claude.Pid;
                state.ClaudeStartedAt = claude.StartedAt;
            }
        }

        var cwd = Str(r, "cwd");
        if (cwd.Length > 0) state.Cwd = cwd;
        var transcript = Str(r, "transcript_path");
        if (transcript.Length > 0) state.TranscriptPath = transcript;
        state.UpdatedAt = now;
        Store.Save(state);
    }

    static string Str(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static string Clean(string prompt)
    {
        // Pasted blocks arrive wrapped in tags: show a short marker instead of the raw markup.
        var text = Regex.Replace(prompt, @"<pasted_content[^>]*>.*?</pasted_content>", $" {L.Pasted} ",
            RegexOptions.Singleline);
        text = Regex.Replace(text, @"</?[a-zA-Z_][\w-]*(\s[^>]*)?>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 300 ? text[..300] + "…" : text;
    }

    static string Translate(string message)
    {
        var m = Regex.Match(message, @"permission to use (.+)$", RegexOptions.IgnoreCase);
        if (m.Success) return L.PermissionNeeded(m.Groups[1].Value.Trim());
        return message.Length > 0 ? message : L.NeedsAttention;
    }
}
