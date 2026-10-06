# Prima release: registro degli interventi

Richiesta del 6 ottobre 2026: realizzare gli interventi della prima release; gli esempi saranno preparati dall'utente. Non rigenerare i file in examples.

## Stato salvato prima delle modifiche

Baseline Arachne: d41d6ff. Baseline TensileLib: 1cf5ac4. Nessuna modifica locale all'avvio.

Problemi verificati leggendo l'implementazione, prima di intervenire:

1. FormResult contiene liste parallele, senza modello associato, identificativi o interrogazione degli elementi.
2. Form Find non espone un oggetto risultato. Le facce vengono conservate solo quando CreateMesh=true.
3. Plate in TensileLib non contiene un tensore di tensione calcolato. SumStresses e un dato di ingresso. Anche il vecchio FeMM aggiornava la geometria delle piastre senza ricavare risultati di membrana.
4. Mancano risultati nodali completi, reazioni, residui, bilancio delle forze/momenti e storia delle iterazioni interne.
5. La cancellazione viene controllata solo prima/dopo un'intera chiamata al solver.
6. Il programma delle verifiche Rhino rigenera gli esempi: serve un percorso di soli test.

## Interventi autorizzati e ordine

- Risultati associati a una copia del modello, identificativi stabili all'interno del modello e interrogazione di nodi, aste, piastre.
- Deconstruct Model/Result, Node/Beam/Plate Results, Check Model ed Equilibrium Report.
- Storia effettiva delle iterazioni e cancellazione cooperativa; anteprima dei risultati.
- Panel Distortion, conversioni esplicite delle unita con GPCUtilities, controllo delle dipendenze.
- Separazione progressiva degli algoritmi dai componenti, conservando GUID e porte preesistenti.
- Verifiche numeriche e Grasshopper ripetibili, pacchetto Release e documentazione con righe da visionare.

## Criteri e limiti

Le forze delle aste sono in N, le densita di forza in N/m, le pressioni in N/m2. I risultati delle piastre devono distinguere forze di bordo, obiettivi di pretensione e tensioni di membrana: non inventare Nx/Ny/Nxy a partire da dati non presenti nel motore. I valori non disponibili devono essere riconoscibili come tali.

I test automatici comprendono casi analitici e geometrie sintetiche; non sostituiscono il confronto completo con il libro indicato, i modelli reali dell'utente e la prova su un altro PC. Queste verifiche esterne devono rimanere dichiarate nel resoconto.
