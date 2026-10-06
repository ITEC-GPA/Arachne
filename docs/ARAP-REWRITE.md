# Riscrittura autonoma ARAP in C#

Richiesta del 6 ottobre 2026: nuova implementazione del motore ARAP in C#, indipendente da libigl/Eigen a runtime, con geometria GPC, da collegare ad Arachne dopo le verifiche.

## Stato prima degli interventi

Baseline Arachne: 6a6a054 (release candidata con 100 test). Albero di lavoro pulito. Conservato il pacchetto precedente Arachne-0.1.0-rc.1-win-x64.zip.

I sorgenti originali sono disponibili in un repository locale: mm_arap.cpp/.h e gli header libigl/Eigen. Il confronto usa anche la DLL nativa x64 ricompilata e verificata nella release precedente; non viene presentata una decompilazione come sostituto dei sorgenti gia disponibili.

API native: Version, SetConfig, MeshFlattening, MeshUnwrap, MeshDeform. La configurazione e globale. I vertici vengono scalati usando gli estremi delle singole coordinate assolute; i pin richiedono la correzione gia aggiunta nel wrapper gestito. L'inizializzazione UV e la proiezione XY, anche per pannelli verticali. Flattening esegue ARAP e successivamente un adattamento dei lati del perimetro.

## Piano esecutivo

1. Progetto Arachne.Arap compilabile separatamente, senza Rhino/Grasshopper/TensileLib ne DLL C++.
2. Modello triangolare e pin con validazione, opzioni per singola chiamata, esiti espliciti, storia e cancellazione.
3. Solver ARAP local/global 2D per elementi e 3D per spokes-and-rims; algebra gestita, vincoli esatti, controllo della convergenza.
4. Flattening con inizializzazione indipendente dall'orientamento del pannello, rilevamento dei bordi e adattamento del perimetro con esito verificato dopo l'ultimo passo.
5. Test analitici, trasformazioni rigide, scale/traslazioni, mesh invalide, limiti di iterazione, concorrenza e confronto numerico con il motore nativo.
6. Collegamento dei componenti e della geodetica al nuovo motore; conservazione di GUID e porte; pacchetto gestito senza dipendenza ARAP nativa.

## Fonti tecniche

- Sorkine e Alexa, As-Rigid-As-Possible Surface Modeling (2007): https://igl.ethz.ch/projects/ARAP/
- Documentazione libigl sulle energie ARAP e la parametrizzazione: https://libigl.github.io/tutorial/
- Implementazione libigl presente localmente: usata per verificare la variante energetica e il comportamento, mantenendone la provenienza; il nuovo codice e una implementazione C# indipendente.

Le configurazioni non sviluppabili ammettono distorsione residua. Convergenza dell'energia e tolleranza del perimetro sono controlli distinti; non e prevista equivalenza bit per bit fra solutori diversi. Eventuali facce invertite o degeneri devono essere segnalate, non pubblicate come sviluppo valido.
