# Cinque esempi, icone e correzioni — 0.2.0-rc.2

6 ottobre 2026. I cinque nuovi documenti usano tutti i **43 tipi di componente Arachne**, compresi i componenti recuperati da FeMM. Ogni componente riceve dati ed esegue il proprio calcolo. Gli esempi 01 e 02 rimangono disponibili.

## Aprire gli esempi

Installare il plugin aggiornato con tutte le DLL del pacchetto Release e riavviare Rhino. Aprire i file `.gh` della cartella `examples`; le versioni `.ghx` contengono lo stesso schema in XML. La geometria di ingresso è incorporata: non occorre un file Rhino esterno.

I nuovi documenti sono salvati con **Run=True** e ricalcolano all'apertura. Disattivarlo prima di cambiare molti ingressi. In Arachne Run=False svuota i risultati: non congela l'ultima soluzione. Gli esempi utilizzano metri e newton, indipendentemente dalle unità del documento Rhino.

| Documento | Generatore e calcolo | Tiri e carichi | Nodi / aste / piastre |
| --- | --- | --- | --- |
| [03_Rectangular_Iterative.gh](../examples/03_Rectangular_Iterative.gh) | Cable Net, sella 8 × 6 m, passo 2 m, LinearIterative | Warp 6000 N, weft 3000 N; -100 N a metà di ogni segmento warp | 20 / 31 / 12 |
| [04_Rectangular_Nonlinear.gh](../examples/04_Rectangular_Nonlinear.gh) | Cable Net, stessa sella, NonLinear | Warp 9000 N, weft 4500 N; pressione normale 100 N/m² | 20 / 31 / 12 |
| [05_Radial_Iterative.gh](../examples/05_Radial_Iterative.gh) | Radial Cable Net, raggio 4 m, 16 raggi, passo radiale 1 m, LinearIterative | Warp iniziale 8000 N con q conservata; weft imposto 4000 N; -100 N per nodo interno | 65 / 128 / 64 |
| [06_Compound_Nonlinear.gh](../examples/06_Compound_Nonlinear.gh) | Compound Net, due pannelli 4 × 4 m separati, passo 1 m, NonLinear | Warp 6000 N, weft 2500 N; bordo XYZ vincolato | 50 / 80 / 32 |
| [07_Patterning_Workshop.gh](../examples/07_Patterning_Workshop.gh) | Demo Saddle Net + sviluppo del primo pannello risolto e laboratorio geometrico | Densità q = 1500 N/m; compensazione del pannello campione 2% warp / 1% weft | 16 / 24 / 9 nella rete dimostrativa |

Nei casi rettangolari e composto, i due slider alimentano sia Cable, in modalità CableForce, sia Cable Constraints: il tiro è un obiettivo verificato sul risultato finale. I cavi del bordo interamente vincolato ricevono separatamente q = 5000 N/m.

Nel radiale il tiro warp è **iniziale**, convertito in densità q dalla lunghezza iniziale. I raggi devono poter assumere forze diverse procedendo verso l'esterno per equilibrare gli anelli. Gli anelli conservano invece il tiro imposto di 4000 N, aggiornato nelle iterazioni esterne. Non si presenta quindi il valore iniziale dei raggi come loro forza finale. Beam Results permette di leggerla per ogni asta. Il nodo centrale compare una sola volta nella lista dei nodi caricati; risultante applicata: -4900 N in Z.

Nel composto i rami `{0}` e `{1}` contengono i bordi dei due pannelli. Il modello ha due parti scollegate, entrambe vincolate: è intenzionale. La generazione delle piastre ora include entrambe le parti.

## Come interrogare i risultati

I quattro esempi di reti contengono Model, Check Model, Deconstruct Model, Solver Settings, Form Find, Deconstruct Result, Node Results, Beam Results, Plate Results, Equilibrium Report, Solver History e Result Preview.

- **Deconstruct Model** legge il modello iniziale, con ID, nodi, aste, vincoli e carichi.
- **Form Find / Result** conserva modello iniziale, impostazioni, soluzione e storia; **Solved model** espone lo snapshot della configurazione risolta.
- **Node Results** restituisce coordinate, spostamenti, forze applicate, reazioni e residui. Gli ID partono da 1.
- **Beam Results** restituisce forze assiali N, densità q e lunghezze; i record conservano l'appartenenza warp/weft.
- **Plate Results** restituisce mesh locali, nodi ordinati e ID/forze delle aste sul bordo, con un ramo per piastra. Queste forze non sono il tensore di membrana Nx/Ny/Nxy, che il motore non restituisce.
- **Equilibrium Report** controlla residui dei nodi liberi e bilanci globali di forze e momenti. **Solver History** mostra gli effettivi passi interni/esterni.

Il caso 03 include anche Unit Settings, Convert Units e Dependency Check. La conversione visualizza in kN le forze calcolate in N senza alterare il modello.

## Laboratorio di pannelli

Il caso 07 contiene un percorso dal form finding al primo pannello risolto, con Plate Results → Flattening ARAP → Panel Distortion.

Un secondo ramo usa un pannello piano 2 × 2 m come campione riproducibile: Mesh Clean, Flattening, Compensation, Borders, OrderBorders, entrambi i descrittori di decompensazione, Decompensation e Line-Up. Gli ingressi curva delle decompensazioni sono graftati: ciascun bordo riceve un descrittore. I due rami sono alternative applicate allo stesso bordo compensato. Nei descrittori 0.02 è una frazione del 2%; Compensation usa invece il numero 2.

Il terzo ramo usa Subdivide Mesh, Deform ARAP, Mesh Remap, Mesh Geodesic, Mesh Divide, Geodesic Offsets, Split Mesh On Curves, Unweld Mesh e Mesh Pipe. Mesh Remap trasferisce un punto sulla mesh deformata. Mesh Pipe visualizza le aste della rete risolta con raggio 0.025 m. Questi strumenti sono esercizi collegati e indipendenti: il documento non pretende di costituire un ciclo automatico completo di confezione della membrana.

## Errori riprodotti e corretti

| Errore | Prima / dopo | Codice da visionare |
| --- | --- | --- |
| Griglia coincidente con il bordo e Match=0 | Punti esterni con la stessa coordinata di ordinamento interrompevano i collegamenti su due lati. La sella produceva 15 nodi e 21 aste; ora 20 e 31. I punti coincidono con il bordo entro la tolleranza anche senza snapping opzionale. | `src/Arachne.Grasshopper/Geometry/CableNetHelper.cs`, righe 1042–1077; stesso criterio nel radiale da 1812. |
| Angoli radiali invertiti due volte | La seconda trasformazione dell'angolo già orientato aggiungeva corde spurie. Il caso radiale passa da 138 a 128 aste e da 70 a 64 piastre. | `CableNetHelper.cs`, angoli da riga 1592, 1636 e nelle quattro estremità del perimetro. |
| Centro radiale ripetuto | Il centro compariva una volta per raggio e moltiplicava il carico se collegato a Node Conditions. I punti interni sono ora unici entro tolleranza. | `CableNetHelper.cs:1860`; analogo controllo nel rettangolare a 1088. |
| Suddivisioni spurie nel composto con Match=0 | Rhino interpreta distanza massima zero come illimitata. La suddivisione usa ora almeno la tolleranza geometrica. Il test sul perimetro passa da 50 a 14 segmenti corretti. | `Components/Networks/CompoundNetComponent.cs:407` e `:429`. |
| Primo punto notevole ignorato | L'indice 0 era escluso dallo snapping. Ora è un indice valido. | `CompoundNetComponent.cs:528`. |
| Piastre mancanti nei modelli scollegati | TensileLib avvia la ricerca delle facce dall'asta 1. Arachne esegue la ricerca per parte connessa e ricompone nodi/aste/ID globali. Il composto passa da 16 a 32 piastre. | `src/Arachne.Core/TensileFormFindingSolver.cs:122` e `:203`; test `tests/Arachne.Core.Tests/ReleaseTests.cs:32`. |

La DLL TensileLib non è stata modificata in questa consegna. La correzione delle parti scollegate è nell'adattatore Arachne. Anche con CreateMesh=False la topologia completa rimane interrogabile e viene usata per distribuire la pressione.

## Icone e strumenti

Creati 43 PNG distinti e un'icona di assembly, tutti 24 × 24 px con trasparenza. Warp usa azzurro, weft arancio; i simboli distinguono geometria, vincoli, solver, interrogazione e pannelli. Sono incorporati nel `.gha`, senza file grafici esterni da installare. GUID e porte dei componenti sono conservati.

Tavola: [ICONS.svg](ICONS.svg). Risorse: `src/Arachne.Grasshopper/Resources/Icons`. Caricamento: `Helpers/ComponentIcons.cs`. Disegno sorgente: `tools/Arachne.ExampleWriter/IconArtwork.cs`. Collegamento generale alle icone: `Components/ComponentBase.cs:14`, oltre ai componenti recuperati da FeMM e all'assembly.

`FiveExamples.cs:53` gestisce generazione, salvataggio e riapertura; da 103 costruisce le reti; da 200 il laboratorio; da 248 verifica output, messaggi, equilibrio, tiri, mesh ARAP e numero atteso di piastre. `NetworkChecks.cs:13` contiene sei regressioni, con Match=0 e Match positivo per ciascun generatore. `ExampleAssets.cs` verifica unicità/dimensioni delle icone ed esporta le anteprime SVG senza aprire il canvas WinForms.

Il caricatore del processo di verifica è isolato dai plugin terzi installati. Non modifica le impostazioni dell'installazione Grasshopper. L'esportazione iniziale delle anteprime con canvas/GDI è stata sostituita da SVG dopo chiusure premature del processo headless; gli SVG dei modelli rappresentano direttamente i risultati calcolati.

## Verifiche

Compilazione Release senza errori o avvisi. **145 controlli superati**: 34 Core, 54 ARAP gestito, 37 Patterning, 14 risultati/utilità e 6 regressioni dei generatori. Tutte le DLL richieste sono presenti e il componente Dependency Check le carica correttamente. I confronti col vecchio motore ARAP nativo non sono stati ripetuti in questa consegna: il motore ARAP non è cambiato.

La generazione calcola ogni documento, salva GH e GHX, riapre entrambi e ripete i controlli: **5 calcoli iniziali + 10 riaperture con ricalcolo**. Controlla anche il numero degli oggetti e dei collegamenti. Non sono ammessi errori o warning nei componenti degli esempi. Check Model può restituire note informative nelle proprie uscite.

| Caso | Iterazioni esterne / passi registrati | Massimo errore relativo dei tiri imposti |
| --- | --- | --- |
| 03 iterativo | 99 / 198 | 8.93 × 10⁻⁸ |
| 04 non lineare | 1 / 98 | 1.39 × 10⁻⁷ |
| 05 radiale | 11 / 21 | 6.44 × 10⁻⁸, sugli anelli |
| 06 composto | 1 / 56 | 2.15 × 10⁻⁷ |
| 07 pannelli | 1 / 2 per la rete dimostrativa | Non applicabile: q assegnata |

Tutti gli esempi hanno cavi in trazione e passano le soglie indipendenti di 0.1 N per forze/residui e 1 N·m per momenti. Il massimo residuo nodale dei cinque casi è circa 1.61 × 10⁻⁵ N. La configurazione dei solver di rete usa massimo 500 iterazioni, tolleranza relativa 10⁻⁷ e tolleranza algebrica A 10⁻⁷.

Risultati generati: [VALIDATION.txt](../examples/VALIDATION.txt). Inventario completo: [COMPONENT-COVERAGE.tsv](../examples/COMPONENT-COVERAGE.tsv). Anteprime nella cartella `examples/previews`.

## Riproduzione e prossimi passi

Eseguire `./scripts/build.ps1 -RequireTensile -RhinoTests` per compilazione Release, test Core/ARAP/componenti e raccolta delle DLL. Eseguire `./scripts/build-examples.ps1` per ricreare i cinque esempi, le anteprime e il rapporto. Tutti gli output compilati rimangono nella cartella bin alla radice.

Restano da ampliare i casi con bordi curvi, fori, grandi reti, parti sovrapposte nella proiezione e accoppiamento reale fra pannelli. Il riconoscimento delle facce conserva i limiti geometrici del motore TensileLib. Il criterio di convergenza del solver non sostituisce il controllo fisico di equilibrio: nei test preliminari una rete collassata aveva restituito Converged, ma Equilibrium Report l'ha respinta. Un rifiuto automatico di questi casi direttamente nel solver è un miglioramento ulteriore da valutare.

Per la confezione rimangono utili orientamento automatico warp/weft, tolleranze di cucitura, margini, etichette dei pannelli e un'esportazione CAD organizzata. La validazione su casi di produzione e il confronto sistematico col testo teorico restano attività separate dai test di regressione.

## Commit

- `2e8f94e`: checkpoint prima degli esempi e delle icone.
- `2056fe2`: checkpoint prima delle correzioni dei generatori.
- `603d7a9`: perimetro rettangolare corretto e regressioni radiali.
- `406f5af`: topologia radiale/composta, icone incorporate e checkpoint prima della correzione delle piastre.

I commit successivi salvano la correzione delle piastre, i cinque documenti verificati e questa documentazione.
