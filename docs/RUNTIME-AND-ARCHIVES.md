# Runtime e archivi (0.4.0 e successive)

Dalla 0.5.0 il plug-in è `net8.0-windows` (x64) e si carica in Rhino 8 sul runtime .NET Core (.NET 8); fino alla 0.4.1 era `net48`. ARAP resta una libreria `netstandard2.0`, TensileLib una libreria .NET Framework 4.7.2 e MathNet.Numerics la build net48 del pacchetto 5.0.0, comune a BridGHe e ReBear. Il modello e i dati internalizzati usano i contratti GPC del set stabile incluso in `lib/GPC`.

## Formato e assenza di retrocompatibilità

Arachne legge e scrive solo `ARCGPC3`, versione **2**. Non usa BinaryFormatter, un lettore NRBF o un convertitore. Rifiuta i payload della 0.3.0, ARCGPC2 e la versione XML 1 sperimentale. Una lettura fallita svuota il valore precedente, evitando di mostrare dati obsoleti.

Per decisione del progetto non esistono documenti utente da migrare dalla 0.3.0: non è prevista retrocompatibilità. Gli esempi del repository sono ricostruiti nel formato corrente. I tag 0.3 restano nello storico del codice, non costituiscono un requisito di interoperabilità degli archivi.

## Risultati e file esterni

Il Form Find conserva i risultati in `<documento>.<InstanceGuid>.gpc-results`. Copiare sempre questi file insieme alla definizione GH/GHX. Le due estensioni con lo stesso nome possono condividere il sidecar. Salva con nome scrive un sidecar nella destinazione e conserva l'originale; un salvataggio invariato conserva contenuto e data del sidecar.

Con Auto Run spento, all'apertura le uscite vengono ripristinate (`Restored`) solo se gli ingressi corrispondono al calcolo salvato. Ingressi modificati richiedono Run. Un sidecar assente o corrotto non impedisce di aprire la definizione: il ripristino segnala il problema, e salvando senza ricalcolare viene scartato il run indisponibile con un avviso esplicito. La definizione resta utilizzabile.

La preparazione degli archivi avviene in background; salvataggio e lettura ne attendono il completamento. Lo stato Grasshopper è accessibile dal thread del documento. Copia/incolla e Annulla usano un pacchetto interno, senza dipendere dal percorso del sidecar originale.

## Verifiche riproducibili

```powershell
.\scripts\build.ps1 -RhinoTests -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
.\scripts\build-arap.ps1 -CompareNative
.\tests\Build.Tests.ps1
```

La prima chiamata esegue Rhino 8 in-process in un host .NET 8 Windows Desktop (ExampleWriter) con BinaryFormatter **disabilitato**, senza cambiare il runtime configurato nell'applicazione Rhino dell'utente. Prima di avviare Rhino l'host carica la `System.Management` Windows di Rhino (`System\netcore\runtimes\win\lib\net7.0`), per non invalidare la licenza Cloud Zoo del Rhino interattivo. Non deduce il supporto a runtime diversi da quelli provati. Fino alla 0.4.1 la stessa suite girava anche sotto .NET Framework 4.8, e `scripts/check-runtime.ps1` ripeteva i binari net48 in un host .NET 8 (`tools/Arachne.RuntimeChecks`): con il plug-in su .NET 8 i due coincidono e sono stati tolti.

L'host verifica inoltre che i metadati di Arachne e delle sei DLL GPC non referenzino BinaryFormatter o lettori NRBF. Il report registra runtime e switch effettivi e `build.ps1` si ferma se BinaryFormatter non risulta spento. La disabilitazione è obbligatoria.

Le suite verificano archivi dei dati GPC, topologia, unità, vincoli, pressioni e reazioni; payload troncati e versioni rifiutate; sidecar mancanti/corrotti, Save As, copia/incolla, salvataggio immediato e ripristino senza solver; i sei esempi GH/GHX e le loro copie temporanee. I test non riscrivono gli esempi originali. `build-examples.ps1` li rigenera esplicitamente.

Gli script accettano `-ArachneBin` per un output isolato. Le note di rilascio sono in `RELEASE-0.4.1.md` e `RELEASE-0.4.0.md`; i risultati storici della rc.1 restano in `RELEASE-0.4.0-rc.1.md`; i report completi vengono prodotti accanto ai binari.
