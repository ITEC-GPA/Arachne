# Arachne 0.4.0 — preparazione del rilascio F8

Windows x64, Rhino 8. Versione prodotto **0.4.0**, assembly/file **0.4.0.2**. Stato dei sorgenti al commit `a06438d`, con TensileLib `5e25765`. Il set GPC è il rilascio ufficiale AW; la distribuzione coordinata dei plug-in avviene a F8.

## Contenuto e differenze dalla rc.1

Modelli, elementi, proprietà e risultati usano i contratti GPC. TensileLib resta il motore di form finding e ARAP resta gestito in C#. Il passaggio dalla 0.4.0-rc.1 a questo candidato non cambia formule, porte o GUID: aggiorna il set comune e precisa il messaggio per gli archivi sperimentali precedenti; le altre correzioni riguardano gli strumenti di prova.

Il confronto F8A **contro la rc.1** dà 32 scenari identici bit per bit e sei esempi identici all'apertura, dopo il salvataggio e al ripristino. Il confronto storico della rc.1 contro la **0.3.0-rc.9** è diverso: 18 scenari identici, 13 diversi soltanto per il segno dello zero e uno per la correzione del peso proprio IPS. Quel cambio IPS era già nella rc.1, non è una nuova differenza introdotta dal set F8. Le evidenze storiche sono nella [nota rc.1](RELEASE-0.4.0-rc.1.md).

## Archivi ed esempi

Il formato corrente è **ARCGPC3 versione 2**. ARCGPC3 versione 1 e i formati precedenti non vengono convertiti: conservare l'originale e ricostruire il dato dai componenti. Il messaggio invita a ricostruire per una versione precedente e ad aggiornare Arachne per una versione futura. Nessun lettore BinaryFormatter o NRBF viene aggiunto.

Gli esempi comprendono i sidecar `.gpc-results` e hanno Auto Run disattivato. Copiare insieme definizione e risultati, conservando i nomi. Un sidecar assente o corrotto lascia indisponibile quel risultato; la definizione può aprirsi, ma salvare non ricrea il calcolo perduto. Al salvataggio il run indisponibile viene scartato con avviso. Salva con nome conserva l'originale e scrive i risultati nella nuova destinazione. Dopo modifiche agli ingressi è necessario un nuovo Run.

## Dipendenze e convivenza

Il ramo F8 usa sei DLL GPC coerenti con il manifest ufficiale AW: Utilities, Geometry, DelaunayMesh, Model, Persistence e Archive. TensileLib resta **2.5.0.1**; la corsia F8A riporta 59 controlli superati senza un nuovo commit. La ricompilazione verificata aveva lo stesso IL/metadati del riferimento, ma non lo stesso hash binario: non confondere equivalenza osservata e identità del file.

MathNet.Numerics **5.0.0 net48**, SHA-256 `4C9803A4B0673B93A99A2237E35543BE2F6741FBD54B0FF045CF92E877BE09D4`, è già la copia usata da Arachne nei collaudi F8A. P2CK ha allineato anche BridGHe e ReBear a quel file: non resta da scegliere quale variante distribuire nei tre pacchetti. La copia net461 nel bin di sviluppo autonomo TensileLib non è quella da trasferire nel pacchetto Arachne.

Un'altra estensione di Rhino può comunque fornire un MathNet già caricato. Il controllo al caricamento di Arachne verifica le dipendenze GPC e TensileLib, non ogni libreria di terzi. Per dopo F8 è prevista una riga informativa con il percorso e l'hash della MathNet effettivamente caricata, senza avvisi sui componenti; questa nota non la dichiara già implementata.

## Verifiche riportate dalle corsie F8A e F8Q

- ARAP gestito: 54 controlli; confronto ARAP nativo: 60 controlli.
- Host Rhino .NET Framework 4.8: 177 controlli, con confronto FeMM obbligatorio; host .NET 8.0.34: gli stessi 177, con BinaryFormatter disabilitato.
- Prove della build: rifiuti dei casi negativi previsti, compresi candidato senza flag e DLL GPC alterata.
- Scala: percorso completo su 1.024 e 10.000 nodi; i valori misurati non sono un limite di capacità garantito.
- Confronti con la rc.1: 32 scenari e sei esempi identici, anche per il segno dello zero.
- Caricamento delle DLL BridGHe prima di Arachne: nessun avviso, copie GPC identiche. Il test negativo con vecchie DLL rc.1 segnala anche i file con stessa versione ma contenuto diverso.

I numeri sopra sono quelli di F8A; F8Q ne riporta la ripetizione senza flag sul set AW. La prova F8A non ha caricato la `.gha` BridGHe dopo Arachne, né certificato una sessione interattiva con tutti e tre i plug-in finali. Non attribuire al candidato un collaudo nei due ordini che quel rapporto non ha eseguito. Le prove indicate sono quelle della corsia, non nuove esecuzioni della redazione.

## Completamento e installazione

Il set ufficiale **AW** è già presente: Model **5.51.0.1**, Persistence **3.15.0.3** e Archive **1.2.0.4**; Utilities, Geometry e DelaunayMesh restano identici. Il manifest identifica Model `c2abf222` e Checker `9c71ca65`, pubblicati su origin/master. La corsia F8Q riporta le prove senza flag candidato e i confronti invariati sul set AW. Restano l'integrazione nei repository di rilascio, la build dei bin definitivi con la relativa provenienza, la distribuzione e il collaudo interattivo dei plug-in insieme. `BUILD.json` del pacchetto deve riportare `Candidate: false` e il commit effettivamente distribuito; nessuna data di pubblicazione viene anticipata.

Chiudere Rhino, conservare la vecchia installazione separata e copiare l'intero pacchetto Arachne da `X:\ITEC-MODELLI\5-RHINO\SGBIM-GR_PLUGIN\Arachne`, con esempi, sidecar, dipendenze e manifest. Tenere un solo percorso Arachne attivo e una sola `.gha` nel pacchetto. Usare BridGHe e ReBear dello stesso rilascio; non sostituire singole DLL con copie prese da bin di sviluppo.

Se manca un risultato esterno, recuperare la coppia originale o ricalcolare; se compare un avviso di librerie, ripristinare il pacchetto completo e riavviare. Per una segnalazione indicare versione, commit, unità, componente, messaggio e nome del sidecar.
