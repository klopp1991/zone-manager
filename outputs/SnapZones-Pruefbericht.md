# SnapZones Prüfbericht

Stand: 30.08.2026

## Automatisierte Prüfung

`scripts\verify.ps1` stellte Pakete wieder her, führte den Volltest aus, baute und veröffentlichte die `win-x64`-EXE und prüfte die hashgleiche Root- und Publish-EXE sowie den schreibgeschützten Diagnosevertrag. Ergebnis: 327 Tests ausgeführt, davon 325 bestanden und exakt zwei dokumentierte Icon-Baselines fehlgeschlagen: `Brand_icon_uses_only_neutral_greys` und `Brand_icon_uses_two_wide_lower_tiles_instead_of_a_monitor_stand`. Der strukturierte TRX-Gegenlauf schloss ausschliesslich diese zwei vollständigen Testnamen aus und bestätigte 325/325 weitere Tests.

Die Diagnose meldet `windowPlacement.enabled`, `learnedEntryCount`, `ruleCount` und `lifecycleHookRegistered=false`. Sie startet keinen Hook, keine Engine und keinen Recovery-Schreibpfad; `settings.json` und `placements.json` bleiben unverändert, auch bei beschädigter Platzierungsdatei.

## Funktionsumfang

Der globale Standard ist **Letzte Platzierung merken**. Gleichartige Hauptfenster teilen Anwendung und Fenstertyp, Dialoge sind getrennt; Wiederherstellung erfolgt nur beim Öffnen und nie laufend. Eine angeforderte Maximierung wird nur für ein bereits im Vordergrund befindliches Ziel ausgeführt; sonst bleibt das Ziel ohne Fokusdiebstahl unverändert. Minimierung wird nie wiederhergestellt. Feste Zone, Ausschluss und optionales `TitlePattern` sind Regeln; die Dateien liegen normal unter `%APPDATA%\SnapZones` und im portablen Betrieb unter `Data\` neben der EXE. Der Not-Aus `Ctrl + Alt + Shift + F12` deaktiviert Snap-Funktion und automatische Platzierung; höhere Fensterrechte bleiben eine Berechtigungsgrenze.

## Reale Abnahme nicht ausgeführt

Die reale Prüfung mit Windows-Einstellungen, Excel-Hauptfenstern und -Dialog, Explorer, Notepad, festen Zonen, Ausschlüssen, Monitorverlust, Not-Aus und Neustart wurde auf ausdrücklichen Wunsch nicht ausgeführt. Virtuelle Desktops, mehrere individuelle Plätze pro gleichen Fenstertyp und fortlaufendes Erzwingen einer Zone bleiben ausserhalb des Umfangs.
