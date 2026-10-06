> Documento storico della 0.1.0-rc.1. Il motore ARAP nativo qui descritto e stato sostituito nella 0.2.0-rc.1: vedere [riscrittura ARAP](ARAP-REWRITE.md).

# Prima release: registro degli interventi

Richiesta del 6 ottobre 2026: realizzare gli interventi della prima release; gli esempi saranno preparati dall'utente. Non rigenerare i file in examples.

## Stato salvato prima delle modifiche

Baseline Arachne: d41d6ff. Baseline TensileLib: 1cf5ac4. Nessuna modifica locale all'avvio.

Problemi verificati leggendo l'implementazione, prima di intervenire:

1. FormResult contiene liste parallele, senza modello associato, identificativi o interrogazione degli elementi.
2. Form Find non espone un oggetto risultato. Le facce vengono conservate solo quando CreateMesh=true.
3. Plate in TensileLib non contiene un tensore di tensione calcolato. SumStresses e un dato di ingresso. Anche il vecchio FeMM aggiornava la geometria delle piastre senza ricavare risultati di membrana.
4. Mancano risultati nodali completi, reazioni, residui, bilancio delle forze/momenti e storia delle iterazioni interne.
5. La cancellazione viene controllata solo prima/dopo un'intera chiamata al solver.
6. Il programma delle verifiche Rhino rigenera gli esempi: serve un percorso di soli test.

## Interventi autorizzati e ordine

- Risultati associati a una copia del modello, identificativi stabili all'interno del modello e interrogazione di nodi, aste, piastre.
- Deconstruct Model/Result, Node/Beam/Plate Results, Check Model ed Equilibrium Report.
- Storia effettiva delle iterazioni e cancellazione cooperativa; anteprima dei risultati.
- Panel Distortion, conversioni esplicite delle unita con GPCUtilities, controllo delle dipendenze.
- Separazione progressiva degli algoritmi dai componenti, conservando GUID e porte preesistenti.
- Verifiche numeriche e Grasshopper ripetibili, pacchetto Release e documentazione con righe da visionare.

## Criteri e limiti

Le forze delle aste sono in N, le densita di forza in N/m, le pressioni in N/m2. I risultati delle piastre devono distinguere forze di bordo, obiettivi di pretensione e tensioni di membrana: non inventare Nx/Ny/Nxy a partire da dati non presenti nel motore. I valori non disponibili devono essere riconoscibili come tali.

I test automatici comprendono casi analitici e geometrie sintetiche; non sostituiscono il confronto completo con il libro indicato, i modelli reali dell'utente e la prova su un altro PC. Queste verifiche esterne devono rimanere dichiarate nel resoconto.

## Esito della realizzazione

Versione preparata: **Arachne 0.1.0-rc.1**, con **TensileLib 2.1.0.0** e **ARAP 0.9.0 ricompilato x64**. Totale: **43 componenti**, i 30 precedenti e 13 nuovi. Gli esempi non sono stati modificati. Le DLL locali necessarie sono presenti e il controllo del caricamento ha esito positivo su questo PC.

E una release candidata: la validazione locale e completata per i casi sotto, mentre restano esplicite le verifiche esterne elencate in fondo. Il funzionamento e l'interpretazione dei risultati sono descritti in [RESULTS.md](RESULTS.md).

## Modifiche e codice da controllare

| Area | Prima e dopo | Codice |
| --- | --- | --- |
| Modello iniziale | Oggetti geometrici modificabili a valle; ora coordinate/vettori/cavi/facce vengono protetti mediante copie. Aggiunti GUID modello e ID degli elementi. | [Model.cs:86](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Model.cs:86) |
| Risultato | Liste parallele senza sorgente; ora snapshot iniziale, impostazioni, identita del calcolo e SolvedModel separato. Le vecchie liste restano per compatibilita. | [Model.cs:122](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Model.cs:122), [Results.cs:96](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Results.cs:96) |
| Nodi | Aggiunti spostamenti, contributi distinti dei carichi, forze interne, reazioni e residui sui gradi liberi. | [Results.cs:19](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Results.cs:19) |
| Aste | Aggiunti record con ID degli estremi, lunghezze iniziali/finali, N e q, obiettivo e direzione. | [Results.cs:49](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Results.cs:49) |
| Piastre | Conservate anche con CreateMesh=False; aggiunti nodi, aree, associazione ordinata alle aste di bordo e loro tiri. Nx/Ny/Nxy dichiarati non disponibili. | [Results.cs:69](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Results.cs:69) |
| Deconstruct e interrogazione | Nuovi Deconstruct Model/Result e Node/Beam/Plate Results, selezione mediante ID e alberi di connettivita. | [ResultComponents.cs:27](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/ResultComponents.cs:27) |
| Equilibrio | Verifica indipendente di residui, risultante e momento; tolleranze esplicite in N e N*m. | [Results.cs:125](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Results.cs:125) |
| Validazione modello | Parti sconnesse, traslazioni non vincolate, elementi degeneri, orientamento facce e lati non manifold; nessuna pretesa di certificare la stabilita non lineare. | [ModelChecks.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/ModelChecks.cs) |
| Storia e annullamento | Callback reali nei solver; storia mantenuta anche quando si annulla, senza pubblicare geometria parziale. | [Solver.cs:18](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/FormFinding/Solver.cs:18), [SolverHistory:150](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/ResultComponents.cs:150) |
| Calcolo in background | Form Find aggiunge Background e Cancel senza spostare le porte precedenti; input variati invalidano i risultati precedenti. | [SolverComponents.cs:116](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/SolverComponents.cs:116) |
| Unita e distorsioni | Unit Settings, Convert Units e Panel Distortion con quantita e soglie esplicite. | [ReleaseUtilities.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/ReleaseUtilities.cs) |
| Anteprima e DLL | Colori per N, q o residuo; controllo dei percorsi e versioni caricate, API del motore ed esportazioni ARAP. | [ReleaseUtilityComponents.cs:53](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/ReleaseUtilityComponents.cs:53) |
| Refactoring pannelli | Algoritmi estratti da Compensation, Decompensation e Flattening in servizi privi di dipendenze dall'interfaccia GH. Tolleranza geometrica passata esplicitamente; GUID e porte preservati. | [CompensationService.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Services/CompensationService.cs), [DecompensationService.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Services/DecompensationService.cs), [FlatteningService.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Services/FlatteningService.cs) |
| ARAP dai sorgenti | Nuovo progetto nativo Arachne.IGL, configurazione x64, sorgenti/include forniti come parametri e manifest con hash. Non modificati gli algoritmi C++ originali. | [Progetto nativo](C:/Users/g.pacini/Documents/Repository/Arachne/native/Arachne.IGL.vcxproj), [build-native.ps1](C:/Users/g.pacini/Documents/Repository/Arachne/scripts/build-native.ps1) |
| Distribuzione | Build con test Core e opzione per test Rhino; manifest delle DLL, provenienza nativa e commit del progetto. Modalita --tests-only senza rigenerare esempi. | [build.ps1](C:/Users/g.pacini/Documents/Repository/Arachne/scripts/build.ps1) |

## Nuovi errori riprodotti e corretti in TensileLib

| Difetto riprodotto | Correzione e conseguenza | Test salvato prima | Codice |
| --- | --- | --- | --- |
| Un triangolo sotto pressione restituiva forza nulla; i nodi di bordo erano ignorati e la normale era forzata verso Z positivo. | Integrazione sulle facce orientate, inclusi i nodi di bordo; triangoli con carico nodale costante e quad divisi lungo la diagonale 1-3. Cambia intenzionalmente la convenzione del verso della pressione. | 9118f7e | [Model.cs:1082](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Model.cs:1082) |
| Ricalcolare il peso proprio portava una risultante -200 N a -400 N. | Azzeramento del contributo prima di ogni ricalcolo. | 9118f7e | [Model.cs:1030](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Model.cs:1030) |
| La copia di un nodo leggeva il settimo vincolo di un array di sei elementi; inoltre condivideva i vettori e perdeva i pesi non lineari. | Copia dei sei vincoli, vettori indipendenti e conservazione dei pesi. | 38044a0 | [Node.cs:251](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Node.cs:251) |
| Un asse completamente vincolato mandava alla libreria algebrica un sistema senza incognite. | Soluzione vuota per quell'asse, mantenendo calcolo dei tiri/carichi/reazioni. Verificati anche modelli completamente vincolati. | 4f1f83a | [ForceDensitySolver.cs:912](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/FormFinding/ForceDensitySolver.cs:912) |

Durante il refactoring una prima guardia rifiutava il valore 0 delle iterazioni algebriche usato dalle geodetiche: il test esistente lo ha rilevato. Ora 0 sceglie il limite predefinito 10000; la regressione Geodesic Offsets e tornata verde. Aggiunte inoltre guardie per direzione warp nulla, percentuali di compensazione non finite o >=100 e pannelli vuoti.

Un ultimo test (commit 1986c27) ha riprodotto una dipendenza impropria tra calcolo e anteprima: CreateMesh=False impediva il calcolo della pressione quando le facce erano generate dal motore. La generazione della topologia e ora indipendente dalla visualizzazione della mesh; vedere [TensileFormFindingSolver.cs:122](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/TensileFormFindingSolver.cs:122). Il test prima della correzione e in `artifacts/tensile-audit/release1-preview-before.txt`.

## Verifiche

**100 test superati:** 22 nel motore, 33 nel Core, 31 Patterning e 14 integrazione dei nuovi componenti. In aggiunta vengono eseguite le verifiche geometriche dei generatori rettangolare e radiale.

- Caso analitico: rete quadrata, q=2 N/m e pressione 8 N/m2; spostamento centrale 1 m, risultante verticale 32 N, reazioni verticali -32 N e bilancio dei momenti entro tolleranza.
- Triangolo completamente vincolato: area 3 m2, pressione 10 N/m2, risultante 30 N e reazioni -30 N.
- Pressione con solver non lineare, distinzione dei canali peso/carichi cavo, trasferimento degli obiettivi di membrana, cancellazione dopo una vera iterazione interna e assenza di risultati parziali.
- Interrogazione GH delle piastre con mesh disabilitata, ID/forze di bordo corretti, stato esplicito delle tensioni non disponibili, background e svuotamento all'arresto.
- Tutti i test Patterning sono eseguiti anche con la DLL ARAP ricompilata e con gli algoritmi estratti nei servizi.

Build gestita: zero errori e zero avvisi. Build C++: riuscita; presenti avvisi di conversione numerica nei sorgenti/header legacy libigl/Eigen, conservati nel log e non soppressi. I plugin Tekla installati producono messaggi esterni di mancata connessione all'avvio di Rhino; i test Arachne distinguono i propri esiti e falliscono con codice di uscita non nullo.

Log locali in `artifacts/tensile-audit`: `release1-tensile.txt`, `release1-core.txt`, `release1-rhino-final.txt`, `release1-native.txt`; prove prima delle correzioni in `release1-before.txt`, `release1-node-copy-before.txt`, `release1-fixed-dofs-before.txt`.

## Commit principali

- Arachne 921c792: piano e audit salvati prima delle modifiche.
- Arachne fa6a2d6: modello/risultati strutturati, interrogazione e diagnostica.
- Arachne 0b920fb: servizi dei pannelli, build nativa, background e regressione dei vincoli completi.
- TensileLib 19c4ce3: pressioni, peso proprio e callback delle iterazioni.
- TensileLib e5d8302: copia corretta dei nodi.
- TensileLib 1408a0b: coordinate completamente vincolate e versione 2.1.0.0.

## Cosa resta da fare

1. **Esempi e modelli di produzione:** li preparera l'utente. Verificare casi reali curvi, con fori, piu pannelli e modelli grandi; non sono sostituiti dai casi sintetici.
2. **Confronto completo con il libro:** il testo completo non e disponibile nei materiali esaminati. La formulazione Membrane/Curvature originaria e i pesi non lineari richiedono una revisione teorica specifica; non sono dichiarati validati integralmente.
3. **Risultati continui di membrana:** definire e validare il recupero di Nx/Ny/Nxy e delle direzioni principali. L'interrogazione attuale espone i tiri reali della rete e non simula risultati di piastra assenti.
4. **Installazione su altro PC e altri runtime Rhino:** il pacchetto e verificato qui con Rhino 8/.NET Framework x64. Rimangono da verificare ambiente pulito, runtime C++ e convivenza con i plugin effettivamente usati dall'utente.
5. **Archiviazione e operazioni lunghe sui pannelli:** i risultati sono dati transitori; l'archivio permanente/versionato dei calcoli e la cancellazione interna di ARAP sono sviluppi successivi. Background e cancellazione di questa release riguardano Form Find.
6. **Estensioni successive:** Material/Fabric Directions, Seam Check/Allowance, etichette ed export, Compare Results, Batch Solve, campi di pretensione/pressione, vincoli su curve e convertitore delle definizioni FeMM. Nessuna di queste funzioni e dichiarata presente nella release.

## Prossimo lavoro in coda: ARAP autonomo

Richiesta aggiunta dall'utente durante le verifiche finali: analizzare/decompilare la libreria ARAP e riscriverla come progetto a se stante, dopo la chiusura della prima release.

I sorgenti C++ originali sono disponibili localmente; il progetto native/Arachne.IGL.vcxproj aggiunto in questa release li compila tramite percorsi esterni, ma non costituisce ancora la riscrittura autonoma richiesta.

Ordine del lavoro successivo:

1. Inventario dell'ABI e confronto fra DLL attuale e sorgenti; usare la decompilazione/disassemblaggio per le parti non ricostruibili o divergenti, preservando le informazioni di provenienza.
2. **Scelta confermata dall'utente: nuova implementazione del motore numerico ARAP, preferibilmente in C#.** Il lavoro previsto e un motore C# autonomo, con geometria GPC, separato da Rhino/Grasshopper e senza dipendenza esecutiva da libigl/Eigen. Sorgenti e DLL C++ restano il riferimento di confronto.
3. Commit della baseline e creazione del progetto indipendente, con API esplicita, configurazione per istanza e gestione degli errori.
4. Eliminazione della dipendenza dai percorsi del vecchio repository e definizione delle dipendenze necessarie alla build.
5. Test comparativi di flattening, unwrap/deform, pin, scale, orientamento, mesh invalide, prestazioni e ripetibilita.
6. Collegamento ad Arachne dopo il superamento delle prove, documentando gli eventuali cambiamenti numerici.

## Ultima correzione: facce esterne sovrapposte

Il test della topologia implicita ha inoltre rivelato che TensileLib generava otto facce su una griglia di quattro celle: triangolava anche il contorno esterno sopra le celle gia presenti. Questo alterava aree e carichi di pressione. Salvata la regressione nel commit TensileLib 9ea9ab6, poi corretto nel commit 0e7f5a6 applicando il controllo TestFace prima di accettare il contorno. Ora la griglia restituisce quattro facce e area totale 4 m2. Codice da visionare: [Model.cs:770](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Model.cs:770). Il log prima del fix e `artifacts/tensile-audit/release1-outer-face-before.txt`.
