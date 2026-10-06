# Riscrittura autonoma ARAP in C#

Richiesta del 6 ottobre 2026: nuova implementazione del motore ARAP in C#, indipendente da libigl/Eigen a runtime, con geometria GPC, da collegare ad Arachne dopo le verifiche.

## Stato prima degli interventi

Baseline Arachne: 6a6a054 (release candidata con 100 test). Albero di lavoro pulito. Conservato il pacchetto precedente Arachne-0.1.0-rc.1-win-x64.zip.

I sorgenti originali sono disponibili in un repository locale: mm_arap.cpp/.h e gli header libigl/Eigen. Il confronto usa anche la DLL nativa x64 ricompilata e verificata nella release precedente; non viene presentata una decompilazione come sostituto dei sorgenti gia disponibili.

API native: Version, SetConfig, MeshFlattening, MeshUnwrap, MeshDeform. La configurazione e globale. I vertici vengono scalati usando gli estremi delle singole coordinate assolute; i pin richiedono la correzione gia aggiunta nel wrapper gestito. L'inizializzazione UV e la proiezione XY, anche per pannelli verticali. Flattening esegue ARAP e successivamente un adattamento dei lati del perimetro.

## Piano esecutivo

1. Progetto Arachne.Arap compilabile separatamente, senza Rhino/Grasshopper/TensileLib ne DLL C++.
2. Modello triangolare e pin con validazione, opzioni per singola chiamata, esiti espliciti, storia e cancellazione.
3. Solver ARAP local/global 2D per elementi e 3D per spokes-and-rims; algebra gestita, vincoli esatti, controllo della convergenza.
4. Flattening con inizializzazione indipendente dall'orientamento del pannello, rilevamento dei bordi e adattamento del perimetro con esito verificato dopo l'ultimo passo.
5. Test analitici, trasformazioni rigide, scale/traslazioni, mesh invalide, limiti di iterazione, concorrenza e confronto numerico con il motore nativo.
6. Collegamento dei componenti e della geodetica al nuovo motore; conservazione di GUID e porte; pacchetto gestito senza dipendenza ARAP nativa.

## Fonti tecniche

- Sorkine e Alexa, As-Rigid-As-Possible Surface Modeling (2007): https://igl.ethz.ch/projects/ARAP/
- Documentazione libigl sulle energie ARAP e la parametrizzazione: https://libigl.github.io/tutorial/
- Implementazione libigl presente localmente: usata per verificare la variante energetica e il comportamento, mantenendone la provenienza; il nuovo codice e una implementazione C# indipendente.

Le configurazioni non sviluppabili ammettono distorsione residua. Convergenza dell'energia e tolleranza del perimetro sono controlli distinti; non e prevista equivalenza bit per bit fra solutori diversi. Eventuali facce invertite o degeneri devono essere segnalate, non pubblicate come sviluppo valido.

## Esito della riscrittura — 0.2.0-rc.1

Implementato il nuovo motore **Arachne.Arap in C#**, target **netstandard2.0**, con soluzione e progetto autonomi nello stesso repository. Sono disponibili Flatten, Unwrap e Deform. Il plugin Arachne usa questo motore per i due componenti ARAP e per il tentativo ARAP di Geodesic Offsets. Restano 43 componenti; GUID, ingressi e uscita mesh preesistenti dei componenti ARAP sono conservati.

La libreria dipende soltanto da GPCGeometry 2.1.0.3 e GPCUtilities 2.0.0.8, oltre alle librerie standard .NET. Non usa Rhino, Grasshopper, TensileLib, MathNet o codice nativo. Algebra, rotazioni e solver sparso sono implementati nel progetto C#. TensileLib e i risultati della ricerca di forma non sono stati modificati in questa riscrittura.

Il lavoro usa i sorgenti originali disponibili e il confronto con il binario: non e stata necessaria una decompilazione della DLL. La DLL nativa rimane soltanto come riferimento facoltativo nei test; non viene distribuita con il plugin 0.2.0. Il progetto e lo script C++ della release precedente restano utilizzabili per ricostruire quel riferimento.

## Modifiche e righe da visionare

| Parte | Modifica e motivo | Codice |
|---|---|---|
| Progetto autonomo | Soluzione Arachne.Arap.sln con libreria e test; nessun riferimento al progetto Core o al plugin. | [Arachne.Arap.csproj](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/Arachne.Arap.csproj:1) |
| Modello geometrico | Copia di punti GPC e indici triangolari; nessuna modifica degli ingressi. Conversioni di unita esplicite con GPCUtilities. | [ArapModel.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapModel.cs:11) |
| Opzioni ed esiti | Impostazioni per chiamata, tolleranze, budget, sette esiti, storia e geometria solo in caso di convergenza. | [Opzioni](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapModel.cs:48), [risultato](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapModel.cs:90) |
| Validazione e scala | Normalizzazione centrata sull'estensione geometrica; controllo di indici, coordinate, triangoli, orientamento, vertici inutilizzati, spigoli e ventagli non manifold. | [MeshTopology.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/MeshTopology.cs:28) |
| Sviluppo iniziale | Piano principale orientato; per i dischi con proiezione ribaltata, inizializzazione armonica con bordo circolare. | [Initial2D](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/MeshTopology.cs:118), [HarmonicDisk](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/MeshTopology.cs:138) |
| Nucleo ARAP | Rotazioni per triangolo in 2D, spokes-and-rims in 3D, pesi cotangenti anche negativi, passi local/global. | [Engine](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapSolver.cs:168) |
| Rotazioni | SO(2) analitica; SO(3) tramite problema quaternionico 4x4 e Jacobi. Nessuna riflessione impropria o dipendenza SVD esterna. | [ManagedAlgebra.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ManagedAlgebra.cs:48) |
| Sistema globale | Matrice sparsa, eliminazione dei pin, gradiente coniugato con precondizionamento diagonale e verifica del residuo reale. | [SparseSystem](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ManagedAlgebra.cs:82), [Solve](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ManagedAlgebra.cs:112) |
| Convergenza | Spostamento relativo, variazione di energia, residuo lineare, limiti esatti, cancellazione e callback di avanzamento. | [Optimize](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapSolver.cs:208) |
| Perimetro | Cicli orientati, lati riconosciuti per angolo, ancoraggi per bordi lisci, aggiornamento simultaneo degli angoli e verifica dopo l'ultimo passo. | [Ciclo perimetro](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapSolver.cs:42), [MakeSides](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapSolver.cs:133) |
| Pin esatti | Conservazione dei valori originali in uscita, senza errore aggiunto dalla denormalizzazione. | [Restituzione pin](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/ArapSolver.cs:88) |
| Adattatore Rhino | Conversione in GPC con lettura e scrittura dei vertici a doppia precisione; eliminati configurazione globale, P/Invoke e workaround della vecchia scala. | [ARAPHelper.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Helpers/ARAPHelper.cs:13) |
| Flattening ARAP | Opzioni locali, gestione dell'esito e cinque uscite diagnostiche aggiunte dopo la mesh. Le facce quadrangolari originali vengono ripristinate. | [FlatteningArapComponent.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/FlatteningArapComponent.cs:36) |
| Deform ARAP | 2D e 3D nel nuovo motore; quattro uscite diagnostiche; un ancoraggio per parte se i pin sono assenti. Conservata la convenzione di un punto senza indice, riferito al vertice 0. | [DeformArapComponent.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/DeformArapComponent.cs:39) |
| Geodetica | Il secondo tentativo usa ARAP gestito e rifiuta l'esito se non converge; non riutilizza una mesh prodotta da un precedente calcolo fallito. | [GeodesicOffsetsComponent.cs](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/Patterning/GeodesicOffsetsComponent.cs:279) |
| Controllo DLL | Il booleano esistente abilita una prova reale del motore gestito. Rimosse LoadLibrary/GetProcAddress; controllata anche Arachne.Arap.dll. | [DependencyCheck](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Grasshopper/Components/ReleaseUtilityComponents.cs:75) |
| Distribuzione | Build ARAP separata, test automatici, manifest e pacchetto del plugin senza Arachne.IGL.dll. Rimossa anche un'eventuale copia obsoleta dalla destinazione del pacchetto. | [build-arap.ps1](C:/Users/g.pacini/Documents/Repository/Arachne/scripts/build-arap.ps1:1), [build.ps1](C:/Users/g.pacini/Documents/Repository/Arachne/scripts/build.ps1:1) |

## Difetti e differenze registrate

1. **Configurazione globale nativa:** chiamate indipendenti potevano interferire tramite SetConfig. Ogni calcolo ora possiede opzioni e stato locali; test con otto calcoli concorrenti e budget diversi.
2. **Scala da coordinate assolute e pin non scalati nel C++:** sostituiti da centro/estensione e trasformazione coerente dei vincoli. Il workaround del precedente wrapper non e piu necessario. Verificate scale da 1e-6 a 1e6 e traslazioni negative fino a 1e8.
3. **Inizializzazione XY del C++:** sostituita da proiezione orientata o mappa armonica. Verificati pannelli verticali, obliqui, cilindrici e ripiegati.
4. **Ultimo passo del perimetro:** il vecchio ciclo non verificava il risultato dell'ultima chiamata ARAP. Il nuovo test sceglie una tolleranza raggiunta proprio all'ultimo passo e verifica l'accettazione.
5. **Bordi senza angoli sufficienti:** il vecchio algoritmo poteva rifiutarli. Il nuovo codice assegna tre ancoraggi deterministici; test su un bordo circolare con soglia angolare che non rileva angoli.
6. **Esiti insufficienti e input problematici:** ora esistono controlli espliciti su topologia, pin, triangoli invertiti/collassati, mancata convergenza e cancellazione. Non viene pubblicata geometria nei casi non riusciti.
7. **Precisione dei pin nel primo motore C#:** il test aggiunto dopo il primo checkpoint ha riprodotto l'arrotondamento di coordinate come 0.1 durante la denormalizzazione. Salvata la regressione in un commit dedicato, poi corretta la restituzione con i valori originali. Il test richiede uguaglianza esatta dei double.
8. **Precisione del collegamento Rhino:** la lettura precedente iterava Point3f. Ora legge Point3d e abilita la doppia precisione anche in uscita. Test con coordinate di ordine 1e8 e dettagli decimali.
9. **Doppio fallimento nella geodetica:** individuato per lettura del ramo di fallback, che poteva conservare una mesh non nulla anche dopo calcoli falliti. Ora entrambi gli esiti negativi interrompono l'operazione. Il normale componente geodetico e il nuovo motore sono verificati; non e stato predisposto un caso Rhino che forzi specificamente entrambi i fallimenti di quel ramo.

La nuova politica di convergenza e piu esplicita: un budget basso che prima restituiva comunque la deformata puo ora produrre IterationLimit. Anche i limiti topologici sono espliciti. Il parametro di velocita del perimetro accetta 0 < valore <= 0.5. I pin duplicati sono rifiutati nell'API; il componente GH mantiene la convenzione preesistente di usare il primo e avvisare.

## Verifiche eseguite

**144 controlli superati, zero fallimenti**, senza contare due ulteriori prove geometriche sui generatori di reti:

| Suite | Numero | Contenuto |
|---|---:|---|
| ARAP gestito | 54 | Invarianti geometrici, vincoli, topologia, condizioni invalide, perimetro, convergenza, cancellazione, concorrenza e mesh da 1.681 vertici. |
| Confronti nativi facoltativi | 6 | Unwrap piano/traslato/curvo, Deform 3D, Flatten piano, perimetro del Flatten curvo. |
| Arachne.Core | 33 | Modellazione, equilibrio reale, carichi, forze, risultati e cancellazione della ricerca di forma. |
| Patterning in Rhino/Grasshopper | 37 | Tutti i 43 componenti istanziati; sviluppo, compensazione, strumenti mesh, nuovi esiti ARAP e assenza di caricamento della DLL ARAP nativa. |
| Componenti release in Rhino/Grasshopper | 14 | Interrogazione modello e risultati, unita, anteprima, dipendenze, background e forze delle piastre. |

Compilazione Release verificata senza errori ne avvisi. Le prove Rhino sono eseguite con `--tests-only`: gli esempi GH/GHX non sono stati rigenerati. I messaggi di inizializzazione dei plugin Tekla installati, presenti nel log di Rhino, sono estranei ai test Arachne e non ne hanno determinato il fallimento.

| Confronto C# / DLL precedente | Valore osservato | Criterio del test |
|---|---:|---:|
| Unwrap piano: massima distanza fra vertici | 9.31e-16 | <= 2e-5 |
| Unwrap traslato: massima distanza fra vertici | 6.53e-14 | <= 2e-5 |
| Unwrap curvo vincolato: massima distanza fra vertici | 7.25e-9 | <= 2e-4 |
| Deform 3D non rigido: massima distanza fra vertici | 1.45e-7 | <= 2e-4 |
| Flatten piano: massima distanza fra vertici | 1.79e-14 | < 2e-5 |
| Flatten curvo: massimo errore dei lati di bordo, nativo / C# | 4.76e-6 / 9.61e-6 | entrambi <= 1e-5 |

I valori sono nelle unita dei modelli di test. Nel caso di Flatten curvo si confrontano le lunghezze dei lati rispetto al riferimento 3D, **non** l'uguaglianza delle coordinate finali: i due cicli di adattamento del perimetro sono differenti. La DLL storica emette inoltre un messaggio diagnostico sul percorso del bordo, pur restituendo successo e rispettando la tolleranza nel caso provato.

Il riferimento nativo e la DLL ricompilata della prima release, SHA256 `D45308A718E04283D7B9B77ADB7A1E4380741F4EAA1A48C3930754BC2315D738`. Nessuna equivalenza generale o bit per bit e dedotta da questi casi. La deformazione non rigida di 1.681 vertici richiede circa 2.1 secondi su questo PC nella prova eseguita; non e una garanzia prestazionale su modelli diversi.

Log e manifest: `artifacts/arap-validation`, `artifacts/Release/Arachne/BUILD.json`, `artifacts/Release/Arachne.Arap/BUILD.json` e i rispettivi DEPENDENCIES.json. Il pacchetto precedente 0.1.0-rc.1 rimane conservato.

## Uso, compatibilita e cosa resta da fare

Documentazione dell'API e delle opzioni: [README della libreria](C:/Users/g.pacini/Documents/Repository/Arachne/src/Arachne.Arap/README.md). Aggiunte solo uscite dopo la mesh: Status, Report, Energy, Edge distortion e, per Flattening, Perimeter error. I collegamenti precedenti restano sugli stessi indici. Dependency Check conserva il booleano al primo ingresso, ora dedicato alla prova ARAP gestita.

ARAP e una minimizzazione geometrica; non sostituisce il form finding meccanico, non calcola i tiri e non altera i risultati di nodi, aste o piastre. Lo sviluppo di superfici a doppia curvatura mantiene distorsione intrinseca. Va esaminata con Edge distortion o Panel Distortion prima dell'uso produttivo.

Lavori successivi, esplicitamente non inclusi in questa riscrittura:

1. Validazione sui pannelli e vincoli reali dell'utente, compresi casi difficili con molte aperture, geometrie molto sottili e cambi di scala estremi.
2. Prove su mesh sensibilmente piu grandi; eventuale precondizionatore incompleto o fattorizzazione sparsa riutilizzabile se i profili prestazionali lo richiedono.
3. Controllo globale delle sovrapposizioni e collisioni. Il controllo attuale rileva facce invertite o collassate, ma non tutte le intersezioni fra facce distanti; parti disconnesse possono sovrapporsi nello sviluppo.
4. Taglio automatico delle superfici chiuse/con manici e dei pannelli forati che si ribaltano in proiezione. Ora il motore li rifiuta con indicazioni per la suddivisione.
5. Background/Cancel nell'interfaccia dei componenti ARAP. Il motore C# supporta gia token e avanzamento; i componenti GH restano sincroni, come prima.
6. Validazione negli altri runtime di Rhino e negli altri host .NET. Verificato qui Windows x64, Rhino 8 in-process e .NET Framework 4.8.
7. Esempi Grasshopper e qualifica di produzione, che l'utente preparera a lavoro finito.

## Traccia Git

- `bffa5cc`: baseline e audit salvati prima della riscrittura.
- `68ac9a4`: prima implementazione C# autonoma, con 53 verifiche iniziali.
- `94fc809`: regressione che riproduce l'arrotondamento dei pin e ampliamento dei test.
- `a4d12a1`: correzione della restituzione esatta dei pin.
- Collegamento Grasshopper, confronti Flatten, documentazione e distribuzione sono registrati nei commit successivi sul ramo `codex/managed-arap`.

I confronti nativi risiedono esclusivamente in [NativeComparison.cs](C:/Users/g.pacini/Documents/Repository/Arachne/tests/Arachne.Arap.Tests/NativeComparison.cs:1); la suite autonoma e in [Program.cs](C:/Users/g.pacini/Documents/Repository/Arachne/tests/Arachne.Arap.Tests/Program.cs:1). Nessun nuovo repository GitHub o pubblicazione remota e stato creato da questa richiesta.
