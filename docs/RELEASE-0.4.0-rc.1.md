# Arachne 0.4.0-rc.1

Data: 10 novembre 2026. Windows x64, Rhino 8. Versione assembly/file `0.4.0.1`.

## Contenuto

Migrazione GPC integrata su master preservando la manutenzione 0.3.0-rc.9. Modelli, elementi, proprietà e risultati sono oggetti GPC; TensileLib resta il motore di form finding e ARAP resta gestito in C#.

Solo archivi Arachne `ARCGPC3` versione 2: nessun BinaryFormatter, lettore NRBF, conversione o retrocompatibilità. Non ci sono documenti utente della 0.3.0 da recuperare. Gli esempi sono rigenerati nel formato corrente e includono i sidecar dei risultati; Auto Run è spento.

Le letture fallite svuotano il dato precedente. Un sidecar assente o corrotto consente comunque di aprire la definizione; al nuovo salvataggio il run indisponibile viene scartato con avviso. La preparazione in background viene attesa prima del salvataggio. Copia/incolla e Save As sono coperti dai controlli.

Gli zeri con segno nelle coordinate sono normalizzati solo nella copia di analisi: Rhino può archiviare `-0` come `+0`. Non si applicano arrotondamenti alle coordinate non nulle; il test rileva anche una modifica di `1e-9 mm`.

Il contorno grafico delle sezioni circolari usa 64 direzioni fissate in modo deterministico: le implementazioni di seno e coseno dei runtime differiscono negli ultimi bit. Le dimensioni e le proprietà meccaniche non sono arrotondate; il contorno completo resta nell'oggetto GPC.

## Dipendenze fissate

Set stabile `2026-11-08T09:46:05`, `candidate=false`, conservato con il manifest originale in `lib/GPC`. La build verifica SHA-256 e versione prima della compilazione e confronta le DLL distribuite. Non compila i repository condivisi.

| Libreria | Versione assembly |
| --- | --- |
| GPCUtilities | 3.0.0.0 |
| GPCGeometry | 2.3.0.0 |
| DelaunayMesh | 2.0.0.14 |
| GPCModel | 5.50.0.2 |
| GPC.Model.Persistence | 3.15.0.2 |
| GPC.Archive | 1.2.0.3 |
| TensileLib | 2.5.0.1 |

I plug-in caricati nello stesso Rhino devono usare le stesse DLL GPC. Il set incluso è stato acquisito dall'insieme stabile di ANTHEA; gli output adiacenti di Model contenevano una compilazione precedente e non sono stati usati né modificati.

## Installazione e archivi

Copiare la cartella `bin/Arachne.Grasshopper/Release/net48` nelle Libraries di Grasshopper, con tutte le dipendenze. Sotto l'output installato deve esserci una sola `.gha`. Per gli esempi copiare insieme definizione e relativi `.gpc-results`; non modificare i nomi dei sidecar separatamente dal documento.

Salva con nome conserva l'originale e scrive i risultati nella nuova destinazione. Se gli ingressi cambiano, Form Find richiede Run e non riutilizza risultati obsoleti.

I comandi riproducibili e la distinzione fra host Rhino e host diagnostico .NET 8 sono in [Runtime e archivi](RUNTIME-AND-ARCHIVES.md). I test non attestano runtime diversi da quelli eseguiti.

## Verifiche del rilascio

| Verifica | Esito |
| --- | --- |
| Release, .NET Framework 4.8.10008.0 / Rhino 8 | 177 superati, 0 falliti, 0 saltati |
| Stessi binari net48, host .NET 8.0.33, BinaryFormatter disabilitato | 177 superati, 0 falliti, 0 saltati |
| ARAP gestito | 54 superati |
| ARAP autonomo con confronto nativo | 60 superati (54 + 6), 0 falliti |
| Compilazione Release e Debug isolata | 0 errori, 0 avvisi |
| Generazione GH/GHX e ripristino esempi | Tutti e 6 verificati, con sidecar |
| Build.Tests: percorsi, FeMM, manifest e hash GPC | Superati, incluse prove negative attese |
| Metadati delle 3 assembly Arachne e 6 GPC | Nessun riferimento a BinaryFormatter o lettori NRBF |
| Scala: 1.024 e 10.000 nodi, percorso completo | Completato, inclusa rilettura dei risultati |

Le 177 verifiche sono: 66 inventario, 15 contratti GPC, 41 form finding, 43 patterning, 6 reti e 6 archivi degli esempi. Il confronto con FeMM usa in sola lettura `../_OLD/FeMM 30-5-2024/Test1/FeMM` ed è obbligatorio nei due test completi.

Il confronto numerico grezzo resta strict: restituisce una differenza anche per il segno dello zero. Il rapporto distingue esplicitamente tali casi dalla correzione IPS; non applica tolleranze globali né modifica i risultati di riferimento.

## Evidenze numeriche e prove aggiuntive

Il confronto con i binari conservati della 0.3.0-rc.9 comprende 32 scenari: 18 identici bit per bit, 13 con sole differenze fra `-0` e `+0`, uno diverso per la correzione del peso proprio IPS. I 13 scenari hanno gli stessi valori numerici e testi; la normalizzazione degli zeri spiega interamente la differenza binaria. Non si interpreta il confronto aggregato come 32 successi: il caso IPS viene validato separatamente confrontando freccia e reazioni con il modello SI equivalente, tolleranza relativa `1e-6`.

La prova di scala esegue costruzione, esportazione, solver, risultati, sidecar, copie e archivi su 1.024 e 10.000 nodi. La rete maggiore ha 29.601 aste e 19.602 triangoli. In Release l'archivio del modello occupa circa 4,43 MB e quello del modello calcolato circa 16,58 MB. Sono misure del banco di prova locale, non limiti garantiti. Il test dedicato verifica inoltre il rapporto dei tempi di esportazione fra 2.500 e 10.000 nodi.

La prima prova di scala, eseguita con altri processi Rhino concorrenti, si è interrotta per indisponibilità della licenza. La prova completa sequenziale è riuscita; il fallimento iniziale non è conteggiato come test superato. Il banco serve gli eventi Rhino fra le operazioni lunghe e restituisce un errore anche se fallisce l'archiviazione del sidecar.

Il set GPC alterato viene rifiutato prima della build. I test verificano anche invalidazione di manifest precedenti, percorsi Debug protetti e confronto FeMM obbligatorio con sorgenti mancanti/incompleti. I casi negativi attesi non sono errori nascosti della suite.