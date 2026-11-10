# Set GPC fissato per Arachne

Sottoinsieme del set stabile generato l'8 novembre 2026, acquisito dalla copia
installata in ANTHEA/lib/Checker e verificato tramite SHA-256. Il manifest completo
originale è conservato; Arachne distribuisce soltanto le sei DLL necessarie.

- GPCUtilities 3.0.0.0; GPCGeometry 2.3.0.0; DelaunayMesh 2.0.0.14.
- GPCModel 5.50.0.2; GPC.Model.Persistence 3.15.0.2; GPC.Archive 1.2.0.3.

La build verifica le impronte prima di compilare e sui file distribuiti. Non
ricompila né modifica i repository GPC. `build.ps1 -ReleaseSet <release-set.json>`
consente un altro set stabile completo; DLL e manifest devono stare insieme.
Il controllo al caricamento segnala librerie diverse già caricate da altri plug-in.
