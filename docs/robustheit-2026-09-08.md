# Robustheitsprüfung vom 08.09.2026

## Umgesetzt in 2026.0908.01

| Änderung | Wirkung | Nachweis |
|---|---|---|
| Vollbildschutz beim Wiederherstellen | Vollbild bleibt unangetastet, auch wenn es erst zwischen den Fensterabfragen beginnt; die bisher gemerkte Zone bleibt erhalten | Drei Tests für unterschiedliche Umschaltzeitpunkte und ein Auffangzonentest |
| Vollbild- und Minimiertschutz für App-Regeln | Regeln für Öffnen, Fokus und Layoutwechsel prüfen den Zustand nach der Verzögerung und vor jedem Versuch | Vier Regeltests |
| Schutz unmittelbar im Windows-Fensterdienst | Ein erkanntes Vollbild wird auch über den direkten Platzierungspfad nicht verkleinert; manuelle Platzierungen erhalten einen verständlichen Hinweis | Vier Tests mit echten Windows-Fenstern, einschliesslich eines weiterhin platzierbaren kleinen rahmenlosen Fensters |
| Rahmenausgleich nach Wiederherstellung | Beim Einrasten maximierter oder minimierter Fenster werden Grösse und unsichtbarer Rand nochmals im Normalzustand gemessen | Zwei Tests gegen den sichtbaren DWM-Rahmen, im selben DPI-Modus wie die Anwendung |
| Auffindbare Hilfe | Unter Verhalten → Fenster merken erklärt eine Karte Vollbild und Videos in einer Zone; Suchbegriffe umfassen F11, Video, YouTube, PiP und Bild-im-Bild | XAML im Release-Build kompiliert |
| Korrigierter Schnellstart | Die Dokumentation verlangt beim normalen Start keine Administratorrechte mehr | Abgleich mit Manifest und Startlogik |

## Ergänzt in 2026.0908.02

| Änderung | Wirkung | Nachweis |
|---|---|---|
| Gemeinsamer Austausch von EXE und Helfer | Beide Dateien werden vor dem Beenden vorbereitet und per SHA-256 geprüft; ein gemeldeter Austauschfehler setzt beide auf den vorherigen Stand zurück | Erfolgsfall, gesperrter Helfer, gesperrte EXE und fehlender Helfer |
| Schutz laufender Instanzen | Ohne bestätigtes Prozessende wird keine Programmdatei ersetzt; parallele Installationsläufe werden gesperrt | Laufendes Testprogramm ignoriert `--exit`; zweiter Zugriff auf die Installationssperre wird abgewiesen |
| Monitoränderungen im Vollbildeditor | Geänderte Arbeitsflächen positionieren den Editor neu; ein getrennter Monitor schliesst ihn mit Statushinweis, ohne die Zonen zu verändern | Zwei Tests mit geöffnetem WPF-Editor und simuliertem Monitorwechsel |
| Einheitliche Vollbildregel | Positionsgedächtnis und direkter Platzierungspfad verwenden dieselbe Entscheidung | Sechs zusätzliche Tests für Fensterzustände, negative Monitorkoordinaten und Pixeltoleranz |
| Neutrale Grössenmeldung | Eine gemessene Abweichung wird nicht mehr als bewiesene Mindestgrösse ausgegeben | Release-Build |

Die sechs Installationstests laufen sowohl unter PowerShell 7 als auch unter Windows PowerShell 5.1 und sind in `scripts/verify.ps1` eingebunden.
Die Rücknahme schützt vor gemeldeten Datei- und Prozessfehlern; ein Stromausfall oder ein erzwungenes Beenden des Installationsskripts mitten im Austausch ist damit nicht vollständig abgesichert.

## Vollbild innerhalb einer Zone

Die frühere automatische Vollbildbegrenzung war beim Beginn dieser Prüfung bereits aus dem Arbeitsstand entfernt; diese bestehende Änderung wurde beibehalten.
Ein normales Fenster kann seine Zone ausfüllen, während echtes Browser-, Video- oder Spielvollbild weiterhin die Monitorfläche verwendet.
Für Videos ist ein separates Bild-im-Bild-Fenster der erste Kandidat; für eine spätere Vollbildfunktion empfehle ich eine ausdrücklich aktivierte Lösung pro unterstütztem Programm, mit Wiederherstellung beim Beenden und begrenzten Korrekturversuchen.

Die neue Erkennung bleibt eine Geometrie- und Stilprüfung: monitorfüllend, ohne vollständige Titelleiste, weder minimiert noch maximiert.
Programme mit abweichenden Fensterstilen, exklusivem Vollbild oder speziellen Bild-im-Bild-Fenstern benötigen eigene Kompatibilitätsprüfungen; Chrome, Edge, Firefox, VLC und Spiele wurden in dieser Prüfung nicht interaktiv durchgespielt.
Maximieren und Vollbild sind unterschiedliche Zustände; Windows beschreibt normales Maximieren als Vergrössern bis auf den für die Taskleiste reservierten Bereich ([Microsoft: About Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/about-windows)).

## Weitere Befunde und nächste Schritte

| Priorität | Beobachtung im Code | Empfehlung |
|---|---|---|
| Mittel | Der uiAccess-Helfer wird vor dem lokalen Wiederherstellungs- und Rahmenausgleichspfad aufgerufen | Maximiert → Zone und Wechsel zwischen unterschiedlich skalierten Monitoren auch mit einem tatsächlich eingerichteten Helfer prüfen |
| Mittel | Reale Browser-, Video- und Spielfenster wurden nicht vollständig durchgespielt | Kompatibilitätsmatrix für Chrome, Edge, Firefox, VLC, exklusives Vollbild und verschiedene DPI-Stufen durchführen |
| Mittel | EXE und Helfer sind getrennte Dateien, die nacheinander ersetzt werden | Für Wiederanlauf nach Stromausfall ein dauerhaftes Austauschprotokoll oder vollständige Versionsverzeichnisse vorsehen |

## Prüfumfang

Ausgangsstand: 711 Tests erfolgreich; erster Durchlauf: 725; aktueller Endstand: **733 Tests erfolgreich, keine übersprungenen Tests**, zusätzlich sechs Installationstests je PowerShell-Version.
Die neuen Fehlerfälle wurden vor den jeweiligen Korrekturen als fehlschlagende Regressionstests ausgeführt.
Ergebnisdateien liegen unter `outputs/robustness-tests`, der aktuelle vollständige Lauf heisst `robustness-followup.trx`.
Die Prüfung umfasst Code, automatisierte Abläufe und echte Testfenster unter Windows; sie ersetzt keinen vollständigen manuellen Test aller Anwendungen und Monitorwechsel.

## Programmdatei

Die aktualisierte portable Ausgabe liegt im Projektordner als `ZoneManager.exe` vor, zusammen mit dem Fensterhelfer in Version `2026.0908.02`.
Release-Build: keine Warnungen oder Fehler; Diagnoseprozess: Exitcode 0, keine registrierten Hooks oder geänderten Einstellungen, Startkonfiguration bereit; beide Programmdateien stimmen per SHA-256 mit den Build-Artefakten überein.
Die bei der Prüfung laufende Installation unter `C:\Program Files\ZoneManager\ZoneManager.exe` wird dadurch nicht aktualisiert.
