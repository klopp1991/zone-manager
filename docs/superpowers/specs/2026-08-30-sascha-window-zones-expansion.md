# Sascha Window Zones: Erweiterungsspezifikation

**Datum:** 30. August 2026  
**Status:** Freigegeben  
**Zielplattform:** Windows 11 x64  
**Technologie:** .NET 8, WPF, dokumentierte Win32-Schnittstellen

## Ziel

Der bestehende SnapZones-Prototyp wird als **Sascha Window Zones** weitergeführt. Die Erweiterung macht Profile als oberste Organisation sichtbar, verbessert die Monitoridentität, ergänzt einen präzisen Prozent-/Pixel-Editor und erhöht die Bedienbarkeit des Layouteditors. Theme, Tray, Fenstertitel, Dateiname und Icon verwenden die neue Produktidentität.

## Sicherheitsgrenze für Windows-Anzeigeeinstellungen

Sascha Window Zones verändert keine undokumentierten DPI-Pakete, keine privaten Explorer-Strukturen und keine nicht dokumentierten Registry-Werte. Windows 11 bietet für normale Desktopprogramme keine unterstützte Schnittstelle, mit der gleichzeitig frei wählbare monitorweise Skalierung, monitorweise Textskalierung und monitorweise Taskleisten-/Icongrössen gesetzt werden können.

Die Anwendung zeigt deshalb pro Monitor den erkannten Windows-Skalierungswert und bietet direkte Aktionen zu den zuständigen Windows-Einstellungsseiten. Die Oberfläche erklärt dabei:

- Die normale Anzeigeskalierung ist in Windows monitorbezogen, aber auf die von Windows angebotenen Werte begrenzt.
- Die benutzerdefinierte Skalierung von 100 bis 500 Prozent ist eine globale Windows-Einstellung und kann eine Abmeldung erfordern.
- Die Textskalierung von 100 bis 225 Prozent ist eine globale Barrierefreiheitseinstellung.
- Taskleisten- und Taskleisten-Icongrössen sind unter Windows 11 nicht frei und nicht pro Monitor über eine unterstützte API einstellbar.

## Datenmodell und Editor

`ZoneDefinition.Bounds` bleibt die kanonische, normierte Geometrie. Dadurch übersteht ein Layout Auflösungs- und DPI-Wechsel. Der Editor kann dieselbe Geometrie wahlweise als Prozent oder Pixel bearbeiten. Die Pixelwerte beziehen sich auf die aktuelle Arbeitsfläche des Monitors; die Oberfläche nennt diese Bezugsfläche ausdrücklich.

Zwei Eingabearten verhindern widersprüchliche sechsfach definierte Rechtecke:

- **Position und Grösse:** Links, Oben, Breite, Höhe sind editierbar; Rechts und Unten werden berechnet.
- **Aussenabstände:** Links, Oben, Rechts, Unten sind editierbar; Breite und Höhe werden berechnet.

Bei jedem Wechsel zwischen Prozent und Pixel werden alle sichtbaren Werte neu aus der kanonischen Geometrie berechnet. Ungültige, negative oder zu grosse Werte werden nicht übernommen. Globale äussere Layoutabstände können links, oben, rechts und unten getrennt in Pixeln gesetzt werden; die zonenbezogenen Ränder werden im Editor pro Monitorlayout definiert.

## Neue Zone und magnetisches Andocken

Beim Hinzufügen einer Zone werden die Kanten aller vorhandenen Zonen zusammen mit den Monitorrändern als Kandidatenraster verwendet. Die Anwendung prüft alle dadurch entstehenden achsenparallelen Rechtecke, verwirft Überschneidungen und wählt die grösste freie Fläche; bei Gleichstand gewinnt die oberste, danach die linkste Fläche. Ist keine Fläche mit der Mindestgrösse 4 Prozent in beiden Richtungen frei, wird keine Zone erstellt und ein verständlicher Hinweis angezeigt.

Beim Verschieben docken Zonen mit ihren Aussenkanten an Monitorrändern und angrenzenden Zonenkanten an. Beim Skalieren dockt nur die bewegte Kante an. Die Distanz ist in Pixeln einstellbar, wird für jede Monitorachse normiert und kann mit `Alt` während des Ziehens vorübergehend ausgeschaltet werden. Die Validierung verhindert weiterhin Überlappungen.

## Monitoridentität

Die Monitorerkennung kombiniert `EnumDisplayMonitors` mit `QueryDisplayConfig` und `DisplayConfigGetDeviceInfo`. Der GDI-Gerätename verbindet die aktive Monitorfläche mit dem aktiven Displaypfad; der EDID-Anzeigename wird bevorzugt, der Monitorgerätepfad dient als stabile Identität. Nur wenn Windows keinen Namen liefert, bleibt der bisherige Geräte- oder GDI-Name als Rückfallwert bestehen.

Die Monitorliste zeigt je Eintrag zwei ungekürzte Zeilen:

```text
DELL U3225QE
3840 x 2160 | Windows-Skalierung 150 %
```

## Oberfläche

Die visuelle Richtung bleibt eine ruhige Windows-Arbeitsoberfläche, erhält aber eine eigene Identität durch ein Monitorraster-Signet. Die Oberfläche verwendet Segoe UI Variable, einen klaren blauen Akzent, geringe Rundungen und systemabhängige helle oder dunkle Flächen. Der Fensterrand folgt über `DWMWA_USE_IMMERSIVE_DARK_MODE` dem gewählten Theme.

```text
SASCHA WINDOW ZONES                       Profil: Arbeit     Speichern
Profile | Layouts | Einstellungen

Layouts
+ Neue Zone     Vorlage anwenden
Monitore        Monitorfläche                         Zonendetails
DELL U3225QE    [zieh- und skalierbare Zonen]         Einheit: % | px
3840 x 2160                                            Definition: Grösse | Ränder
```

Der Profilreiter steht vor Layouts. `+ Neue Zone` ist die primäre Aktion über dem Editor. Jede Einstellung enthält direkt darunter einen neutralen Hilfetext mit Wirkung, Gültigkeitsbereich und allfälligem Neustart-/Abmeldehinweis.

## Theme und Icon

`ThemeMode` kennt `System`, `Light` und `Dark`; Standard ist `System`. Bei Systemänderungen werden Farben und Titelleiste ohne Neustart aktualisiert. Das Icon besteht aus einem stilisierten Monitor mit vier Zonen und einer hervorgehobenen Zone; es wird deterministisch als Vektorquelle und als Mehrgrössen-ICO für Fenster, EXE und Infobereich erzeugt.

## Zusätzliche Einstellungen

- Aktivierung: sofort oder nur mit Umschalttaste.
- Overlaybereich: aktiver Monitor oder alle Monitore.
- Overlayfarbe, Deckkraft und Zonenname.
- Zonenabstand sowie separate äussere Abstände links, oben, rechts und unten.
- Magnetdistanz von 0 bis 40 Pixel; 0 deaktiviert das Andocken.
- Theme: System, Hell oder Dunkel.
- Snap-Funktion, Autostart und dokumentierter Not-Aus.

## Verifikation

Neue Geometrie- und Konvertierungslogik wird testgetrieben gebaut. Automatisierte Tests decken grösste freie Rechtecke, Gleichstände, vollständige Belegung, Prozent-/Pixel-Roundtrips, Randdefinition, magnetisches Verschieben/Skalieren, asymmetrische Aussenabstände, alte Konfigurationsdateien und Displaypfad-Zuordnung ab. Der Release-Ablauf führt Restore, alle Tests, Build, Self-contained-Publish, sichere Diagnose ohne Hook/Änderungen, DPI-Prüfung und Artefaktprüfung aus. Reale Drag-, Theme- und Einstellungsseiten-Tests bleiben der abschliessenden Benutzerprüfung vorbehalten.
