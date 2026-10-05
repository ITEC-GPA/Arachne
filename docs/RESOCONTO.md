# Resoconto del trasferimento FeMM → Arachne

Data: 5 ottobre 2026. Questo documento descrive lo stato finale verificato e distingue migrazione, correzioni numeriche, componenti trasferiti e lavoro futuro. Il sorgente FeMM e rimasto invariato.

## Risultato disponibile

- Arachne: 30 componenti Grasshopper, inclusi tutti i 22 della cartella Patterning FeMM (3 generatori di reti e 19 strumenti aggiunti).
- TensileLib: compilazione Release riuscita con GPCGeometry e GPCUtilities locali, senza riferimenti agli assembly gestiti Maffeis.
- Collegamento Arachne alla DLL `../tensilelib/TensileLib/bin/Release/TensileLib.dll`, verificata anche per uguaglianza SHA-256 con la copia nel pacchetto Arachne.
- 64 controlli automatici superati: 16 TensileLib, 17 Arachne.Core e 31 Patterning. In aggiunta: generazione reti rettangolari/radiali, equilibrio SaddleNet e riapertura/ricalcolo degli esempi.
- Soluzione Arachne ed ExampleWriter compilati in Release con zero errori e zero avvisi.
- Due schemi reali: [01_SaddleNet.gh](../examples/01_SaddleNet.gh) e [02_Patterning.gh](../examples/02_Patterning.gh), entrambi disponibili anche in GHX.

Il pacchetto pronto da copiare nelle Libraries di Grasshopper e `artifacts/Release/Arachne`. Occorre copiare l'intera cartella, non soltanto il GHA. L'installazione nelle Libraries non e stata eseguita automaticamente. La verifica effettuata usa Rhino 8 in-process, Windows x64 e .NET Framework 4.8.

## Salvataggi e pubblicazione

### Arachne

Branch locale: `codex/tensile-gpc-integration`. Nessun remote Arachne e stato creato: la richiesta di pubblicazione riguardava TensileLib.

| Commit | Contenuto |
| --- | --- |
| `3fab618` | Salvataggio dello scheletro e dei primi componenti prima del collegamento reale a TensileLib. |
| `abdd921` | Adeguamento API, framework, DLL Release e verifica dell'equilibrio reale. |
| `2edbdae` | Primo trasferimento dei 19 componenti mancanti e collegamento ARAP nativo. |
| `388be78` | Test e registro di quattro errori riprodotti, salvati PRIMA delle correzioni. |
| `779dfed` | Correzione ARAP/decompensazione; 25 prove superate. |
| `66b2bac` | Secondo esempio, documentazione e sei ulteriori regressioni salvate PRIMA delle correzioni. |
| `6d9bd97` | Correzione dei percorsi di errore, aggiornamento al solver planare corretto e 31 prove superate. |

Il commit conclusivo della documentazione contiene questo resoconto e la generazione del manifest delle DLL; non cambia gli algoritmi numerici.

### TensileLib

Repository privato creato e popolato: [ITEC-GPA/TensileLib](https://github.com/ITEC-GPA/TensileLib). Branch predefinito `main`, 7 branch e 21 tag pubblicati. Ultimo commit del motore: `1cf5ac4`.

- `08d4149`: migrazione a GPC e diagnosi iniziale, algoritmi invariati.
- `2a5d910`: dipendenza System.Drawing.Common e riferimento teorico.
- `8c292de`: correzione dei sei difetti numerici iniziali.
- `1cf5ac4`: correzione aggiuntiva dell'ultima iterazione nel solver planare.

Autore e committer di tutti i commit pubblicati sono `GPC <g.pacini@itec-engineering.it>`, l'identita Git dell'utente. La verifica finale conta 134 commit unici considerando tutti i branch e tag; il contatore mostrato da GitHub sul solo main e inferiore perche non include la storia esclusiva degli altri branch.

La riscrittura ha conservato messaggi, date e contenuto degli alberi dei vecchi commit. Per questo i vecchi snapshot e tag possono contenere nomi o dipendenze precedenti: la migrazione del codice riguarda lo stato attuale di main, non altera retroattivamente ogni snapshot. Il vecchio server GitLab non e stato modificato.

Backup locale completo prima della riscrittura: `artifacts/tensile-audit/tensilelib-complete-original.bundle`. I due stash originali sono stati conservati localmente e inclusi nel backup, non pubblicati.

## Dipendenze trovate e distribuite

| File nel pacchetto | Versione verificata / funzione |
| --- | --- |
| Arachne.Grasshopper.gha | 0.1.0.0, plugin x64 net48 |
| Arachne.Core.dll | 0.1.0.0, modello e adattatore |
| TensileLib.dll | API/assembly 2.0.5.3, sorgente aggiornato a `1cf5ac4` |
| GPCGeometry.dll | 2.1.0.3, dal progetto locale Geometry |
| GPCUtilities.dll | 2.0.0.8, dal progetto locale Utilities |
| MathNet.Numerics.dll | 5.0.0.0 |
| alglibnet2.dll | 0.0.0.0 |
| System.Drawing.Common.dll | assembly 4.0.0.2, pacchetto 4.7.3 |
| Arachne.IGL.dll | nativa x64, ARAP 0.9.0, 708096 byte |

RhinoCommon, Grasshopper e GH_IO sono forniti dall'installazione Rhino e non copiati nel pacchetto. Lo script di build genera `DEPENDENCIES.json` con versione, dimensione e SHA-256 di ogni binario distribuito.

La DLL ARAP e stata trovata in `../maffeisigl/x64/Release/MaffeisIGL.dll` e viene copiata come Arachne.IGL.dll. Il wrapper gestito precedente e stato sostituito da un collegamento C ABI locale. Questo binario ARAP e una dipendenza nativa separata: non e GPCGeometry e il suo sorgente non e stato migrato o ricompilato. La normalizzazione dei pin nel nuovo wrapper corrisponde al comportamento della versione 0.9.0. Il percorso e configurabile tramite NativeIglPath.

Non mancano DLL per compilare e verificare il progetto su questo PC. Per un altro PC occorrono le stesse dipendenze, Rhino utilizzabile e il runtime C++ richiesto dalla DLL nativa; l'installazione su un PC pulito resta da provare.

## Adeguamento del collegamento al motore

- Arachne.Core e test portati a net48, compatibile con TensileLib net472 e con il plugin. Aggiunto il riferimento alglibnet2.
- AddNewBeam riceve gli ID interi dei nodi, non gli oggetti Node.
- Eliminata la chiamata a Model.CreateMesh assente nell'API disponibile. L'impostazione Arachne controlla l'uscita mesh; il motore puo comunque creare facce interne.
- Costruttori dei solver adeguati ai cinque argomenti disponibili. A=0 e AI=0 scelgono i default 1e-5 e 10000.
- La seconda tolleranza algebrica B e mantenuta come porta per compatibilita, ma deve essere 0: valori diversi sono rifiutati esplicitamente.
- Ricostruzione del modello a ogni richiesta, esiti e log espliciti, nessuna geometria di equilibrio in caso di errore o mancata convergenza.

Punti da leggere: [adattatore, creazione dei cavi](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/TensileFormFindingSolver.cs:77) e [impostazioni solver](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/TensileFormFindingSolver.cs:123).

## Sei difetti iniziali del motore, tutti corretti

| Difetto | Prima | Correzione | Codice da visionare |
| --- | --- | --- | --- |
| Convergenza all'origine | Il rapporto 0/0 classificava come non convergente un equilibrio con coordinate libere nulle. | Passo nullo e scala nulla danno errore 0; passo non nullo e scala nulla continuano le iterazioni. | [LinearForceDensitySolver:73](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/FormFinding/LinearForceDensitySolver.cs:73) |
| Ultima iterazione non lineare | Rifiutata prima di controllare la convergenza. | Controllo della convergenza prima del limite. | [NonLinearForceDensitySolver:151](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/FormFinding/NonLinearForceDensitySolver.cs:151) |
| Somma dei carichi del cavo | Componenti nulle causavano divisioni per zero e LoadPos non finita. | Somma vettoriale delle forze e dei primi momenti; conservati anche carichi non paralleli o a risultante nulla. | [Beam:338](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Beam.cs:338) |
| Risultante dei carichi nodali | Un carico -10 su un cavo lungo 2 diventava complessivamente -20. | F2=sum(F*s)/L e F1=sum(F)-F2. | [Model:1065](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Model.cs:1065) |
| Accumulo a ogni iterazione | I carichi dei cavi si sommavano di nuovo nei nodi. | Azzeramento dei soli BeamLoadForce prima di ogni ricalcolo. | [Model:1054](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Model.cs:1054) |
| Copia delle piastre | Array non inizializzati, errori sulle piastre triangolari e perdita di dati. | Copia degli array con dimensioni effettive, piano e materiale; mantenute le identita di nodi/cavi del modello. | [Plate:135](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/Geometry/Plate.cs:135) |

La correzione dei carichi mantiene la semantica originale di forze concentrate: pos e una distanza assoluta dal primo nodo nel motore. Arachne converte la frazione del componente Cable Load nella distanza iniziale. Non e stata introdotta una nuova formulazione per carichi distribuiti o mobili lungo il cavo deformato.

## Recupero completo dei componenti

La [mappa di trasferimento](PATTERNING-PORT.md) contiene i nomi e i GUID originali/nuovi. Sono stati recuperati:

- Cable Net, Radial Cable Net, Compound Net.
- Flattening, Flattening ARAP, Deform ARAP.
- Compensation, Decompensation, DecompensationDescriptor, DecompensationDescriptorExt.
- Borders, OrderBorders, Line-Up.
- Mesh Geodesic, Mesh Divide, Geodesic Offsets.
- Mesh Clean, Mesh Pipe, Mesh Remap, Split Mesh On Curves, Subdivide Mesh, Unweld Mesh.

Insieme a questi: 16 icone, MeshGeodesic, ARAPHelper e il modello dei descrittori. Le funzioni degli attributi FeMM di densita di forza, lunghezza, forza assiale, compensazione e mantenimento posizione sono raccolte in Cable, Cable Constraints e Node Conditions. Il solutore generale e esposto da Model, Solver Settings e Form Find.

GUID nuovi consentono di mantenere FeMM e Arachne insieme. Non e stato creato un convertitore automatico delle vecchie definizioni GH. Compensation ora riceve cavi Arachne con FixedLength e ConstraintWeight, senza dipendere dagli oggetti FEM FeMM.

## Difetti emersi nel trasferimento e correzioni aggiuntive

| Problema | Intervento | Codice da visionare |
| --- | --- | --- |
| ARAP portava il pin (1,1,0) a (0.03,0.03,0). | Adeguata la scala dei pin alla normalizzazione nativa, sia in 3D sia in Unwrap 2D; controllo degli indici prima della chiamata. | [ARAPHelper:16](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Helpers/ARAPHelper.cs:16) |
| Decompensation interpolava sul parametro invece che sulla lunghezza. | Campionamento delle lunghezze originali e dei punti di cambio prima di spostare i vertici. | [Decompensation:96](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/DecompensationComponent.cs:96) |
| Il descrittore condiviso veniva convertito e modificato. | Normalizzazione e ordinamento su una copia privata per ogni curva. | [Decompensation:77](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/DecompensationComponent.cs:77) |
| Descriptor Ext proseguiva dopo l'avviso sulle liste diverse. | Interruzione prima dell'accesso agli indici. | [DescriptorExt:68](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/DecompensationDescriptorExtComponent.cs:68) |
| Anche il solver planare respingeva l'ultima iterazione convergente. | Stesso ordine corretto dei controlli; due test numerici dedicati. | [PlanarFixedLengthsSolver:205](C:/Users/g.pacini/Documents/Repository/tensilelib/TensileLib/FormFinding/PlanarFixedLengthsSolver.cs:205) |
| Flattening pubblicava una mesh non convergente. | Avviso e nessun output; copia della mesh di ingresso nel metodo condiviso. | [Flattening:88](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/FlatteningComponent.cs:88) |
| Compensation pubblicava mesh dopo mancata convergenza o eccezione. | Interruzione in entrambi i percorsi, con messaggio dell'errore. | [Compensation:226](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/CompensationComponent.cs:226) |
| ARAP false veniva comunque pubblicato. | Nessun output in caso di esito false o coordinate non finite. | [FlatteningArap:83](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/FlatteningArapComponent.cs:83) |
| GeodesicOffsets doveva gestire il nuovo rifiuto del flattening non convergente. | Fallback ARAP configurato esplicitamente, con copia triangolata e topologia di uscita inizializzata. | [GeodesicOffsets:276](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/GeodesicOffsetsComponent.cs:276) |
| Mesh Remap proseguiva con topologie diverse. | Rifiuto dei conteggi e delle connessioni delle facce incompatibili. | [MeshRemap:84](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/MeshRemapComponent.cs:84) |

Decompensation accetta ora anche linee, gestisce estremi e punti duplicati con scale uguali, rifiuta valori non finiti e scale contrastanti allo stesso punto. Rimane la convenzione FeMM: centro comune medio degli estremi, scala effettiva = 1 - ingresso. Non e stata trasformata in un'inversa automatica della compensazione.

Le correzioni iniziali dello scheletro (limite delle iterazioni esterne, unificazione dei nodi, somma deterministica dei carichi, controlli sugli input e generatori) sono descritte una per una in [PORTING.md](PORTING.md). Il [CHANGELOG](CHANGELOG.md) conserva la sequenza completa prima/dopo.

Per rivedere anche quel primo gruppo: [IterationControl:14](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/IterationControl.cs:14) contiene il limite esterno; [ModelBuilder:10](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/ModelBuilder.cs:10) contiene copie, unificazione spaziale e carichi; [Validation:7](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Core/Validation.cs:7) contiene i controlli numerici; [CompoundNet:137](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Networks/CompoundNetComponent.cs:137) gestisce i rami e [CompoundNet:196](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Networks/CompoundNetComponent.cs:196) conserva anche le curve senza ReferenceID.

I generatori si trovano in [CableNetHelper:744](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Geometry/CableNetHelper.cs:744) e [CableNetHelper:1477](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Geometry/CableNetHelper.cs:1477); il controllo corretto sull'intersezione nulla e a [riga 2113](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Geometry/CableNetHelper.cs:2113).

## Verifiche eseguite e loro limiti

| Suite | Risultato | Che cosa verifica |
| --- | --- | --- |
| TensileLib diagnostics | 16/16 | Equilibri analitici, convergenza all'origine/ultima iterazione, risultanti e momenti, carichi opposti, copie delle piastre, convergenza planare e rifiuto di lunghezze incompatibili. |
| Arachne.Core.Tests | 17/17 | Topologia, copia degli ingressi, carichi, input invalidi, iterazioni, cancellazione, conversione unita GPC e tre solver reali. |
| PatterningChecks | 31/31 | Tutti i 19 componenti aggiunti, ARAP nativo in 2D/3D, compensazione 10%/20%, interpolazione e riuso dei descrittori, geodetiche, strumenti mesh e percorsi di errore. |
| SaddleNet | Riuscito | Equilibrio di 25 nodi, 40 cavi, 16 facce; GHX riaperto e modello ricalcolato; GH prodotto; Run=False al salvataggio. |
| Patterning example | Riuscito | GH e GHX riaperti e ricalcolati; 10 oggetti, 8 collegamenti, quattro bordi e area compensata attesa. |

All'avvio Grasshopper carica anche plugin Tekla presenti sul PC, che scrivono messaggi di mancata connessione a Tekla. Sono esterni ad Arachne; i test controllano separatamente gli errori dei componenti Arachne e terminano con codice diverso da zero se falliscono.

I log locali sono in `artifacts/tensile-audit`: `final-tensile-tests.txt`, `final-build.txt`, `final-rhino-checks.txt`, `patterning-before-fixes.txt` e `patterning-failure-paths-before.txt`. La prima migrazione del motore era stata confrontata con la DLL originale: stessi 3 pass e 6 fail, prima delle correzioni.

## Lavoro ancora da fare, da concordare prima del refactoring

1. **Separare algoritmi e componenti Grasshopper.** Primo passo proposto: estrarre compensazione, decompensazione e sviluppo pannelli in servizi dedicati, conservando GUID, nomi e porte. Eliminare codice duplicato e usings/commenti obsoleti dopo aver mantenuto verdi le regressioni. Non e ancora iniziato un refactoring generale dell'architettura.
2. **Validazione teorica e strutturale piu ampia.** Confrontare il codice con il libro indicato, in particolare Force Density e Nonlinear Force Density; verificare pressioni, peso proprio, membrane, pesi combinati, unita e segni su casi analitici e modelli FeMM rappresentativi. Il testo completo del libro non e stato analizzato: i test non provano conformita integrale alla teoria.
3. **Geometrie di produzione.** Eseguire casi curvi complessi, pannelli non sviluppabili, piu bordi/fori, mesh degeneri o sconnesse, direzioni arbitrarie e modelli grandi. I test attuali comprendono casi piccoli, planari e alcuni percorsi negativi; non esauriscono tutte le casistiche geometriche.
4. **Diagnostica dei pannelli.** Esporre residui, variazione di area/lunghezze e qualita del risultato; chiarire nell'interfaccia le diverse unita di Compensation e dei descrittori. Dopo questi interventi stabilire soglie di accettazione condivise per la produzione.
5. **ARAP riproducibile.** Portare o configurare anche la compilazione del progetto nativo, definire un controllo di versione/ABI e provarne le dipendenze C++ su un secondo PC. La DLL attuale e verificata localmente, non sostituita da una libreria geometrica GPC.
6. **Runtime e prestazioni.** Provare caricamento interattivo e compatibilita con altri runtime di Rhino, poi gestire operazioni lunghe e cancellazione. Attualmente il calcolo GH e sincrono; la cancellazione Core non interrompe una singola chiamata interna del motore.
7. **Distribuzione e migrazione dei documenti.** Versionare una release, predisporre build/test ripetibili su un ambiente pulito e, se utile, un convertitore GUID dei documenti FeMM. I vecchi branch/tag TensileLib restano snapshot storici. La pubblicazione di Arachne richiede scegliere il relativo repository.

Il riferimento teorico registrato e *Shell Structures for Architecture: Form Finding and Optimization*, a cura di Sigrid Adriaenssens, Philippe Block, Diederik Veenendaal e Chris Williams, Routledge 2014; [scheda dell'editore](https://www.routledge.com/Shell-Structures-for-Architecture-Form-Finding-and-Optimization-1st-Edition/Adriaenssens-Block-Veenendaal-Williams/p/book/9780415840590).
