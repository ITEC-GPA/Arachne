# Arachne.Arap

Motore ARAP autonomo in C#, target **.NET Standard 2.0**, con tipi geometrici di **GPCGeometry** e conversione esplicita delle unita mediante **GPCUtilities**. Nessun riferimento a Rhino, Grasshopper, TensileLib, libigl, Eigen o MathNet. Progetto e soluzione separati, conservati nel repository Arachne.

## Compilare e usare

Dalla radice del repository: `./scripts/build-arap.ps1`. Richiede SDK .NET, Developer Pack .NET Framework 4.8 per l'eseguibile di test e le due DLL GPC locali. I parametri GpcGeometryPath e GpcUtilitiesPath permettono di specificarle. Il solo progetto `src/Arachne.Arap/Arachne.Arap.csproj` compila anche senza il test runner Windows.

La soluzione `Arachne.Arap.sln` esclude completamente il plugin e il motore FEM. La cartella di distribuzione contiene Arachne.Arap.dll, GPCGeometry.dll e GPCUtilities.dll; copiarle insieme. `-CompareNative` e facoltativo e richiede la vecchia DLL x64 soltanto per i confronti.

```csharp
using Arachne.Arap;
using GPC.Geometry;

var mesh = new ArapMesh(
    new[] { new Point3d(0, 0, 0), new Point3d(2, 0, 0), new Point3d(0, 1, 0) },
    new[] { 0, 1, 2 });
var result = ArapSolver.Flatten(mesh,
    new ArapOptions { PerimeterTolerance = 0.001 });
if (result.Success)
{
    Point3d[] developed = result.GetVertices();
    // Gli indici delle facce sono gli stessi della mesh originale.
}
```

- `Flatten`: sviluppo piano e adattamento della lunghezza dei lati del bordo.
- `Unwrap`: parametrizzazione piana con pin opzionali in coordinate XY assolute (Z=0), senza adattamento del perimetro.
- `Deform`: deformazione 3D con pin opzionali in coordinate assolute.
- Senza pin: un ancoraggio automatico per ogni parte connessa. Con pin espliciti: almeno un pin per parte. I pin duplicati sono rifiutati.
- Indici delle facce: triple consecutive, orientate coerentemente. Mesh triangolare manifold; nessuna saldatura implicita dei vertici coincidenti.
- Coordinate e tolleranza del perimetro usano la medesima unita dell'ingresso. Nessuna conversione implicita in metri. `mesh.ConvertUnits(from, to)` usa GPCUtilities; convertire anche pin e tolleranze se si cambia unita.

## Esiti e diagnostica

`GetVertices()` restituisce una copia solo con stato `Converged`. Gli altri stati sono `IterationLimit`, `PerimeterLimit`, `InvalidInput`, `InvalidParameterization`, `NumericalFailure`, `Cancelled`. Le posizioni dei pin espliciti sono restituite esattamente; i vertici e gli indici originari non sono modificati.

`History` registra energia normalizzata, spostamento massimo nelle unita della mesh, residuo normalizzato del sistema lineare e indice del passo di perimetro. L'energia e confrontabile a vincoli fissi: quando cambia PerimeterPass, i pin cambiano e non si deve pretendere monotonia fra i due passi. `MaxRelativeEdgeError` misura la massima variazione frazionaria degli spigoli, `MaxPerimeterError` la massima differenza assoluta fra lati corrispondenti. La distorsione degli spigoli e disponibile per gli esiti convergenti; il controllo del perimetro riguarda Flatten.

Ogni chiamata possiede il proprio stato. Le opzioni sono copiate all'avvio: non modificarle contemporaneamente durante la creazione dello snapshot. Sono disponibili CancellationToken e callback progress sincrona sul thread chiamante. Il callback non deve generare eccezioni; la cancellazione coopera anche durante il solver lineare. Nessuna gestione implicita di thread, UI o documenti.

I limiti di iterazione sono massimi, con arresto anticipato alla convergenza. PositionTolerance e relativa alla massima estensione iniziale del bounding box. Il controllo lineare e ||r|| / max(1, ||b||), sulla mesh normalizzata. Una mancata convergenza non produce una mesh di produzione. MaxPerimeterIterations=0 restituisce PerimeterLimit, preservando la soppressione dell'output del componente precedente.

## Algoritmo e limiti

Sviluppo 2D: energia ARAP per triangolo, coordinate locali intrinseche e rotazioni SO(2). Deformazione 3D: energia spokes-and-rims con rotazioni SO(3), stimate tramite autovettore dominante del problema quaternionico 4x4. Passo globale: Laplaciano a cotangenti con eliminazione esatta dei pin e gradiente coniugato precondizionato diagonale, a memoria proporzionale agli spigoli. I pesi cotangenti negativi dei triangoli ottusi sono mantenuti.

La normalizzazione usa centro ed estensione geometrica. L'inizializzazione 2D usa il piano principale orientato; per dischi che si ribaltano in proiezione usa un bordo circolare e una mappa armonica a pesi positivi. I bordi sono percorsi come cicli orientati; gli angoli definiscono i lati e i bordi lisci ricevono ancoraggi deterministici. Il perimetro viene ricontrollato dopo l'ultimo aggiornamento.

ARAP minimizza distorsione geometrica: non calcola tensioni, tiri, rigidezze o resistenza. Una superficie a doppia curvatura conserva distorsione residua anche quando il perimetro coincide. Convergenza non equivale a validazione per taglio o produzione.

Superfici chiuse o con manici devono essere tagliate per lo sviluppo. Pannelli con fori sono ammessi quando la proiezione iniziale e valida; se si ribalta, vanno suddivisi in pannelli a disco. Si rifiutano triangoli invertiti/degeneri in 2D e collassati in 3D. Non sono implementati taglio automatico, collisioni, controllo globale delle sovrapposizioni, packing dei pannelli o un solver FEM. In particolare, parti disconnesse possono sovrapporsi nello sviluppo. Non e garantito il minimo globale dell'energia.

Verificato su Windows/.NET Framework 4.8 e Rhino 8 in-process; il target della libreria consente altri host .NET compatibili, ancora da verificare. Per grandi mesh e triangoli molto allungati possono essere necessari piu passi lineari: il budget esaurito produce un esito esplicito. Benchmark iniziale fino a 1.681 vertici; non e una qualifica su modelli di produzione.

Riferimenti: [Sorkine e Alexa, 2007](https://igl.ethz.ch/projects/ARAP/) per il metodo local/global e [tutorial libigl](https://libigl.github.io/tutorial/) per le varianti energetiche. La nuova implementazione e indipendente; sorgenti e DLL preesistenti sono stati usati per l'audit e per i confronti, non costituiscono una dipendenza del motore C#.
