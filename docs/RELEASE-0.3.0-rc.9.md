# Verifica Arachne 0.3.0-rc.9

Data: 10 novembre 2026. Piattaforma: Windows x64, Rhino 8 installato sul PC di
sviluppo. Versione assembly/file: `0.3.0.9`. Il pacchetto è la cartella
`bin/Arachne.Grasshopper/Release/net48`; `bin` contiene una sola `.gha`.

## Risultati

| Verifica eseguita | Esito |
| --- | --- |
| Release, ARAP gestito | 54 superati, 0 falliti |
| Release, Rhino/.NET Framework 4.8.10008.0 | 161 superati, 0 falliti, 0 saltati |
| Debug isolato, ARAP gestito | 54 superati, 0 falliti |
| Debug isolato, Rhino/.NET Framework | 161 superati, 0 falliti, 0 saltati |
| Release, host Windows Desktop .NET 8.0.33 | 161 superati, 0 falliti, 0 saltati |
| Soluzione ARAP autonoma, con confronto nativo | 60 superati, 0 falliti (54 gestiti + 6 confronti) |
| Generazione isolata degli esempi | 3 definizioni, ciascuna GH e GHX, riaperte e ricalcolate |
| Protezioni Debug | rifiutato `bin` anche da MSBuild; accettato output isolato con spazi |
| Report FeMM e manifest | assenza/incompletezza obbligatoria rifiutata; assenza facoltativa registrata; vecchio manifest invalidato |
| Collegamenti della documentazione aggiornata e diff | nessun collegamento locale rotto o errore di spaziatura |

I 161 controlli Rhino comprendono inventario 66, form finding 40, patterning 43,
reti 6 e archivi degli esempi 6. Il confronto FeMM rigoroso legge i sorgenti da
`../_OLD/FeMM 30-5-2024/Test1/FeMM`; la cartella non è stata modificata.

La prima esecuzione Release ha incontrato `NotLicensedException` durante
l'inizializzazione della licenza Rhino. È stato aggiunto il controllo preliminare
limitato a 30 secondi e la suite completa è stata rieseguita con successo.
Quel primo esito non è incluso fra le verifiche superate.

## Limiti verificati

L'host .NET 8 Windows Desktop abilita la serializzazione legacy. Disabilitando
esplicitamente BinaryFormatter nel processo diagnostico, la controprova produce
5 fallimenti nei percorsi di serializzazione. Il limite della 0.3.0 resta
documentato in [RUNTIME-AND-ARCHIVES](RUNTIME-AND-ARCHIVES.md): non viene aggirato
con impostazioni globali e non si dichiara supporto a runtime non provati.
La migrazione 0.4.0 è conservata separatamente, non integrata in questa release.

## Evidenze e riproduzione

- `BUILD.json` accanto al plug-in: commit della build e risultati delle suite.
- `DEPENDENCIES.json`: versioni, dimensioni e SHA-256 dei binari distribuiti.
- `RHINO-CHECKS.json`, `RHINO-CHECKS.log`, `ARAP-CHECKS.log`: esiti dettagliati.
- `bin/Arachne.RuntimeChecks/Release/net8.0-windows/CHECKS-desktop.json`: .NET 8.
- `artifacts/maintenance/`: log delle prove Debug, native, generazione esempi,
  casi negativi e controllo del runtime. Output locali ignorati da Git.

Comandi principali:

```powershell
.\scripts\build.ps1 -RhinoTests -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
.\scripts\check-runtime.ps1 -RequireFemm -FemmSourcePath '..\_OLD\FeMM 30-5-2024\Test1\FeMM'
.\tests\Build.Tests.ps1
.\scripts\build-arap.ps1 -ArachneBin 'artifacts/standalone arap' -CompareNative
```

I confronti nativi richiedono la DLL di riferimento locale. I test numerici
coprono i casi descritti dalle suite e non equivalgono a una qualifica generale
di qualsiasi modello o pannello di produzione.
