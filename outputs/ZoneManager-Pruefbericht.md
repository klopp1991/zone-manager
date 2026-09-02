# Sascha’s Zone Manager — Prüfbericht

Stand: 02.09.2026 · Lauf: `dotnet test ZoneManager.sln -c Release` · Abschluss: bestanden

Geprüfte Version: Arbeitsstand nach **2026.0831.01** mit den vier Etappen vom 02.09.2026 (Fehler die heute wirken, zuverlässig platzieren, Monitore zur Laufzeit, Oberfläche und Ausbau). Noch nicht als Release veröffentlicht.

## Bestanden

- Release-Build für Windows 11 x64: 0 Fehler, 0 Warnungen (`TreatWarningsAsErrors` in allen Projekten aktiv).
- 612 automatisierte Tests: 612 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- **Etappe 1:** `SnapActivationPolicyTests`, `MainZoneSweepTests` und `WindowPlacementEngineTests` halten fest, dass Hauptzone, Auffang und Positionsgedächtnis an «ein Layout ist aktiv» hängen; der frühere Schalter `SnappingEnabled` ist entfernt. `SnappingStateTests` deckt Statuszeile (Zustand, Meldung, Schaltfläche nur im Stopp), Infobereichsmenü mit «Einrasten wieder aktivieren» und den zurücksetzbaren Schutzschalter ab. `AppRuleListItemTests` deckt die Hinweise «Ziellayout fehlt», «Zielzone fehlt», «Abgeschaltet» in der Regelliste ab. `FileLogTests` deckt Mindeststufe, `--verbose`, Aufrufstapel samt innerer Ausnahme und fünf Generationen ab. `StaleTemporaryFilesTests` deckt das Aufräumen liegengebliebener Temp-Dateien ab.
- **Etappe 2:** `VerifiedPlacementTests` halten fest, dass ein gemerktes Fenster in seine Zone zurückkehrt, kleine Fenster klein bleiben, der Layoutwechsel Fenster mit unsichtbarem Rand erkennt und der Befehlsdienst die gemessene Ablehnung durchreicht. `PartMonitorResolverTests` deckt Abstände in der Platzierung, den Treffer im Zwischenraum und den nächsten Monitor über der Taskleiste ab. `PixelRectTests` deckt Toleranz, Zentrieren und Abstand ab. `AppRuleCoordinatorTests` rechnen mit denselben Abständen wie das Overlay.
- **Etappe 3:** `MonitorReconciliationTests` deckt die Übernahme eines umgesteckten Monitors über die Hardwarekennung, die Nicht-Übernahme zweier baugleicher Monitore, das Nachtragen von Kennung und Grösse und das Bereinigen verwaister Namen und Reihenfolgen ab. `MonitorSetsTests` deckt Schlüssel, Aufzeichnen, Wiederherstellen und Bereinigen der Monitorsätze ab. `EdidReaderTests` deckt Hersteller-, Modell- und Seriennummer aus der EDID ab. `SchemaSixUpgradeTests` deckt den Schemawechsel 5 auf 6 und die Ablehnung toter Verweise ab.
- **Etappe 4:** `LayoutEditorUndoTests` deckt Rückgängig, Wiederholen, das Zusammenfassen eines Mausziehens und das Verwerfen des Wiederholen-Zweigs ab. `ZoneHotkeyNavigatorTests` deckt Zone weiter/zurück, Zone nach Nummer und Zurücksetzen ab. `UpdateCheckTests` halten fest, dass ohne Prüfsummendatei nichts geladen wird, dass `sha256sum`- und `Get-FileHash`-Schreibweise gelesen werden und dass der Release-Feed die Prüfsummendatei findet. `MainWindowNavigationTests` und `ThemeResourceTests` decken die sechs Seiten und die Verteilung der Karten auf «Verhalten» und «Programm» ab.
- Weiterhin abgedeckt: Ausschlüsse, Regelidentität, nicht verbundene Monitore, Beenden mit Zeitgrenze, Versionsschema, Zonen verbinden, Autostart, Updates, Installation, Rechte, Fensterhelfer, Overlay-Geltungsbereich, UI-Richtlinien.

## Am laufenden System nachgestellt (02.09.2026)

- Ein neu geöffnetes Fenster landet in der Hauptzone; die Einstellungsseite zählt danach gemerkte Fensterpositionen.
- Ein per Maus gezogenes Fenster sitzt pixelgenau auf der Fläche, die das Overlay zeigt: sichtbarer Rahmen 8,8 mit 2041 × 1145 bei Rand 8 und Zonenabstand 2 auf 5120 × 2100.
- Beim Start wurden vier verwaiste Monitoreinträge bereinigt, die Hardwarekennungen mit Seriennummer nachgetragen und der Monitorsatz aufgezeichnet.
- Tastenkürzel am laufenden System: `Ctrl + Alt + 2` setzte das Vordergrundfenster in Zone 2 (Ziel 2051,8 3061 × 2084, gemessen mit unsichtbarem Rand 2042,-1 3079 × 2102), `Ctrl + Alt + Links` anschliessend zurück in Zone 1.
- Statuszeile, Regel-Hinweis «Ziellayout fehlt – Regel pausiert», Monitorseite mit Skalierungswerten, die Seiten «Verhalten» und «Programm» sowie der Layouteditor mit Nummern, Rückgängig, Wiederholen und Vorschau wurden in Augenschein genommen.

## Nicht abgedeckt in diesem Lauf

- **Der vollständige Prüflauf `scripts\verify.ps1` wurde nicht ausgeführt.** Geprüft sind Release-Build ohne Warnungen und die Testsuite.
- **Monitorwechsel zur Laufzeit** (Anstecken, Abstecken, Auflösungswechsel) wurde nicht physisch nachgestellt; der Weg von `MonitorWatcher` über den Abgleich bis zum Neuaufbau ist durch Tests der Kernlogik und den Startabgleich abgedeckt, das Windows-Ereignis selbst nicht.
- **Die Updatefunktion** wurde nie gegen die echte Release-Ablage ausgeführt; es gibt noch keine Veröffentlichung mit Prüfsummendatei.
- **Der Fensterhelfer mit `uiAccess`**, die Installation nach «Programme» und der erhöhte Neustart bleiben ungeprüft; diese Schritte verlangen Administratorrechte und verändern das System.
- **Die Farbprüfung des gerenderten Hauptfensters** überspringt sich in einer Sitzung ohne Fensterdarstellung.

## Reproduktion

Dieser Lauf:

```powershell
dotnet test ZoneManager.sln -c Release
```

Der vollständige Release-Lauf:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
