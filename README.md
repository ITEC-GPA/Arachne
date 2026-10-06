# Arachne

Plug-in Grasshopper per il form finding e il patterning di tele e tensostrutture. Contiene i componenti di FeMM dedicati a questi due temi. Ingressi e uscite sono identici a FeMM; i GUID sono nuovi, così i due plug-in si possono caricare insieme. Sono corretti i bug trovati durante il trasferimento ([dettagli](docs/PORTING.md)).

**Versione:** 0.3.0-rc.1. Motore di form finding: TensileLib 2.1 (`../tensilelib`). Sviluppo dei pannelli ARAP: motore C# `Arachne.Arap` ([resoconto](docs/ARAP-REWRITE.md)).

## Struttura

```text
Arachne.sln
Arachne.Arap.sln             Soluzione autonoma del motore ARAP e dei suoi test
src/
  Arachne.Common/            Modello FEM e helper di FeMM.Common usati dal form finding
  Arachne.Grasshopper/       Componenti, data type e parametri (.gha)
  Arachne.Arap/              Motore ARAP C# netstandard2.0
tests/Arachne.Arap.Tests/    Verifiche numeriche del motore ARAP
tools/Arachne.ExampleWriter/ Verifiche in Rhino/Grasshopper senza finestra, esempi e icone
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
  PL[Plate + Plate Property] --> BM
  BM --> FF[Form Find]
  FF --> OUT[Model · Mesh]
  OUT --> PAT[Patterning: Flattening, Compensation, Borders, ...]
```

Note d'uso, come in FeMM:

- **Unità:** Build Model usa per default Nmm. Per lavorare in metri scegliere SI. Le tolleranze di Form Find sono relative.
- **Rami:** collegare a Build Model tutti gli elementi in un'unica lista, appiattendo l'ingresso Elements se arrivano da percorsi diversi. Gli elementi con lo stesso GUID (per esempio una Beam passata da Force Density e da Fixed Length) vengono uniti.
- **Fusione dei nodi:** due nodi più vicini di 0.001 (unità del modello) vengono uniti con attributi e carichi. FeMM leggeva questa tolleranza dalle opzioni del suo plug-in Rhino.
- **Form Find:** calcola con il pulsante **Run** oppure con Auto Run = True. La pressione richiede un Load Case, a cui vengono assegnati i carichi nodali risultanti. I solutori Linear e Non-Linear pubblicano il risultato anche al limite di iterazioni, con un avviso. Linear step by step si ferma invece con un errore.
- **Cable Net:** Warp, Weft e Restraint edges sono alberi con un ramo per cavo. Appiattirli prima di Beam.

## Componenti

| Pannello | Componenti |
| --- | --- |
| 01-Parameters | Load Combinations, CoordinateSystem |
| 02-Materials | Aluminium, Concrete, Custom Material, Steel, Table Material |
| 03-Sections | Angle, Brep, C, Generic, Hollow Circle, Hollow Rect, I, Solid Circle, Solid Rect, T, Section Geometry, Section Geometric Property |
| 04-Definitions | Load Case, Group |
| 05-Properties | Beam Property, Plate Property |
| 06-Model | Node, Beam, Plate, Build Model, Decompose Model |
| 07-Attributes | Force Density, Fixed curvature cable, Membrane type, Fixed Length, Fixed Axial Force, Fixed compensation, Keep Position, Restraints |
| 08-Loads | Node Load, Beam Point Load, Beam Distributed Load |
| 09-Patterning | Cable Net, Radial Cable Net, Compound Net, Borders, OrderBorders, Compensation, Decompensation, DecompensationDescriptor, DecompensationDescriptorExt, Flattening, Flattening ARAP, Deform ARAP, Geodesic Offsets, Line-Up, Mesh Clean, Mesh Divide, Mesh Geodesic, Mesh Pipe, Mesh Remap, Remesh, Split Mesh On Curves, Subdivide Mesh, Unweld Mesh |
| 12-Analysis | Form Find |
| 14-Tools | Curve2Mesh |

La tabella con sorgente FeMM e GUID di ogni componente è in [PORTING.md](docs/PORTING.md); le icone sono in [ICONS.svg](docs/ICONS.svg).

## Esempi

[01_SaddleNet](examples/01_SaddleNet.gh) (sella con vincoli al bordo), [02_Patterning](examples/02_Patterning.gh) (sviluppo, compensazione, bordi e decompensazione di un pannello) e [03_CableNet](examples/03_CableNet.gh) (rete generata da Cable Net). Ogni file esiste anche in GHX. Vedere [examples/README.md](examples/README.md).

## Compilazione e verifiche

Prerequisiti: Windows x64, SDK .NET 8 o successivo, .NET Framework 4.8 Developer Pack, Rhino 8, DLL Release di GPCGeometry, GPCUtilities e TensileLib nei repository adiacenti.

```powershell
.\scripts\build.ps1 -RhinoTests
```

Lo script compila tutti i progetti in `bin/<progetto>/<configurazione>/<framework>` ed esegue i test del motore ARAP. Con `-RhinoTests` avvia Rhino senza finestra ed esegue le verifiche Grasshopper:

- ingressi e uscite di ogni componente confrontati con i sorgenti FeMM;
- GUID e icone;
- form finding su casi analitici;
- patterning e reti.

Infine raccoglie il plug-in in `artifacts/<configurazione>/Arachne`: copiare l'intera cartella nelle Libraries di Grasshopper. Se Rhino carica il plug-in dalla cartella `bin`, chiuderlo prima di compilare in Release. In alternativa usare `-Configuration Debug`.

Percorsi predefiniti, modificabili con proprietà MSBuild o parametri dello script:

| Proprietà | Percorso rispetto alla cartella Arachne |
| --- | --- |
| GpcGeometryPath | `../Geometry/GPCGeometry/bin/Release/netstandard2.0/GPCGeometry.dll` |
| GpcUtilitiesPath | `../Utilities/GPCUtilities/bin/Release/netstandard2.0/GPCUtilities.dll` |
| TensileLibPath | `../tensilelib/TensileLib/bin/Release/TensileLib.dll` |
| RhinoSystemDir | `C:/Program Files/Rhino 8/System` |
| GrasshopperDir | `C:/Program Files/Rhino 8/Plug-ins/Grasshopper` |

Altri script: `scripts/build-examples.ps1` rigenera gli esempi e `docs/ICONS.svg`; `scripts/build-arap.ps1` compila solo il motore ARAP. Per le icone dei nuovi componenti aggiungere il disegno in `tools/Arachne.ExampleWriter/IconArtwork.cs` ed eseguire ExampleWriter con `--icons`.

Vedere anche [architettura](docs/ARCHITECTURE.md) e [registro modifiche](docs/CHANGELOG.md).
