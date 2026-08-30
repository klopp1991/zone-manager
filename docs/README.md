# SnapZones Prototyp

SnapZones erstellt frei bearbeitbare Fensterbereiche pro Monitor. Beim Ziehen eines normalen Fensters an der Titelleiste zeigt die aktivierte Snap-Funktion die Bereiche als Overlay; beim Loslassen füllt das Fenster den gewählten Bereich.

> **Prüfstatus:** Layout-Editor, Profile, Speichern, Diagnose und Not-Aus sind geprüft. Die Titelleisten-Erkennung des aktuellen Windows-11-Notepad ist noch fehlerhaft; die Snap-Funktion bleibt deshalb in diesem Build ausgeschaltet und ist noch nicht zur Nutzung freigegeben.

## Schnellstart

1. `outputs/SnapZones-prototype/SnapZones.exe` starten.
2. Unter **Layouts** einen Monitor wählen und Zonen direkt ziehen, skalieren oder über eine Vorlage erzeugen.
3. **Speichern** wählen. Layouts und Einstellungen liegen danach in `%APPDATA%\SnapZones\settings.json`.
4. Die **Snap-Funktion** bis zur Behebung der dokumentierten Titelleisten-Erkennung ausgeschaltet lassen.

Die Snap-Funktion und der Autostart sind beim ersten Start ausgeschaltet. SnapZones enthält keinen Treiber, keinen Windows-Dienst, keine Code-Injection und benötigt keine Administratorrechte.

## Not-Aus

`Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort und speichert die Snap-Funktion als ausgeschaltet. Dasselbe geschieht bei einem Callback-Fehler oder bei mehr als 100 Hook-Ereignissen innerhalb von zehn Sekunden.

## Layouts bearbeiten

- Zone anklicken und innerhalb der Fläche ziehen.
- Die acht Griffe der ausgewählten Zone ändern ihre Grösse.
- Name und Prozentwerte rechts numerisch bearbeiten und **Werte anwenden** wählen.
- Vorlagen ersetzen die Zonen nur im aktuellen Entwurf; **Entwurf zurücksetzen** stellt den zuletzt gespeicherten Stand wieder her.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden rot markiert und können nicht gespeichert werden.

## Profile und Schnellwahl

Profile enthalten getrennte Layouts für jeden erkannten Monitor. Sie lassen sich oben im Editor oder im Infobereich bei der Uhr wechseln; den Schnellwahlplätzen 1 bis 9 zugeordnete Profile reagieren auf `Ctrl + Alt + 1` bis `Ctrl + Alt + 9`.

Die Standardkonfiguration ordnet «Standard» dem Platz 1 zu. Neue Profile übernehmen zunächst die vorhandenen Monitorlayouts; eine frei konfigurierbare Schnellwahlzuordnung ist noch nicht Teil dieses Prototyps.

## Einstellungen

- **Overlays anzeigen:** auf allen Monitoren oder nur auf dem Monitor unter dem Mauszeiger.
- **Aktivierung:** sofort beim Titelleisten-Drag oder nur bei gedrückter Umschalttaste.
- **Aussenrand / Zonenabstand:** Pixelabstand zur Arbeitsfläche beziehungsweise zwischen Zonen.
- **Overlay-Deckkraft:** Sichtbarkeit der Zonen während des Ziehens.
- **Mit Windows starten:** schreibt nach bewusstem Speichern ausschliesslich den Wert `SnapZones` unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Infobereich

Das Schliessen des Hauptfensters beendet SnapZones nicht, sondern lässt es im Infobereich weiterlaufen. Das Menü wechselt Profile, aktiviert oder deaktiviert die Snap-Funktion, öffnet den Editor und beendet die Anwendung vollständig.

## Diagnose

```powershell
SnapZones.exe --diagnostics
```

Die Diagnose liest Konfigurationsstatus, Monitore, DPI und Autostartstatus. Sie registriert keinen Fenster-Hook und verändert weder Einstellungen noch Registry.

## Einschränkungen des Prototyps

- Nur Windows 11 x64.
- Fenster mit höheren Administratorrechten können nicht positioniert werden.
- Die aktuelle Titelleisten-Erkennung akzeptiert Windows-11-Notepad noch nicht; das zugehörige Windows-Verschiebeereignis kommt korrekt an, wird aber fälschlich als Nicht-Titelleisten-Drag verworfen.
- Nicht rechteckige oder überlappende Zonen, virtuelle Desktops, Fensterregeln und automatische Updates sind nicht enthalten.
- Der Prototyp ist noch nicht digital signiert; Windows kann deshalb beim ersten Start eine Sicherheitswarnung anzeigen.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht selbständig für `win-x64` und prüft danach die lesende Diagnose.
