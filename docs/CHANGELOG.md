# Registro delle modifiche

## Stato iniziale salvato

Commit `3fab618`: progetto Arachne completo prima del collegamento reale a TensileLib. Lo scheletro compilava senza motore; 14 controlli superati.

## Passo 1 - collegamento TensileLib GPC

Modifiche autorizzate dopo il salvataggio iniziale:

| File | Modifica e motivo |
| --- | --- |
| Directory.Build.props | Riferimento alla DLL Release del progetto adiacente tensilelib. |
| src/Arachne.Core/Arachne.Core.csproj | Target net48 coerente con il motore net472 e il plugin; riferimento e controllo di presenza di alglibnet2. |
| src/Arachne.Core/TensileFormFindingSolver.cs | API 2.0.5.3: ID dei nodi; CreateMesh controlla l'output; costruttori a cinque argomenti. Valore 0 nelle impostazioni algebriche diventa il default reale del motore. |
| src/Arachne.Core/Validation.cs | Rifiuto esplicito della seconda tolleranza algebrica non supportata. |
| src/Arachne.Grasshopper/Components/SolverComponents.cs | Conservato l'ingresso TB per i documenti esistenti, con descrizione che richiede 0. |
| scripts/build.ps1 | Esecuzione test net48 e distribuzione di alglibnet2 insieme al motore. |
| tests/Arachne.Core.Tests | Test nel runtime del plugin e tre prove reali di equilibrio, compreso il controllo delle forze finali non lineari. |
| tools/Arachne.ExampleWriter/Program.cs | Verifica della ricerca di forma nel documento Grasshopper prima del salvataggio con Run=False. |

Compilazione Release: zero errori e avvisi. Test Core: 17 superati. Le sei anomalie diagnostiche del motore non sono corrette in questo passo; le correzioni vengono registrate separatamente.

## Riferimento teorico

Shell Structures for Architecture: Form Finding and Optimization, Adriaenssens, Block, Veenendaal e Williams (a cura di), Routledge 2014. Riferimenti pertinenti: capitoli 6 e 12, come riportato nel README di TensileLib. La verifica completa contro il testo resta distinta dai test di regressione.

Verifica Rhino/Grasshopper del passo 1: schema risolto (25 nodi, 40 cavi, 16 facce), generazione e riapertura GHX riuscite, file GH salvato con Run=False. Il caricamento di plugin Tekla installati produce messaggi di connessione esterni; i componenti Arachne non hanno errori.

## Passo 2 - sei correzioni nel motore

TensileLib commit `8c292de` (pubblicato su GitHub): convergenza all'origine, ultima iterazione non lineare, somma dei carichi, conservazione della risultante, eliminazione dell'accumulo dei carichi e copia delle piastre. Registro dettagliato in `../tensilelib/CHANGELOG.md`. Le 14 prove del motore passano.

## Passo 3 - primo trasferimento Patterning

Recuperati 19 componenti, che completano i 22 della cartella Patterning con i tre generatori gia trasferiti. Conservati nomi, ingressi/uscite e 16 icone; assegnati GUID nuovi elencati in PATTERNING-PORT.md. Copiati MeshGeodesic, ARAPHelper e DecompensationDescriptor. Compensation ora riceve Cable/Cable Constraints di Arachne al posto dei tipi FEM. Flattening usa gli ID interi richiesti da TensileLib. Copiata la DLL nativa x64 locale come Arachne.IGL.dll e aggiunto wrapper C ABI, senza dipendenza da assembly di geometria precedenti. Gli errori della libreria nativa vengono propagati invece di produrre vertici a zero.

Compilazione di tutti i componenti riuscita senza errori e avvisi. Verifiche funzionali del trasferimento in corso nel passo successivo.

## Passo 4 - regressioni riprodotte prima delle correzioni

La suite Rhino/Grasshopper copre tutti i 19 componenti aggiunti: 21 prove, 17 riuscite e 4 fallite sul codice trasferito. Stato salvato prima di intervenire, come richiesto.

| Errore riprodotto | Risultato attuale | Risultato atteso |
| --- | --- | --- |
| ARAP Deform: scala dei vincoli | Pin (1,1,0) restituito come (0.03,0.03,0). La DLL normalizza la mesh a 100, ma non i pin. | Pin nella posizione richiesta, nelle unita del modello. |
| Decompensation: interpolazione | Vertice a 1/4 della lunghezza trattato come parametro 1/2; X=0.875. | X=0.9375, interpolando la lunghezza geometrica originale. |
| Decompensation: modifica del descrittore | La lunghezza assoluta 4 viene sostituita con la frazione 1 nell'oggetto condiviso. | Descrittore riutilizzabile e invariato. |
| Descriptor Ext: liste diverse | Avviso seguito da eccezione per indice fuori intervallo. | Avviso e interruzione senza risultato. |

I primi falsi negativi del banco di prova (documento Rhino non attivo, output letti dopo la chiusura del documento GH e valori aggiunti ai default) sono stati eliminati nel test harness; non richiedono cambiamenti ai componenti. Line-Up e gli strumenti che usano la tolleranza del documento passano con il contesto Rhino corretto.

## Passo 5 - correzioni Patterning dopo il commit 388be78

- ARAPHelper: scala i pin secondo la normalizzazione della DLL nativa 0.9.0, sia per Deform sia per Unwrap. Mesh, indici e coordinate controllati prima delle chiamate native. La DLL nativa non e stata modificata.
- DeformArap: rifiuta indici negativi o fuori mesh e posizioni in eccesso; non elimina piu solo gli indici lasciando disallineate le posizioni. Conserva il completamento dei pin senza coordinate e la gestione dei duplicati. Gli errori di caricamento/configurazione sono gestiti nello stesso blocco della chiamata nativa.
- Decompensation: usa la lunghezza geometrica originale; inserisce i punti di cambio del descrittore; interpola copie locali ordinate. Gestisce segmenti lineari, estremi, punti coincidenti con scale uguali e segnala quelli con scale incompatibili. Non modifica piu descrittori o curve condivisi. Conservata la convenzione originale di scala rispetto al centro medio degli estremi.
- Descriptor Ext: dopo l'avviso per liste diverse termina senza produrre un descrittore parziale.
- Aggiunti quattro controlli ai 21 precedenti: ARAP Unwrap traslato, rifiuto dei pin negativi, compensazione 10%/20% e descrittori con punti coincidenti su una linea.

Esito: **25/25 prove Patterning**, nessun errore di compilazione. Ricalcolo e riapertura dello schema SaddleNet riusciti; equilibrio con 25 nodi, 40 cavi e 16 facce. Gli output nulli predisposti da Grasshopper nei percorsi di errore vengono distinti da vera geometria nei test.
