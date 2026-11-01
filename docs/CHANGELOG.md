# Registro delle modifiche

## 0.3.0-rc.7 - 1 novembre 2026: librerie GPC attuali

Nessun componente cambia: GUID, ingressi e uscite sono quelli della rc.6.

- **Versione delle DLL:** 0.3.0.7.
- **Librerie GPC:** GPCGeometry 2.3.0.0 (era 2.1.0.4) e GPCUtilities 3.0.0.0 (era 2.0.0.8). Sono le DLL di ReBear e BridGHe: i tre plug-in si caricano nello stesso Rhino e devono usare le stesse versioni, quindi si aggiornano insieme.
- **TensileLib 2.5.0.1:** la 2.5 compilata con le stesse librerie. Il codice non cambia. I risultati sono identici bit per bit: confrontate coordinate, densità, forze e piastre di 37 calcoli Linear, Non-Linear e planari.
- **System.Drawing.Common tolto:** serviva solo a GPCUtilities 2.x e non è più nel plug-in.
- **Banco di prova:** 153 controlli in Rhino/Grasshopper, 54 del motore ARAP e 59 di TensileLib, con gli stessi esiti della rc.6.
- **Esempi** rigenerati con il plug-in rc.7.

## 0.3.0-rc.6 - 7 ottobre 2026: vincoli ruotati, pressione, piastre

Dettagli in [PORTING.md](PORTING.md). Restraints ha un GUID nuovo e va ricollegato nelle definizioni esistenti.

- **Versione delle DLL:** 0.3.0.6.
- **Vincoli ruotati (TensileLib 2.4):** Restraints ha l'ingresso facoltativo Coordinate System. Con un piano, Tx, Ty e Tz agiscono lungo i suoi assi X, Y e la normale Z: un nodo può scorrere su una retta inclinata (Ty e Tz) o su un piano inclinato (solo Tz).
  - La TensileLib risolve le coordinate legate dai vincoli obliqui con un unico sistema (Linear) o nella stessa jacobiana (Non-Linear). Con assi allineati a X, Y, Z il calcolo e il risultato sono quelli dei vincoli globali.
  - Le reazioni sono lungo le direzioni vincolate.
  - Check Model indica le direzioni in cui una parte non è vincolata, anche inclinate.
  - Tolto l'ingresso Rotation (il form finding non usa le rotazioni); Transational diventa Translational.
- **Pressione contata due volte** (PORTING.md, 37): i carichi nodali della pressione salvati dal Form Find, come in FeMM, venivano applicati di nuovo quando il modello calcolato passava a un altro Form Find, e si accumulavano. Ora sono un risultato riconoscibile: restano nel caso scelto per la pressione, ma un nuovo Form Find non li applica e li sostituisce.
- **TensileLib 2.5:**
  - **piastre generate fino a 130 volte più in fretta** sulle reti triangolate, con le stesse facce: 20.000 triangoli da 105 s a 0,8 s. Verificato faccia per faccia su 58 reti diverse;
  - **nessuna faccia mancante accanto a un nodo con un albero, un puntone o uno strallo** (PORTING.md, 38): prima su quella cella mancavano pressione, peso e mesh;
  - il solutore planare non rimette più i nodi vincolati alla loro quota 3D (nessun effetto in Arachne: i modelli di sviluppo non hanno vincoli).
- **Attributi** (PORTING.md, 39): un attributo aggiunto a un elemento letto da file, o a un nodo copiato, ora riceve il suo elemento.
- **Banco di prova:** 153 controlli in Rhino/Grasshopper (66 inventario, 38 form finding, 43 patterning, 6 reti), 54 del motore ARAP e 59 di TensileLib. Nuove prove:
  - vincoli lungo gli assi di un piano: nodo su un piano e su una retta inclinati con posizione e reazioni analitiche (Linear) e sul piano con la reazione lungo la normale (Non-Linear); piano ruotato attorno a Z uguale ai vincoli globali; Check Model con una parte vincolata solo in una direzione inclinata; salvataggio su file;
  - modello calcolato con pressione e ricalcolato: stessa forma con la stessa pressione, forma senza pressione senza di essa, una sola forza di pressione per nodo;
  - attributo aggiunto a un nodo letto da file, a un nodo copiato e a un'asta letta da byte.

## 0.3.0-rc.5 - 7 ottobre 2026: snellimento

Dettagli e GUID in [PORTING.md](PORTING.md). I componenti con ingressi diversi hanno un GUID nuovo: Load Case, Plate Property e Form Find vanno ricollegati nelle definizioni esistenti.

- **Pannelli:** Build Model, Decompose Model e Check Model passano da 06-Model a 09-Model, dopo attributi e carichi; il patterning passa a 13-Patterning, dopo l'analisi; Cable Net, Radial Cable Net e Compound Net, che generano la rete, passano a 14-Tools.
- **TensileLib 2.3:**
  - **peso proprio delle piastre:** spessore × densità × gravità × area attuale, ricalcolato a ogni iterazione. Pesano le piastre in ingresso con la loro Plate Property e quelle generate dalle aste con il Plate Property di Solver Options. Per un Membrane Material vale il peso in g/m² della scheda. Le reazioni lo comprendono;
  - **ToleranceA,s e ToleranceB,s**, le due tolleranze di FeMM, di nuovo distinte: A per il gradiente coniugato e per i minimi quadrati di LSQR, B per il residuo di LSQR. Predefinite 1e-6, il valore di ALGLIB (FeMM: 0).
- **Casi di carico:** il solutore applicava tutti i carichi. Il nuovo ingresso Load Cases di Solver Options sceglie i casi dei carichi nodali e sulle aste; vuoto: tutti.
- **Mesh Pipe:** tolto.
- **Fixed curvature cable:** nickname FCC; FC era anche quello di Fixed compensation.
- **Load Case:** tolto Gravity NSM. Dal modello sono stati tolti anche gli altri dati SAP sull'inerzia (gravità, accelerazioni, temperatura).
- **Materiali:** tolto Table Material. Nuovo **Membrane Material**, con i valori della scheda tecnica:
  - peso in g/m² e resistenze di ordito e trama in N/5cm;
  - facoltativi: rigidezze E·t di ordito e trama e rigidezza a taglio G·t in kN/m, coefficiente di Poisson.
  
  Si collega a Plate Property come gli altri materiali.
- **Plate Property:** Name, Thickness, Material. Tolti il tipo SAP (Shell-Thin, Membrane, Load Patch...) e il secondo spessore.
- **Form Find:** restano Model, Solver Type, Plane e Auto Run. Gravity, Pressure, Pressure Load Case, Max Iter, Tolerance, Max Iter,s, ToleranceA,s, ToleranceB,s, Create mesh e Plate Property passano al nuovo componente **Solver Options** (con in più Load Cases); senza Solver Options valgono i predefiniti.
- **Fixed compensation:** verificato che la Compensation impone la lunghezza ridotta al lato del pannello piano con gli stessi vertici; nel Form Find agisce come Fixed Length. Descrizioni del componente e del peso precisate.
- **Sezioni:** restano tutte, perché servono anche all'anteprima delle aste.
- **Bug di FeMM corretti** (PORTING.md, 33-36):
  - l'anteprima della sezione Generic era 1000 volte troppo sottile;
  - i dati interni di Plate Property si salvavano senza materiale e spessori e non si potevano rileggere;
  - Custom Material con Kind = Rebar terminava con un'eccezione;
  - la resistenza ultima a taglio del materiale leggeva e scriveva quella a trazione.
- **Compilazione:** la proprietà `ArachneBin` sposta gli output fuori dal repository, per verificare una build mentre Rhino tiene aperto il plug-in in `bin`. ExampleWriter prende il plug-in compilato accanto a sé.
- **Banco di prova:** 147 controlli in Rhino/Grasshopper (66 inventario, 32 form finding, 43 patterning, 6 reti) e 54 del motore ARAP. Nuove prove:
  - predefiniti senza Solver Options, casi di carico, peso delle piastre (membrana e acciaio) bilanciato dalle reazioni;
  - Membrane Material e salvataggio di Plate Property, anteprima della sezione Generic, Fixed compensation nella Compensation.

## 0.3.0-rc.4 - 7 ottobre 2026: solo bin

- **Versione delle DLL:** 0.3.0.4. Il quarto numero segue la release candidate: prima tutte le rc avevano versione file 0.3.0.0.
- **Compilazione:** in radice resta solo la cartella `bin`.
  - Niente più `artifacts`: il plug-in è `bin/Arachne.Grasshopper/Release/net48`, con `DEPENDENCIES.json` e `BUILD.json`. Lo stesso vale per il motore ARAP autonomo in `bin/Arachne.Arap/Release/netstandard2.0`.
  - La vecchia DLL nativa per i confronti ARAP si compila in `bin/Arachne.IGL/Release`.
  - `BUILD.json` è scritto solo se tutte le verifiche passano.
- **Un solo `.gha` sotto `bin`:** Grasshopper cerca i plug-in in tutte le sottocartelle di `bin`.
  - Si compila solo in Release: una build Debug aggiungerebbe una seconda copia.
  - ExampleWriter non copia più il `.gha` nella sua cartella: lo carica da `bin/Arachne.Grasshopper`.
  - I file intermedi restano nelle cartelle `obj` dei progetti, perché contengono anche loro una copia del `.gha`.
- **Pulizia:** tolti le build Debug, gli output del vecchio Arachne.Core, i pacchetti 0.1 e 0.2 e i log di verifica della prima versione.

## 0.3.0-rc.3 - 7 ottobre 2026: TensileLib 2.2

- **TensileLib 2.2.0.0** (`../tensilelib`, branch `claude/tensilelib-fixes`): le correzioni che Arachne applicava nel FormFindingHelper sono ora nella libreria, e sono stati tolti gli aggiramenti lato Arachne:
  - carichi sulle aste fermi quando l'asta si allunga;
  - pressione orientata secondo il piano;
  - modello risolto centrato nell'origine;
  - piastre generate per ogni parte della rete.
  
  Corretti inoltre: pesi del Non-Linear, membrane con cavi lunghi, direzioni delle curvature, pressioni azzerate dal Linear al limite di iterazioni, codice 7 dei solutori algebrici e tempi di generazione piastre e del Non-Linear. Dettagli nel registro di TensileLib.
- **Flattening:** Tolerance,s = 0 riprende il significato di FeMM; il caso dei pannelli curvi è risolto in TensileLib.
- **Linear step by step:** riparte a ogni passo dalle densità appena trovate, come prima: TensileLib 2.2 non le scrive più negli obiettivi delle aste.
- **Mesh Clean:** non modifica più la mesh in ingresso (bug di FeMM, vedi PORTING.md n. 32).
- **Banco di prova:** riscritte le prove del patterning, delle reti di cavi e del motore ARAP ereditate dalla prima versione:
  - ogni prova controlla un valore atteso (lati, aree, numero di cavi, di pezzi di perimetro e di punti) invece della sola assenza di errori; la prova delle reti stampava un conteggio fisso;
  - una sola prova verifica che tutti i componenti con una mesh in ingresso la lascino invariata, leggendo i dati del parametro e non una copia: così è emerso il bug di Mesh Clean;
  - le prove ARAP sono raggruppate per argomento (invarianti, vincoli, mesh non valide, perimetro, convergenza, concorrenza) con nomi che dicono il risultato atteso.

  Totale: 144 controlli in Rhino/Grasshopper (68 inventario, 27 form finding, 43 patterning, 6 reti) e 54 del motore ARAP, tutti superati.

## 0.3.0-rc.2 - 6 ottobre 2026: N/mm, snellimento, risultati

Dettagli e GUID in [PORTING.md](PORTING.md).

- Unità: si lavora in N e mm. Test ed esempi sono in N/mm; un controllo verifica che SI e Nmm diano la stessa freccia per peso proprio e le stesse reazioni.
- Tolto quello che il form finding non usa:
  - dal modello: combinazioni di carico, named sets, costruzioni per fasi, funzioni, superelementi;
  - da Build Model: gli ingressi Load Combinations, Named sets e Staged constructions;
  - da Beam Distributed Load: P1 e P2 e gli schemi diversi da uniforme e trapezio; senza Coordinate System i carichi sono globali;
  - il parametro Load Combinations e il componente Section Geometry.
- Portati da FeMM: Element Id, Node Filter, Beam Filter, Plate Filter.
- Nuovi componenti Arachne: Check Model, Beam Forces, Support Reactions, Panel Distortion.
- Form Find:
  - salva le reazioni dei vincoli quando converge;
  - salva il pre-carico anche sulle aste con sola forza assiale o lunghezza fissa.
- Correzioni lato Arachne di difetti della TensileLib:
  - carichi sulle aste ripartiti sulla lunghezza sbagliata;
  - verso della pressione dipendente dall'ordine delle aste;
  - convergenza dipendente dalla distanza dall'origine;
  - nessuna piastra quando la prima asta è uno strallo.
- Flattening: con la tolleranza del solutore predefinita (0) falliva su ogni pannello curvo. Ora 0 vuol dire automatica.
- Node e Plate Filter: Units ha il predefinito 2, come Beam Filter.
- Banco di prova: 140 controlli in Rhino/Grasshopper e 54 del motore ARAP. Il confronto con FeMM elenca gli ingressi tolti di proposito.

## 0.3.0-rc.1 - 6 ottobre 2026: componenti originali FeMM

Sostituiti i componenti introdotti nel primo trasferimento con quelli di FeMM. I componenti rimossi sono:
- Cable, Cable Constraints, Cable Load, Node Conditions, Model, Solver Settings e Form Find;
- Check Model, Deconstruct Model e Deconstruct Result;
- Node, Beam e Plate Results, Equilibrium Report, Solver History e Result Preview;
- Unit Settings, Convert Units, Panel Distortion, Dependency Check e Demo Saddle Net.

Ingressi e uscite dei componenti ripristinati sono identici a FeMM, con GUID nuovi e icone nel nuovo stile. Mappa completa e bug corretti in [PORTING.md](PORTING.md).

- Nuovo progetto `Arachne.Common` (modello FEM e helper di FeMM.Common), al posto di `Arachne.Core`.
- 42 componenti e parametri FeMM aggiunti: Form Find, attributi, carichi, modello, definizioni, proprietà, materiali, sezioni, parametri, Remesh e Curve2Mesh.
- Patterning:
  - Compensation riceve di nuovo le Beam FeMM con Fixed Length o Fixed compensation;
  - Flattening ARAP e Deform ARAP hanno di nuovo le sole uscite FeMM, con GUID nuovo, e continuano a usare il motore C#;
  - Flattening e Compensation pubblicano il risultato non convergente con un avviso, come FeMM;
  - le reti di cavi tornano nel pannello 09-Patterning.
- Pannelli Grasshopper con i nomi di FeMM (01-Parameters … 14-Tools).
- Banco di prova riscritto (176 controlli). Confronta ingressi e uscite con i sorgenti FeMM e verifica il form finding su casi analitici.
- Esempi rigenerati: 01 Sella, 02 Patterning, 03 Cable Net.

TensileLib e il motore ARAP non sono stati modificati. Le voci seguenti descrivono la versione precedente. I documenti che citano (RELEASE-1, RESULTS, RESOCONTO, FIVE-EXAMPLES, PATTERNING-PORT) sono stati rimossi insieme a quei componenti; restano nella storia git.

## 0.2.0-rc.2 - 6 ottobre 2026

Cinque nuovi esempi GH/GHX con tutti i 43 componenti, tiri warp/weft distinti, calcolo iterativo e non lineare, risultati interrogabili e laboratorio completo dei pannelli. Nuove icone incorporate per ogni componente e assembly, anteprime SVG e rigenerazione tramite scripts/build-examples.ps1.

Corretti collegamenti mancanti al bordo, ordinamento degli angoli radiali, duplicati del centro, suddivisioni spurie del composto con Match=0 e snapping del primo punto notevole. L'adattatore genera ora le piastre di tutte le parti scollegate: il composto conserva 32 piastre anziché 16. TensileLib e il motore ARAP non sono stati modificati. Dettagli, prove, commit e righe: [FIVE-EXAMPLES.md](FIVE-EXAMPLES.md).

## Output di compilazione centralizzati - 6 ottobre 2026

Checkpoint prima delle modifiche: `5f7ef66`. Directory.Build.props imposta automaticamente `bin/<progetto>/<configurazione>/<framework>` alla radice per tutti i progetti C#, anche con compilazione diretta o da Visual Studio. Aggiornati i percorsi negli script completi/autonomi, nel caricamento del plugin da ExampleWriter e nella documentazione. Eliminate le sei precedenti cartelle bin sotto src, tests e tools dopo aver verificato i percorsi e l'assenza di file tracciati o collegamenti. Gli intermedi obj e i pacchetti artifacts mantengono la loro collocazione; le DLL dei repository GPC/TensileLib restano referenziate nei percorsi esterni esistenti.

## 0.2.0-rc.1 - 6 ottobre 2026

Nuovo progetto Arachne.Arap C# netstandard2.0, indipendente da Rhino/FEM e dalle librerie native. Implementati sviluppo 2D, deformazione 3D, vincoli, perimetro, algebra sparsa, diagnostica e cancellazione; collegati i componenti Grasshopper e aggiunte uscite informative senza cambiare GUID o ingressi. Correzioni a precisione dei pin, conversione Rhino e gestione del doppio fallimento nella geodetica. Pacchetto autonomo e distribuzione Arachne senza Arachne.IGL.dll. 144 controlli superati, inclusi sei confronti con il motore nativo. Resoconto, righe e limiti: [ARAP-REWRITE.md](ARAP-REWRITE.md). Esempi invariati.

## 0.1.0-rc.1 - 6 ottobre 2026

13 nuovi componenti (43 totali), snapshot del modello, risultati per ID, forze di bordo delle piastre, reazioni/residui, bilancio, storia/cancellazione, background, unita, distorsioni e controllo DLL. Refactoring dei tre algoritmi principali dei pannelli e build nativa ARAP dai sorgenti. TensileLib aggiornato a 2.1.0.0 dopo correzioni di pressione, accumulo del peso proprio, copia dei nodi e coordinate interamente vincolate. 100 test locali superati; esempi invariati. Dettagli, righe e limiti in [RELEASE-1.md](RELEASE-1.md) e [RESULTS.md](RESULTS.md).

## Stato iniziale salvato

Commit `3fab618`: progetto Arachne completo prima del collegamento reale a TensileLib. Lo scheletro compilava senza motore; 14 controlli superati.

## Passo 1 - collegamento TensileLib GPC

Modifiche autorizzate dopo il salvataggio iniziale:

| File | Modifica e motivo |
| --- | --- |
| Directory.Build.props | Riferimento alla DLL Release del progetto adiacente tensilelib. |
| src/Arachne.Core/Arachne.Core.csproj | Target net48 coerente con il motore net472 e il plugin; riferimento e controllo di presenza di alglibnet2. |
| src/Arachne.Core/TensileFormFindingSolver.cs | API 2.0.5.3: ID dei nodi; CreateMesh controlla l'output; costruttori a cinque argomenti. Valore 0 nelle impostazioni algebriche diventa il default reale del motore. |
| src/Arachne.Core/Validation.cs | Rifiuto esplicito della seconda tolleranza algebrica non supportata. |
| src/Arachne.Grasshopper/Components/SolverComponents.cs | Conservato l'ingresso TB per i documenti esistenti, con descrizione che richiede 0. |
| scripts/build.ps1 | Esecuzione test net48 e distribuzione di alglibnet2 insieme al motore. |
| tests/Arachne.Core.Tests | Test nel runtime del plugin e tre prove reali di equilibrio, compreso il controllo delle forze finali non lineari. |
| tools/Arachne.ExampleWriter/Program.cs | Verifica della ricerca di forma nel documento Grasshopper prima del salvataggio con Run=False. |

Compilazione Release: zero errori e avvisi. Test Core: 17 superati. Le sei anomalie diagnostiche del motore non sono corrette in questo passo; le correzioni vengono registrate separatamente.

## Riferimento teorico

Shell Structures for Architecture: Form Finding and Optimization, Adriaenssens, Block, Veenendaal e Williams (a cura di), Routledge 2014. Riferimenti pertinenti: capitoli 6 e 12, come riportato nel README di TensileLib. La verifica completa contro il testo resta distinta dai test di regressione.

Verifica Rhino/Grasshopper del passo 1: schema risolto (25 nodi, 40 cavi, 16 facce), generazione e riapertura GHX riuscite, file GH salvato con Run=False. Il caricamento di plugin Tekla installati produce messaggi di connessione esterni; i componenti Arachne non hanno errori.

## Passo 2 - sei correzioni nel motore

TensileLib commit `8c292de` (pubblicato su GitHub): convergenza all'origine, ultima iterazione non lineare, somma dei carichi, conservazione della risultante, eliminazione dell'accumulo dei carichi e copia delle piastre. Registro dettagliato in `../tensilelib/CHANGELOG.md`. Le 14 prove del motore passano.

## Passo 3 - primo trasferimento Patterning

Recuperati 19 componenti, che completano i 22 della cartella Patterning con i tre generatori gia trasferiti. Conservati nomi, ingressi/uscite e 16 icone; assegnati GUID nuovi elencati in PATTERNING-PORT.md. Copiati MeshGeodesic, ARAPHelper e DecompensationDescriptor. Compensation ora riceve Cable/Cable Constraints di Arachne al posto dei tipi FEM. Flattening usa gli ID interi richiesti da TensileLib. Copiata la DLL nativa x64 locale come Arachne.IGL.dll e aggiunto wrapper C ABI, senza dipendenza da assembly di geometria precedenti. Gli errori della libreria nativa vengono propagati invece di produrre vertici a zero.

Compilazione di tutti i componenti riuscita senza errori e avvisi. Verifiche funzionali del trasferimento in corso nel passo successivo.

## Passo 4 - regressioni riprodotte prima delle correzioni

La suite Rhino/Grasshopper copre tutti i 19 componenti aggiunti: 21 prove, 17 riuscite e 4 fallite sul codice trasferito. Stato salvato prima di intervenire, come richiesto.

| Errore riprodotto | Risultato attuale | Risultato atteso |
| --- | --- | --- |
| ARAP Deform: scala dei vincoli | Pin (1,1,0) restituito come (0.03,0.03,0). La DLL normalizza la mesh a 100, ma non i pin. | Pin nella posizione richiesta, nelle unita del modello. |
| Decompensation: interpolazione | Vertice a 1/4 della lunghezza trattato come parametro 1/2; X=0.875. | X=0.9375, interpolando la lunghezza geometrica originale. |
| Decompensation: modifica del descrittore | La lunghezza assoluta 4 viene sostituita con la frazione 1 nell'oggetto condiviso. | Descrittore riutilizzabile e invariato. |
| Descriptor Ext: liste diverse | Avviso seguito da eccezione per indice fuori intervallo. | Avviso e interruzione senza risultato. |

I primi falsi negativi del banco di prova (documento Rhino non attivo, output letti dopo la chiusura del documento GH e valori aggiunti ai default) sono stati eliminati nel test harness; non richiedono cambiamenti ai componenti. Line-Up e gli strumenti che usano la tolleranza del documento passano con il contesto Rhino corretto.

## Passo 5 - correzioni Patterning dopo il commit 388be78

- ARAPHelper: scala i pin secondo la normalizzazione della DLL nativa 0.9.0, sia per Deform sia per Unwrap. Mesh, indici e coordinate controllati prima delle chiamate native. La DLL nativa non e stata modificata.
- DeformArap: rifiuta indici negativi o fuori mesh e posizioni in eccesso; non elimina piu solo gli indici lasciando disallineate le posizioni. Conserva il completamento dei pin senza coordinate e la gestione dei duplicati. Gli errori di caricamento/configurazione sono gestiti nello stesso blocco della chiamata nativa.
- Decompensation: usa la lunghezza geometrica originale; inserisce i punti di cambio del descrittore; interpola copie locali ordinate. Gestisce segmenti lineari, estremi, punti coincidenti con scale uguali e segnala quelli con scale incompatibili. Non modifica piu descrittori o curve condivisi. Conservata la convenzione originale di scala rispetto al centro medio degli estremi.
- Descriptor Ext: dopo l'avviso per liste diverse termina senza produrre un descrittore parziale.
- Aggiunti quattro controlli ai 21 precedenti: ARAP Unwrap traslato, rifiuto dei pin negativi, compensazione 10%/20% e descrittori con punti coincidenti su una linea.

Esito: **25/25 prove Patterning**, nessun errore di compilazione. Ricalcolo e riapertura dello schema SaddleNet riusciti; equilibrio con 25 nodi, 40 cavi e 16 facce. Gli output nulli predisposti da Grasshopper nei percorsi di errore vengono distinti da vera geometria nei test.

## Passo 6 - esempio pannelli e verifica dei percorsi di errore

Creato 02_Patterning (10 oggetti, 8 collegamenti), verificato e ricalcolato dopo la riapertura di GH e GHX. Aggiornata la documentazione per le dipendenze e i componenti ora disponibili.

Sei ulteriori prove aggiunte prima delle correzioni: 25 passano, 6 falliscono. Rilevati: PlanarFixedLengthsSolver respinge un pannello gia convergente all'ultima iterazione; Flattening e Compensation pubblicano la mesh non convergente; Compensation pubblica una mesh anche dopo un'eccezione del solver; Flattening ARAP pubblica l'output quando la DLL restituisce false; Mesh Remap prosegue nonostante topologie incompatibili. Quest'ultimo gruppo e distinto dai sei difetti originali di TensileLib e dai quattro difetti del passo 4.

## Passo 7 - correzioni dopo il commit 66b2bac

- TensileLib `1cf5ac4`: risolto il controllo dell'ultima iterazione anche in PlanarFixedLengthsSolver, con due regressioni numeriche aggiuntive. Pubblicato su GitHub. Totale motore: 16 prove superate.
- Flattening e Compensation: interruzione senza geometria in caso di mancata convergenza; Compensation interrompe anche dopo un'eccezione, riportandone il messaggio.
- Flattening ARAP e ARAPHelper: esito false non pubblicato; controllo mesh triangolata e coordinate finite; configurazione nativa nel blocco di gestione errori.
- GetFlattenedMesh duplica l'ingresso e restituisce null se il solver non converge. GeodesicOffsets adeguato: fallback ARAP con copia triangolata, output con topologia inizializzata e configurazione esplicita, senza riusare un risultato non convergente.
- Mesh Remap: interrompe su conteggi incompatibili e su connettivita delle facce diversa, anziche proseguire con un semplice avviso.
- Il banco di prova usa un vincolo di lunghezza geometricamente incompatibile per il test negativo della compensazione: una riduzione uniforme puo convergere correttamente in una sola iterazione. Aggiunto riferimento GPCGeometry al progetto dei test Rhino per costruire questo vincolo.

Esito finale dei componenti: **31/31**; Core **17/17**; motore **16/16**. Entrambi gli esempi ricalcolano correttamente. Il refactoring generale delle classi e rinviato al passo da concordare; le modifiche fino a qui sono trasferimento, adeguamento API e correzione dei difetti riprodotti.
