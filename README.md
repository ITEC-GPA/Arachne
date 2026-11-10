# Arachne

Plug-in Grasshopper per il form finding e il patterning di tele e tensostrutture. Contiene i componenti di FeMM dedicati a questi due temi, con gli stessi ingressi e uscite tranne lo snellimento (dati SAP tolti, opzioni del Form Find in Solver Options); i GUID sono nuovi, così i due plug-in si possono caricare insieme. Sono corretti i bug trovati durante il trasferimento ([dettagli](docs/PORTING.md)). Componenti nuovi: Membrane Material, Solver Options, Check Model, Beam Forces, Support Reactions e Panel Distortion.

**Versione:** 0.4.0-rc.1 (DLL 0.4.0.1). I dati del modello sono oggetti GPC (GPCModel): nodi, aste, piastre, proprietà, carichi e risultati ([registro modifiche](docs/CHANGELOG.md)). Si lavora in N e mm. Motore di form finding: TensileLib 2.5 (`../tensilelib`). Sviluppo dei pannelli ARAP: motore C# `Arachne.Arap` ([resoconto](docs/ARAP-REWRITE.md)). Librerie GPC: GPCGeometry, GPCUtilities, DelaunayMesh, GPCModel, GPC.Model.Persistence e GPC.Archive. Sono le stesse di ReBear e BridGHe, che si caricano nello stesso Rhino: i tre plug-in devono usare lo stesso set (`release-set.json`) e si aggiornano insieme.

## Struttura

```text
Arachne.sln
Arachne.Arap.sln             Soluzione autonoma del motore ARAP e dei suoi test
src/
  Arachne.Common/            Confine verso GPC: Build Model, form finding con TensileLib, sezioni, archivi dei dati GH
  Arachne.Grasshopper/       Componenti, data type e parametri (.gha)
  Arachne.Arap/              Motore ARAP C# netstandard2.0
tests/Arachne.Arap.Tests/    Verifiche numeriche del motore ARAP
tools/Arachne.ExampleWriter/ Verifiche in Rhino/Grasshopper senza finestra, esempi e icone
tools/Arachne.Compare/       Confronto con un'altra versione: definizioni, esempi, controllo al caricamento
examples/                    Definizioni GH e GHX
scripts/                     Compilazione, verifiche, pacchetto ed esempi
docs/                        Provenienza, architettura, ARAP, registro modifiche
```

## Flusso di lavoro

È quello di FeMM: gli elementi del modello ricevono gli attributi di form finding, Build Model li assembla e Form Find calcola la forma.

```mermaid
flowchart LR
  MAT[Material] --> BP[Beam Property]
  SEC[Section] --> BP
  BP --> B[Beam]
  LC[Load Case] --> FD
  B --> FD[Force Density / Fixed Length / Fixed Axial Force / ...]
  N[Node] --> R[Restraints / Keep Position / Node Load]
  FD --> BM[Build Model]
  R --> BM
  MM[Membrane Material] --> PP[Plate Property]
  PP --> PL[Plate]
  PL --> BM
  BM --> CK[Check Model]
  BM --> FF[Form Find]
  PP -.-> SO[Solver Options]
  SO --> FF
  FF --> OUT[Model · Mesh]
  OUT --> RES[Beam Forces · Support Reactions]
  OUT --> PAT[Patterning: Flattening, Compensation, Borders, ...]
  PAT --> PD[Panel Distortion]
```

Note d'uso, come in FeMM:

- **Unità:** si lavora in N e mm, il predefinito di Build Model (Nmm). Le dimensioni delle sezioni sono nelle unità del modello (mm), le densità di forza in N/mm e la gravità di Solver Options, se si vuole il peso proprio, vale 9806.65 mm/s². Le tolleranze di Solver Options sono relative; TensileLib risolve il modello centrato nell'origine, quindi le coordinate di cantiere non peggiorano la convergenza né la precisione. In IPS masse e densità sono in libbre-massa (Custom Material in lb/in³), le membrane sempre in g/m².
- **Rami:** collegare a Build Model tutti gli elementi in un'unica lista, appiattendo l'ingresso Elements se arrivano da percorsi diversi. Gli elementi con lo stesso GUID (per esempio una Beam passata da Force Density e da Fixed Length) vengono uniti.
- **Fusione dei nodi:** due nodi più vicini di 0.001 (unità del modello) vengono uniti con attributi e carichi. FeMM leggeva questa tolleranza dalle opzioni del suo plug-in Rhino.
- **Form Find:** calcola con il pulsante **Run** oppure con Auto Run = True. Gravità, pressione, casi di carico, iterazioni, tolleranze e mesh si danno con Solver Options; senza, valgono i predefiniti. Load Cases sceglie i casi dei carichi nodali e sulle aste da applicare (vuoto: tutti).
- **Peso proprio:** con Gravity diversa da 0 pesano le aste (area × densità della sezione) e le piastre con una Plate Property (spessore × densità; per un Membrane Material il peso in g/m² della scheda). Le piastre generate dalle aste prendono il Plate Property di Solver Options. La pressione richiede un Load Case, a cui vengono assegnati i carichi nodali risultanti; una pressione positiva agisce secondo la normale dell'ingresso Plane (+Z con il piano predefinito). Quei carichi nodali sono un risultato, come i pre-carichi: se il modello calcolato passa a un altro Form Find, non vengono applicati di nuovo (la pressione di Solver Options agisce sulla nuova forma) e vengono sostituiti. I solutori Linear e Non-Linear pubblicano il risultato anche al limite di iterazioni, con un avviso. Linear step by step si ferma invece con un errore.
- **Vincoli:** Restraints vincola le traslazioni lungo X, Y, Z oppure, con un piano nell'ingresso Coordinate System, lungo gli assi X e Y del piano e la sua normale Z. Per esempio, un nodo che scorre su una retta inclinata è vincolato in Y e Z con l'asse X del piano lungo la retta; un nodo su un piano inclinato è vincolato solo in Z. Le rotazioni di FeMM non ci sono più, perché il form finding non le usa.
- **Risultati:** il modello calcolato contiene le forze assiali delle aste (pre-carichi, come in FeMM) e, se il calcolo converge, le reazioni dei vincoli, solo nelle direzioni vincolate. Beam Forces e Support Reactions le estraggono.
- **File:** i risultati del Form Find si salvano nel file `<documento>.<InstanceGuid>.gpc-results` accanto al `.gh`, che tiene solo i riferimenti: copiare o spostare il `.gh` con i suoi `.gpc-results`. Riaprendo il documento con Auto Run spento il Form Find rimette le uscite senza ricalcolare («Restored»). I dati internalizzati di Arachne salvati dalla 0.3 non si leggono più: un messaggio lo dice e vanno internalizzati di nuovo; componenti, fili e dati Grasshopper standard restano.
- **Carichi sulle aste:** solo forze, uniformi o trapezie, in un sistema globale o in un Coordinate System; sono ripartite sui nodi con la regola della leva e restano alla stessa frazione della lunghezza quando l'asta si allunga.
- **Filtri:** Node, Beam e Plate Filter cercano per ID gli identificativi assegnati con Element Id.
- **Cable Net:** Warp, Weft e Restraint edges sono alberi con un ramo per cavo. Appiattirli prima di Beam.

## Componenti

| Pannello | Componenti |
| --- | --- |
| 01-Parameters | CoordinateSystem |
| 02-Materials | Aluminium, Concrete, Custom Material, Steel, *Membrane Material* |
| 03-Sections | Angle, Brep, C, Generic, Hollow Circle, Hollow Rect, I, Solid Circle, Solid Rect, T, Section Geometric Property |
| 04-Definitions | Load Case, Group |
| 05-Properties | Beam Property, Plate Property |
| 06-Model | Node, Beam, Plate |
| 07-Attributes | Force Density, Fixed curvature cable, Membrane type, Fixed Length, Fixed Axial Force, Fixed compensation, Keep Position, Restraints, Element Id |
| 08-Loads | Node Load, Beam Point Load, Beam Distributed Load |
| 09-Model | Build Model, Decompose Model, *Check Model* |
| 12-Analysis | Form Find, *Solver Options*, *Beam Forces*, *Support Reactions* |
| 13-Patterning | Borders, OrderBorders, Compensation, Decompensation, DecompensationDescriptor, DecompensationDescriptorExt, Flattening, Flattening ARAP, Deform ARAP, Geodesic Offsets, Line-Up, Mesh Clean, Mesh Divide, Mesh Geodesic, Mesh Remap, Remesh, Split Mesh On Curves, Subdivide Mesh, Unweld Mesh, *Panel Distortion* |
| 14-Tools | Cable Net, Radial Cable Net, Compound Net, Curve2Mesh, Node Filter, Beam Filter, Plate Filter |

Le sezioni servono al peso proprio (area) e all'anteprima delle aste, disegnate con la loro sezione. In corsivo i componenti nuovi di Arachne, che non esistono in FeMM. La tabella con sorgente FeMM e GUID di ogni componente è in [PORTING.md](docs/PORTING.md); le icone sono in [ICONS.svg](docs/ICONS.svg).

## Esempi

[01_SaddleNet](examples/01_SaddleNet.gh) (sella con vincoli al bordo, con Check Model, Beam Forces e Support Reactions), [02_Patterning](examples/02_Patterning.gh) (sviluppo, compensazione, bordi e decompensazione di un pannello) e [03_CableNet](examples/03_CableNet.gh) (rete generata da Cable Net). Ogni file esiste anche in GHX. Vedere [examples/README.md](examples/README.md). Gli esempi sono rigenerati per la 0.4: includono dati GPC internalizzati e i sidecar dei risultati. Auto Run è spento e i risultati vengono ripristinati senza ricalcolo.

## Compilazione e verifiche

Prerequisiti: Windows x64, SDK .NET 8 o successivo, .NET Framework 4.8 Developer Pack, Rhino 8, TensileLib compilata in Release nel repository adiacente. Le sei librerie GPC stabili sono incluse in `lib/GPC` con il manifest originale del set.

```powershell
.\scripts\build.ps1 -RhinoTests
.\scripts\build.ps1 -RhinoTests -ReleaseSet <cartella del set>\release-set.json   # per un rilascio
```

La build usa le DLL stabili di `lib/GPC` e verifica versione e SHA-256 prima della compilazione; non ricompila i moduli condivisi. `-ReleaseSet` permette un set stabile alternativo, con le DLL accanto al manifest. `-FemmSourcePath` indica i sorgenti FeMM da confrontare in sola lettura.

Gli output della compilazione stanno nella cartella `bin` alla radice, in `bin/<progetto>/<configurazione>/<framework>`; i file intermedi restano nella cartella `obj` di ogni progetto, perché contengono una copia del `.gha`. Lo script esegue i test del motore ARAP. Con `-RhinoTests` avvia Rhino senza finestra ed esegue le verifiche Grasshopper:

- ingressi e uscite di ogni componente confrontati con i sorgenti FeMM;
- GUID e icone;
- form finding su casi analitici;
- patterning e reti.

Il plug-in è la cartella `bin/Arachne.Grasshopper/Release/net48`: `.gha` e DLL necessarie, con `DEPENDENCIES.json` (versioni e SHA-256) e `BUILD.json` (commit, verifiche eseguite e set GPC, scritto solo se tutte le verifiche passano). Le DLL sono `Arachne.Grasshopper.gha`, `Arachne.Common.dll`, `Arachne.Arap.dll`, `GPCGeometry.dll`, `GPCUtilities.dll`, `DelaunayMesh.dll`, `GPCModel.dll`, `GPC.Model.Persistence.dll`, `GPC.Archive.dll`, `TensileLib.dll` (con `TensileLib.dll.config`), `alglibnet2.dll` e `MathNet.Numerics.dll`; lo script si ferma se ce n'è un'altra o se manca una, e se trova Gmsh o i pacchetti NRBF. Per installarlo, copiare la cartella nelle Libraries di Grasshopper.

Se Grasshopper carica il plug-in dalla cartella `bin`, chiudere Rhino prima di compilare e compilare solo in Release. Grasshopper cerca i `.gha` anche nelle sottocartelle, quindi sotto `bin` deve essercene uno solo: una build Debug ne aggiungerebbe un secondo. Per lo stesso motivo ExampleWriter non copia il `.gha` nella sua cartella e lo carica da `bin/Arachne.Grasshopper`. Per verificare una build con Rhino aperto si compila fuori dal repository, con la proprietà `ArachneBin` (`dotnet build Arachne.sln -c Release -p:ArachneBin=<cartella>\`), e si lanciano le verifiche da lì.

Percorsi predefiniti, modificabili con proprietà MSBuild o parametri dello script:

| Proprietà | Percorso rispetto alla cartella Arachne |
| --- | --- |
| GpcGeometryPath | `lib/GPC/GPCGeometry.dll` |
| GpcUtilitiesPath | `lib/GPC/GPCUtilities.dll` |
| TensileLibPath | `../tensilelib/TensileLib/bin/Release/TensileLib.dll` |
| RhinoSystemDir | `C:/Program Files/Rhino 8/System` |
| GrasshopperDir | `C:/Program Files/Rhino 8/Plug-ins/Grasshopper` |

`GpcSetDirectory` seleziona la cartella del set completo (default `lib/GPC`). Non ci sono riferimenti di progetto a `../Model`: una build di Arachne non modifica gli output degli altri plug-in.

Altri script: `scripts/build-examples.ps1` rigenera gli esempi e `docs/ICONS.svg`; `scripts/build-arap.ps1` compila solo il motore ARAP; `scripts/build-native.ps1` ricompila in `bin/Arachne.IGL/Release` la vecchia DLL nativa, usata solo per i confronti facoltativi del motore ARAP. Per le icone dei nuovi componenti aggiungere il disegno in `tools/Arachne.ExampleWriter/IconArtwork.cs` ed eseguire ExampleWriter con `--icons`.

Tutti gli script accettano `-ArachneBin`: i percorsi relativi sono risolti dalla
radice del checkout, con o senza separatore finale e anche con spazi. Test,
esempi e manifest usano lo stesso output della compilazione. Per esempio:

```powershell
.\scripts\build.ps1 -Configuration Debug -ArachneBin 'artifacts/debug checks' -RhinoTests
.\scripts\build-examples.ps1 -Configuration Debug -ArachneBin 'artifacts/debug checks' -ExamplesRoot 'artifacts/generated examples'
```

Le build Debug del plug-in dentro `bin` (anche nelle sue sottocartelle) sono
rifiutate sia dagli script sia da MSBuild. La soluzione ARAP autonoma non
produce `.gha` e può usare Debug anche nell'output predefinito.

Per una verifica rigorosa della compatibilità con FeMM:

```powershell
.\scripts\build.ps1 -RhinoTests -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
```

Il percorso FeMM è configurabile ed è usato solo in lettura. Senza `-RequireFemm`
il confronto può essere saltato, ma viene dichiarato `Skipped` o `Incomplete`:
non è una verifica superata. Con l'opzione rigorosa, sorgenti assenti o incompleti
fanno fallire il controllo. I sei componenti nuovi di Arachne sono esclusi
esplicitamente dal confronto, perché non esistono in FeMM.

`RHINO-CHECKS.json` registra runtime, sorgenti FeMM, stato e conteggi per suite;
`ARAP-CHECKS.log` e `RHINO-CHECKS.log` conservano le singole prove. `BUILD.json`
include i risultati strutturati; le verifiche non richieste risultano `NotRun`.
Il manifest di build viene eliminato all'avvio e ricreato solo dopo il successo.

`tests/Build.Tests.ps1 -ArachneBin <output>` verifica le protezioni dei percorsi,
i casi FeMM assente/incompleto e l'invalidazione dei vecchi manifest. Lo script
richiede una build già disponibile. `build-examples.ps1 -ExamplesRoot <cartella>`
genera esempi e tavola delle icone in una destinazione alternativa; senza questa
opzione aggiorna i file del repository.

Vedere anche [architettura](docs/ARCHITECTURE.md) e [registro modifiche](docs/CHANGELOG.md).

La compatibilità dei runtime e il formato degli archivi sono descritti in
[Runtime e archivi](docs/RUNTIME-AND-ARCHIVES.md). `scripts/check-runtime.ps1`
esegue la suite sugli stessi binari in un host diagnostico .NET 8 Windows Desktop;
non cambia il runtime configurato in Rhino. BinaryFormatter è sempre disabilitato in questa prova; vengono controllati anche i riferimenti ai tipi nei binari Arachne e GPC. Non sono previsti lettori legacy o conversioni dalla 0.3.0.
