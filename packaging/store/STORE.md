# Microsoft Store submission

## 1. One-time setup in Partner Center

1. **Apps and games → New product → MSIX or PWA app**, reserve the name **LePa Sentinel**.
2. **Product management → Product identity**: copy *Package/Identity/Name*, *Package/Identity/Publisher* and *Package/Properties/PublisherDisplayName* into `packaging/msix/identity.json`.
3. **Pricing and availability**: free, all markets.
4. **Properties**: category *Developer tools*; *This product accesses, collects or transmits personal information*: **No**. Privacy policy URL: the `PRIVACY.md` page on GitHub (https://github.com/emanueleparini/LePa-Sentinel/blob/main/PRIVACY.md).
5. **Age ratings**: complete the questionnaire (no user interaction, no content: suitable for everyone).

## 2. Build the package

```powershell
./build.ps1 -Msix -SkipInstaller
```

Upload `artifacts/LePa-Sentinel-<version>-x64-store.msix` in **Packages**. Store packages are unsigned: Microsoft signs them.
For every new release raise `<Version>` in `src/LePaSentinel.csproj` (the MSIX version is `<Version>.0`).

Before submitting you can run the **Windows App Certification Kit** (`appcert.exe`, Windows SDK) on the `.msix`.

## 3. Restricted capability: `runFullTrust`

Partner Center asks why the app needs it. Suggested answer:

> LePa Sentinel is a classic Win32 desktop app (WPF, .NET 8) packaged with MSIX. It needs runFullTrust because it runs as a notification-area (tray) app, is invoked by Claude Code (a command-line developer tool) as a hook process through its app execution alias, and, with the user's explicit consent, edits Claude Code's user settings file in %USERPROFILE%\.claude to register those hooks. It has no network access.

## 4. Notes for certification ("Notes for certification" field)

> LePa Sentinel monitors Claude Code sessions (Anthropic's command-line coding tool). Without Claude Code installed the app starts in the notification area and shows an empty panel; this is expected. To test: click the tray icon to open the panel; right-click it for the menu. On first launch the app asks for consent before modifying %USERPROFILE%\.claude\settings.json; answering No is fully supported. No account or login is needed.

## 5. Store listing

### English

**Product name:** LePa Sentinel

**Short description:** Tray monitor for Claude Code: see which sessions are working, waiting for you or done, and hear a chime when a task finishes.

**Description:**

LePa Sentinel keeps an eye on your Claude Code sessions so you don't have to.

If you run several Claude Code sessions at once, in terminals or VS Code, you keep switching windows just to check whether one of them finished or is stuck on a permission prompt. LePa Sentinel lives in the notification area and tells you at a glance.

• Tray icon with live status: the colour of the most urgent state and the number of active tasks
• Panel with one card per session: project, prompt, elapsed time and status
• Soft chime when a task is done, a distinct alert when Claude needs your permission or input
• Click a session to bring its VS Code or terminal window to the front
• Pin the panel on screen, always on top, or let it close when you click elsewhere
• English and Italian
• Completely local: no account, no network access, no telemetry
• Open source (MIT)

On first launch LePa Sentinel asks your permission to connect to Claude Code by adding hooks to its settings file; a backup is saved first and you can disconnect at any time.

LePa Sentinel is an independent project, not affiliated with or endorsed by Anthropic. Claude and Claude Code are trademarks of Anthropic, PBC.

**Keywords:** Claude Code, AI agent, coding assistant, monitor, tray, notification, developer tools

**Features (one per line):**
Live status of every Claude Code session in the notification area
Chime when a task is done, alert when Claude waits for you
One-click jump to the session's VS Code or terminal window
Pinnable always-on-top panel
Local only: no account, no telemetry

### Italiano

**Nome prodotto:** LePa Sentinel

**Descrizione breve:** Monitor nella tray per Claude Code: vedi quali sessioni lavorano, aspettano te o hanno finito, con un avviso sonoro a task completato.

**Descrizione:**

LePa Sentinel tiene d'occhio le tue sessioni di Claude Code al posto tuo.

Se usi più sessioni di Claude Code insieme, nei terminali o in VS Code, finisci per saltare da una finestra all'altra solo per controllare se una ha finito o è ferma su una richiesta di permesso. LePa Sentinel vive nell'area di notifica e te lo dice a colpo d'occhio.

• Icona nella tray con lo stato in tempo reale: il colore dello stato più urgente e il numero di task attivi
• Pannello con una scheda per sessione: progetto, prompt, tempo trascorso e stato
• Suono morbido quando un task è finito, avviso distinto quando Claude chiede un permesso o una risposta
• Clic su una sessione per portare in primo piano la sua finestra di VS Code o del terminale
• Pannello fissabile sullo schermo, sempre in primo piano, oppure che si chiude quando clicchi altrove
• Italiano e inglese
• Tutto in locale: nessun account, nessun accesso alla rete, nessuna telemetria
• Open source (MIT)

Al primo avvio LePa Sentinel chiede il permesso di collegarsi a Claude Code aggiungendo degli hooks al suo file di impostazioni; prima viene salvato un backup e puoi scollegarlo quando vuoi.

LePa Sentinel è un progetto indipendente, non affiliato né approvato da Anthropic. Claude e Claude Code sono marchi di Anthropic, PBC.

**Parole chiave:** Claude Code, agenti AI, assistente di programmazione, monitor, tray, notifiche, strumenti per sviluppatori

## 6. Images

- Store logo: `packaging/store/StoreListingLogo-300.png` (regenerate with `python tools/make-assets.py`).
- Screenshots (at least one, 1366×768 or larger): take them on a real desktop with the panel open. `docs/screenshot.png` is the README crop.
