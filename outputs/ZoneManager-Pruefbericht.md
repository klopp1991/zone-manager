# Sascha’s Zone Manager — Prüfbericht

Stand: 31.08.2026 · Lauf: `scripts\publish-release.ps1 -SkipDpiCheck` (enthält `scripts\verify.ps1`) · Abschluss: `VERIFY_OK`

Geprüfte Version: **2026.0831.01** (Tag `v2026.0831.01`).

## Bestanden

- Release-Build für Windows 11 x64: 0 Fehler, 0 Warnungen (`TreatWarningsAsErrors` in allen Projekten aktiv).
- 329 automatisierte Tests: 329 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Selbständiger `win-x64`-Einzeldatei-Publish: 71'893'653 Bytes, unter dem Limit von 100'000'000 Bytes.
- Root-EXE-Installation: `ZoneManager.exe` im Rootverzeichnis, identisch mit dem Publish-Artefakt (Prüfung über SHA-256 im Lauf).
- `scripts\verify-root-build.ps1`: auch ein gewöhnlicher `dotnet build` erzeugt eine lauffähige, selbständige Root-EXE (`ROOT_BUILD_OK`).
- Diagnose auf dem Zielsystem: 2 Monitore erkannt, für jeden Monitor ein Startlayout, kein Hook registriert, Einstellungen und Registry unverändert.
- Mehrgrössen-Icon erzeugt: 16 bis 256 Pixel (`ICON_OK`).
- Versionsschema `YYYY.MMDD.NN`: Die EXE trägt Dateiversion `2026.831.1` und Produktversion `2026.0831.01`; `scripts\test-set-version.ps1` deckt Tagesstart, Hochzählen, Tag-Erkennung und Vorschau ab (`SET_VERSION_TEST_OK`).
- Oberflächenüberarbeitung nach `docs\ui-richtlinien.md` automatisiert abgedeckt: einheitliche Steuerelementhöhe, gleiche gemessene Höhe benachbarter Schaltflächen auf den Seiten Monitore und Regeln, Texthierarchie, Kontrast von `SubtleInkBrush` in hellem und dunklem Theme, gemeinsamer Einheitenumschalter im Layouteditor, ganze Prozentschritte der Regler sowie die neue Auswahl laufender Programme.

## Nicht abgedeckt in diesem Lauf

- **Per-Monitor-DPI-Prüfung übersprungen** (`dpi=skipped`). `scripts\verify-dpi-awareness.ps1` startet die Oberfläche, die sich selbst über die Windows-UAC-Abfrage erhöht. In einer nicht interaktiven Sitzung bleibt der Start an der unbeantworteten Abfrage stehen. Für die vollständige Prüfung `scripts\verify.ps1` ohne `-SkipDpiCheck` in einer interaktiven Sitzung ausführen und die UAC-Abfrage bestätigen.
- **Die überarbeitete Oberfläche wurde nicht am laufenden Programm in Augenschein genommen.** Alle Aussagen zur Darstellung stammen aus den automatisierten WPF-Tests, nicht aus einem Sichttest. Insbesondere die Auswahl **Laufendes Programm wählen …** wurde nur mit einer eingespeisten Prozessliste geprüft; die tatsächliche Systemabfrage `RunningProcessCatalog.FromSystem` ist ungeprüft.
- **Titelleisten-Erkennung nicht manuell nachgestellt.** Der frühere Blocker — `IsTitleBarDrag = false` bei einem leeren Windows-11-Notepad — ist im Code adressiert: `WindowsWindowService.IsTitleBarDrag` fragt zunächst `WM_NCHITTEST` mit Timeout ab und fällt nur bei ausbleibender Antwort auf eine geometrische Prüfung zurück. Ein erneuter manueller Hook-Test mit Notepad steht aus.
- **Das GitHub-Release zu `v2026.0831.01` ist noch nicht erstellt.** Commit und Tag sind gepusht, die EXE liegt gebaut im Rootverzeichnis; das Anhängen als Release-Asset braucht ein angemeldetes GitHub CLI (`gh auth login`).
- **Die Versionsanzeige in der Kopfzeile wurde nicht am laufenden Programm in Augenschein genommen**, nur der ausgelesene Wert der gebauten EXE ist geprüft.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Reproduktion

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
