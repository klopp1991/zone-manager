# SnapZones Prüfbericht

Stand: 30.08.2026

## Bestanden

- Release-Build für Windows 11 x64: 0 Fehler, 0 Warnungen.
- 44 automatisierte Tests: 44 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Diagnose auf dem Zielsystem: 2 Monitore erkannt; kein Hook registriert; Einstellungen und Registry unverändert.
- Oberfläche: beide Monitore, Layout-Editor, Vorlagen, Zonenwerte, Profile und Sicherheitseinstellungen sichtbar und bedienbar.
- Standardzustand: Snap-Funktion aus; Autostart aus.
- Not-Aus `Ctrl + Alt + Shift + F12`: Hook deaktiviert und Einstellung dauerhaft auf aus gespeichert.

## Offener Blocker

Beim kontrollierten Test mit einem leeren Windows-11-Notepad kamen `EVENT_SYSTEM_MOVESIZESTART` und `EVENT_SYSTEM_MOVESIZEEND` korrekt an. Die anschliessende Prüfung meldete jedoch `IsTitleBarDrag = false`, obwohl das Fenster an der Titelleiste gezogen wurde; der Koordinator blieb deshalb absichtlich im sicheren Zustand `Idle` und positionierte das Fenster nicht.

Nach drei kontrollierten Versuchen wurden weitere Hook-Tests gestoppt. Snap-Funktion und Autostart sind ausgeschaltet, SnapZones und das leere Test-Notepad wurden beendet.

## Nächster technischer Schritt

Die Titelleisten-Erkennung muss für moderne benutzerdefinierte Windows-Titelleisten auf eine robuste, DPI-konsistente Erkennung umgestellt und danach erneut ausschliesslich mit einem leeren Notepad-Fenster geprüft werden.
