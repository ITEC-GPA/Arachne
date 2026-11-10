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
migrazione 0.4.0 senza integrarla nella 0.3.0. Dopo conferma esplicita dell'utente,
il default branch GitHub è stato impostato a `master` e verificato dal remoto.

## Percorsi di compilazione (step 4)

Normalizzazione condivisa di `ArachneBin` e protezione Debug anche in MSBuild.
Build Release in `artifacts/output with spaces` eseguita: 54 controlli ARAP
superati. Verificato il rifiuto Debug per `bin`, sottocartelle e percorsi con `..`.
Le prove complete Debug e Rhino sono comprese nella verifica finale.

L'utente ha indicato il nuovo riferimento FeMM:
`../_OLD/FeMM 30-5-2024/Test1/FeMM`, usato esclusivamente in lettura.

## Tracciamento delle verifiche (step 5)

Eseguiti 54 controlli ARAP e 153 controlli in Rhino/.NET Framework: inventario 66,
form finding 38, patterning 43, reti 6. Nessun fallimento o controllo saltato;
confronto FeMM rigoroso completato. I report includono runtime e conteggi per suite.
Provata anche l'assenza dei sorgenti: errore con `--require-femm`; senza l'opzione,
89 verifiche superate e 64 confronti saltati, esplicitamente registrati.
La regressione ripetibile è `tests/Build.Tests.ps1`.

## Runtime e archivi (step 6)

Suite ampliata a 161 controlli Rhino: inventario 66, form finding 40, patterning
43, reti 6, esempi GH/GHX 6. Tutti superati sia su .NET Framework 4.8.10008.0
sia sull'host Windows Desktop .NET 8.0.33, con confronto FeMM rigoroso.
La controprova .NET 8 con BinaryFormatter disabilitato riproduce 5 fallimenti
nei percorsi di serializzazione: limite documentato, non corretto abilitando
globalmente il formato. I report sono negli output diagnostici.

Esaminato `ContractArchive` della migrazione conservata: ARCGPC3 rifiuta i vecchi
payload della 0.3.0. La sostituzione richiede quindi una migrazione separata,
descritta in [RUNTIME-AND-ARCHIVES](RUNTIME-AND-ARCHIVES.md).

## Cartelle dei componenti (step 7)

Spostati Cable Net, Radial Cable Net e Compound Net in `Components/Tools`,
Remesh in `Components/Patterning`. I quattro file sono identici byte per byte:
namespace, GUID, ingressi, uscite e algoritmi restano invariati.
