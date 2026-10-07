# Architettura

```mermaid
flowchart TD
  GH[Arachne.Grasshopper · componenti, data type, parametri] --> COMMON[Arachne.Common · modello FEM e FormFindingHelper]
  GH --> ARAP[Arachne.Arap · motore ARAP C#]
  GH --> RHINO[RhinoCommon e Grasshopper]
  COMMON --> RHINO
  COMMON --> TL[TensileLib 2.2 · form finding e flattening]
  COMMON --> GPC[GPCGeometry e GPCUtilities]
  GH --> TL
  ARAP --> GPC
```

## Arachne.Common

Porting di FeMM.Common limitato a ciò che serve al form finding:

- `Models`: modello FEM (`ModelModel`, nodi, aste, piastre), attributi (vincoli, pesi, ID), carichi (nodali, sulle aste, pre-carico), attributi di form finding (`ForceDensityModel`, `FixedLengthModel`, `FixedAxialForceModel`), casi di carico, reazioni (`NodeReactResultModel`), proprietà, sezioni e materiali.
- `Helpers/FormFindingHelper`: converte il modello FEM nel modello TensileLib (nodi, aste, piastre, carichi, obiettivi), calcola le reazioni e genera mesh da reti di curve.
- `Helpers/ModelChecks`: controlli di Check Model (vincoli per parte e direzione, aste senza forza, valori non validi).
- `Helpers/ModelHelper`: conversioni di unità tramite GPCUtilities (ex Maffeis.Utilities).
- `Geometry/Shapes`: forme generiche delle sezioni, su GPCGeometry (ex Maffeis.Geometry).

I modelli sono serializzabili in binario (.NET Framework 4.8). Lo stesso formato serve per i dati interni dei parametri Grasshopper e per le copie profonde.

## Arachne.Grasshopper

- `DataTypes`: wrapper Grasshopper dei modelli (`GH_Model`, `GH_Node`, `GH_Beam`, `GH_Plate`, proprietà, sezioni, materiali, casi di carico). Ogni nodo, asta e piastra ha un GUID. Build Model unisce gli elementi con lo stesso GUID e poi i nodi coincidenti entro la tolleranza delle impostazioni (`Settings`, 0.001).
- `Components`: un file per componente, nelle cartelle FeMM (Analysis, Attributes, Definitions, ElementProperties, Loads, Materials, Models, Parameters, Patterning, Sections, Tools).
- `ComponentAttributes`: pulsante Run di Form Find e immagini dei componenti di carico.
- `Services`: algoritmi di Compensation, Decompensation e Flattening separati dai componenti.
- `Geometry`: generatori Cable Net (CableNetHelper) e geodetiche su mesh.
- `Helpers/ARAPHelper`: conversione tra mesh Rhino e motore `Arachne.Arap`.

## Form Find

Form Find costruisce a ogni esecuzione il modello TensileLib (`FormFindingHelper.AssemblyModel`) e chiama il solutore scelto. Le opzioni (gravità, pressione, iterazioni, tolleranze, mesh) arrivano da Solver Options come `SolverOptionsModel`; senza Solver Options valgono i predefiniti del modello, gli stessi di FeMM.

- **Linear**: densità di forza lineare;
- **Linear step by step**: ciclo esterno che corregge le densità fino a lunghezze e forze fisse;
- **Non-Linear**: obiettivi pesati.

TensileLib risolve il modello con il centro nell'origine e poi lo riporta al suo posto. Il risultato è una copia del modello in ingresso con:

- le posizioni calcolate;
- il pre-carico di trazione sulle aste con Force Density, Fixed Axial Force o Fixed Length;
- i carichi nodali della pressione nel caso di carico indicato;
- le reazioni dei vincoli, se il calcolo converge;
- la mesh delle piastre.

Beam Forces e Support Reactions leggono pre-carichi e reazioni dal modello; Check Model usa `ModelChecks` prima del calcolo.

## Verifiche

`tools/Arachne.ExampleWriter` avvia Rhino 8 in-process senza finestra e usa i componenti in documenti Grasshopper reali:

- `InventoryChecks`: GUID, icone e confronto di ingressi e uscite con i sorgenti FeMM;
- `FormFindingChecks`: form finding su casi analitici in N/mm, unione dei rami, carichi sulle aste, reazioni, peso proprio in N/mm e in SI, pressione, predefiniti senza Solver Options, modelli lontani dall'origine, Check Model, filtri, Membrane Material e Plate Property, anteprima delle sezioni, serializzazione;
- `PatterningChecks`: sviluppo e compensazione dei pannelli, ARAP, decompensazione, strumenti sulle mesh e Panel Distortion, con valori attesi (lati, aree, distanze); una prova unica verifica che nessun componente modifichi la mesh in ingresso;
- `NetworkChecks`: reti di cavi rettangolari, radiali e composte, con il numero di cavi, di pezzi di perimetro e di punti interni, con e senza tolleranza di corrispondenza.

Il motore ARAP ha una suite propria, `tests/Arachne.Arap.Tests` (54 controlli), con il confronto facoltativo con la DLL nativa della prima versione.

Gli esempi in `examples/` vengono generati dallo stesso strumento, riaperti e ricalcolati.
