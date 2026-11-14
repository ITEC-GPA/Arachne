# Set GPC fissato per Arachne

Sottoinsieme del set GPC ufficiale del 14 novembre 2026 (AW,
`gpc-release-20261114-aw`, `candidate=false`), il set comune del rilascio F8
con BridGHe e ReBear. Le DLL e il manifest completo originale vengono dalla
cartella del set e sono verificati tramite SHA-256; Arachne distribuisce
soltanto le sei DLL necessarie.

- GPCUtilities 3.0.0.0; GPCGeometry 2.3.0.0; DelaunayMesh 2.0.0.14.
- GPCModel 5.51.0.1; GPC.Model.Persistence 3.15.0.3; GPC.Archive 1.2.0.4.

La build verifica le impronte prima di compilare e sui file distribuiti. Non
ricompila né modifica i repository GPC. `build.ps1 -ReleaseSet <release-set.json>`
consente un altro set stabile completo; DLL e manifest devono stare insieme.
Un set candidato si accetta solo con `-GpcAllowCandidate`, per il collaudo.
Il controllo al caricamento segnala librerie diverse già caricate da altri plug-in.
