# Motore ARAP C#: implementazione e provenienza

## Stato attuale (serie 0.3.0)

`Arachne.Arap` è una libreria C# `netstandard2.0`, compilabile con
[`Arachne.Arap.sln`](../Arachne.Arap.sln). Dipende da GPCGeometry e GPCUtilities;
non dipende da Rhino, Grasshopper, TensileLib, MathNet o DLL native. Versioni e
percorsi delle dipendenze sono nel [README](../README.md) e in
[`Directory.Build.props`](../Directory.Build.props).

La riscrittura è iniziata nella 0.2.0. La serie 0.3.0 ha ripristinato le porte
FeMM: Flattening ARAP e Deform ARAP espongono la mesh; gli esiti negativi sono
riportati nei messaggi Grasshopper. Le uscite aggiuntive Status, Report, Energy,
Edge distortion e Perimeter error della 0.2.0 non esistono più nei componenti.
La diagnostica numerica resta nell'API C#. Panel Distortion è presente come
componente separato; Arachne.Core e Dependency Check sono stati rimossi.
Inventario e GUID attuali sono in [PORTING](PORTING.md).

## Architettura numerica

| Parte | Implementazione |
| --- | --- |
| Mesh, pin, opzioni, esiti | [ArapModel.cs](../src/Arachne.Arap/ArapModel.cs): copia degli ingressi e conversioni di unità esplicite |
| Topologia e inizializzazione | [MeshTopology.cs](../src/Arachne.Arap/MeshTopology.cs): controlli manifold, normalizzazione centrata, piano principale e mappa armonica per dischi |
| Solver | [ArapSolver.cs](../src/Arachne.Arap/ArapSolver.cs): Flatten, Unwrap, Deform, passi local/global e adattamento del perimetro |
| Algebra | [ManagedAlgebra.cs](../src/Arachne.Arap/ManagedAlgebra.cs): rotazioni SO(2)/SO(3), sistema sparso e gradiente coniugato precondizionato |
| Adattatore Rhino | [ARAPHelper.cs](../src/Arachne.Grasshopper/Helpers/ARAPHelper.cs): conversioni a doppia precisione, senza P/Invoke |

Lo sviluppo 2D usa energia per triangolo; la deformazione 3D usa spokes-and-rims.
I pesi cotangenti, anche negativi, sono conservati. I pin vengono eliminati dal
sistema globale e restituiti esattamente. I controlli distinguono spostamento,
variazione dell'energia, residuo lineare ed errore del perimetro. L'ultimo
aggiornamento del perimetro viene verificato prima di decidere l'esito.

Opzioni, unità, stati e limiti sono descritti nel
[README della libreria](../src/Arachne.Arap/README.md). La geometria è disponibile
solo per esiti convergenti. Questo non certifica la producibilità del pannello:
superfici a doppia curvatura conservano distorsione. Non sono implementati taglio
automatico, packing o controllo globale delle sovrapposizioni. Parti disconnesse
possono sovrapporsi. I componenti GH sono sincroni; cancellazione e avanzamento
sono disponibili nell'API del motore.

## Compilazione e verifiche attuali

```powershell
.\scripts\build-arap.ps1
.\scripts\build-arap.ps1 -CompareNative
.\scripts\build.ps1 -RhinoTests
```

La seconda chiamata richiede la DLL storica x64 indicata da `NativeIglPath`.
Il [progetto nativo](../native/Arachne.IGL.vcxproj) e
[`build-native.ps1`](../scripts/build-native.ps1) ricompilano il riferimento
libigl/Eigen da sorgenti esterni. La DLL non è distribuita con il plug-in.

La [suite ARAP](../tests/Arachne.Arap.Tests/Program.cs) controlla casi analitici,
trasformazioni rigide, scale, pin, topologia invalida, limiti, cancellazione e
concorrenza. I [confronti nativi](../tests/Arachne.Arap.Tests/NativeComparison.cs)
sono facoltativi e non implicano equivalenza generale o bit per bit. Le verifiche
dei componenti sono in [ExampleWriter](../tools/Arachne.ExampleWriter/Program.cs).

Gli output correnti sono in `bin/<progetto>/<configurazione>/<framework>`:
`Arachne.Arap/Release/netstandard2.0`, `Arachne.Arap.Tests/Release/net48` e
`Arachne.Grasshopper/Release/net48`. `BUILD.json` e `DEPENDENCIES.json` sono scritti
dagli script. I vecchi percorsi `artifacts/Release` e i conteggi della 0.2.0 non
descrivono il pacchetto corrente: consultare log e metadati della specifica build.

## Provenienza e storia

Il porting parte da `mm_arap.cpp/.h` e dagli header libigl/Eigen disponibili
localmente; non è una decompilazione. Il motore gestito è una nuova implementazione
indipendente, con stato per chiamata anziché configurazione nativa globale.
Normalizzazione centrata e inizializzazione orientata eliminano la dipendenza
dalla posizione assoluta e dalla proiezione XY del vecchio wrapper.

- `6a6a054`: baseline prima della riscrittura.
- `68ac9a4`: prima implementazione autonoma C#.
- `94fc809`, `a4d12a1`: regressione e correzione della restituzione esatta dei pin.
- `b403857`: completamento del lavoro sul branch storico `codex/managed-arap`.

I risultati numerici e i conteggi delle prove storiche restano consultabili nella
storia Git di questo documento; non sono test eseguiti sulla versione corrente.
Vedere anche [ARCHITECTURE](ARCHITECTURE.md) e [PORTING](PORTING.md).
