# Set GPC fissato per Arachne

Sottoinsieme del set GPC ufficiale del 18 novembre 2026 (BA,
`gpc-release-20261118-ba`, `candidate=false`), il set comune della 0.5.1
con BridGHe 0.4.2 e ReBear 0.4.4. Le DLL e il manifest completo originale vengono
dalla cartella del set e sono verificati tramite SHA-256; Arachne distribuisce
soltanto le sei DLL necessarie.

- GPCUtilities 3.0.0.0; GPCGeometry 2.3.0.0; DelaunayMesh 2.0.0.14.
- GPCModel 5.54.0.0; GPC.Model.Persistence 3.17.0.1; GPC.Archive 1.2.0.10.

Rispetto ad AX (16 novembre) le tre DLL di Model vengono dai commit
Model `55c15ed5` e Checker `5bdb88dd` di origin/master: l'opzione B delle
sezioni aperte (J e centro di taglio numerici di T, C e L, che Arachne non
usa) sopra il set AZ. Utilities, Geometry e DelaunayMesh sono identiche.
Il set precedente era AX del 16 novembre (0.4.1 e 0.5.0).

La build verifica le impronte prima di compilare e sui file distribuiti. Non
ricompila né modifica i repository GPC. `build.ps1 -ReleaseSet <release-set.json>`
consente un altro set stabile completo; DLL e manifest devono stare insieme.
Un set candidato si accetta solo con `-GpcAllowCandidate`, per il collaudo.
Il controllo al caricamento segnala librerie diverse già caricate da altri plug-in.
