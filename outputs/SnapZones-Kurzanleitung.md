# SnapZones Kurzanleitung

## Start

`SaschaWindowZones.exe` im Rootverzeichnis startet den Layout-Editor unter Windows 11 x64. Unter **Layouts** lassen sich pro erkanntem Monitor Zonen ziehen, skalieren, numerisch bearbeiten oder aus Vorlagen erzeugen; **Profile** verwaltet getrennte Arbeitskonfigurationen.

## Fensterplatzierung

- Unter **Fensterplatzierung** aktiviert der Hauptschalter die automatische Wiederherstellung. Der globale Standard ist **Letzte Platzierung merken**.
- Die Wiederherstellung wirkt nur einmal beim Öffnen. Danach sind Verschieben, Skalieren und Maximieren frei; beim nächsten Öffnen wird daraus wieder gelernt.
- Gleichartige Hauptfenster teilen einen Anwendung-/Fenstertyp. Dialoge bleiben getrennt. Maximierung wird gespeichert, Minimierung nie.
- **Feste Zone** legt eine Startzone für Profil, Monitor und Zone fest. **Nicht verwalten** schliesst den passenden Typ aus. `TitlePattern` steht nur in erweiterten Regeln zur Verfügung.
- Gelernte Einträge liegen in `placements.json`; Regeln und Hauptschalter liegen in `settings.json`.

## Sicherheitsstatus

- Snap-Funktion und Autostart sind ausgeschaltet. Die automatische Fensterplatzierung ist im Standard eingeschaltet und kann separat ausgeschaltet werden.
- Der Build nutzt keinen Treiber, keinen Dienst, keine Code-Injection, keine Administratorrechte und verändert die Registry nur nach bewusst gespeichertem Autostart.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook, Overlays und automatische Fensterplatzierung sofort und speichert beide Automatikflags als ausgeschaltet.
- Fenster mit höheren Administratorrechten werden ohne gleich hohe Rechte nicht verschoben. Virtuelle Desktops, mehrere individuelle Plätze für gleiche Fenstertypen und fortlaufendes Erzwingen einer Zone sind nicht enthalten.

## Ablage

Gültige Änderungen werden automatisch gespeichert. Normal liegen `settings.json` und `placements.json` unter `%APPDATA%\SnapZones`; bei `portable.flag` neben der EXE liegen sie unter `Data\` neben der Anwendung. Die fünf vorherigen Konfigurationsstände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; `placements.backup-1.json` sichert den letzten Platzierungsstand. **Export** und **Import** übertragen sämtliche Einstellungen, Profile, Monitorlayouts, Zonen und IDs in einem vollständigen JSON-Backup. Protokolle liegen unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`.
