# SnapZones Prototyp

## Start

`SnapZones-prototype\SnapZones.exe` startet den Layout-Editor unter Windows 11 x64. Unter **Layouts** lassen sich pro erkanntem Monitor Zonen ziehen, skalieren, numerisch bearbeiten oder aus Vorlagen erzeugen; **Profile** verwaltet getrennte Arbeitskonfigurationen.

## Sicherheitsstatus

- Snap-Funktion und Autostart sind ausgeschaltet.
- Der Build nutzt keinen Treiber, keinen Dienst, keine Code-Injection, keine Administratorrechte und verändert die Registry nur nach bewusst gespeichertem Autostart.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort.
- Die Snap-Funktion ist noch nicht freigegeben: Windows-11-Notepad wird beim Titelleisten-Drag derzeit fälschlich verworfen.

## Ablage

Gespeicherte Einstellungen liegen unter `%APPDATA%\SnapZones\settings.json`, Protokolle unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`.
