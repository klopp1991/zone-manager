# SnapZones Prüfbericht

Stand: 30.08.2026

## Automatisierte Prüfung

`scripts\verify.ps1` stellt Pakete wieder her, führt den Volltest aus, baut und veröffentlicht die `win-x64`-EXE, prüft die hashgleiche Root- und Publish-EXE sowie den schreibgeschützten Diagnosevertrag. Die zwei dokumentierten Icon-Baselines `Brand_icon_uses_only_neutral_greys` und `Brand_icon_uses_two_wide_lower_tiles_instead_of_a_monitor_stand` werden nur bei exakt diesem Ergebnis akzeptiert; ein zweiter Lauf bestätigt alle übrigen Tests. Der konkrete Laufstatus wird in `task-11-report.md` festgehalten.

Die Diagnose meldet `windowPlacement.enabled`, `learnedEntryCount`, `ruleCount` und `lifecycleHookRegistered=false`. Sie startet keinen Hook, keine Engine und keinen Recovery-Schreibpfad; `settings.json` und `placements.json` bleiben unverändert, auch bei beschädigter Platzierungsdatei.

## Funktionsumfang

Der globale Standard ist **Letzte Platzierung merken**. Gleichartige Hauptfenster teilen Anwendung und Fenstertyp, Dialoge sind getrennt; Wiederherstellung erfolgt nur beim Öffnen und nie laufend. Maximierung wird wiederhergestellt, Minimierung nie. Feste Zone, Ausschluss und optionales `TitlePattern` sind Regeln; die Dateien liegen normal unter `%APPDATA%\SnapZones` und im portablen Betrieb unter `Data\` neben der EXE. Der Not-Aus `Ctrl + Alt + Shift + F12` deaktiviert Snap-Funktion und automatische Platzierung; höhere Fensterrechte bleiben eine Berechtigungsgrenze.

## Reale Abnahme ausstehend

Die reale Prüfung mit Windows-Einstellungen, Excel-Hauptfenstern und -Dialog, Explorer, Notepad, festen Zonen, Ausschlüssen, Monitorverlust, Not-Aus und Neustart ist noch nicht ausgeführt. Virtuelle Desktops, mehrere individuelle Plätze pro gleichen Fenstertyp und fortlaufendes Erzwingen einer Zone bleiben ausserhalb des Umfangs.
