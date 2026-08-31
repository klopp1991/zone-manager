# Aktueller Auftrag

Vor Arbeitsbeginn `docs/auftraege/2026-08-31-konsolidierung.md` lesen. Dort stehen Ausgangslage, bereits erledigte Punkte, die offenen Arbeitspakete in bindender Reihenfolge und die Abnahmekriterien.

# Parallele Arbeit

Vor jeder Dateiänderung muss jede Aufgabe in einem eigenen Git-Worktree und auf einem eigenen Branch arbeiten.

1. Im primären Worktree auf `main` sind nur Bestandsaufnahme und abschliessende Integration erlaubt; dort niemals Quellcode, Tests, Dokumentation oder Build-Artefakte ändern.
2. Befindet sich die Aufgabe noch im primären Worktree, `scripts/new-task-worktree.ps1 -TaskName "<kurzer-name>"` ausführen und danach alle Befehle ausschliesslich im ausgegebenen `WorktreePath` ausführen.
3. Einen bereits vorhandenen verknüpften Worktree weiterverwenden; innerhalb eines Worktrees keinen weiteren Worktree anlegen.
4. Nie fremde Änderungen stagen, stashen, zurücksetzen, bereinigen oder überschreiben. Jeder Aufgabenbranch enthält nur die Änderungen dieser Aufgabe.
5. Vor einer Integration Arbeitsbaum, Zielbranch und `git worktree list` prüfen. Nur vollständig commitete und geprüfte Aufgabenbranches in einem sauberen Integrations-Worktree zusammenführen.
6. Die Root-EXE nur in einer ausdrücklich dafür vorgesehenen Integrations- oder Release-Aufgabe aktualisieren.

Wenn ein sauberer Integrations-Worktree nicht verfügbar ist, die Arbeit auf dem Aufgabenbranch abschliessen und die Integration als offen melden; niemals ersatzweise in einen belegten `main`-Worktree schreiben.

# Dokumentation

Die Dokumentation wächst nicht, sie wird gepflegt. Bei jeder Änderung gilt:

1. Betroffene Dokumente in derselben Aufgabe aktualisieren, nicht in einer späteren. Eine Aufgabe ist erst fertig, wenn Code und Dokumentation übereinstimmen.
2. Bestehende Abschnitte korrigieren statt neue danebenzustellen. Kein Anhängen von "Update", "Neu", "Siehe auch" an einen überholten Text.
3. Überholte Aussagen, Zahlen, Pfade und Dateinamen entfernen. Eine falsche Doku ist schlechter als keine.
4. Jede Aussage muss zum Zeitpunkt des Schreibens geprüft sein. Nicht geprüfte Punkte ausdrücklich als ungeprüft kennzeichnen.
5. Feste Zuständigkeiten: `README.md` Überblick und Einstieg, `docs/README.md` Bedienung und Architektur, `docs/ui-richtlinien.md` verbindliche Regeln für Oberfläche und Hilfetexte, `outputs/ZoneManager-Kurzanleitung.md` Kurzfassung für Anwender, `outputs/ZoneManager-Pruefbericht.md` Ergebnis des letzten Prüflaufs. Inhalte gehören genau an eine Stelle; sonst querverweisen statt kopieren.
6. `docs/superpowers/**` sind datierte historische Plan- und Spezifikationsdokumente. Sie werden nicht rückwirkend umgeschrieben und nicht als aktueller Stand gelesen.
7. Aufträge unter `docs/auftraege/` werden bei Abschluss auf den Endstand nachgeführt und als erledigt markiert, nicht gelöscht.

# Oberfläche

Jede Änderung an der Oberfläche folgt `docs/ui-richtlinien.md`. Dort stehen die Texthierarchie, die Regeln für Hilfetexte, die einheitliche Steuerelementhöhe, die Gruppierung, die Behandlung von Masseinheiten und die Anforderungen an Barrierefreiheit. Neue Werte gehören zentral in `src/SnapZones.App/Themes/Theme.xaml`, nicht lokal in eine Seite. Weicht eine Änderung bewusst ab, wird die Richtlinie angepasst statt umgangen.
