# Esempi Grasshopper

`01_SaddleNet.gh` è il documento binario Grasshopper; `01_SaddleNet.ghx` è lo stesso schema in formato XML. Installare prima il plugin Arachne con tutte le DLL raccolte da `scripts/build.ps1`.

Il documento contiene 9 oggetti e 10 collegamenti. Usare Demo Saddle Net per modificare la discretizzazione, la luce e la quota dei vertici. Cable applica una densità uniforme di 1 N/m; Node Conditions blocca il bordo. Model assembla la rete, mentre Solver Settings e il booleano Run controllano Form Find.

I valori predefiniti producono una rete 10 x 10 m, discretizzata in 4 x 4 celle, con quote agli angoli pari a +2 e -2 m. Il peso proprio e la pressione sono disattivati. Le coordinate interne sono inizialmente a quota zero; il file non contiene una forma di equilibrio precalcolata.

Il file e salvato con Run=False. Con le DLL del pacchetto Release, Run=True calcola l'equilibrio. Il generatore ha verificato convergenza e geometria risultante prima del salvataggio.

## Sviluppo dei pannelli

`02_Patterning.gh` e `02_Patterning.ghx` contengono 10 oggetti e 8 collegamenti. Un pannello rettangolare inclinato viene sviluppato con Flattening e, in un ramo alternativo, con ARAP. Compensation applica 2% in trama e 1% in ordito. Seguono estrazione e ordinamento dei bordi, quattro descrittori neutri, Decompensation e Line-Up.

I descrittori conservano la convenzione FeMM: ingresso 0 = scala 1, ingresso 0.02 = scala 0.98. Non sono le percentuali intere di Compensation e non calcolano automaticamente l'inversa della compensazione. La lunghezza del descrittore puo essere assoluta o normalizzata; i valori negativi partono dall'estremita finale. Lo schema usa quattro descrittori per i quattro bordi: se cambia il numero dei bordi, adeguare anche la lista dei descrittori.

Entrambi i formati del secondo esempio sono stati riaperti e ricalcolati; il controllo confronta l'area con `2*sqrt(5)*0.98*0.99` e verifica quattro curve valide.

Per rigenerare l'esempio da sorgente, da PowerShell nella radice Arachne:

```powershell
dotnet build tools/Arachne.ExampleWriter/Arachne.ExampleWriter.csproj -c Release
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
& bin/Arachne.ExampleWriter/Release/net48/Arachne.ExampleWriter.exe $PWD.Path 'C:\Program Files\Rhino 8\System' 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
```

Il generatore richiede un'installazione Rhino utilizzabile e inizializza il runtime senza finestra. La registrazione del plugin è limitata al suo processo; non installa il plugin nelle Libraries. Utilizza il caricatore interno di Grasshopper solo per la verifica dell'archivio, quindi potrebbe richiedere adeguamenti con versioni diverse di Grasshopper.
