# Zone Manager

Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `ZoneManager.exe` starten und die Windows-UAC-Abfrage bestätigen. Die Datei kommt entweder aus dem neuesten [Release](https://github.com/klopp1991/zone-manager/releases/latest) oder entsteht im Rootverzeichnis, sobald das Projekt gebaut wird.
2. Die **Übersicht** zeigt jeden Monitor mit seinem aktiven Layout. Ein Klick auf einen Monitor öffnet **Zonen & Layouts**; dort steht ein Tab je Layout, **+ Neu** legt ein leeres, ein Vorlagen- oder ein dupliziertes Layout an.
3. Die vorhandenen Zonen anpassen und mit **+ Zone** die grösste freie Fläche belegen – im Fenster oder mit **Auf dem Monitor zeichnen** in echter Grösse direkt auf dem Bildschirm.
4. Zonen ziehen, über acht Griffe skalieren oder im Werte-Panel als Zahlen eingeben – wahlweise über Position und Grösse oder über die vier Randabstände. Die **Masseinheit** wird einmal pro Panel auf Prozent oder Pixel gestellt und gilt für alle acht Felder.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet. Die Statuszeile nennt die letzte Aktion mit Uhrzeit.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme. Bestehende Profilkonfigurationen aus Schema 1 werden beim Laden in unabhängige Layouts pro Monitor migriert.

Die fünf Sicherungen erscheinen unter **Programm → Frühere Stände** mit Zeitstempel und einem Satz, was sich danach geändert hat («Layout «Video» angelegt», «Deckkraft 24 % → 30 %»). **Wiederherstellen** holt einen Stand zurück; der bisherige wird dabei selbst als jüngste Sicherung abgelegt, und der Hinweis unten bietet **Rückgängig**.

## Oberfläche

Die Navigation links hat sieben Seiten in drei Gruppen: **Übersicht** · **Einrichten** (Monitore, Zonen & Layouts, Fenster zuordnen, In Ruhe lassen) · **Einstellungen** (Verhalten, Programm). Die Zähler neben den Einträgen nennen Monitore, Layouts, Zuordnungen und in Ruhe gelassene Programme. Das Suchfeld oben findet jede Einstellung über Beschriftung, Pfad und Synonyme («dunkel» führt zum Erscheinungsbild); ein Treffer öffnet Seite und Untertab, `Enter` den ersten Treffer, `Esc` leert das Feld.

Die **Übersicht** zeigt jeden Monitor als Karte mit Miniatur seines aktiven Layouts und einer Auswahl zum Umschalten, darunter drei Zähler (zugeordnete Fenster mit Hinweis auf pausierte Zuordnungen, in Ruhe gelassene Programme, gemerkte Fenster) und die drei häufigsten Aktionen: **Zonen auf dem Monitor zeichnen**, **+ Fenster zuordnen**, **Zonen kurz einblenden** (drei Sekunden auf allen Monitoren).

Löschende Aktionen fragen nicht mehr nach. Zuordnung entfernen, wieder einrasten lassen, Zone oder Layout löschen, gemerkte Positionen verwerfen, Einstellungen zurücksetzen und einen früheren Stand wiederherstellen werden sofort gespeichert und unten in der Mitte mit einem Hinweis samt **Rückgängig** bestätigt; er bleibt sechs Sekunden stehen, länger, solange der Mauszeiger darauf liegt. Nachgefragt wird nur noch, wo sich nichts zurücknehmen lässt: Zertifikat, Installation, Update.

Jede Einstellung trägt ein **?**; beim Darüberfahren oder mit Tastaturfokus erklärt es in zwei bis fünf Sätzen Wirkung, Beispiel, Bedeutung des leeren Werts und den erlaubten Bereich. Schalter ersetzen die Checkboxen in Listen und Einstellungszeilen.

## Mehrere Zonen verbinden

Wird ein Fenster mit gedrückter **Strg**-Taste über mehrere Zonen gezogen, sammeln sich die überstrichenen
Zonen auf und werden gemeinsam hervorgehoben. Beim Loslassen belegt das Fenster ihre gemeinsame Fläche.
Wird Strg wieder losgelassen, fällt die Auswahl auf die Zone unter dem Zeiger zurück.

Strg ist dafür die einzige freie Taste: Umschalt löst je nach Einstellung erst das Einrasten aus, Alt schaltet
im Layouteditor den Magnetismus ab.

Zwei Grenzen sind bewusst gesetzt. Eine Auswahl bleibt auf einen Monitor beschränkt, weil die Fläche über zwei
Bildschirme hinweg den Zwischenraum und fremde Zonen mit einschlösse. Und liegen die gewählten Zonen nicht
aneinander, deckt das Fenster auch die Lücke dazwischen ab — ein Fenster kann nur ein Rechteck einnehmen.

Ein so platziertes Fenster wird wie jedes andere gemerkt, allerdings über sein Rechteck statt über eine
einzelne Zone.

## Fenster zuordnen

Auf der Seite **Fenster zuordnen** bringt eine Zuordnung ein Fenster immer in dieselbe Zone – beim Öffnen, beim Fokus oder beim Layoutwechsel. Jede Zuordnung ist eine Zeile: Programmsymbol, «Explorer.exe → Referenz», darunter Ereignis · Monitor › Layout · Eingrenzung, rechts ein Schalter zum Ein- und Ausschalten und **Bearbeiten**. Fehlt das Ziel, weil Layout oder Zone gelöscht wurden, trägt die Zeile einen gelben Rand, «Ziel fehlt – Zuordnung pausiert» und die Schaltfläche **Beheben**; es gibt keinen stillen Fallback, und Zuordnungen starten keine Programme.

**+ Fenster zuordnen** öffnet einen Dialog: links die laufenden Fenster mit Symbol, Programmname und Fenstertitel, durchsuchbar; rechts das Ziel als Monitor · Layout mit seinen Zonen und den bereits zugeordneten Programmen als Chips («Explorer.exe ×»). Ein Fenster wird auf eine Zone gezogen oder gewählt und die Zone angeklickt; **Wann?** legt das Ereignis fest, **Zuordnen** legt die Zuordnung an, **Eingrenzen & Feinheiten …** öffnet gleich das Detail. Übernommen wird bewusst **nur der Dateiname**, etwa `Discord.exe`: viele Programme installieren sich in ein Verzeichnis mit Versionsnummer, und eine Zuordnung auf den vollständigen Pfad hörte beim nächsten Update auf zu greifen. **Programmdatei wählen, die gerade nicht läuft …** führt in den Dateidialog. Ohne Zuordnung zeigt die Seite ein Beispiel und **+ Erstes Fenster zuordnen**.

**Bearbeiten** klappt das Detail unter der Zeile auf: Programm (mit **ändern**), Wann (**Fenster wird geöffnet** greift einmalig beim Erscheinen, **Fenster erhält den Fokus** bei jedem Wechsel zu einem passenden Fenster, **Layout wird aktiviert** ordnet beim Layoutwechsel alle offenen passenden Fenster neu an), Ziellayout und Zielzone. Aufklappbar darunter **Fenster eingrenzen** (Titel enthält, Fensterklasse wie `CabinetWClass`; `*` und `?` als Platzhalter) und **Feinheiten** (Verzögerung 0 bis 30000 ms, 0 bis 3 Wiederholungen, Priorität 0 bis 100). Programm, Titel und Fensterklasse sind gleichrangige Filter; mindestens eines der drei muss stehen, sonst weist das Detail darauf hin und die Zuordnung bleibt wirkungslos. Passen mehrere Zuordnungen bei gleicher Priorität, gewinnt die enger gefasste. Alles wird sofort gespeichert; **Zuordnung entfernen** lässt sich über den Hinweis rückgängig machen.

## In Ruhe lassen

Auf der Seite **In Ruhe lassen** stehen die Fenster, die die Anwendung vollständig in Ruhe lässt. Für ein
solches Fenster erscheint beim Ziehen kein Overlay, es rastet in keine Zone ein, keine Zuordnung bewegt
es, und seine Position wird weder gemerkt noch beim nächsten Öffnen wiederhergestellt. Es behält damit
dauerhaft die Grösse und Position, die ihm der Benutzer selbst gibt.

Jede Zeile nennt das Programm und «Alle Fenster» oder die Eingrenzung; der Schalter schaltet den Eintrag aus,
ohne ihn zu löschen. **Eingrenzen …** klappt das Detail auf: **Titel enthält** und **Fensterklasse (selten
nötig)**, damit nur eine Fensterart frei bleibt – etwa ein Vorschaufenster, während das Hauptfenster
weiterhin einrastet. Die gestrichelte Zeile **+ Programm in Ruhe lassen** öffnet die Auswahl der laufenden
Programme oder eine .exe-Datei. Ein Eintrag ohne jedes Merkmal wird nicht gespeichert; er würde auf jedes
Fenster passen. **Wieder einrasten lassen** entfernt den Eintrag mit **Rückgängig**.

Ein Eintrag hier ist stärker als jede Zuordnung: passen auf ein Fenster beide, bleibt das Fenster unberührt.
Priorität und Konflikte gibt es nicht, weil mehrere zutreffende Einträge zum selben Ergebnis führen.

## Layouteditor

- Über der Zeichenfläche steht die Monitorauswahl und ein **Tab je Layout**; das auf dem Monitor aktive trägt «● aktiv». Ein Klick wechselt nur das bearbeitete Layout, aktiviert wird über die Übersicht, das Infobereichsmenü oder den Rechtsklick auf den Tab (**Aktivieren**, **Umbenennen …**, **Duplizieren**, **Layout löschen**). **+ Neu ⌵** legt ein leeres Layout, eines aus einer Vorlage oder eine Kopie des aktuellen an.
- **+ Zone** belegt die grösste freie achsenparallele Fläche; ohne ausreichenden freien Bereich wird nichts verändert. **Zone löschen** und `Entf` entfernen die ausgewählte Zone mit **Rückgängig**.
- **Vorlage ⌵** zeigt die fünf Vorschläge, die zu Seitenverhältnis, Auflösung, Skalierung und Monitorgrösse passen; eine Vorlage ersetzt alle Zonen, der Hinweis bietet **Rückgängig**.
- **↶** und **↷** (auch `Strg + Z` / `Strg + Y`) nehmen jede Änderung am Entwurf zurück; ein Mausziehen zählt als eine Änderung.
- **Doppelklick** auf eine Zone benennt sie an Ort um (`Enter` übernimmt, `Esc` bricht ab). **Rechtsklick** öffnet das Menü: Auffangzone festlegen oder aufheben, Umbenennen, **Mit Zone n verbinden** (zwei Zonen, die eine ganze Kante teilen, werden zu einer), Zone entfernen.
- Jede Zone trägt vor ihrem Namen eine Nummer (`1 · Links`); dieselbe Nummer steht im Overlay und ist die Taste in `Ctrl + Shift + Nummer`.
- Zonen docken innerhalb der eingestellten Magnetdistanz an Monitor- und Zonenkanten an; `Alt` deaktiviert das Andocken während des Ziehens.
- Das **Werte-Panel** rechts (mit **Werte ausblenden ›** einklappbar; die Stellung wird gespeichert) schaltet die **Masseinheit** an einer einzigen Stelle um; die Umschaltung gilt gemeinsam für alle acht Zahlenfelder. **Prozent** bleibt bei Auflösungsänderungen proportional; **Pixel** bezieht sich auf die aktuelle Windows-Arbeitsfläche des Monitors. **Position und Grösse** bearbeitet X, Y, Breite und Höhe; **Abstände zum Rand** beschreibt dieselbe Zone von den vier Rändern aus. Die Checkbox **Auffangzone** markiert die ausgewählte Zone. Siehe [Auffangzone](#auffangzone).
- **Auf dem Monitor zeichnen ⤢** öffnet denselben Editor als randloses Fenster über der Arbeitsfläche des gewählten Monitors: die Zonen in echter Grösse, der Desktop abgedunkelt dahinter, oben eine schwebende Werkzeugleiste (+ Zone, Vorlage, ↶ ↷, **Werte ⌵** mit den acht Zahlenfeldern, Fertig), an jeder Zone ihr Mass in Prozent **und** Pixel, beim Ziehen eine Hilfslinie mit Masstooltip. Doppelklick, Rechtsklick, `Alt` und `Strg + Z` gelten wie im Fenster; `Esc` oder **Fertig** schliesst. Gespeichert ist ohnehin schon alles. Solange gezeichnet wird, blendet **Zonen kurz einblenden** nichts ein.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden markiert und können nicht gespeichert werden.
- Nach jedem Setzen misst das Programm nach. Sitzt das Fenster nicht innerhalb von zwei Pixeln auf der Zielfläche, wird es einmal erneut gesetzt (ein Wechsel zwischen Monitoren mit unterschiedlicher Skalierung braucht häufig zwei Anläufe). Bleibt eine Abweichung, nennt die Statuszeile den Grund, etwa eine Mindestgrösse des Fensters; nach Administratorrechten wird nur gefragt, wenn sich das Fenster gar nicht bewegen liess. Fenster ohne veränderbare Grösse werden in der Zone zentriert statt gestreckt.
- Beginnt das Ziehen über der Taskleiste, gilt der nächstgelegene Monitor; bleibt das Endereignis von Windows aus (Fenster geschlossen, Maustaste losgelassen), zieht ein Wachhund die Overlays nach spätestens einer Sekunde ein.
- Beim Einrasten wird der unsichtbare Fensterrand ausgeglichen. Windows gibt Fenstern mit veränderbarer Grösse einen Griffbereich zum Ziehen, der zum Fensterrechteck zählt, aber nicht gezeichnet wird – typischerweise sieben Pixel links, rechts und unten. Ohne Ausgleich stünden zwei Fenster in lückenlos aneinandergrenzenden Zonen sichtbar auseinander. Das Programm vergrössert das Fensterrechteck deshalb um genau diesen Rand, sodass der sichtbare Rahmen exakt in der Zone liegt.

## Auffangzone

Die Auffangzone (bis zum 05.09.2026 «Auffangzone») ist die Arbeitszone: dort landen neu erscheinende Fenster,
die sonst nirgends hingehören. Festgelegt wird sie im Layouteditor an der Zone selbst – über die Checkbox
**Auffangzone** im Werte-Panel oder den Rechtsklick auf die Zone. Die markierte Zone trägt in der
Zeichenfläche das Feld «Auffangzone».

Jedes Layout darf eine eigene Auffangzone tragen. Welche davon wirksam ist, entscheidet die Monitorreihenfolge
aus der Seite **Monitore**: es gilt die Auffangzone des ersten Monitors, dessen aktives Layout überhaupt eine
trägt. Monitore ohne Eintrag in dieser Reihenfolge stehen hinten.

Daraus folgt das Verhalten, das im Alltag zählt:

- Ist nur eine einzige Zone markiert, landen neue Fenster immer am selben Ort.
- Markierst du in mehreren Layouts desselben Monitors je eine, überlebt die Auffangzone den Layoutwechsel:
  das neue Layout bringt seine eigene mit.
- Trägt das aktive Layout des vordersten Monitors keine, rutscht die Wahl auf den nächsten Monitor, statt
  ganz auszufallen.
- Trägt kein aktives Layout eine, gibt es keine Auffangzone, und neue Fenster bleiben unangetastet — so
  verhält sich das Programm auch ohne jede Markierung.

Ein kopiertes Layout übernimmt die Markierung auf der Zone an derselben Stelle; ohne das hätte eine Kopie
nie eine Auffangzone. Nach dem Setzen nennt die Statuszeile, welche Auffangzone tatsächlich wirksam ist.

Ein Fenster kommt in die Auffangzone, wenn nacheinander nichts anderes zutrifft:

1. Eine passende Regel platziert das Fenster — sie gewinnt immer.
2. Eine gemerkte Position liegt vor — sie gewinnt vor der Auffangzone.
3. Das Fenster liegt bereits auf einer Zone eines aktiven Layouts eingerastet — es bleibt, wo es ist.
4. Sonst: Auffangzone.

Eingerastet heisst, dass die vier Fensterränder auf höchstens 40 Pixel genau mit den Zonenrändern
zusammenfallen; genau so viel kann der unsichtbare Fensterrand ausmachen. `scripts\measure-window-frame.ps1`
misst diesen Rand an bereits offenen Fenstern und bricht ab, wenn er die 40 Pixel überschreitet; gemessen
wurden zuletzt 13 bis 16 Pixel. Ein Fenster, das eine Zone bloss
überlappt, gilt nicht als eingerastet — bei einem lückenlos gekachelten Monitor überlappt sonst jedes
Fenster irgendeine Zone.

Aufgefangen wird in zwei Fällen:

- **Beim Erscheinen eines Fensters.** Maximierte und minimierte Fenster bleiben in Ruhe; ihre Grösse kommt
  nicht von einer Zone.
- **Beim Wechsel des aktiven Layouts.** Fenster des betroffenen Monitors, die im neuen Layout auf keiner
  Zone mehr liegen, werden eingesammelt. Bewusst nur beim tatsächlichen Wechsel: während des Bearbeitens
  speichert die Oberfläche nach jedem Zug, und ein Auffang bei jedem Speichern zöge die Fenster unter den
  Händen weg.

Ausgeschlossene Fenster fasst die Auffangzone nie an. Wie das Einrasten selbst läuft der Auffang genau dann, wenn
mindestens ein Layout aktiv ist; einen eigenen Schalter dafür gibt es nicht.

### Welche Fenster von selbst angefasst werden

Von sich aus platziert die Anwendung nur echte Programmfenster. Ein Fenster ist davon ausgenommen, sobald
eines zutrifft:

- Die Fensterklasse gehört zu einem kurzlebigen Fenster: Win32-Menü (`#32768`), Dialogklasse (`#32770`),
  Tooltip, Aufklappliste, Vorschlagsliste oder ein Popup-Wirt von XAML und WinUI.
- Es hat keine Titelleiste. Kontextmenüs und Aufklappfenster jeder Oberfläche fallen darunter.
- Es gehört einem anderen Fenster (Dialog, Palette, Hinweisfenster).
- Seine Grösse lässt sich nicht ändern — es kann eine Zone gar nicht füllen.
- Es hat keine Maximieren-Schaltfläche. Daran trennt Windows den Dialog vom Programmfenster; der
  Kopierdialog des Explorers etwa bleibt so unangetastet.
- Es ist kleiner als 200 × 120 Pixel.

Die Regel gilt ausschliesslich für den automatischen Weg — Auffang in der Auffangzone, gemerkte Positionen,
Auffang nach einem Layoutwechsel und das Nachziehen bei geänderten Zonen. Ziehst du ein solches Fenster
selbst auf eine Zone oder drückst du ein Zonenkürzel, rastet es weiterhin ein: dort ist die Absicht
eindeutig. Jede Ablehnung steht mit ihrer Begründung im Protokoll.

## Gemerkte Fensterpositionen

Sobald die Snap-Funktion aktiv ist, merkt sich die Anwendung für jedes platzierte Fenster den Monitor, die
Zone und das Fensterrechteck und stellt diesen Stand beim nächsten Öffnen desselben Fensters wieder her.
Ändert sich die Auflösung, wird die gemerkte Lage anteilig umgerechnet. Der Katalog fasst höchstens 500
Einträge und liegt neben den Einstellungen.

Ist die gemerkte Zone im aktiven Layout noch vorhanden, kehrt das Fenster in diese Zone zurück, auch wenn
sich deren Fläche inzwischen geändert hat; erst ohne Zone zählt die gemerkte Lage. Ein kleines Fenster bleibt
beim Wiederherstellen klein.

Fehlt eine gemerkte Position, greift die [Auffangzone](#auffangzone), sofern eine festgelegt ist.

Zurückgelegt wird ein Fenster nur beim Erscheinen; ein blosser Fokuswechsel verschiebt nie ein Fenster,
das gerade von Hand irgendwohin gestellt wurde. Erkannt wird ein Fenster an Programm, Fensterklasse und
Fensterart, nicht am Titel. Mehrere Fenster
desselben Programms teilen sich deshalb einen Eintrag. Ausgeschlossene Fenster kommen gar nicht erst in den
Katalog. Ein Fenster im Vollbild — rahmenlos über dem ganzen Monitor — wird nicht gemerkt; sein Eintrag
bleibt auf dem Stand vor dem Vollbild, damit ein Browser, der im Vollbild geschlossen wurde, beim
nächsten Start in seiner Zone erscheint und nicht monitorfüllend.

Unter **Verhalten** lässt sich das Merken abschalten und die Anzahl der gemerkten Positionen ablesen;
**Gemerkte Positionen verwerfen** löscht sämtliche Einträge. Ausgeschaltet bleiben bestehende Einträge
erhalten, werden aber weder angewendet noch ergänzt. Einzelne Einträge lassen sich nicht ansehen oder
gezielt löschen.

## Zonen-Vollbild

Schaltet ein Fenster, das in einer Zone liegt, auf Vollbild, wird es auf die Fläche dieser Zone
zurückgeholt statt auf den ganzen Monitor. Der Schalter **Vollbild in der Zone halten** steht unter
**Verhalten** in der Karte **Vollbild** und ist ausgeschaltet, weil er das gewohnte Verhalten von Windows
ändert.

Möglich ist das, weil ein Browser oder Videoplayer im Vollbild keinen Exklusivmodus der Grafikkarte
anfordert, sondern sein Fenster randlos über die volle Monitorfläche legt. Das bleibt ein gewöhnliches
Fenster und lässt sich setzen. Das Programm bleibt dabei in seinem Vollbildzustand: Twitch, YouTube und die
üblichen Player legen Bild und Bedienelemente auf die kleinere Fläche aus, so wie sie es auf einem
kleineren Bildschirm täten.

Erkannt wird das Vollbild daran, dass das Fenster die **ganze** Monitorfläche einnimmt — nicht die
Arbeitsfläche. Ein maximiertes Fenster endet an der Taskleiste und fällt damit von selbst heraus; ist die
Taskleiste automatisch ausgeblendet, decken sich beide Flächen, weshalb zusätzlich geprüft wird, dass das
Fenster nicht maximiert ist.

Angefasst wird nur ein Fenster, das vorher in einer Zone eingerastet lag, gemessen mit derselben Toleranz
wie beim Auffang in der Auffangzone. Ein über mehrere Zonen gezogenes Fenster kehrt in deren gemeinsame
Fläche zurück. Was frei auf dem Bildschirm liegt, geht weiterhin auf den ganzen Monitor: ohne Zone gäbe es
keinen Bezugspunkt, und das Programm würde Fenster an Stellen zwingen, die niemand gewählt hat. Ein
Ausschluss gilt auch hier und ist wie überall stärker.

Setzt ein Programm sein Fenster nach dem Umschalten noch einmal selbst auf den Monitor, wird es erneut
zurückgeholt. Das gilt auch für einen halben Rückfall: Chromium-Browser stellen bei jedem
Aktivierungswechsel einen Teil ihrer Vollbildgrösse wieder her, oft nur die Breite, sobald ein anderes
Fenster den Fokus bekommt. Solange dem Fenster Titelleiste und Griffrahmen fehlen, versteht es sich noch
als Vollbild und wird in die Zone zurückgesetzt; erst wenn der Rahmen zurück ist, gilt das Vollbild als
verlassen. Ein misslungenes Setzen lässt die gemerkte Zone ebenfalls stehen, damit der nächste Rückfall
wieder dorthin führt. Damit daraus kein Dauerkampf wird, ist die Zahl der Versuche je Vollbildsitzung
begrenzt (erweiterte Einstellung **Versuche beim Zonen-Vollbild**, Vorgabe 5). Ist sie erreicht, behält
das Fenster sein Monitorvollbild, und der Grund steht als WARN im Protokoll — ebenso jedes Setzen, das
Windows abgelehnt hat, und als INFO jedes gelungene. Diese Zeilen erscheinen auch ohne `--verbose`; nur die
Spur je Fensterereignis bleibt dem ausführlichen Protokoll vorbehalten. Nach fünf Sekunden ohne Korrektur
beginnt die Zählung von vorn.

Beim Setzen weicht der Weg an zwei Stellen vom gewöhnlichen Einrasten ab, beide am laufenden System
gemessen:

1. **Die Grösse wird erzwungen.** Ein Fenster im Vollbild legt seinen Griffrahmen ab (`WS_THICKFRAME`
   fehlt) und gälte sonst als Fenster fester Grösse, das nur in der Zone zentriert statt auf sie
   gestreckt würde.
2. **Das Fenster verliert sein Einspruchsrecht** (`SWP_NOSENDCHANGING`). Windows fragt ein Fenster vor
   jeder Grössenänderung über `WM_WINDOWPOSCHANGING`, und das Fenster darf die vorgeschlagenen Werte
   darin abändern. Genau das tut ein Browser im Vollbild: er klemmt sich auf die Monitorfläche zurück.
   Ohne das Flag blieb die Platzierung wirkungslos, und das Nachmessen meldete eine «Mindestgrösse» in
   Monitorgrösse — der Grund, aus dem der erste Anlauf des Zonen-Vollbilds bei Chromium-Browsern
   scheiterte.

Beides gilt ausschliesslich für das Zonen-Vollbild. Beim gewöhnlichen Einrasten behält ein Fenster sein
Einspruchsrecht: dort ist eine gemeldete Mindestgrösse eine echte Eigenschaft, und sie zu übergehen
hiesse, ein Fenster kleiner zu zwingen, als es sich zeichnen kann. Nachgemessen wird beim Zonen-Vollbild
mit acht statt zwei Pixeln Toleranz: ein Vollbildfenster korrigiert seine Grösse nach dem Setzen gern um
ein paar Pixel, und das ist kein Fehlschlag.

Nicht erreichbar ist echtes Exklusivvollbild, wie es Spiele über DirectX anfordern — diesen Bildschirmmodus
vergibt der Grafiktreiber. Spiele im randlosen Fenster liegen dagegen als gewöhnliches Fenster vor.

## Monitore

Die Seite **Monitore** zeigt einen Monitor auf einmal: **‹** und **›** blättern («Monitor 1 von 2»), in der Mitte steht das aktive Layout als grosse Vorschau mit dem Verweis **Layout «…» · bearbeiten**. Darunter das Feld **Name** (leer stellt die automatische Bezeichnung «Monitor n» wieder her), **Auf Monitor zeigen** blendet den verwendeten Namen drei Sekunden lang auf jedem Bildschirm ein, **Reihenfolge ⌵** verschiebt den Monitor nach oben oder unten. Die Reihenfolge entscheidet, welche Auffangzone wirksam ist. Monitornamen werden bevorzugt aus dem aktiven Displaypfad und den EDID-Daten gelesen.

**Erkannte Werte** (aufgeklappt) nennt Skalierung, Auflösung, Arbeitsfläche und, sofern Windows die EDID liefert, die Diagonale sowie den gemeldeten Namen mit der Kennung aus der EDID. **Windows-Einstellungen öffnen** (zugeklappt) führt zu Anzeige, Textgrösse und Taskleiste. Ändern lassen sich diese Werte nur in Windows selbst: Windows 11 stellt normalen Desktopanwendungen keine unterstützte Schnittstelle bereit, um Anzeigeskalierung, Textskalierung oder monitorweise Taskleisten- und Icongrössen zu setzen; Zone Manager verwendet dafür bewusst keine Explorer-Injektion, keine privaten DPI-Pakete und keine undokumentierten Registry-Werte.

Monitore werden zur Laufzeit beobachtet. Anstecken, Abstecken, eine geänderte Auflösung, Skalierung oder Drehung und eine verschobene Taskleiste werden nach einer kurzen Ruhepause übernommen: Zonen, Overlays und Zielflächen werden neu aufgebaut, die Statuszeile meldet den neuen Stand, und Fenster, die im neuen Bild auf keiner Zone mehr liegen, werden in die Auffangzone geholt. Ein Neustart ist nicht nötig.

Wiedererkannt wird ein Monitor an seiner Hardware: Hersteller, Modell und, sofern der Monitor eine liefert, Seriennummer aus der EDID. Hängt derselbe Monitor an einem anderen Anschluss oder hinter einem anderen Treiber, ändert sich der Anzeigepfad von Windows; die Layouts, der eigene Name und die Position in der Reihenfolge werden dann übernommen, und die Statuszeile nennt das. Zwei baugleiche Monitore ohne Seriennummer bleiben getrennt, weil eine Verwechslung schlimmer wäre als ein neues Standardlayout. Verwaiste Namen und Reihenfolgeeinträge, die zu keinem Monitor und keinem Layout mehr gehören, werden beim Abgleich entfernt.

Platzhalteranzeigen von Windows sind keine Monitore: `WinDisc` meldet Windows, solange die Sitzung gesperrt oder per Fernzugriff getrennt ist, `Default_Monitor` («Generic PnP Monitor»), solange alle echten Monitore aus oder im Standby sind. Beide werden beim Einlesen übersprungen; Layouts, die frühere Versionen dafür angelegt hatten, verschwinden beim nächsten Laden der Konfiguration.

Je Monitorkombination merkt sich das Programm, welche Layouts zuletzt aktiv waren: am Dock mit zwei Monitoren ein anderes als unterwegs mit dem Laptopdisplay allein. Kehrt eine Kombination zurück, werden ihre Layouts wieder aktiviert, ohne dass jemand umschalten muss. Ein Wechsel des aktiven Layouts gilt immer für die gerade verbundene Kombination.

Die Monitorauswahl enthält auch Monitore, die gerade **nicht verbunden** sind, solange für sie noch mindestens ein Layout gespeichert ist. Sie sind als solche gekennzeichnet und stehen am Ende. Der Grund: solche Layouts erscheinen weiterhin als Ziel einer Zuordnung, wären ohne diesen Eintrag aber nirgends erreichbar und liessen sich nicht mehr löschen. Bei einem nicht verbundenen Monitor darf deshalb auch sein letztes Layout gelöscht werden — danach verschwindet der Monitor. Bei einem verbundenen Monitor bleibt das letzte Layout weiterhin geschützt.

## Einstellungen

Die Einstellungen sind auf zwei Seiten verteilt: **Verhalten** mit den Untertabs **Beim Ziehen**, **Darstellung**, **Abstände**, **Fenster merken** und **Tastenkürzel**, und **Programm** (Erscheinungsbild, Autostart, Updates, Administratorrechte, Installation, Sicherung, Frühere Stände, Fensterhelfer, Zurücksetzen). Alle Einstellungen sind sichtbar; einen Standard-/Experten-Schalter gibt es seit dem 05.09.2026 nicht mehr. Jede Zeile trägt links die Beschriftung mit **?**, rechts das Steuerelement; das **?** erklärt beim Darüberfahren, was die Einstellung tut, mit Beispiel und Wertebereich. Jeder Wert hat einen sicheren Standard und einen begrenzten Bereich; ungültige Werte werden schon beim Eingeben auf den Bereich gestutzt und beim Laden der Datei abgewiesen.

**Beim Ziehen**

- **Zonen anzeigen auf** bestimmt, wo die Zonen beim Ziehen erscheinen:
  - **Alle Monitore** — gleichzeitig auf jeder Anzeige.
  - **Monitor beim Ziehbeginn** — nur auf dem Bildschirm, auf dem das Fenster angefasst wurde. Die Zonen
    bleiben dort; wandert der Zeiger auf einen anderen Bildschirm, sieht er dort keine Zonen.
  - **Monitor unter dem Mauszeiger** — die Zonen wandern mit. Sie erscheinen immer auf dem Bildschirm, auf
    dem der Zeiger gerade steht, und verschwinden auf allen übrigen. Liegt der Zeiger kurz auf keinem
    Bildschirm — über der Taskleiste oder in der Lücke zwischen zwei unterschiedlich hohen Monitoren —,
    bleibt die bisherige Anzeige stehen, statt zu flackern.
- **Zonen einblenden**: sofort oder nur mit Umschalttaste. Die Umschalttaste darf auch erst während des
  Ziehens gedrückt werden; wird sie losgelassen, verschwinden die Zonen wieder, bis sie erneut gedrückt wird.
- **Zonennamen anzeigen** und die Karte **Feinabstimmung**: Anzeigeverzögerung, Fenster nach dem Einrasten in den Vordergrund holen, Grösse beim Herausziehen wiederherstellen.

**Darstellung**: Farbe der Zonen (Farbfeld und Hexwert), Deckkraft, Beschriftung, dazu der **Overlay-Stil** mit Rahmenbreite, Eckenradius, Hervorhebung (Farbe und Deckkraft der Zielzone) und Schriftgrösse der Beschriftung. Rechts zeigt die Karte **Vorschau** ein Overlay mit drei Zonen, das jede Änderung sofort übernimmt.

**Abstände**: Abstand zum Bildschirmrand (links, oben, rechts, unten in Pixel), Abstand zwischen Zonen und Andocken im Editor in ganzen Prozent. Aussen- und Zonenabstand gelten für Vorschau **und** Fenster: ein Fenster landet genau auf der Fläche, die das Overlay zeigt, auch über Zuordnungen, Auffangzone und Layoutwechsel. Neben jedem Prozentregler steht der abgeleitete Pixelwert als `≙ n px`.

**Fenster merken**: **Fensterpositionen merken** schaltet den Positionskatalog ein und aus; darunter stehen die Anzahl der Einträge und der Verweis **alle verwerfen** (mit Rückgängig). Siehe [Gemerkte Fensterpositionen](#gemerkte-fensterpositionen). **Vollbild in der Zone halten** begrenzt das Vollbild eines eingerasteten Fensters auf seine Zone; ausgeschaltet, siehe [Zonen-Vollbild](#zonen-vollbild). Darunter die Karten **Feinabstimmung Platzieren** und **Schutz und Zeiten**, siehe Tabelle.

**Tastenkürzel**: Zonenkürzel aktiv, Zusatztasten mit AltGr-Warnung bei `Ctrl + Alt`, und die Tabelle aller Kürzel.

**Programm**: System-, helles oder dunkles Theme (Systemänderungen werden ohne Neustart übernommen); Autostart pro Benutzer über eine Anmeldeaufgabe der Windows-Aufgabenplanung, die das Programm bereits erhöht startet, sodass bei der Anmeldung **keine** UAC-Abfrage erscheint (schlägt das Anlegen fehl, weicht das Programm auf den Registry-Eintrag `Run` aus und meldet das im Protokoll; eingetragen ist immer nur einer der beiden Wege); Updates mit Schalter «beim Start» und **Jetzt suchen**; Administratorrechte; Installation; Sicherung mit **Exportieren** und **Importieren**; **Frühere Stände**; die gestrichelte Karte **Erweitert: Fensterhelfer ohne Administratorrechte** mit dem dreischrittigen **Assistent …**; ganz unten **Alle Einstellungen zurücksetzen** – mit Rückgängig, ohne Nachfrage. Zurückgesetzt werden Abstände, Darstellung, Verhalten und alle Feinabstimmungen; Erscheinungsbild, Autostart, Rechte, Updatesuche, Layouts, Zuordnungen und die Liste «In Ruhe lassen» bleiben.

### Feinabstimmung

| Untertab · Karte | Einstellung | Bereich (Standard) | Wirkung |
|---|---|---|---|
| Beim Ziehen · Feinabstimmung | Anzeigeverzögerung | 0–1000 ms (0) | Zonen erscheinen erst, wenn das Ziehen so lange dauert; kurze Züge bleiben ohne Aufblitzen. |
| | Fenster nach dem Einrasten in den Vordergrund holen | aus | Ein per Zuordnung, Auffangzone oder Kürzel gesetztes Fenster erhält den Fokus. |
| | Grösse beim Herausziehen wiederherstellen | aus | Ein aus der Zone gezogenes, nirgends abgelegtes Fenster bekommt seine frühere Grösse zurück. |
| Darstellung · Overlay-Stil | Beschriftung | Nummer und Name / nur Nummer / nur Name | Inhalt der Beschriftungsfläche. |
| | Rahmenbreite, Eckenradius, Schriftgrösse | 1–6 px (1), 0–24 px (4), 10–24 pt (13) | Optik der Zonen im Overlay. |
| | Hervorhebung | #RRGGBB oder leer, 10–90 % (36) | Farbe und Deckkraft der Zone unter dem Mauszeiger. |
| Fenster merken · Feinabstimmung Platzieren | Fenster mit fester Grösse | zentrieren / oben links / nicht anfassen | Dialoge, die keine Zone füllen können. |
| | Toleranz beim Nachmessen | 0–10 px (2) | Ab welcher Abweichung ein zweiter Anlauf und eine Meldung folgen. |
| | Toleranz für «eingerastet» | 8–80 px (40) | Wie nah ein Fenster an den Zonenkanten liegen muss, damit Auffangzone und Layoutwechsel es in Ruhe lassen. |
| | Neue Fenster in der Auffangzone auffangen | ein | Aus: die Auffangzone dient nur dem Layoutwechsel. |
| | Gemerkte Zone vor gemerkter Lage | ein | Aus: ein Fenster kehrt pixelgenau an seine letzte Lage zurück. |
| | Maximierte Fenster maximiert wiederherstellen | ein | |
| | Katalog gemerkter Positionen | 50–2000 (500) | Obergrenze des Positionsgedächtnisses. |
| | Wartezeit vor dem Beurteilen neuer Fenster | 0–2000 ms (0) | Für Programme, die ihr Fenster spät fertig aufbauen. |
| | Abstand zwischen Regelversuchen | 50–2000 ms (250) | |
| Tastenkürzel | Zonenkürzel aktiv, Zusatztasten | ein; Ctrl + Shift / Ctrl + Alt / Alt + Shift / Ctrl + Win | Gilt für alle Zonenkürzel; der Not-Aus bleibt fest. `Ctrl + Alt` blockiert AltGr und wird in der Oberfläche mit einer Warnung angeboten. |
| Fenster merken · Schutz und Zeiten | Schutzschalter des Verschiebe-Hooks | 100–5000 Ereignisse je 10 s (400) | Darüber hält das Programm das Einrasten an. |
| | Wachhund für hängende Ziehvorgänge | 5–600 s (120) | Danach werden die Zonen eingezogen, was auch immer Windows meldet. |
| | Versuche beim Zonen-Vollbild | 1–20 je Sitzung (5) | Danach behält ein Fenster, das sich wiederholt zurücksetzt, sein Monitorvollbild. |

Wie Titel, Beschriftungen und Erklärungen dabei aufgebaut sind, steht verbindlich in [ui-richtlinien.md](ui-richtlinien.md).

## Installation

Das Programm läuft ohne Installation aus dem Verzeichnis, in dem die Datei liegt. Das genügt, ist im
Downloadordner aber unaufgeräumt und erschwert Updates.

**Programm → Installation** kopiert die Programmdatei nach `%ProgramFiles%\ZoneManager`, legt eine
Verknüpfung im Startmenü an und trägt das Programm in «Apps und Features» ein. Danach startet es von dort
neu. Dasselbe leistet `ZoneManager.exe --install` auf der Kommandozeile.

Entfernt wird es über «Apps und Features» wie jedes andere Programm, oder mit
`ZoneManager.exe --uninstall`. Die Einstellungen unter `%APPDATA%\SnapZones` bleiben dabei erhalten — sie
gehören dem Benutzer, und eine Neuinstallation soll sie wiederfinden. Wer sie loswerden will, löscht das
Verzeichnis von Hand.

Es gibt bewusst kein getrenntes Setup-Programm: es müsste die 66 MB grosse Programmdatei ein zweites Mal
enthalten und die Auslieferung verdoppeln. Installieren und Entfernen sind deshalb Modi derselben Datei.

Beides schreibt nach `%ProgramFiles%` und in `HKEY_LOCAL_MACHINE` und verlangt darum Administratorrechte.
Läuft das Programm gewöhnlich berechtigt — die Voreinstellung, siehe [Rechte](#rechte) —, erledigt die
Installation ein zweiter, erhöhter Prozess derselben Programmdatei (`--install --silent --no-launch`);
Windows fragt dafür einmal nach. Das installierte Programm startet anschliessend der gewöhnlich
berechtigte Prozess, damit es nicht die Administratorrechte des Hilfsprozesses erbt. Ist das Programm
bereits erhöht, läuft die Installation direkt.

## Updates

Unter **Programm → Updates** steht die installierte Version, daneben **Nach Updates suchen** und
**Update installieren und neu starten**.

Die Suche fragt die Veröffentlichungen des Projekts ab und sendet dabei nichts ausser der Anfrage selbst —
keine Version, keine Rechnerkennung, keine Zählung. Sie ist deshalb voreingestellt aus und läuft sonst nur
auf Anstoss; **Beim Start nach Updates suchen** schaltet sie ein. Ein erfolgloser Blick beim Start bleibt
still im Protokoll, gemeldet wird nur ein gefundenes Update.

Angeboten wird ausschliesslich eine eindeutig höhere Version im Schema `YYYY.MMDD.NN`. Lässt sich eine der
beiden Versionen nicht lesen, wird nichts angeboten, statt auf Verdacht eine fremde Datei vorzuschlagen.
Heruntergeladen wird nur über HTTPS aus der Release-Ablage des Projekts; ein Verweis auf eine andere Adresse
wird abgelehnt. Die geladene Datei muss ausserdem genau die angekündigte Grösse haben, sonst wird sie
verworfen — eine abgebrochene Übertragung sieht sonst wie eine vollständige Datei aus.

Der Austausch geht in zwei Hälften, die in zwei Prozessen laufen. Die laufende Anwendung lädt
Programmdatei und Fensterhelfer nach `%LOCALAPPDATA%\ZoneManager\updates` und prüft beide; ihre eigene
Programmdatei fasst sie dabei nicht an. Dann startet sie die bereitgestellte Datei im Übernahmemodus
(`--apply-update <Programmdatei> --wait-for-pid <Prozess>`) und beendet sich. Der neue Prozess wartet
auf ihr Ende, schiebt die bisherige Datei als `ZoneManager.exe.previous.<Zeitstempel>` beiseite, legt die
neue an ihren Platz und startet sie von dort. Liegt das Programm unter `%ProgramFiles%`, holt sich der
Übernahmeprozess dafür einmal Administratorrechte. Scheitert die Übernahme, kommt die alte Datei zurück
und wird gestartet; es bleibt nie eine halb ersetzte Programmdatei liegen. Beim nächsten Start werden
die beiseitegeschobenen Dateien und das Bereitstellungsverzeichnis gelöscht.

Diese Reihenfolge ist zwingend. Die Einzeldatei lädt viele ihrer Bausteine erst bei Bedarf über den Pfad
der eigenen Programmdatei nach. Wird sie unter dem laufenden Prozess weggeschoben, scheitert jedes
spätere Nachladen mit einer `FileNotFoundException` — beim Beenden, beim ersten Fehlerdialog, bei der
nächsten Updatesuche. Bis zum 04.09.2026 wurde die laufende Datei sofort nach dem Download ersetzt, und
genau so endete das Programm mehrfach.

Ohne digitale Signatur kann das Programm die geladene Datei nur an Herkunft und Grösse prüfen, nicht an
einer Signatur. Wer das nicht will, lädt Releases von Hand herunter und lässt die Suche ausgeschaltet.

## Rechte

Windows teilt laufende Programme in Vertrauensstufen ein. Ein Programm darf nur Fenster verschieben, die
derselben oder einer niedrigeren Stufe angehören. Alltägliche Fenster — Browser, Editor, Explorer — gehören
zur gewöhnlichen Stufe; der Taskmanager, der Registrierungs-Editor und alles «als Administrator» Gestartete
stehen darüber.

Unter **Programm → Rechte** steht deshalb zur Wahl:

- **Nur wenn nötig** (Voreinstellung). Das Programm startet ohne UAC-Abfrage. Trifft es später auf ein
  Fenster, das es nicht bewegen darf, fragt es **einmal je Sitzung** nach und startet auf Wunsch erhöht neu.
  Wer ablehnt, arbeitet normal weiter; nur diese eine Sorte Fenster bleibt unberührt.
- **Immer beim Start**. Das bisherige Verhalten: jeder Start geht über die UAC-Abfrage, dafür kommt im
  Betrieb keine Nachfrage mehr.

Die Voreinstellung ist die zurückhaltende, weil ein dauerhaft erhöhter Prozess eine grosse Angriffsfläche
ist: jeder ausnutzbare Fehler darin — auch in einer Abhängigkeit — wäre eine lokale Rechteausweitung.

Die Umstellung wirkt beim nächsten Start; ein laufendes Programm kann seine Rechte weder ablegen noch
nachträglich erweitern. Ist der Autostart eingeschaltet, wird die Anmeldeaufgabe mit umgestellt: sie startet
nur dann erhöht, wenn das Programm es auch sonst tut — sonst wäre der Autostart mächtiger als jeder Start von
Hand.

Die Rechte-Einstellung wird vor dem Laden der Oberfläche gelesen, also aus `settings.json` allein für dieses
eine Feld. Lässt sich die Datei nicht lesen, gilt die Voreinstellung.

### Fensterhelfer ohne Administratorrechte

Windows kennt eine dritte Möglichkeit: `uiAccess`. Ein Programm mit diesem Merkmal darf höher berechtigte
Fenster bewegen, **ohne** selbst Administratorrechte zu besitzen. Windows verlangt dafür zwingend zweierlei
— eine gültige Authenticode-Signatur und einen geschützten Installationsort. Fehlt eines von beiden, startet
die Datei gar nicht (gemessen: Win32-Fehler 740).

Weil das Hauptprogramm auch unsigniert und aus jedem Verzeichnis starten können muss, trägt es dieses
Merkmal nicht. Stattdessen liegt neben ihm ein eigenes, rund 10 MB grosses Hilfsprogramm
`ZoneManager.Helper.exe`. Es kann genau eine Sache: ein Fenster an eine bestimmte Stelle setzen. Es öffnet
keine Datei, startet keinen Prozess, sendet keine Fenstermeldungen und keine Eingaben, nimmt nur über eine
benannte Pipe Befehle entgegen und endet, sobald die Verbindung abreisst.

Abgesichert ist der Weg dreifach: die Pipe trägt einen bei jedem Lauf zufälligen Namen und eine
Zugriffsliste, die nur den angemeldeten Benutzer zulässt; der Helfer prüft, dass am anderen Ende wirklich
`ZoneManager.exe` aus seinem eigenen Verzeichnis sitzt; und das Protokoll kennt genau zwei Befehle, deren
Zahlen streng geprüft werden. Der Helfer wird erst beim ersten Fenster gestartet, das ihn wirklich braucht.

Unter **Programm → Fensterhelfer ohne Administratorrechte** wird ein selbst ausgestelltes Zertifikat
erzeugt, in die Vertrauensspeicher der lokalen Maschine gelegt und der Helfer damit signiert. Das ist
freiwillig; ohne diesen Schritt bleiben die beiden Wahlmöglichkeiten oben die einzigen.

Die Karte nennt den Zustand in zwei Worten — **Eingerichtet** oder **Nicht eingerichtet** — und darunter
ausführlich, was das im Einzelnen heisst; eine zweite Zeile sagt dasselbe für den Fensterhelfer. Darunter
steht genau **eine** Schaltfläche, die sich nach dem Zustand richtet: **Zertifikat einrichten**, solange
keines besteht, und **Zertifikat entfernen**, sobald eines eingerichtet ist. Der Zustand wird bei jedem
Öffnen der Seite neu gelesen, weil sich am Zertifikatspeicher auch ausserhalb des Programms etwas ändern
kann.

**Was das bedeutet.** Der Rechner vertraut anschliessend allem, was mit diesem Zertifikat signiert wurde.
Der private Schlüssel liegt auf der Maschine. Wer ihn erbeutet, kann Schadsoftware so signieren, dass
Windows sie für vertrauenswürdig hält. Zwei Dinge halten den Schaden klein: das Zertifikat ist **keine**
Zertifizierungsstelle und kann keine weiteren Zertifikate ausstellen, und sein Schlüssel ist nicht
exportierbar. Ein Restrisiko bleibt. Das Zertifikat gilt zudem nur auf diesem Rechner — weitergeben lässt
sich das Programm damit nicht.

Das Einrichten und das Entfernen verlangen einmalig Administratorrechte, weil sie in den Zertifikatspeicher
der lokalen Maschine schreiben. Läuft das Programm gewöhnlich berechtigt, übernimmt ein zweiter, erhöhter
Prozess derselben Programmdatei die Aktion (`--install-certificate` beziehungsweise
`--remove-certificate`); Windows fragt dafür einmal nach, und das Ergebnis steht danach in der Karte und
im Protokoll. Gearbeitet wird über die Windows-eigene PowerShell (`New-SelfSignedCertificate`,
`Set-AuthenticodeSignature`); ein externes Werkzeug wird nicht gebraucht.

**Entfernen** nimmt das Zertifikat aus allen drei Speichern. Der Helfer startet danach nicht mehr, und das
Programm fragt bei Bedarf wieder nach eigenen Administratorrechten.

## Tastenkürzel

Die Tasten sind fest, die Zusatztasten wählbar; die Kürzel wirken auf das Fenster im Vordergrund und gelten nur,
solange das Einrasten aktiv ist. Belegt ein anderes Programm ein Kürzel bereits, meldet die Statuszeile das beim Start.
Die Tabelle zeigt die Voreinstellung `Ctrl + Shift`.

| Kürzel | Wirkung |
|---|---|
| `Ctrl + Shift + Links` / `Rechts` | Fenster eine Zone zurück oder weiter, über Monitorgrenzen hinweg. Liegt es in keiner Zone, beginnt es bei der ersten beziehungsweise letzten Zone seines Monitors. |
| `Ctrl + Shift + 1` bis `9` | Fenster in die Zone mit dieser Nummer auf seinem Monitor. |
| `Ctrl + Shift + Rücktaste` | Fenster zurück an die Stelle vor dem letzten Einrasten. |
| `Ctrl + Alt + Shift + F12` | Einrasten anhalten und wieder starten (Not-Aus). |
| `Strg + Z` / `Strg + Y` | Im Layouteditor: Änderung zurücknehmen oder wiederherstellen. |

Der Untertab **Tastenkürzel** der Seite **Verhalten** listet dieselben Kürzel.

## Not-Aus und Schutzschalter

`Ctrl + Alt + Shift + F12` schaltet das Einrasten um: einmal gedrückt legt es Hook und Overlays still, erneut
gedrückt läuft es weiter. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber,
keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern
oder ungewöhnlich vielen Hook-Ereignissen (400 Verschiebe-Ereignisse in zehn Sekunden). Der Diagnosemodus läuft
bewusst ohne Elevation.

Der Hook für Positionsgedächtnis und Zonen-Vollbild hört jede Lageänderung jedes Fensters und erreicht
seine Grenze (2000 Ereignisse in zehn Sekunden) auch bei harmloser Last, etwa einem zügig gezogenen Fenster
neben laufenden Animationen. Ein Stopp wegen dieser Grenze hebt sich nach zehn Sekunden von selbst wieder
auf, die Statuszeile nennt die Wartezeit. Erst beim vierten Stopp innerhalb von fünf Minuten bleibt das
Einrasten pausiert — dann ist es keine Last mehr, sondern eine Rückkopplung. Ein Stopp nach einem Fehler
wird nie von selbst aufgehoben.

Der Zustand ist immer sichtbar: die Statuszeile am unteren Fensterrand zeigt «Einrasten aktiv», «Kein aktives
Layout» oder «Einrasten pausiert», daneben die letzte Meldung des Programms (Speicherfehler, Namenskonflikte,
pausierte Regeln). Das Infobereichsmenü nennt denselben Zustand. Ist das Einrasten pausiert, schalten die
Schaltfläche **Einrasten wieder aktivieren** in der Statuszeile, der gleichnamige Menüpunkt im Infobereich oder
der Hotkey es wieder ein; ein Neustart ist dafür nicht mehr nötig.

## Beenden

Das Schliessen des Fensters blendet die Anwendung nur in den Infobereich aus. Beendet wird sie über **Rechtsklick auf das Infobereichssymbol → Beenden**.

Beim Beenden werden zuerst Hooks, Zeitgeber und die Platzierungs-Engine stillgelegt, damit keine neue Arbeit mehr anfällt; anschliessend werden Einstellungen und Fensterplatzierungen gespeichert. Für diesen Abschluss gilt eine Zeitgrenze von fünf Sekunden. Lässt sich in dieser Zeit nicht vollständig speichern, meldet ein Hinweisfenster die Ursache und fragt, ob trotzdem beendet werden soll — die Anwendung bleibt nie ohne sichtbare Begründung geöffnet.

Scheitert danach das Herunterfahren von WPF selbst — das geschieht, wenn die Programmdatei nicht mehr am Platz liegt und WPF dafür noch Bausteine nachladen will —, räumt das Programm Hooks, Infobereichssymbol und Einzelinstanz von Hand auf und beendet den Prozess direkt. Bis zum 02.09.2026 blieb es in diesem Fall mit der Meldung «Das Beenden ist fehlgeschlagen» im Infobereich stehen.

Eine zweite Instanz, die mit `--exit` gestartet wird, bittet die laufende um genau dieses geordnete Beenden und endet selbst sofort. So tauscht der Build die Programmdatei aus, ohne sie unter dem laufenden Prozess wegzuziehen.

## Diagnose

```powershell
ZoneManager.exe --diagnostics
```

Das Protokoll liegt unter `%LOCALAPPDATA%\SnapZones\logs\snapzones.log` und rotiert bei einem Megabyte in fünf
Generationen (`snapzones.log.1` bis `.5`). Geschrieben werden INFO, WARN, ERROR und FATAL, bei Ausnahmen mit
Aufrufstapel und innerer Ausnahme. Die ausführlichen DEBUG-Zeilen zu jedem Fensterereignis und Ziehvorgang
erscheinen nur, wenn das Programm mit `--verbose` gestartet wird; sonst verdrängen sie innerhalb eines Tages
jeden Fehler aus dem Protokoll. Ein unbehandelter Fehler wird einmal protokolliert, die Einstellungen werden
gesichert, und ein Hinweisfenster nennt Ursache und Protokollpfad; Folgefehler während der Sicherung werden nur
gezählt statt erneut behandelt.

Die Diagnose liest Konfigurationsstatus, Monitore, DPI und Autostartstatus. Sie registriert keinen Fenster-Hook und verändert weder Einstellungen noch Registry.

Alle drei Sekunden prüft das Programm, ob seine eigene Programmdatei noch unverändert am Platz liegt. Die
Einzeldatei lädt Bausteine erst bei Bedarf über diesen Pfad nach; wird sie ersetzt oder entfernt — durch
einen Build, ein Kopieren von Hand, ein fremdes Update —, scheitert von da an jedes Nachladen. Bestätigt
sich der Austausch in zwei Prüfungen nacheinander, speichert das Programm, legt alles still und startet in
die neue Datei hinüber (`--wait-for-pid` lässt den Nachfolger auf das Ende des Vorgängers warten); fehlt
die Datei ganz, beendet es sich nach dem Speichern. Beides steht als WARN im Protokoll. Am 03. und
04.09.2026 endete das Programm dreimal mit einer `FileNotFoundException` für eine .NET-Assembly, jeweils
Minuten nach einem Build — der Fall, den diese Prüfung seither abfängt.

Holt das [Zonen-Vollbild](#zonen-vollbild) ein bestimmtes Programm nicht zurück, zeigt

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\measure-fullscreen-window.ps1
```

bei geöffnetem Vollbild, wie dessen Fenster tatsächlich aussieht: Rechteck, Monitorfläche, ob Windows es als
maximiert führt und welche Stile es trägt. Erscheint das Fenster dort gar nicht, fordert das Programm ein
Exklusivvollbild an und ist von aussen nicht erreichbar. Das Skript liest ausschliesslich; es startet,
schliesst und verschiebt nichts.

## Einschränkungen

- Nur Windows 11 x64.
- Wird die Windows-UAC-Abfrage bei «Immer beim Start» abgebrochen, startet die Anwendung nicht.
- Fenster höher berechtigter Programme lassen sich nur einrasten, wenn das Programm selbst erhöht läuft oder der signierte Fensterhelfer eingerichtet ist.
- Der Fensterhelfer ist mit einem selbst ausgestellten Zertifikat signiert und funktioniert deshalb nur auf dem Rechner, auf dem es eingerichtet wurde.
- Nicht rechteckige oder überlappende Zonen und virtuelle Desktops sind noch nicht enthalten.
- Zwei baugleiche Monitore ohne Seriennummer in der EDID werden nach einem Umstecken nicht wiedererkannt; ihre Layouts bleiben als «nicht verbunden» stehen.
- Updates werden nur auf Anstoss oder beim Start gesucht, nie im Hintergrund während des Betriebs.
- Die geladene Programmdatei wird an Herkunft, Grösse und der SHA-256-Prüfsumme aus `ZoneManager.exe.sha256` derselben Veröffentlichung geprüft, nicht an einer digitalen Signatur. Eine Veröffentlichung ohne Prüfsummendatei wird nicht geladen.
- Die gemerkten Fensterpositionen lassen sich nur gesamthaft verwerfen, nicht einzeln ansehen oder löschen.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Das Programm ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Version und Releases

Die Version folgt dem Schema `YYYY.MMDD.NN`. `NN` beginnt an jedem Tag bei `01` und zählt je Veröffentlichung des Tages um eins hoch, sodass jede Auslieferung an ihrem Namen erkennbar datiert ist. Die Kopfzeile des Hauptfensters zeigt die Version rechts neben dem Programmnamen; dieselbe Angabe steht in den Dateieigenschaften der EXE.

`Directory.Build.props` hält die Werte für alle Projekte und wird ausschliesslich von `scripts\set-version.ps1` geschrieben. Die Anzeigeform mit führender Null (`2026.0831.01`) steht in `ZoneManagerVersion` und `InformationalVersion`; `AssemblyVersion` und `FileVersion` tragen die numerische Form `2026.831.1`, weil Assemblyversionen keine führenden Nullen speichern können. Die Anwendung liest ausschliesslich die `InformationalVersion` und schneidet ein etwaiges Metadatensuffix ab.

`scripts\publish-release.ps1` führt den vollständigen Weg aus: Version schreiben, `scripts\verify.ps1` ausführen, `Directory.Build.props` committen, Tag `v<Version>` setzen, Commit und Tag pushen und das GitHub-Release mit `ZoneManager.exe` als Anhang erstellen. Das Skript arbeitet nur auf `main` und nur bei sauberem Arbeitsbaum und reicht `-SkipDpiCheck` an den Prüflauf durch; ohne angemeldetes GitHub CLI oder `GH_TOKEN` endet es nach dem Push und nennt den Befehl für das Release.

Die EXE wird bewusst nicht versioniert, sondern nur an Releases angehängt: Sie ist ein reproduzierbares Build-Artefakt von rund 66 MB, das die Repository-Historie sonst mit jeder Auslieferung dauerhaft vergrössern würde.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `ZoneManager.exe` ins Rootverzeichnis und prüft Diagnose sowie Per-Monitor-DPI ohne aktivierten Hook.

Der Lauf schliesst eine Per-Monitor-DPI-Prüfung ein, die die Oberfläche startet und deshalb eine interaktive Sitzung mit bestätigter UAC-Abfrage braucht. In nicht interaktiven Umgebungen bleibt dieser Schritt sonst an der unbeantworteten Abfrage stehen; `-SkipDpiCheck` überspringt ihn.

Neben der Programmdatei entsteht `ZoneManager.Helper.exe` mit rund 10 MB. Sie ist getrimmt, weil sie ohne
Oberfläche auskommt, und wird von der Installation mitgenommen.

Die Einzeldatei enthält die vollständige .NET-Laufzeit, damit sie ohne Installation startet. Sie liefert
bewusst nur die englischen Satellitenressourcen mit (`SatelliteResourceLanguages`); die dreizehn übrigen
Sprachordner enthielten übersetzte .NET-Meldungen, die in diesem einsprachig deutschen Programm nie
erscheinen. Weiter lässt sich die Datei nicht verkleinern: `PublishTrimmed` ist für WPF nicht unterstützt,
und der Self-contained-Publish liefert die Windows-Desktop-Laufzeit unabhängig davon vollständig mit —
gemessen kostet ein zusätzlicher Verweis auf Windows Forms in der komprimierten Einzeldatei sechs Bytes.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `ZoneManager.exe` direkt ins Rootverzeichnis. Läuft daraus gerade eine Instanz, bittet `scripts\install-root-executable.ps1` sie über `--exit` um ein geordnetes Beenden, wartet bis zu 30 Sekunden, tauscht dann Programmdatei und Fensterhelfer aus und startet die Instanz mit dem neuen Stand im Infobereich neu. Beendet sie sich nicht, wird die Datei trotzdem ersetzt; die Instanz erkennt den Austausch dann selbst (siehe [Diagnose](#diagnose)) und startet sich neu. Die weggeschobene Vorgängerdatei bleibt bis zum nächsten Start als ignorierte Sicherungsdatei liegen.

Dieser Schritt kostet bei jedem Build einen vollständigen Self-contained-Publish. Für schnelle Zwischenbuilds und in Prüfläufen, die die Root-EXE separat erzeugen, lässt er sich mit `-p:SkipRootExecutablePublish=true` überspringen; `scripts\verify-root-build.ps1` prüft den impliziten Weg gezielt in einem Wegwerfverzeichnis unter `work\`.

Der Skripttest `scripts\test-set-version.ps1` läuft ausserhalb von `verify.ps1`, legt dafür ein temporäres Repository an und prüft das Versionsschema samt Tageswechsel und Tag-Erkennung.
