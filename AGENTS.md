# Aktueller Auftrag

Vor Arbeitsbeginn `docs/auftraege/2026-08-31-konsolidierung.md` lesen. Dort stehen Ausgangslage, bereits erledigte Punkte, die offenen Arbeitspakete in bindender Reihenfolge und die Abnahmekriterien.

# Parallele Arbeit

`main` ist der einzige dauerhafte Branch. Aufgabenbranches leben Minuten bis Stunden, nie Tage. Es gibt keine Themen-, Feature- oder Sammelbranches.

1. Standardfall ist die direkte Arbeit im primaeren Worktree auf `main`. Arbeitet gerade kein zweiter Agent am Repository, wird kein Branch und kein Worktree angelegt.
2. Ein eigener Worktree wird nur angelegt, wenn tatsaechlich ein zweiter Agent gleichzeitig arbeitet. Der Grund ist technische Isolation, nicht Prozess: ein gemeinsamer Git-Index und gemeinsame `obj/`- und `bin/`-Verzeichnisse vertragen keine zwei gleichzeitigen Laeufe. Dann `scripts/new-task-worktree.ps1 -TaskName "<kurzer-name>"` ausfuehren und alle weiteren Befehle ausschliesslich im ausgegebenen `WorktreePath`.
3. Einen bereits vorhandenen verknuepften Worktree weiterverwenden; innerhalb eines Worktrees keinen weiteren Worktree anlegen.
4. Aufgaben werden so geschnitten, dass gleichzeitig laufende Agents verschiedene Dateien anfassen. Ueberschneiden sich zwei Aufgaben inhaltlich, werden sie nacheinander erledigt statt parallel.
5. Nie fremde Aenderungen stagen, stashen, zuruecksetzen, bereinigen oder ueberschreiben. Jeder Aufgabenbranch enthaelt nur die Aenderungen dieser Aufgabe.
6. Die Root-EXE nur in einer ausdruecklich dafuer vorgesehenen Integrations- oder Release-Aufgabe aktualisieren.

# Abschluss einer Aufgabe

Die Integration gehoert zur Aufgabe. Eine Aufgabe, deren Ergebnis nicht in `main` steht, ist nicht fertig.

1. Alle Aenderungen commiten, bis der Arbeitsbaum sauber ist.
2. `scripts/finish-task.ps1` ausfuehren. Das Skript holt `origin/main`, rebased die Aufgabe darauf, laesst die Testsuite laufen, zieht `main` per Fast-Forward auf den Aufgabenstand, pusht nach `origin/main` und entfernt Worktree und Aufgabenbranch.
3. Die Standardpruefung ist die Testsuite, nicht der Release-Lauf. `scripts/verify.ps1` publisht eine 72-MB-EXE und schreibt die Root-EXE; es laeuft nur in einer Release-Aufgabe, dort ueber `scripts/finish-task.ps1 -Check Full`. `-Check None` ist nur zulaessig, wenn die Pruefung in derselben Aufgabe schon auf dem endgueltigen Stand gelaufen ist.
4. Bricht das Skript ab, wird die Ursache in derselben Aufgabe behoben: Rebase-Konflikte aufloesen, fehlgeschlagene Pruefungen reparieren, danach erneut ausfuehren. Ein Aufgabenbranch wird nicht liegengelassen und nicht an eine spaetere Aufgabe uebergeben.
5. Laesst sich eine Aufgabe nicht abschliessen, wird sie zurueckgenommen statt gestapelt: Worktree und Branch entfernen und den erreichten Stand im zugehoerigen Auftrag unter `docs/auftraege/` festhalten.
6. Es wird nicht gemerged. `main` bleibt linear.

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
