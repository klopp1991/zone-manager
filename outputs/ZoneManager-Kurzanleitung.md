# Sascha’s Zone Manager — Kurzanleitung

## Start

`ZoneManager.exe` startet den Layout-Editor unter Windows 11 x64. Unter **Layouts** besitzt jeder erkannte Monitor eigene Layouts, die unabhängig aktiviert, erstellt, umbenannt, gelöscht und bearbeitet werden können.

Die Anwendung prüft beim Start ohne Prozessstart, ob sie sich selbst erhöhen kann, und fordert nur dann Administratorrechte an. Ist das nicht möglich oder wird die Abfrage der Benutzerkontensteuerung abgebrochen, läuft sie mit den vorhandenen Rechten weiter. In diesem Fall zeigt ein Banner im Fenster den Grund und bietet **Mit Administratorrechten neu starten** an; der Tooltip im Infobereich weist den eingeschränkten Betrieb ebenfalls aus. Eingeschränkt heisst: Fenster von Programmen mit höheren Rechten können nicht positioniert werden. Alles Übrige funktioniert unverändert.

## Sicherheitsstatus

- Snap-Funktion und Autostart sind im Auslieferungszustand ausgeschaltet.
- Der Build nutzt keinen Treiber, keinen Dienst und keine Code-Injection und verändert die Registry nur nach bewusst gespeichertem Autostart.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort.

## Ablage

Gültige Änderungen werden automatisch gespeichert. Die aktive Konfiguration liegt unter `%APPDATA%\ZoneManager\settings.json`, die fünf vorherigen Stände daneben als `settings.backup-1.json` bis `settings.backup-5.json`; **Export** und **Import** übertragen sämtliche Einstellungen, Monitorlayouts, Zonen und IDs in einem vollständigen JSON-Backup. Protokolle liegen unter `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`.

Frühere Versionen legten beides unter `SnapZones` ab. Beim ersten Start übernimmt die Anwendung den Inhalt von `%APPDATA%\SnapZones` einmalig nach `%APPDATA%\ZoneManager`, sofern dort noch nichts liegt. Der alte Ordner bleibt unverändert als Rückfallebene erhalten und wird nicht gelöscht. Liegt am neuen Ort bereits eine Konfiguration, hat diese Vorrang; es wird nichts überschrieben. Alte Protokolle wandern nicht mit.
