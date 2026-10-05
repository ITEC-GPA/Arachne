# Architettura dopo il trasferimento

Arachne.Core usa GPCGeometry per punti, vettori e piani e GPCUtilities per le conversioni di unità. Non dipende da Rhino, Grasshopper, SAP o Straus. Arachne.Grasshopper converte la geometria Rhino nel modello Core e visualizza gli esiti. I generatori di reti rimangono nel progetto Grasshopper perché utilizzano intersezioni, curve, mesh e Brep di Rhino.

```mermaid
flowchart TD
  GH[Arachne.Grasshopper · interfaccia e geometria Rhino] --> CORE[Arachne.Core · modello e controlli]
  GH --> RHINO[RhinoCommon e Grasshopper]
  CORE --> GPC[GPCGeometry e GPCUtilities]
  CORE --> A[Adattatore TensileFormFindingSolver]
  A --> TL[TensileLib · calcolo numerico]
  GH --> P[Patterning · algoritmi Rhino e sviluppo pannelli]
  P --> TL
  P --> IGL[Arachne.IGL · ARAP nativo x64]
```

## Dati

- `Cable`: estremi, obiettivo di ricerca di forma, vincoli opzionali, area, densità e forze concentrate.
- `NodeCondition`: punto, vincoli traslazionali, forza e pesi XYZ non lineari.
- `FormModel`: nodi unificati, cavi con indici di connessione e facce opzionali triangolari/quadrangolari.
- `SolverSettings`: tipo di calcolo, limiti e tolleranze, pressione, gravità e piano.
- `FormResult`: stato, messaggi, numero di chiamate al solutore, coordinate, forze, densità di forza e facce.

ModelBuilder registra immediatamente ogni nodo nuovo nella tabella spaziale. La fusione usa una tolleranza geometrica; le condizioni devono corrispondere agli estremi dei cavi, quindi un punto appoggiato nel mezzo di un cavo richiede prima la suddivisione del cavo. Cavi duplicati o collassati e facce degeneri vengono rifiutati. I valori in ingresso vengono copiati, senza modificare i cavi a monte.

## Calcolo

L'adattatore ricostruisce un modello TensileLib a ogni richiesta. Per il modo Linear iterative usa un ciclo esterno limitato e aggiorna le densità rispetto a lunghezza e forza prescritte. Gli esiti non riconosciuti, le eccezioni e la mancata convergenza non producono geometria di risultato. I messaggi della preparazione vengono mantenuti.

La cancellazione è verificata prima e dopo ogni chiamata a TensileLib: non interrompe una singola chiamata interna già avviata. La prima interfaccia Grasshopper esegue il calcolo in modo sincrono con un booleano Run; l'esecuzione asincrona con pulsante di annullamento è un'estensione futura.

## Confini della prima versione

Il progetto contiene l'interfaccia dei tre modi osservati in FeMM, non una reimplementazione matematica di TensileLib. Il plugin completo richiede TensileLib e ARAP; il Core isolato conserva un ramo diagnostico senza motore. I tre modi sono stati verificati con equilibri analitici reali. Pressioni, peso proprio, membrane e pesi combinati richiedono una campagna di confronto piu ampia.

Flattening, geodetiche, compensazione dei pannelli 2D e strumenti mesh sono ora inclusi in Components/Patterning. Il loro primo trasferimento mantiene gli algoritmi di FeMM e dipende direttamente da Rhino e, per lo sviluppo, da TensileLib. ARAPHelper traduce i dati per il C ABI della DLL nativa e adegua la scala dei pin alla versione 0.9.0. Il prossimo refactoring proposto e separare questi algoritmi dalle classi GH_Component senza cambiare GUID e porte. Le verifiche di resistenza e lo scambio con programmi FEM restano fuori dallo scopo.
