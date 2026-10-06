## 0.2.0-rc.1 - 6 ottobre 2026

Nuovo progetto Arachne.Arap C# netstandard2.0, indipendente da Rhino/FEM e dalle librerie native. Implementati sviluppo 2D, deformazione 3D, vincoli, perimetro, algebra sparsa, diagnostica e cancellazione; collegati i componenti Grasshopper e aggiunte uscite informative senza cambiare GUID o ingressi. Correzioni a precisione dei pin, conversione Rhino e gestione del doppio fallimento nella geodetica. Pacchetto autonomo e distribuzione Arachne senza Arachne.IGL.dll. 144 controlli superati, inclusi sei confronti con il motore nativo. Resoconto, righe e limiti: [ARAP-REWRITE.md](ARAP-REWRITE.md). Esempi invariati.

## 0.1.0-rc.1 - 6 ottobre 2026

13 nuovi componenti (43 totali), snapshot del modello, risultati per ID, forze di bordo delle piastre, reazioni/residui, bilancio, storia/cancellazione, background, unita, distorsioni e controllo DLL. Refactoring dei tre algoritmi principali dei pannelli e build nativa ARAP dai sorgenti. TensileLib aggiornato a 2.1.0.0 dopo correzioni di pressione, accumulo del peso proprio, copia dei nodi e coordinate interamente vincolate. 100 test locali superati; esempi invariati. Dettagli, righe e limiti in [RELEASE-1.md](RELEASE-1.md) e [RESULTS.md](RESULTS.md).
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

## Passo 6 - esempio pannelli e verifica dei percorsi di errore

Creato 02_Patterning (10 oggetti, 8 collegamenti), verificato e ricalcolato dopo la riapertura di GH e GHX. Aggiornata la documentazione per le dipendenze e i componenti ora disponibili.

Sei ulteriori prove aggiunte prima delle correzioni: 25 passano, 6 falliscono. Rilevati: PlanarFixedLengthsSolver respinge un pannello gia convergente all'ultima iterazione; Flattening e Compensation pubblicano la mesh non convergente; Compensation pubblica una mesh anche dopo un'eccezione del solver; Flattening ARAP pubblica l'output quando la DLL restituisce false; Mesh Remap prosegue nonostante topologie incompatibili. Quest'ultimo gruppo e distinto dai sei difetti originali di TensileLib e dai quattro difetti del passo 4.

## Passo 7 - correzioni dopo il commit 66b2bac

- TensileLib `1cf5ac4`: risolto il controllo dell'ultima iterazione anche in PlanarFixedLengthsSolver, con due regressioni numeriche aggiuntive. Pubblicato su GitHub. Totale motore: 16 prove superate.
- Flattening e Compensation: interruzione senza geometria in caso di mancata convergenza; Compensation interrompe anche dopo un'eccezione, riportandone il messaggio.
- Flattening ARAP e ARAPHelper: esito false non pubblicato; controllo mesh triangolata e coordinate finite; configurazione nativa nel blocco di gestione errori.
- GetFlattenedMesh duplica l'ingresso e restituisce null se il solver non converge. GeodesicOffsets adeguato: fallback ARAP con copia triangolata, output con topologia inizializzata e configurazione esplicita, senza riusare un risultato non convergente.
- Mesh Remap: interrompe su conteggi incompatibili e su connettivita delle facce diversa, anziche proseguire con un semplice avviso.
- Il banco di prova usa un vincolo di lunghezza geometricamente incompatibile per il test negativo della compensazione: una riduzione uniforme puo convergere correttamente in una sola iterazione. Aggiunto riferimento GPCGeometry al progetto dei test Rhino per costruire questo vincolo.

Esito finale dei componenti: **31/31**; Core **17/17**; motore **16/16**. Entrambi gli esempi ricalcolano correttamente. Il refactoring generale delle classi e rinviato al passo da concordare; le modifiche fino a qui sono trasferimento, adeguamento API e correzione dei difetti riprodotti.
