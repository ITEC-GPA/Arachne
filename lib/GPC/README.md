# Set GPC fissato per Arachne

Sottoinsieme del set GPC ufficiale del 16 novembre 2026 (AX,
`gpc-release-20261116-ax`, `candidate=false`), il set comune della 0.4.1
con BridGHe 0.4.1 e ReBear 0.4.3. Le DLL e il manifest completo originale vengono
dalla cartella del set e sono verificati tramite SHA-256; Arachne distribuisce
soltanto le sei DLL necessarie.

- GPCUtilities 3.0.0.0; GPCGeometry 2.3.0.0; DelaunayMesh 2.0.0.14.
- GPCModel 5.51.0.2; GPC.Model.Persistence 3.15.0.4; GPC.Archive 1.2.0.5.

Rispetto ad AW (14 novembre) le tre DLL di Model sono ricompilate sui commit
Model `d126edb2` e Checker `99c1966e` di origin/master, con l'ultimo componente
della versione aumentato; Utilities, Geometry e DelaunayMesh sono identiche.
Le sei DLL hanno gli stessi byte del candidato AX del 15 novembre.

La build verifica le impronte prima di compilare e sui file distribuiti. Non
ricompila né modifica i repository GPC. `build.ps1 -ReleaseSet <release-set.json>`
consente un altro set stabile completo; DLL e manifest devono stare insieme.
Un set candidato si accetta solo con `-GpcAllowCandidate`, per il collaudo.
Il controllo al caricamento segnala librerie diverse già caricate da altri plug-in.
