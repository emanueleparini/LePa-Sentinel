using System.Globalization;

namespace LePaSentinel;

/// <summary>
/// UI strings: Italian when Windows is in Italian, English otherwise.
/// The LEPA_SENTINEL_LANG environment variable ("it" / "en") overrides the choice.
/// </summary>
public static class L
{
    static readonly bool It =
        (System.Environment.GetEnvironmentVariable("LEPA_SENTINEL_LANG") is { Length: >= 2 } lang
            ? lang[..2]
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName).ToLowerInvariant() == "it";
    static string T(string it, string en) => It ? it : en;

    // Panel
    public static string Title => T("Lavorazione Agenti", "Agents at Work");
    public static string HeaderTip => T("Trascina per spostare (il pannello resta fissato sullo schermo)",
                                        "Drag to move (the panel stays pinned on screen)");
    public static string CountWaitingTip => T("In attesa di te", "Waiting for you");
    public static string CountWorkingTip => T("In lavorazione", "Working");
    public static string CountDoneTip => T("Completati", "Done");
    public static string CountIdleTip => T("Inattivi", "Idle");
    public static string NoSessions => T("nessuna sessione", "no sessions");
    public static string CloseTip => T("Chiudi (resta nella tray)", "Close (stays in the tray)");
    public static string Empty => T("Nessun task in corso. Scrivi un prompt in Claude Code e comparirà qui.",
                                    "No tasks running. Send a prompt in Claude Code and it will show up here.");
    public static string NotConnected => T("LePa Sentinel non è collegato a Claude Code: clic destro sull'icona nella tray → \"Collegato a Claude Code\".",
                                           "LePa Sentinel is not connected to Claude Code: right-click the tray icon → \"Connected to Claude Code\".");
    public static string MenuGoTo => T("Vai alla finestra", "Go to window");
    public static string MenuOpenFolder => T("Apri cartella", "Open folder");
    public static string MenuRemove => T("Rimuovi dalla lista", "Remove from list");
    public static string SoundOnTip => T("Audio attivo — clic per disattivare", "Sound on — click to mute");
    public static string SoundOffTip => T("Audio disattivato — clic per attivare", "Sound off — click to unmute");
    public static string PinnedTip => T("Fissato sullo schermo — clic per tornare al pannello della tray",
                                        "Pinned on screen — click to go back to the tray panel");
    public static string UnpinnedTip => T("Fissa sullo schermo (resta sempre visibile)", "Pin on screen (always visible)");

    // Cards
    public static string WaitingForInput => T("In attesa di input", "Waiting for input");
    public static string DoneAt(string time) => T($"fatto {time}", $"done {time}");
    public static string IdleFor(string span) => T($"inattivo {span}", $"idle {span}");
    public static string Stalled => T(" · fermo?", " · stalled?");
    public static string Duration(string span) => T($"Durata: {span}", $"Duration: {span}");
    public static string CardHint => T("Clic: vai alla finestra · tasto destro: altre azioni",
                                       "Click: go to window · right-click: more actions");
    public static string Unknown => T("(sconosciuto)", "(unknown)");
    public static string SummaryWaiting(int n) => T($"{n} in attesa", $"{n} waiting");
    public static string SummaryWorking(int n) => T($"{n} al lavoro", $"{n} working");
    public static string SummaryDone(int n) => T($"{n} fatti", $"{n} done");

    // Tray
    public static string TrayOpen => T("Apri pannello", "Open panel");
    public static string TrayPin => T("Fissa sullo schermo", "Pin on screen");
    public static string TraySound => T("Audio", "Sound");
    public static string TrayTestSound => T("Prova suoni", "Test sounds");
    public static string TrayAutostart => T("Avvia con Windows", "Start with Windows");
    public static string TrayConnected => T("Collegato a Claude Code", "Connected to Claude Code");
    public static string TrayClear => T("Rimuovi sessioni concluse", "Clear finished sessions");
    public static string TrayAbout => T("Informazioni e codice sorgente", "About and source code");
    public static string TrayExit => T("Esci", "Exit");

    // Hook messages (written into the session files)
    public static string PermissionNeeded(string tool) => T($"Permesso richiesto: {tool}", $"Permission needed: {tool}");
    public static string NeedsAttention => T("Richiede la tua attenzione", "Needs your attention");
    public static string WaitingForYourInput => T("In attesa del tuo input", "Waiting for your input");
    public static string Pasted => T("[testo incollato]", "[pasted text]");

    // Connecting to Claude Code
    public static string ConsentText(string settingsPath) => T(
        "LePa Sentinel mostra lo stato delle tue sessioni di Claude Code.\n\n" +
        "Per ricevere gli eventi deve aggiungere alcuni hooks al file di configurazione di Claude Code:\n" +
        $"{settingsPath}\n\n" +
        "Prima viene salvata una copia di backup e puoi scollegarlo quando vuoi dal menu dell'icona nella tray. " +
        "Nessun dato lascia il tuo computer.\n\nCollegare ora LePa Sentinel a Claude Code?",
        "LePa Sentinel shows the status of your Claude Code sessions.\n\n" +
        "To receive events it needs to add a few hooks to Claude Code's settings file:\n" +
        $"{settingsPath}\n\n" +
        "A backup copy is saved first, and you can disconnect it at any time from the tray icon menu. " +
        "No data ever leaves your computer.\n\nConnect LePa Sentinel to Claude Code now?");
    public static string ConnectedBalloon => T("Collegato. Le sessioni di Claude Code avviate da ora compariranno qui.",
                                               "Connected. Claude Code sessions started from now on will show up here.");
    public static string HookError(string path, string error) => T(
        $"Impossibile aggiornare {path}:\n{error}", $"Could not update {path}:\n{error}");
    public static string About => T(
        $"LePa Sentinel {AppInfo.Version}\nMonitor nella tray per le sessioni di Claude Code.\n\n" +
        "Software open source (licenza MIT) di LePa s.r.l.\n" +
        "Non affiliato né approvato da Anthropic. Claude e Claude Code sono marchi di Anthropic, PBC.\n\n" +
        "Aprire la pagina del progetto?",
        $"LePa Sentinel {AppInfo.Version}\nTray monitor for Claude Code sessions.\n\n" +
        "Open-source software (MIT license) by LePa s.r.l.\n" +
        "Not affiliated with or endorsed by Anthropic. Claude and Claude Code are trademarks of Anthropic, PBC.\n\n" +
        "Open the project page?");
}
