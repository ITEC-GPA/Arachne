# Esempi Grasshopper

Ogni esempio è disponibile in formato binario (`.gh`) e XML (`.ghx`). Installare prima il plug-in Arachne con tutte le DLL raccolte da `scripts/build.ps1`.

| File | Contenuto |
| --- | --- |
| [01_SaddleNet.gh](01_SaddleNet.gh) | Sella 10 x 10 m con 4 x 4 campi e bordo vincolato, quote ±2 m agli angoli, coordinate in mm. Comprende:<br>• Beam con Beam Property (Cable, sezione piena D 10 mm, acciaio) e Force Density 1 N/mm nel caso di carico FF;<br>• Node con Restraints;<br>• Build Model in N/mm, Check Model e Form Find lineare;<br>• Beam Forces (tiri in N) e Support Reactions. |
| [02_Patterning.gh](02_Patterning.gh) | Pannello inclinato: Flattening e, in alternativa, Flattening ARAP; Compensation 2% / 1%, Borders, OrderBorders, descrittori neutri, Decompensation e Line-Up. |
| [03_CableNet.gh](03_CableNet.gh) | Cable Net su un perimetro a sella 12 x 8 m, maglia 1 x 1 m, coordinate in mm. Ordito con q = 2 N/mm, trama con q = 1 N/mm, linee di bordo vincolate; Form Find lineare con mesh. |

Negli esempi 01 e 03 Form Find ha **Auto Run** collegato a un interruttore attivo: il calcolo parte all'apertura. Disattivarlo per lavorare con il pulsante Run.

Nel 03 gli ingressi delle Beam e l'ingresso Elements di Build Model sono appiattiti: le uscite di Cable Net sono alberi con un ramo per cavo.

Gli esempi vengono generati da `scripts/build-examples.ps1` (`tools/Arachne.ExampleWriter`). Il generatore calcola ogni definizione, la salva nei due formati, la riapre e la ricalcola. Controlla anche che Form Find produca modello e mesh senza errori. Per l'esempio 02 confronta l'area compensata con `2*sqrt(5)*0.98*0.99` e verifica i quattro bordi decompensati.

I descrittori di decompensazione seguono la convenzione FeMM: ingresso 0 = scala 1, ingresso 0.02 = scala 0.98. Non sono le percentuali intere di Compensation e non calcolano automaticamente l'inversa della compensazione.
