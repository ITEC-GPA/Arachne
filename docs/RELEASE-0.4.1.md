# Arachne 0.4.1 — set GPC ufficiale del 16 novembre (AX)

Versione prodotto 0.4.1, assembly/file 0.4.1.0. Il set GPC è il rilascio ufficiale del 16 novembre 2026 (`gpc-release-20261116-ax`, `candidate=false`), comune con BridGHe 0.4.1 e ReBear 0.4.3.

La riga informativa MathNet riporta il percorso e lo SHA-256 del file effettivamente caricato. Il caricamento accoda il riferimento all'assembly; il lavoro di lettura/hash avviene successivamente e la stampa usa Idle di Rhino. Non cambia la politica di caricamento né le formule di form finding o patterning.

Le sei DLL GPC vengono dal set AX. GPCModel 5.51.0.2, GPC.Model.Persistence 3.15.0.4 e GPC.Archive 1.2.0.5 sono ricompilate rispetto ad AW sugli stessi sorgenti; GPCUtilities 3.0.0.0, GPCGeometry 2.3.0.0 e DelaunayMesh 2.0.0.14 sono identiche. ARAP resta gestito in C#; TensileLib 2.5.0.1 e i contratti degli archivi restano quelli di F8. Nessun GUID, porta, esempio o baseline è cambiato.

Verifiche sul set AX, senza flag per i candidati: 54 controlli ARAP; in Rhino senza finestra 66 controlli di inventario, 41 di form finding, 43 di patterning e 6 di rete, con il confronto FeMM. `BUILD.json` del pacchetto riporta il commit, il set (`Candidate: false`) e le verifiche eseguite.

Per l'installazione valgono le indicazioni della [0.4.0](RELEASE-0.4.0.md): chiudere Rhino, copiare l'intero pacchetto con esempi, sidecar, dipendenze e manifest, tenere un solo percorso Arachne attivo e usare BridGHe e ReBear dello stesso rilascio.
