# Libreria TensileLib

Collocare qui `TensileLib.dll`, quindi ricompilare con `scripts/build.ps1 -RequireTensile` dalla radice del progetto.

La versione referenziata da FeMM era 2.0.6. L'adattatore Arachne usa GPCGeometry: se la DLL ha riferimenti binari a MaffeisGeometry, occorre verificarne la compatibilità o ottenere una build per GPC. Non sostituire le DLL semplicemente rinominandole.

GPCGeometry e GPCUtilities sono già referenziate dalle cartelle locali adiacenti e copiate nell'output del plugin. I loro percorsi sono configurabili in Directory.Build.props o nello script di compilazione.
