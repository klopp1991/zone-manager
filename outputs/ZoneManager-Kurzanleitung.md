# Sascha’s Zone Manager — Kurzanleitung

## Start

`ZoneManager.exe` starten – eine UAC-Abfrage erscheint dabei nicht. Administratorrechte fordert das Programm erst an, wenn du ein Fenster einrasten willst, das einem Programm mit höheren Rechten gehört; dann fragt es einmal je Sitzung nach. Unter **Einstellungen → Rechte** lässt sich stattdessen «Immer beim Start» wählen.

Unter **Layouts** besitzt jeder erkannte Monitor eigene Layouts, die unabhängig aktiviert, erstellt, umbenannt, gelöscht und bearbeitet werden können. Sobald mindestens ein Layout aktiv ist, zeigt die Snap-Funktion beim Ziehen eines Fensters an der Titelleiste das Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Zonen genau setzen

Rechts neben dem Editor liegt die Karte **Ausgewählte Zone**. Die **Masseinheit** wird dort einmal auf Prozent oder Pixel gestellt und gilt für alle acht Zahlenfelder. **Position und Grösse** und **Abstände zum Rand** beschreiben dieselbe Zone, einmal von links oben und einmal von den vier Rändern aus.

## Mehrere Zonen verbinden

Fenster mit gedrückter **Strg**-Taste über mehrere Zonen ziehen: die überstrichenen Zonen werden gemeinsam
hervorgehoben, beim Loslassen belegt das Fenster ihre gemeinsame Fläche. Eine Auswahl bleibt auf einen Monitor
beschränkt.

## Gemerkte Fensterpositionen

Das Programm merkt sich, wo ein eingerastetes Fenster zuletzt stand, und legt es beim nächsten Öffnen wieder
dorthin. Erkannt wird ein Fenster an Programm, Fensterklasse und Fensterart, nicht am Titel; mehrere Fenster
desselben Programms teilen sich deshalb einen Eintrag. Unter **Einstellungen** lässt sich das abschalten und
über **Gemerkte Positionen verwerfen** zurücksetzen.

## Regeln

Eine Regel schiebt Fenster eines Programms in eine feste Zone. Das Programm wird entweder über **Programmdatei wählen …** aus dem Dateisystem gesucht oder über **Laufendes Programm wählen …** aus den gerade laufenden Programmen übernommen. Titelmuster und Fensterklasse grenzen optional weiter ein; leer bedeutet: jedes Fenster des Programms. Das Ereignis legt fest, wann die Regel greift, und wird unter der Auswahl im Klartext erklärt.

## Ausschlüsse

Ein Ausschluss lässt ein Fenster vollständig in Ruhe: kein Overlay beim Ziehen, kein Einrasten, keine Regel,
kein Merken der Position. Das Fenster behält dauerhaft die Grösse und Position, die du ihm gibst. Beschrieben
wird es wie bei einer Regel über Programm, Titelmuster und Fensterklasse; mindestens eines der drei muss
stehen. Trifft auf ein Fenster beides zu, gewinnt der Ausschluss.

## Installation

Das Programm läuft auch ohne Installation. **Einstellungen → Installation** kopiert es nach «Programme»,
verknüpft es im Startmenü und trägt es in «Apps und Features» ein; von dort lässt es sich wie jedes andere
Programm entfernen. Die Einstellungen bleiben dabei erhalten.

## Updates

**Einstellungen → Updates** zeigt die installierte Version, sucht auf Knopfdruck nach einer neueren und
installiert sie mitsamt Neustart. Die Suche ist voreingestellt aus und sendet nichts ausser der Anfrage
selbst. Geladen wird nur aus der Release-Ablage des Projekts; die bisherige Programmdatei bleibt bis zum
nächsten Start als Sicherung daneben liegen.

## Sicherheitsstatus

- Autostart ist beim ersten Start ausgeschaltet.
- Die Anwendung nutzt keinen Treiber, keinen Windows-Dienst und keine Code-Injektion.
- Autostart läuft über eine Anmeldeaufgabe der Windows-Aufgabenplanung und startet ohne UAC-Abfrage. Nur wenn sich die Aufgabe nicht anlegen lässt, wird ersatzweise der Registry-Eintrag `Run` gesetzt.
- Das Programm startet ohne Administratorrechte und fragt erst nach, wenn ein Fenster sie wirklich verlangt – höchstens einmal je Sitzung.
- Wer das vermeiden will, richtet unter **Einstellungen → Fensterhelfer ohne Administratorrechte** ein eigenes Zertifikat ein. Danach rasten auch Fenster höher berechtigter Programme ein, ohne dass das Programm je Administratorrechte bekommt. Der Rechner vertraut dafür einem selbst ausgestellten Zertifikat – die Oberfläche erklärt Nutzen und Risiko im Klartext.
- `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort; `Escape` bricht nur den laufenden Ziehvorgang ab.
- Ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Ablage

Gültige Änderungen werden automatisch gespeichert. Die aktive Konfiguration liegt unter `%APPDATA%\SnapZones\settings.json`, die fünf vorherigen Stände daneben als `settings.backup-1.json` bis `settings.backup-5.json`; **Export** und **Import** übertragen sämtliche Einstellungen, Monitorlayouts, Zonen und IDs in einem vollständigen JSON-Backup. Protokolle liegen unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`.

Die Pfade behalten den historischen Ordnernamen `SnapZones`, damit bestehende Installationen ohne Migration weiterlaufen.
