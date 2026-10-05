# Primo esempio di ricerca di forma

`01_SaddleNet.gh` è il documento binario Grasshopper; `01_SaddleNet.ghx` è lo stesso schema in formato XML. Installare prima il plugin Arachne con tutte le DLL raccolte da `scripts/build.ps1`.

Il documento contiene 9 oggetti e 10 collegamenti. Usare Demo Saddle Net per modificare la discretizzazione, la luce e la quota dei vertici. Cable applica una densità uniforme di 1 N/m; Node Conditions blocca il bordo. Model assembla la rete, mentre Solver Settings e il booleano Run controllano Form Find.

I valori predefiniti producono una rete 10 x 10 m, discretizzata in 4 x 4 celle, con quote agli angoli pari a +2 e -2 m. Il peso proprio e la pressione sono disattivati. Le coordinate interne sono inizialmente a quota zero; il file non contiene una forma di equilibrio precalcolata.

Il file è salvato con Run=False. Senza TensileLib si può già verificare la costruzione del modello. Run=True segnala la dipendenza mancante. Il motore deve essere aggiunto e verificato prima di usare il risultato della ricerca di forma.

Per rigenerare l'esempio da sorgente, da PowerShell nella radice Arachne:

```powershell
dotnet build tools/Arachne.ExampleWriter/Arachne.ExampleWriter.csproj -c Release
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
& tools/Arachne.ExampleWriter/bin/Release/net48/Arachne.ExampleWriter.exe $PWD.Path 'C:\Program Files\Rhino 8\System' 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
```

Il generatore richiede un'installazione Rhino utilizzabile e inizializza il runtime senza finestra. La registrazione del plugin è limitata al suo processo; non installa il plugin nelle Libraries. Utilizza il caricatore interno di Grasshopper solo per la verifica dell'archivio, quindi potrebbe richiedere adeguamenti con versioni diverse di Grasshopper.
