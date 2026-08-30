# Parallele Arbeit

Vor jeder Dateiänderung muss jede Aufgabe in einem eigenen Git-Worktree und auf einem eigenen Branch arbeiten.

1. Im primären Worktree auf `main` sind nur Bestandsaufnahme und abschliessende Integration erlaubt; dort niemals Quellcode, Tests, Dokumentation oder Build-Artefakte ändern.
2. Befindet sich die Aufgabe noch im primären Worktree, `scripts/new-task-worktree.ps1 -TaskName "<kurzer-name>"` ausführen und danach alle Befehle ausschliesslich im ausgegebenen `WorktreePath` ausführen.
3. Einen bereits vorhandenen verknüpften Worktree weiterverwenden; innerhalb eines Worktrees keinen weiteren Worktree anlegen.
4. Nie fremde Änderungen stagen, stashen, zurücksetzen, bereinigen oder überschreiben. Jeder Aufgabenbranch enthält nur die Änderungen dieser Aufgabe.
5. Vor einer Integration Arbeitsbaum, Zielbranch und `git worktree list` prüfen. Nur vollständig commitete und geprüfte Aufgabenbranches in einem sauberen Integrations-Worktree zusammenführen.
6. Die Root-EXE nur in einer ausdrücklich dafür vorgesehenen Integrations- oder Release-Aufgabe aktualisieren.

Wenn ein sauberer Integrations-Worktree nicht verfügbar ist, die Arbeit auf dem Aufgabenbranch abschliessen und die Integration als offen melden; niemals ersatzweise in einen belegten `main`-Worktree schreiben.
