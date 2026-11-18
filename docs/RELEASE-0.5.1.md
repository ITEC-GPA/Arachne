# Arachne 0.5.1 — .NET 8 e set GPC ufficiale del 18 novembre (BA)

Versione prodotto 0.5.1, assembly/file 0.5.1.0. Il set GPC è il rilascio ufficiale del 18 novembre 2026 (`gpc-release-20261118-ba`, `candidate=false`), comune con BridGHe 0.4.2 e ReBear 0.4.4. È il primo rilascio dopo la 0.4.1: comprende il passaggio a .NET 8 della 0.5.0, mai distribuita da sola.

## .NET 8 (dalla 0.5.0)

- Il plug-in è `net8.0-windows` (x64) e si carica in Rhino 8 sul runtime .NET Core, cioè .NET 8, l'impostazione predefinita. **Con Rhino impostato su .NET Framework (comando `SetDotNetRuntime`) non si carica.**
- La cartella del plug-in è `bin/Arachne.Grasshopper/Release/net8.0-windows` (prima `…/net48`), sempre con una sola `.gha` sotto `bin`. Chi aggiorna da una 0.4.x deve togliere la cartella `net48` vecchia, altrimenti Grasshopper trova due `.gha`.
- Risultati identici bit per bit alla 0.4.1 su .NET Framework: 32 scenari e 6 esempi aperti, risalvati e ripristinati (`tools/Arachne.Compare`), testi e messaggi compresi. Dettagli nel [registro delle modifiche](CHANGELOG.md).

## Set GPC BA (0.5.1)

- GPCModel 5.51.0.2 → 5.54.0.0, GPC.Model.Persistence 3.15.0.4 → 3.17.0.1, GPC.Archive 1.2.0.5 → 1.2.0.10 (Model `55c15ed5` su origin/master); GPCUtilities 3.0.0.0, GPCGeometry 2.3.0.0 e DelaunayMesh 2.0.0.14 identiche.
- GPCModel 5.54 calcola J e centro di taglio delle sezioni T, C e L con il metodo numerico (opzione B delle sezioni aperte). Arachne non usa queste sezioni: i componenti T, C e Angle di Arachne creano una sezione generica con J assegnata, quindi i risultati non cambiano.
- TensileLib 2.5.0.1 (`756EB181…`), alglibnet2 e MathNet.Numerics 5.0.0 (build net48, lo stesso file di BridGHe e ReBear) restano quelli della 0.5.0: Geometry e Utilities hanno gli stessi byte, TensileLib non va ricompilata.
- Nessun GUID, porta, esempio o formato degli archivi è cambiato.

Per l'installazione valgono le indicazioni della [0.4.0](RELEASE-0.4.0.md), con la cartella nuova `net8.0-windows`: chiudere Rhino, copiare l'intero pacchetto, tenere un solo percorso Arachne attivo e usare BridGHe 0.4.2 e ReBear 0.4.4 dello stesso set.
