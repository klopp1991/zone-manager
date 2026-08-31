# Sascha’s Zone Manager — Kurzanleitung

## Start

`ZoneManager.exe` im Rootverzeichnis starten und die Windows-UAC-Abfrage bestätigen. Der Diagnosemodus `ZoneManager.exe --diagnostics` läuft absichtlich ohne Elevation.

Unter **Layouts** besitzt jeder erkannte Monitor eigene Layouts, die unabhängig aktiviert, erstellt, umbenannt, gelöscht und bearbeitet werden können. Sobald mindestens ein Layout aktiv ist, zeigt die Snap-Funktion beim Ziehen eines Fensters an der Titelleiste das Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Sicherheitsstatus

- Autostart ist beim ersten Start ausgeschaltet.
- Die Anwendung nutzt keinen Treiber, keinen Windows-Dienst und keine Code-Injektion. Die Registry wird nur nach bewusst gespeichertem Autostart verändert.
- Normale Programmstarts wechseln über die Windows-UAC-Abfrage in den Administratormodus, damit auch erhöhte Fenster positioniert werden können.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort; `Escape` bricht nur den laufenden Ziehvorgang ab.
- Ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Ablage

Gültige Änderungen werden automatisch gespeichert. Die aktive Konfiguration liegt unter `%APPDATA%\SnapZones\settings.json`, die fünf vorherigen Stände daneben als `settings.backup-1.json` bis `settings.backup-5.json`; **Export** und **Import** übertragen sämtliche Einstellungen, Monitorlayouts, Zonen und IDs in einem vollständigen JSON-Backup. Protokolle liegen unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`.

Die Pfade behalten den historischen Ordnernamen `SnapZones`, damit bestehende Installationen ohne Migration weiterlaufen.
