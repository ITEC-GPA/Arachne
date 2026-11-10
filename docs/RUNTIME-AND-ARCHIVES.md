# Runtime e archivi della serie 0.3.0

Il plug-in resta `net48`, con il formato di serializzazione dei modelli della
0.3.0. La manutenzione non converte i dati internalizzati e non cambia GUID,
porte o nomi dei tipi. Il motore ARAP resta una libreria `netstandard2.0`.

## Verifiche riproducibili

```powershell
.\scripts\build.ps1 -RhinoTests -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
.\scripts\check-runtime.ps1 -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
```

La prima chiamata esegue Rhino 8 in-process sotto .NET Framework 4.8. La seconda
compila un host diagnostico .NET 8 Windows Desktop e carica gli stessi binari
net48, senza ricompilarli per un framework diverso. Non modifica il runtime
configurato nell'applicazione Rhino dell'utente. Entrambi gli script accettano
`-ArachneBin` per usare una build isolata già prodotta.

La suite verifica:

- serializzazione e copia dei modelli, dei nodi e delle aste;
- archivi GH/GHX di un modello calcolato: unità, topologia, proprietà delle aste,
  vincoli, carichi della pressione, pre-carichi e reazioni;
- rifiuto di un payload troncato;
- apertura dei sei esempi distribuiti, ricalcolo, salvataggio su copie temporanee
  e nuova apertura. Gli esempi originali non vengono riscritti.

I report riportano la versione effettiva del runtime e il valore dello switch
`System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization`.
Il valore `null` significa che lo switch non è dichiarato, non che la
serializzazione è stata disabilitata.

## Limite della serializzazione legacy

L'host Windows Desktop .NET 8 abilita BinaryFormatter tramite la propria
configurazione generata. Questa condizione deve essere distinta dalla
compatibilità generale con qualsiasi host .NET 8. La controprova è:

```powershell
.\scripts\check-runtime.ps1 -DisableBinaryFormatter
```

Questa è una prova negativa: il comando deve fallire sui percorsi che usano la
serializzazione legacy. Non abilitare globalmente BinaryFormatter come soluzione
automatica. La serie 0.3.0 continua a richiedere un host che consenta il formato
legacy e i suoi archivi devono provenire da una fonte fidata. Il supporto ad altri
runtime non è dedotto dai risultati delle due configurazioni provate.

## Migrazione 0.4.0: decisione separata

Il branch remoto `codex/gpc-migration-preserved`, commit `bf2d6bc`, contiene
`ContractArchive`: intestazione `ARCGPC3`, versione 2, contratti GPC compatti.
Legge anche la versione 1 XML del suo formato; rifiuta esplicitamente i payload
BinaryFormatter della 0.3.0 e gli archivi sperimentali ARCGPC2. Non è quindi un
sostituto retrocompatibile da inserire in una release di manutenzione.

Per adottarlo occorre una migrazione distinta: inventario dei dati internalizzati,
ricostruzione o conversione esplicita da sorgenti fidate, confronto numerico dei
modelli e verifica dei file dei risultati accanto al documento. Conservare i
documenti originali e la relativa versione del plug-in fino alla verifica della
migrazione. Non viene introdotto un secondo formato transitorio nella 0.3.0.
