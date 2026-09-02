# Sascha’s Zone Manager

Sascha’s Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `ZoneManager.exe` starten und die Windows-UAC-Abfrage bestätigen. Die Datei kommt entweder aus dem neuesten [Release](https://github.com/klopp1991/zone-manager/releases/latest) oder entsteht im Rootverzeichnis, sobald das Projekt gebaut wird.
2. Unter **Layouts** einen Monitor und eines seiner Layouts wählen oder ein neues Layout erstellen.
3. Die vorhandenen Zonen anpassen und mit **+ Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder rechts als Zahlen eingeben – wahlweise über Position und Grösse oder über die vier Randabstände. Die **Masseinheit** wird einmal pro Karte auf Prozent oder Pixel gestellt und gilt für alle acht Felder.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme. Bestehende Profilkonfigurationen aus Schema 1 werden beim Laden in unabhängige Layouts pro Monitor migriert.

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

## Regeln

Auf der Seite **Regeln** verbindet eine Regel ein Fenster mit einer Zielzone. Der Editor führt in vier nummerierten Gruppen durch die Eingabe:

1. **Programm** — Prozesspfad oder Programmname; wie Titelmuster und Fensterklasse ein Filter und kein Pflichtfeld. Zwei Wege führen dorthin: **Programmdatei wählen …** öffnet den Dateidialog und trägt den vollständigen Pfad ein; das ist die richtige Wahl, wenn genau eine bestimmte Programmdatei gemeint ist. **Laufendes Programm wählen …** listet die laufenden Programme mit sichtbarem Fenster samt Fenstertitel und Pfad, ist durchsuchbar und übernimmt bewusst **nur den Dateinamen**, etwa `claude.exe`. Viele Programme installieren sich in ein Verzeichnis mit Versionsnummer; eine Regel auf den vollständigen Pfad hört beim nächsten Update auf zu greifen, während der blosse Dateiname unabhängig vom Installationsort trifft. Läuft ein Programm mit erhöhten Rechten, gibt Windows den Pfad ohnehin nicht preis; dann steht der Programmname allein zur Verfügung.
2. **Fenster eingrenzen** — Titelmuster vergleicht einen Teil des Fenstertitels ohne Rücksicht auf Gross- und Kleinschreibung; Fensterklasse vergleicht den internen Windows-Fenstertyp wie `CabinetWClass`. Leer bedeutet jeweils: keine Einschränkung. `*` und `?` dienen als Platzhalter.
   Programm, Titelmuster und Fensterklasse sind gleichrangig; **mindestens eines der drei** muss stehen, welches, entscheidest du. Eine Regel lässt sich deshalb jederzeit umstellen: wer vom Pfad auf das Titelmuster wechselt, trägt das Muster ein und leert das Programmfeld. Solange kein Merkmal gesetzt ist, weist der Editor darauf hin und die Regel bleibt wirkungslos – sie würde sonst auf jedes Fenster passen. Passen mehrere Regeln bei gleicher Priorität, gewinnt die enger gefasste.
3. **Auslöser** — Ereignis, Verzögerung von 0 bis 30000 Millisekunden, 0 bis 3 Wiederholungen, Priorität von 0 bis 100. Unter der Zeile erklärt ein Hinweisfeld das gewählte Ereignis im Klartext: **Fenster wird geöffnet** greift einmalig beim Erscheinen eines neuen Fensters, **Fenster erhält den Fokus** jedes Mal beim Wechsel zu einem passenden Fenster, **Layout wird aktiviert** ordnet beim Layoutwechsel alle bereits offenen passenden Fenster neu an.
4. **Ziel** — Ziellayout und Zielzone; das Layout bestimmt zugleich den Monitor.

**+ Regel** legt den Eintrag sofort an und wählt ihn in der Liste aus, auch wenn noch kein Feld ausgefüllt ist; der Entwurf geht damit beim nächsten Klick in der Liste nicht verloren.

In der Regelliste steht als Überschrift das Titelmuster, sofern eines gesetzt ist, sonst der Dateiname des Programms, sonst die Fensterklasse – nie der vollständige Pfad. Den vollständigen Pfad zeigt der Tooltip des Listeneintrags.

Vor jedem Platzierungsversuch werden Fensteridentität, Regel und Ziel erneut geprüft. Fehlende Layouts, Monitore oder Zonen pausieren die Regel sichtbar; es gibt keinen stillen Fallback und Regeln starten keine Programme.

## Ausschlüsse

Auf der Seite **Ausschlüsse** stehen die Fenster, die die Anwendung vollständig in Ruhe lässt. Für ein
ausgeschlossenes Fenster erscheint beim Ziehen kein Overlay, es rastet in keine Zone ein, keine Regel bewegt
es, und seine Position wird weder gemerkt noch beim nächsten Öffnen wiederhergestellt. Es behält damit
dauerhaft die Grösse und Position, die ihm der Benutzer selbst gibt.

Ein Ausschluss beschreibt das Fenster nach denselben drei Merkmalen wie eine Regel — Programm, Titelmuster,
Fensterklasse — und über dieselben zwei Wege der Programmauswahl. Leere Felder schränken nicht ein; mindestens
eines der drei muss stehen, sonst würde der Ausschluss auf jedes Fenster passen und wird nicht gespeichert.

Ein Ausschluss ist stärker als jede Regel: passen auf ein Fenster gleichzeitig ein Ausschluss und eine Regel,
bleibt das Fenster unberührt. Priorität und Konflikte gibt es beim Ausschluss nicht, weil mehrere zutreffende
Ausschlüsse zum selben Ergebnis führen.

## Layouteditor

- **+ Zone** belegt die grösste freie achsenparallele Fläche; ohne ausreichenden freien Bereich wird nichts verändert.
- Zonen docken innerhalb der eingestellten Magnetdistanz an Monitor- und Zonenkanten an; `Alt` deaktiviert den Magnetismus während des Ziehens.
- Die Karte **Ausgewählte Zone** schaltet die **Masseinheit** an einer einzigen Stelle um; die Umschaltung gilt gemeinsam für alle acht Zahlenfelder. **Prozent** bleibt bei Auflösungsänderungen proportional; **Pixel** bezieht sich auf die aktuelle Windows-Arbeitsfläche des Monitors.
- **Position und Grösse** bearbeitet X, Y, Breite und Höhe; **Abstände zum Rand** beschreibt dieselbe Zone von den vier Rändern aus.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden markiert und können nicht gespeichert werden.
- Die Gruppe **Hauptzone** in derselben Karte macht die ausgewählte Zone zur Arbeitszone und hebt die Markierung wieder auf. Siehe [Hauptzone](#hauptzone).
- Nach jedem Setzen misst das Programm nach. Sitzt das Fenster nicht innerhalb von zwei Pixeln auf der Zielfläche, wird es einmal erneut gesetzt (ein Wechsel zwischen Monitoren mit unterschiedlicher Skalierung braucht häufig zwei Anläufe). Bleibt eine Abweichung, nennt die Statuszeile den Grund, etwa eine Mindestgrösse des Fensters; nach Administratorrechten wird nur gefragt, wenn sich das Fenster gar nicht bewegen liess. Fenster ohne veränderbare Grösse werden in der Zone zentriert statt gestreckt.
- Beginnt das Ziehen über der Taskleiste, gilt der nächstgelegene Monitor; bleibt das Endereignis von Windows aus (Fenster geschlossen, Maustaste losgelassen), zieht ein Wachhund die Overlays nach spätestens einer Sekunde ein.
- Beim Einrasten wird der unsichtbare Fensterrand ausgeglichen. Windows gibt Fenstern mit veränderbarer Grösse einen Griffbereich zum Ziehen, der zum Fensterrechteck zählt, aber nicht gezeichnet wird – typischerweise sieben Pixel links, rechts und unten. Ohne Ausgleich stünden zwei Fenster in lückenlos aneinandergrenzenden Zonen sichtbar auseinander. Das Programm vergrössert das Fensterrechteck deshalb um genau diesen Rand, sodass der sichtbare Rahmen exakt in der Zone liegt.

## Hauptzone

Die Hauptzone ist die Arbeitszone: dort landen neu erscheinende Fenster, die sonst nirgends hingehören.
Festgelegt wird sie im Layouteditor an der Zone selbst, in der Karte **Ausgewählte Zone** unter
**Hauptzone**. Die markierte Zone trägt in der Zeichenfläche das Feld «Hauptzone».

Jedes Layout darf eine eigene Hauptzone tragen. Welche davon wirksam ist, entscheidet die Monitorreihenfolge
aus der Seite **Monitore**: es gilt die Hauptzone des ersten Monitors, dessen aktives Layout überhaupt eine
trägt. Monitore ohne Eintrag in dieser Reihenfolge stehen hinten.

Daraus folgt das Verhalten, das im Alltag zählt:

- Ist nur eine einzige Zone markiert, landen neue Fenster immer am selben Ort.
- Markierst du in mehreren Layouts desselben Monitors je eine, überlebt die Hauptzone den Layoutwechsel:
  das neue Layout bringt seine eigene mit.
- Trägt das aktive Layout des vordersten Monitors keine, rutscht die Wahl auf den nächsten Monitor, statt
  ganz auszufallen.
- Trägt kein aktives Layout eine, gibt es keine Hauptzone, und neue Fenster bleiben unangetastet — so
  verhält sich das Programm auch ohne jede Markierung.

Ein kopiertes Layout übernimmt die Markierung auf der Zone an derselben Stelle; ohne das hätte eine Kopie
nie eine Hauptzone. Nach dem Setzen nennt die Statuszeile, welche Hauptzone tatsächlich wirksam ist.

Ein Fenster kommt in die Hauptzone, wenn nacheinander nichts anderes zutrifft:

1. Eine passende Regel platziert das Fenster — sie gewinnt immer.
2. Eine gemerkte Position liegt vor — sie gewinnt vor der Hauptzone.
3. Das Fenster liegt bereits auf einer Zone eines aktiven Layouts eingerastet — es bleibt, wo es ist.
4. Sonst: Hauptzone.

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

Ausgeschlossene Fenster fasst die Hauptzone nie an. Wie das Einrasten selbst läuft der Auffang genau dann, wenn
mindestens ein Layout aktiv ist; einen eigenen Schalter dafür gibt es nicht. (Bis zum 02.09.2026 hing der Auffang
an einer nie gesetzten internen Einstellung und lief im Betrieb nie.)

## Gemerkte Fensterpositionen

Sobald die Snap-Funktion aktiv ist, merkt sich die Anwendung für jedes platzierte Fenster den Monitor, die
Zone und das Fensterrechteck und stellt diesen Stand beim nächsten Öffnen desselben Fensters wieder her.
Ändert sich die Auflösung, wird die gemerkte Lage anteilig umgerechnet. Der Katalog fasst höchstens 500
Einträge und liegt neben den Einstellungen.

Ist die gemerkte Zone im aktiven Layout noch vorhanden, kehrt das Fenster in diese Zone zurück, auch wenn
sich deren Fläche inzwischen geändert hat; erst ohne Zone zählt die gemerkte Lage. Ein kleines Fenster bleibt
beim Wiederherstellen klein.

Fehlt eine gemerkte Position, greift die [Hauptzone](#hauptzone), sofern eine festgelegt ist.

Zurückgelegt wird ein Fenster nur beim Erscheinen; ein blosser Fokuswechsel verschiebt nie ein Fenster,
das gerade von Hand irgendwohin gestellt wurde. Erkannt wird ein Fenster an Programm, Fensterklasse und
Fensterart, nicht am Titel. Mehrere Fenster
desselben Programms teilen sich deshalb einen Eintrag. Ausgeschlossene Fenster kommen gar nicht erst in den
Katalog.

Unter **Einstellungen** lässt sich das Merken abschalten und die Anzahl der gemerkten Positionen ablesen;
**Gemerkte Positionen verwerfen** löscht sämtliche Einträge. Ausgeschaltet bleiben bestehende Einträge
erhalten, werden aber weder angewendet noch ergänzt. Einzelne Einträge lassen sich nicht ansehen oder
gezielt löschen.

## Monitore

Auf der Seite **Monitore** wählt die Liste links den Monitor, der im ganzen Programm als aktiver Monitor gilt. **Nach oben** und **Nach unten** ändern die Reihenfolge, **Monitore identifizieren** blendet den verwendeten Namen drei Sekunden lang auf jedem Bildschirm ein. Rechts wird der Monitor umbenannt; ein leerer Name stellt die automatische Bezeichnung wieder her. Monitornamen werden bevorzugt aus dem aktiven Displaypfad und den EDID-Daten gelesen.

Monitore werden zur Laufzeit beobachtet. Anstecken, Abstecken, eine geänderte Auflösung, Skalierung oder Drehung und eine verschobene Taskleiste werden nach einer kurzen Ruhepause übernommen: Zonen, Overlays und Zielflächen werden neu aufgebaut, die Statuszeile meldet den neuen Stand, und Fenster, die im neuen Bild auf keiner Zone mehr liegen, werden in die Hauptzone geholt. Ein Neustart ist nicht nötig. (Bis zum 02.09.2026 wurden die Monitore genau einmal beim Start gelesen.)

Wiedererkannt wird ein Monitor an seiner Hardware: Hersteller, Modell und, sofern der Monitor eine liefert, Seriennummer aus der EDID. Hängt derselbe Monitor an einem anderen Anschluss oder hinter einem anderen Treiber, ändert sich der Anzeigepfad von Windows; die Layouts, der eigene Name und die Position in der Reihenfolge werden dann übernommen, und die Statuszeile nennt das. Zwei baugleiche Monitore ohne Seriennummer bleiben getrennt, weil eine Verwechslung schlimmer wäre als ein neues Standardlayout. Verwaiste Namen und Reihenfolgeeinträge, die zu keinem Monitor und keinem Layout mehr gehören, werden beim Abgleich entfernt.

Je Monitorkombination merkt sich das Programm, welche Layouts zuletzt aktiv waren: am Dock mit zwei Monitoren ein anderes als unterwegs mit dem Laptopdisplay allein. Kehrt eine Kombination zurück, werden ihre Layouts wieder aktiviert, ohne dass jemand umschalten muss. Ein Wechsel des aktiven Layouts gilt immer für die gerade verbundene Kombination.

Die Liste enthält auch Monitore, die gerade **nicht verbunden** sind, solange für sie noch mindestens ein Layout gespeichert ist. Sie sind als solche gekennzeichnet und stehen am Ende der Liste. Der Grund: solche Layouts erscheinen weiterhin als Regelziel, wären ohne diesen Eintrag aber nirgends erreichbar und liessen sich nicht mehr löschen. Bei einem nicht verbundenen Monitor darf deshalb auch sein letztes Layout gelöscht werden — danach verschwindet der Monitor aus der Liste. Bei einem verbundenen Monitor bleibt das letzte Layout weiterhin geschützt.

## Skalierung

Die Seite **Skalierung** liest die erkannten Werte des gewählten Monitors aus — Anzeigeskalierung, Auflösung, Arbeitsfläche und, sofern Windows die EDID liefert, die Bildschirmdiagonale — und öffnet die zuständige Windows-Seite.

Ändern lassen sich diese Werte nur in Windows selbst. Windows 11 stellt normalen Desktopanwendungen keine unterstützte Schnittstelle bereit, um Anzeigeskalierung, Textskalierung oder monitorweise Taskleisten- und Icongrössen zu setzen. Benutzerdefinierte Windows-Skalierung von 100 bis 500 % und Textskalierung von 100 bis 225 % sind zudem globale Windows-Einstellungen. Sascha’s Zone Manager verwendet dafür bewusst keine Explorer-Injektion, keine privaten DPI-Pakete und keine undokumentierten Registry-Werte; die Seite bleibt deshalb lesend.

## Einstellungen

- System-, helles oder dunkles Theme; Systemänderungen werden ohne Neustart übernommen.
- **Zonen anzeigen auf** bestimmt, wo die Zonen beim Ziehen erscheinen:
  - **Alle Monitore** — gleichzeitig auf jeder Anzeige.
  - **Monitor beim Ziehbeginn** — nur auf dem Bildschirm, auf dem das Fenster angefasst wurde. Die Zonen
    bleiben dort; wandert der Zeiger auf einen anderen Bildschirm, sieht er dort keine Zonen.
  - **Monitor unter dem Mauszeiger** — die Zonen wandern mit. Sie erscheinen immer auf dem Bildschirm, auf
    dem der Zeiger gerade steht, und verschwinden auf allen übrigen. Liegt der Zeiger kurz auf keinem
    Bildschirm — über der Taskleiste oder in der Lücke zwischen zwei unterschiedlich hohen Monitoren —,
    bleibt die bisherige Anzeige stehen, statt zu flackern.
- Sofortige Aktivierung oder Aktivierung mit Umschalttaste. Die Umschalttaste darf auch erst während des
  Ziehens gedrückt werden; wird sie losgelassen, verschwinden die Zonen wieder, bis sie erneut gedrückt wird.
- **Abstände**: Aussenabstände links, oben, rechts und unten in Pixel, Zonenabstand und Magnetdistanz in ganzen Prozent. Seit dem 02.09.2026 gelten Aussen- und Zonenabstand für Vorschau **und** Fenster: ein Fenster landet genau auf der Fläche, die das Overlay zeigt, auch über Regeln, Hauptzone und Layoutwechsel. (Bis dahin betrafen die Werte nur die Vorschau; das Fenster wurde auf die volle Zone gesetzt.) Neben jedem Prozentregler steht der abgeleitete Pixelwert als `≙ n px`.
- Overlayfarbe, Deckkraft und ein-/ausblendbare Zonennamen.
- **Fensterpositionen merken** schaltet den Positionskatalog ein und aus; daneben stehen die Anzahl der
  Einträge und die Schaltfläche zum Verwerfen. Siehe [Gemerkte Fensterpositionen](#gemerkte-fensterpositionen).
- Autostart pro Benutzer über eine Anmeldeaufgabe der Windows-Aufgabenplanung. Sie startet das Programm
  bereits erhöht, sodass bei der Anmeldung **keine** UAC-Abfrage erscheint. Das Anlegen der Aufgabe braucht
  Administratorrechte, die das Programm im Normalbetrieb ohnehin besitzt. Schlägt es fehl, weicht das
  Programm auf den bisherigen Registry-Eintrag `Run` aus und meldet das im Protokoll; dann erscheint die
  Abfrage bei jeder Anmeldung wieder. Eingetragen ist immer nur einer der beiden Wege.

Jede Einstellung erklärt direkt in der Oberfläche Wirkung, Gültigkeitsbereich und Einschränkungen. Wie Titel, Beschriftungen und Hilfetexte dabei aufgebaut sind, steht verbindlich in [ui-richtlinien.md](ui-richtlinien.md).

## Installation

Das Programm läuft ohne Installation aus dem Verzeichnis, in dem die Datei liegt. Das genügt, ist im
Downloadordner aber unaufgeräumt und erschwert Updates.

**Einstellungen → Installation** kopiert die Programmdatei nach `%ProgramFiles%\ZoneManager`, legt eine
Verknüpfung im Startmenü an und trägt das Programm in «Apps und Features» ein. Danach startet es von dort
neu. Dasselbe leistet `ZoneManager.exe --install` auf der Kommandozeile.

Entfernt wird es über «Apps und Features» wie jedes andere Programm, oder mit
`ZoneManager.exe --uninstall`. Die Einstellungen unter `%APPDATA%\SnapZones` bleiben dabei erhalten — sie
gehören dem Benutzer, und eine Neuinstallation soll sie wiederfinden. Wer sie loswerden will, löscht das
Verzeichnis von Hand.

Es gibt bewusst kein getrenntes Setup-Programm: es müsste die 66 MB grosse Programmdatei ein zweites Mal
enthalten und die Auslieferung verdoppeln. Installieren und Entfernen sind deshalb Modi derselben Datei.

Beides schreibt nach `%ProgramFiles%` und in `HKEY_LOCAL_MACHINE` und verlangt darum Administratorrechte,
die das Programm im Normalbetrieb ohnehin besitzt.

## Updates

Unter **Einstellungen → Updates** steht die installierte Version, daneben **Nach Updates suchen** und
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

Der Austausch geht in drei Schritten, weil Windows eine laufende Programmdatei nicht überschreiben lässt:
die neue Datei landet zuerst daneben, dann wird die laufende als `ZoneManager.exe.previous.<Zeitstempel>`
beiseitegeschoben, dann die neue an ihren Platz gelegt. Scheitert der zweite Schritt, kommt die alte Datei
zurück; es bleibt nie eine halb ersetzte Programmdatei liegen. Beim nächsten Start werden die
beiseitegeschobenen Dateien gelöscht.

Ohne digitale Signatur kann das Programm die geladene Datei nur an Herkunft und Grösse prüfen, nicht an
einer Signatur. Wer das nicht will, lädt Releases von Hand herunter und lässt die Suche ausgeschaltet.

## Rechte

Windows teilt laufende Programme in Vertrauensstufen ein. Ein Programm darf nur Fenster verschieben, die
derselben oder einer niedrigeren Stufe angehören. Alltägliche Fenster — Browser, Editor, Explorer — gehören
zur gewöhnlichen Stufe; der Taskmanager, der Registrierungs-Editor und alles «als Administrator» Gestartete
stehen darüber.

Unter **Einstellungen → Rechte** steht deshalb zur Wahl:

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

Unter **Einstellungen → Fensterhelfer ohne Administratorrechte** wird ein selbst ausgestelltes Zertifikat
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
der lokalen Maschine schreiben. Gearbeitet wird über die Windows-eigene PowerShell
(`New-SelfSignedCertificate`, `Set-AuthenticodeSignature`); ein externes Werkzeug wird nicht gebraucht.

**Entfernen** nimmt das Zertifikat aus allen drei Speichern. Der Helfer startet danach nicht mehr, und das
Programm fragt bei Bedarf wieder nach eigenen Administratorrechten.

## Not-Aus und Schutzschalter

`Ctrl + Alt + Shift + F12` schaltet das Einrasten um: einmal gedrückt legt es Hook und Overlays still, erneut
gedrückt läuft es weiter. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber,
keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern
oder ungewöhnlich vielen Hook-Ereignissen (400 Verschiebe-Ereignisse in zehn Sekunden). Der Diagnosemodus läuft
bewusst ohne Elevation.

Der Zustand ist immer sichtbar: die Statuszeile am unteren Fensterrand zeigt «Einrasten aktiv», «Kein aktives
Layout» oder «Einrasten pausiert», daneben die letzte Meldung des Programms (Speicherfehler, Namenskonflikte,
pausierte Regeln). Das Infobereichsmenü nennt denselben Zustand. Ist das Einrasten pausiert, schalten die
Schaltfläche **Einrasten wieder aktivieren** in der Statuszeile, der gleichnamige Menüpunkt im Infobereich oder
der Hotkey es wieder ein; ein Neustart ist dafür nicht mehr nötig.

## Beenden

Das Schliessen des Fensters blendet die Anwendung nur in den Infobereich aus. Beendet wird sie über **Rechtsklick auf das Infobereichssymbol → Beenden**.

Beim Beenden werden zuerst Hooks, Zeitgeber und die Platzierungs-Engine stillgelegt, damit keine neue Arbeit mehr anfällt; anschliessend werden Einstellungen und Fensterplatzierungen gespeichert. Für diesen Abschluss gilt eine Zeitgrenze von fünf Sekunden. Lässt sich in dieser Zeit nicht vollständig speichern, meldet ein Hinweisfenster die Ursache und fragt, ob trotzdem beendet werden soll — die Anwendung bleibt nie ohne sichtbare Begründung geöffnet.

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

## Einschränkungen

- Nur Windows 11 x64.
- Wird die Windows-UAC-Abfrage bei «Immer beim Start» abgebrochen, startet die Anwendung nicht.
- Fenster höher berechtigter Programme lassen sich nur einrasten, wenn das Programm selbst erhöht läuft oder der signierte Fensterhelfer eingerichtet ist.
- Der Fensterhelfer ist mit einem selbst ausgestellten Zertifikat signiert und funktioniert deshalb nur auf dem Rechner, auf dem es eingerichtet wurde.
- Nicht rechteckige oder überlappende Zonen und virtuelle Desktops sind noch nicht enthalten.
- Zwei baugleiche Monitore ohne Seriennummer in der EDID werden nach einem Umstecken nicht wiedererkannt; ihre Layouts bleiben als «nicht verbunden» stehen.
- Updates werden nur auf Anstoss oder beim Start gesucht, nie im Hintergrund während des Betriebs.
- Die geladene Programmdatei wird an Herkunft und Grösse geprüft, nicht an einer digitalen Signatur.
- Die gemerkten Fensterpositionen lassen sich nur gesamthaft verwerfen, nicht einzeln ansehen oder löschen.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

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

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `ZoneManager.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.

Dieser Schritt kostet bei jedem Build einen vollständigen Self-contained-Publish. Für schnelle Zwischenbuilds und in Prüfläufen, die die Root-EXE separat erzeugen, lässt er sich mit `-p:SkipRootExecutablePublish=true` überspringen; `scripts\verify-root-build.ps1` prüft den impliziten Weg gezielt in einem Wegwerfverzeichnis unter `work\`.

Die Skripttests laufen ausserhalb von `verify.ps1` und legen dafür je ein temporäres Repository an: `scripts\test-new-task-worktree.ps1` prüft die Worktree-Erstellung, `scripts\test-set-version.ps1` das Versionsschema samt Tageswechsel und Tag-Erkennung.
