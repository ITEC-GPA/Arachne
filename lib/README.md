# Dipendenze locali

Questa cartella non è una sorgente di DLL né un pacchetto distribuibile.
I riferimenti predefiniti provengono dai repository adiacenti Geometry,
Utilities e tensilelib; percorsi e prerequisiti sono nel [README](../README.md).

Per binari esterni specificare `GpcGeometryPath`, `GpcUtilitiesPath` e
`TensileLibPath` agli script o a MSBuild. `alglibnet2.dll` deve trovarsi accanto
a `TensileLib.dll`. MathNet.Numerics viene ripristinata da NuGet.

`scripts/build.ps1 -RhinoTests` compila e verifica il plug-in. La distribuzione
è `bin/Arachne.Grasshopper/Release/net48`, con dipendenze e manifest generati.
Non esiste `-RequireTensile`: TensileLib è sempre richiesta per il plug-in.
