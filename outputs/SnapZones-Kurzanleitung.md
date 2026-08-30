# SnapZones Prototyp

## Start

`SaschaZoneManager.exe` startet den Layout-Editor unter Windows 11 x64. Unter **Layouts** besitzt jeder erkannte Monitor eigene Layouts, die unabhängig aktiviert, erstellt, umbenannt, gelöscht und bearbeitet werden können.

## Sicherheitsstatus

- Snap-Funktion und Autostart sind ausgeschaltet.
- Der Build nutzt keinen Treiber, keinen Dienst, keine Code-Injection, keine Administratorrechte und verändert die Registry nur nach bewusst gespeichertem Autostart.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort.
- Die Snap-Funktion ist noch nicht freigegeben: Windows-11-Notepad wird beim Titelleisten-Drag derzeit fälschlich verworfen.

## Ablage

Gültige Änderungen werden automatisch gespeichert. Die aktive Konfiguration liegt unter `%APPDATA%\SnapZones\settings.json`, die fünf vorherigen Stände daneben als `settings.backup-1.json` bis `settings.backup-5.json`; **Export** und **Import** übertragen sämtliche Einstellungen, Monitorlayouts, Zonen und IDs in einem vollständigen JSON-Backup. Protokolle liegen unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`.
