# Monitorbezogene Layouts

## Ziel

Sascha’s Zone Manager verwaltet keine globalen Profile mehr. Jeder erkannte Monitor besitzt eine eigene Liste gespeicherter Layouts und genau ein aktives Layout; ein Wechsel auf einem Monitor verändert keinen anderen Monitor.

## Begriffe und Hierarchie

```text
Monitor
├─ aktives Layout
└─ gespeicherte Layouts
   └─ Zonen
```

Ein Layout gehört exakt zu einem Monitor und enthält Name, gespeicherte Monitorgrösse und Zonen. Der Begriff «Profil» verschwindet aus Hauptfenster, Infobereich, Toast, Importhinweisen und Kurzanleitung.

## Bedienung

Die bisherige Profilseite entfällt. Auf der Seite **Layouts** wird zuerst der Monitor und danach eines seiner Layouts gewählt. Dort können Layouts erstellt, umbenannt, gelöscht und bearbeitet werden; das letzte Layout eines Monitors ist nicht löschbar. Ein neu erstelltes Layout kopiert die Zonen des aktuell gewählten Layouts und wird sofort nur auf diesem Monitor aktiv.

Im Infobereich erscheint pro Monitor ein Untermenü. Beim Öffnen zeigt es dessen Layouts, markiert das aktive Layout und aktiviert per Klick ausschliesslich das gewählte Layout dieses Monitors.

## Layout-Stabilität

Dynamische Inhalte wie Überschriften, Beschriftungen, Formularfelder und Aktionsleisten dürfen nicht in Zeilen mit fester Inhaltshöhe liegen. Diese Bereiche verwenden automatische Höhen; eine Mindesthöhe darf lediglich die Grundgrösse sichern und muss bei grösserer Schrift oder höherem Platzbedarf mitwachsen.

Zwischen dem Layoutkopf und dem nachfolgenden Editor bleiben bei der normalen Fenstergrösse von 1480 × 900 sowie bei der Mindestgrösse von 1180 × 720 mindestens 12 geräteunabhängige Pixel frei. Monitorwahl, Layoutwahl, Layoutname und Aktionen bleiben vollständig innerhalb der Seite. Reicht der verfügbare Platz in anderen Seiten nicht aus, muss der Inhalt scrollbar bleiben, statt von nachfolgenden Bereichen überzeichnet oder abgeschnitten zu werden. Feste Höhen bleiben ausschliesslich für bewusst begrenzte grafische Vorschauen zulässig.

## Daten und Migration

Schema 2 speichert eine flache Liste von `MonitorLayout`-Einträgen. Jeder Eintrag besitzt `Id`, `Name` und `IsActive`; Einträge desselben logischen Monitors werden anhand stabiler ID und ersatzweise Gerätename gruppiert. Pro Monitorgruppe ist exakt ein Layout aktiv.

Beim Laden von Schema 1 wird jedes bisherige Profil-Monitor-Paar in ein eigenständiges Monitorlayout umgewandelt. Der Profilname wird zum Layoutnamen, die Zonen bleiben erhalten und das Layout des zuvor aktiven Profils wird auf dem betreffenden Monitor aktiv; fehlt es dort, wird das erste migrierte Layout aktiviert. Die migrierte Konfiguration wird als Schema 2 gespeichert, während die normale Sicherungsrotation den alten Stand erhält.

## Laufzeit

Overlay und Einrastlogik verwenden für jeden angeschlossenen Monitor dessen aktives Layout. Globale Profil-Schnellwahlen entfallen; der Sicherheits-Hotkey bleibt unverändert. Export und Import arbeiten mit Schema 2 und können bestehende Schema-1-Archive migrieren.

## Fehlerregeln

- Leere oder doppelte Layoutnamen sind innerhalb desselben Monitors ungültig.
- Das letzte Layout eines Monitors kann nicht gelöscht werden.
- Konfigurationen mit doppelten Layout-IDs, ungültigen Zonen oder mehr beziehungsweise weniger als einem aktiven Layout pro Monitorgruppe werden abgewiesen.
- Nicht mehr angeschlossene Monitore und ihre Layouts bleiben gespeichert.

## Prüfung

Automatisierte Tests decken unabhängige Aktivierung, Erstellen/Umbenennen/Löschen, Overlay-Zielauswahl, Tray-Menüplan, Schema-1-Migration, Schema-2-Rundlauf und die WPF-Struktur ohne visuelle Abnahme ab. Die WPF-Prüfung misst die reale Anordnung bei 1480 × 900 und 1180 × 720 und weist Überdeckungen sowie horizontal abgeschnittene Layoutaktionen zurück. Danach laufen die vollständige Test-Suite, Release-Build, Self-contained-Publish und Diagnose; ein grafischer App-Start gehört nicht zu dieser Prüfung.
