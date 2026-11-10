# Manutenzione dopo la stabilizzazione GPC

## Base verificata il 10 novembre 2026

- Checkout pulito su `master`, commit `39c54a2`, versione `0.3.0-rc.8`.
- Fetch e interrogazione del remoto eseguiti: `Arachne/master` coincide con il checkout.
- Il default branch remoto è ancora `claude/femm-original-components` (`d0d7abb`).
- La migrazione `0.4.0-f6.4`, commit `bf2d6bc`, è conservata nei branch locali
  `claude/f8-arachne` e `codex/gpc-unification-20261102`, 31 commit avanti.
  Si mantiene separata: questa manutenzione non converte gli archivi della 0.3.0.
- DLL Release disponibili: GPCGeometry `2.3.0.0`, GPCUtilities `3.0.0.0`,
  TensileLib `2.5.0.1`. Il riordino GPC non richiede di sostituirle con build Debug.
- I sorgenti FeMM non sono più nel percorso predefinito. Un confronto saltato
  deve risultare nei metadati e non può essere dichiarato superato.

## Sequenza dei commit

1. Registrazione della base e dell'ambito (questo documento).
2. Conservazione della migrazione su branch remoto dedicato e allineamento del
   default branch, se consentito dall'accesso al repository.
3. Documentazione ARAP e dipendenze allineata al codice della 0.3.0.
4. Percorsi di output coerenti negli script e isolamento delle build Debug.
5. Tracciamento delle verifiche e modalità rigorosa del confronto FeMM.
6. Verifica del runtime e degli archivi; strategia di migrazione documentata.
7. Cartelle dei componenti allineate ai pannelli, senza cambiare identità o porte.
8. Test finali, nuova release candidata, changelog e push.

Ogni step che modifica il repository ha un commit dedicato. I risultati dei test
saranno riportati solo dopo l'esecuzione. Le cartelle di copie e backup escluse
dal lavoro non vengono modificate.

## Conservazione della migrazione (step 2)

Pubblicato `codex/gpc-migration-preserved` sul remoto Arachne, al commit
`bf2d6bc27e0485214f8ce4de656d7be1f4b1e6b0`. Il branch mantiene l'intera storia della
migrazione 0.4.0 senza integrarla nella 0.3.0. Il cambio del default branch richiede
una conferma specifica ed è separato dal push dei sorgenti.
