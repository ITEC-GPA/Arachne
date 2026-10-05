# Provenienza e correzioni

Sorgente di riferimento: `../FeMM 30-5-2024/Test1/FeMM`.

Il progetto FeMM originario è stato lasciato invariato. Le correzioni descritte qui appartengono al codice di Arachne.

## Parti trasferite

| Origine FeMM | Destinazione Arachne |
|---|---|
| FeMM.Common/Helpers/CableNetHelper.cs | Arachne.Grasshopper/Geometry/CableNetHelper.cs, con controlli aggiuntivi |
| Components/Patterning/CableNetComponent.cs | Components/Networks/CableNetComponent.cs |
| Components/Patterning/CableNetRadialComponent.cs | Components/Networks/CableNetRadialComponent.cs |
| Components/Patterning/CompoundNetComponent.cs | Components/Networks/CompoundNetComponent.cs |
| Components/Analysis/FormFindComponent.cs e Helpers/FormFindingHelper.cs | Riorganizzati in Core/ModelBuilder, Core/IterationControl e Core/TensileFormFindingSolver |
| Attributi di force density, fixed length, fixed axial force, keep position e fixed compensation | Sostituiti dai tipi Core e dai componenti Cable, Cable Constraints e Node Conditions |

I componenti Arachne hanno GUID nuovi per coesistere con FeMM. Le vecchie definizioni non vengono convertite automaticamente. Il secondo trasferimento comprende gli altri 19 componenti Patterning e 16 icone originali; vedere [mappa completa](PATTERNING-PORT.md).

## Difetti corretti nel trasferimento

1. **Contatore esterno mai incrementato:** sostituito con un ciclo che esegue al massimo il numero di chiamate richiesto, accettando anche la convergenza nell'ultima iterazione.
2. **Somma concorrente dei carichi sullo stesso vettore:** accumulazione deterministica nel costruttore del modello.
3. **Nuovi estremi non registrati nella mappa dei nodi:** registrazione immediata con ricerca per tolleranza, anche tra celle spaziali adiacenti.
4. **Accesso con First su attributi eventualmente assenti:** il modello dedicato usa proprietà esplicite e vincoli opzionali; non dipende da ricerche tra attributi FEM generici.
5. **Continuazione dopo errore o mancata convergenza:** esiti separati e nessun risultato geometrico pubblicato in tali casi.
6. **Divisioni per zero negli aggiornamenti:** verifica di obiettivi, denominatori e risultati finiti. Sono accettati solo obiettivi positivi in questo primo nucleo per reti tese.
7. **Avvisi della preparazione ignorati:** restituiti in Log e nei messaggi Grasshopper.
8. **Controllo null errato nelle intersezioni:** `interss != null || interss.Count > 0` diventa `&&`.
9. **Passi della rete controllati solo quando entrambi nulli:** ogni passo deve essere positivo e finito; suddivisioni radiali almeno tre.
10. **Accesso ai rami tramite indice anziché percorso:** Compound Net recupera i rami mediante `GH_Path`.
11. **Prosecuzione dopo eccezione nei generatori:** il componente termina senza pubblicare risultati parziali.
12. **Matching dei bordi sui dati condivisi:** copie delle curve prima del matching nei due generatori principali.
13. **Curve Grasshopper prive di ReferenceID:** Compound Net non scarta più tutte le curve successive alla prima quando gli ID sono vuoti. Aggiunti controlli su perimetri, rami, tipo di rete, piano e patch.

## Verifiche

La suite console verifica topologia, fusione dei nodi, somma dei carichi, copia dei dati, input invalidi, limite delle iterazioni, cancellazione, divisioni per zero, conversione tramite GPCUtilities e comportamento in assenza del motore. Il programma termina con codice diverso da zero se una verifica fallisce.

```powershell
./tests/Arachne.Core.Tests/bin/Release/net48/Arachne.Core.Tests.exe
```

I 17 controlli includono tre equilibri analitici con la DLL TensileLib reale. La suite non copre tutte le tipologie strutturali. I generatori geometrici derivati da FeMM richiedono ulteriori prove su perimetri e superfici rappresentativi.

Il generatore in `tools/Arachne.ExampleWriter` avvia Rhino senza finestra con un documento attivo temporaneo. Verifica reti rettangolari e radiali, tutti i 19 componenti Patterning recuperati e i difetti riprodotti. Risolve l'esempio a sella (25 nodi, 40 cavi, 16 facce) e salva lo schema con Run=False. Genera inoltre lo schema Patterning, verifica area compensata e quattro bordi decompensati, riaprendo e ricalcolando sia GH sia GHX. Gli esiti aggiornati sono in [RESOCONTO.md](RESOCONTO.md).
