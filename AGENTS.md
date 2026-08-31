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

Die Dokumentation soll laufend stimmen und nicht laufend wachsen.

1. Kein Arbeitspaket gilt als fertig, solange ein Dokument eine Aussage enthält, die durch dieses Arbeitspaket falsch geworden ist.
2. Neue Erkenntnisse in das zuständige bestehende Dokument einarbeiten, statt einen neuen Anhang danebenzustellen. Neue Dokumente nur, wenn kein bestehendes zuständig ist.
3. Überholte Abschnitte ersetzen oder löschen, nicht ergänzen.
4. Keine Aussage ungeprüft dokumentieren. Zahlen (Testanzahl, Bytes, Laufzeiten) stammen aus dem tatsächlichen Lauf; nicht Geprüftes wird als nicht geprüft ausgewiesen.
5. `outputs/ZoneManager-Pruefbericht.md` gibt nach jedem Arbeitspaket den Stand des letzten tatsächlichen Laufs wieder, einschliesslich dessen, was nicht geprüft wurde.
6. `docs/superpowers/**` ist ein historisches Archiv abgeschlossener Pläne und Spezifikationen und bleibt unverändert, auch wenn dort alte Namen stehen.
