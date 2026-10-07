# Provenienza dei componenti

Sorgente di riferimento: `../FeMM 30-5-2024/Test1/FeMM` (non modificato).

I componenti di Arachne sono quelli di FeMM, con **gli stessi ingressi e le stesse uscite**: nomi, nickname, tipi e accesso dei parametri. Il banco di prova lo controlla automaticamente leggendo i sorgenti FeMM (`tools/Arachne.ExampleWriter/InventoryChecks.cs`). Le differenze rispetto a FeMM sono solo:

- le correzioni dei bug elencate più sotto;
- gli ingressi tolti di proposito (definizioni SAP e carichi non gestiti dal form finding, vedi Adattamenti), elencati anche nel banco di prova;
- lo snellimento: Table Material e Mesh Pipe tolti, Plate Property con un solo spessore e senza tipo, le opzioni del Form Find nel componente Solver Options, Build, Decompose e Check Model nel pannello 09-Model, il patterning dopo l'analisi (13-Patterning), le reti di cavi in 14-Tools;
- gli adattamenti alla TensileLib 2.3 (repository `../tensilelib`), al motore ARAP C# e all'assenza del plug-in Rhino di FeMM;
- sei componenti nuovi di Arachne, elencati a parte.

Si lavora in N e mm: Build Model ha come predefinito Nmm (come FeMM), le sezioni sono nelle unità del modello e la gravità di Solver Options, in N/mm, vale 9806.65 mm/s².

I GUID sono nuovi, così FeMM e Arachne possono essere caricati insieme. Le definizioni FeMM non vengono convertite automaticamente. Le icone sono quelle nuove di Arachne (`tools/Arachne.ExampleWriter/IconArtwork.cs`).

## Progetti

| FeMM | Arachne |
| --- | --- |
| FeMM.Common: modelli (Models), helper e geometria usati dal form finding | `src/Arachne.Common` |
| FeMM.Grasshopper: data type, attributi grafici, componenti | `src/Arachne.Grasshopper` |
| MaffeisIGL (ARAP nativo, libigl) | `src/Arachne.Arap`, motore C# riscritto |
| TensileLib 2.0.6 (NuGet, non più disponibile) | TensileLib 2.3.0.0 da `../tensilelib` |

Non sono portati: esportazione e analisi SAP/ETABS/Straus, verifiche, FemToRhino, plug-in FeMM.Rhino e risultati delle analisi FEM (resta solo il modello delle reazioni, `NodeReactResultModel`, usato dal form finding). Di conseguenza:

- il bake di nodi, aste, piastre e modelli produce geometria Rhino semplice (punti, linee, mesh) al posto degli oggetti del plug-in FeMM;
- la tolleranza di fusione dei nodi di Build Model, che FeMM leggeva dalle opzioni del plug-in Rhino, è nelle impostazioni Grasshopper di Arachne con lo stesso valore predefinito (0.001, unità del modello);
- combinazioni di carico, named sets, costruzioni per fasi, funzioni e superelementi sono stati tolti dal modello, insieme al parametro Load Combinations e al componente Section Geometry;
- dal caso di carico sono stati tolti i dati SAP sull'inerzia (gravità sulla massa non strutturale, accelerazioni, temperatura), dai materiali la tabella sforzi-deformazioni di Table Material.

## Componenti

| Pannello | Componente | Sorgente FeMM | GUID FeMM | GUID Arachne |
| --- | --- | --- | --- | --- |
| 01-Parameters | CoordinateSystem (CS) | `Components/Parameters/CoordinateSystemParam.cs` | 6a7ad660-29b0-4cbb-af29-7f5ce72f1cf3 | c1a59f62-2d20-4f66-aeb0-38999eb77b7a |
| 02-Materials | Aluminium (AM) | `Components/Materials/MaterialAluminiumComponent.cs` | ababcb15-84b5-499d-9803-871ee43d0bd7 | 9baa5979-36ea-4247-baff-7d9b4e8a832d |
| 02-Materials | Concrete (CM) | `Components/Materials/MaterialConcreteComponent.cs` | 9d2f4b12-e3f3-4cbd-af84-1b67fb3157b9 | 76802e52-c065-47ff-9c04-97094ab9edc8 |
| 02-Materials | Custom Material (CM) | `Components/Materials/MaterialCustomComponent.cs` | 262c4c26-81bf-4cfc-9ff9-28cb6db0c7bd | 7d402b00-2590-463f-950a-acf6d0e970f4 |
| 02-Materials | Steel (SM) | `Components/Materials/MaterialSteelComponent.cs` | f8fb5764-1e8a-420c-a530-f34727c05fe7 | d6938aa3-6c82-4648-8d33-f61505ddd262 |
| 03-Sections | Angle Section (A) | `Components/Sections/BeamSectionAngleComponent.cs` | c991750b-19ce-4ba8-b94d-ef31c2de20b7 | ea358ee1-c6ec-4849-9411-77fa20cc0b8c |
| 03-Sections | Brep Section (BS) | `Components/Sections/BeamSectionBrepComponent.cs` | 348fb5b1-671c-4fd5-b409-8c241761dbf1 | b105ee76-1e25-432e-a2b6-8b0ae1b7ed54 |
| 03-Sections | C Section (C) | `Components/Sections/BeamSectionCComponent.cs` | 19a82708-2ee3-4e75-aa88-436e47cbd1be | d915ff63-5959-4f99-ac4a-8c3d7fb20713 |
| 03-Sections | Generic Section (G) | `Components/Sections/BeamSectionGenericComponent.cs` | 95a189e5-a04c-487f-a704-a389d3232603 | 794e34ed-861f-4f67-8466-cf74fda4d459 |
| 03-Sections | Hollow Circle (HC) | `Components/Sections/BeamSectionHollowCircleComponent.cs` | b4e99212-ddb6-4e61-88df-071156cb83b6 | 540b17d6-3c75-457f-8e5b-6b76f349b363 |
| 03-Sections | Hollow Rect (HR) | `Components/Sections/BeamSectionHollowRectComponent.cs` | 80ed3751-0e95-4f75-8a66-b27c94d781f4 | c37923d7-be85-4846-92f3-79ae601f4f52 |
| 03-Sections | I Section (I) | `Components/Sections/BeamSectionIComponent.cs` | 625ad93f-4c24-4b5b-85e5-746b83c8e9bd | 79118195-ca0f-409f-ac01-55b72161131a |
| 03-Sections | Section Geometric Property (BGP) | `Components/Sections/SectionGeometricPropertyComponent.cs` | 13915ea9-7393-4a2e-9724-90c0df6530e2 | 1da0a1a0-214f-4a76-9f34-b50fadca708d |
| 03-Sections | Solid Circle (SC) | `Components/Sections/BeamSectionSolidCircleComponent.cs` | a5965076-70d7-4ce1-bbb6-966dadf0d9b6 | 3562882c-f91f-445b-be98-2494b937fd07 |
| 03-Sections | Solid Rect (SR) | `Components/Sections/BeamSectionSolidRectComponent.cs` | c68750fa-b296-4dab-ab51-8e9ff9ea2b27 | 31479893-1941-41e3-99d8-238efc1bcff6 |
| 03-Sections | T Section (T) | `Components/Sections/BeamSectionTComponent.cs` | 2a2a2c2f-c079-4ad1-b298-5ba8a53e0003 | 4573bce8-dc25-4b73-a0d5-cdce1f86846f |
| 04-Definitions | Group (G) | `Components/Definitions/GroupComponent.cs` | 7cf9eb7e-1292-4d7c-b1d7-44f0c29e65be | b9dfba03-6308-4ee0-ab3c-076b1fe0b0a8 |
| 04-Definitions | Load Case (LC) | `Components/Definitions/LoadCaseComponent.cs` | fc022fd8-4ebf-4c56-a428-3a9efeff43b3 | dd9360b8-20ab-4a62-aa15-bbad93f0f9b6 |
| 05-Properties | Beam Property (BP) | `Components/ElementProperties/BeamPropertyComponent.cs` | dfa97707-eae3-465a-84d6-90b240e1e972 | d44afeb3-cd24-42bf-a5d5-45a14cee69f3 |
| 05-Properties | Plate Property (PP) | `Components/ElementProperties/PlatePropertyComponent.cs` | 02de1de2-3151-4ff5-8059-0ce082fb5f44 | 77ce7fd4-99f4-4bc1-9d3f-5c6ce0955ab1 |
| 06-Model | Beam (BM) | `Components/Models/BeamComponent.cs` | a5b774a8-51ec-4c79-bfe7-e19558f444a9 | 03f091b7-2779-4f4b-ae65-9953fe26a366 |
| 09-Model | Build Model (BM) | `Components/Models/BuildModelComponent.cs` | c3c8837b-57fa-4fdc-8b99-849bfe468232 | 36226b88-8b00-470b-abf2-26f40b07f0ca |
| 09-Model | Decompose Model (DM) | `Components/Models/DecomposeModelComponent.cs` | 700a15f9-cf8d-4062-9c99-32628b99cda8 | c4146124-3da2-491d-96e4-7e0f565ece4b |
| 06-Model | Node (N) | `Components/Models/NodeComponent.cs` | 768f9c12-8a9f-4525-8d3a-f33a77793444 | 2d7a3c66-282f-4bbf-a11e-db55827e3224 |
| 06-Model | Plate (PL) | `Components/Models/PlateComponent.cs` | 2e9fcc4d-5f9b-403c-be31-40804205df1a | ad3fea4a-1c4b-4923-bb06-3e58ae1f3247 |
| 07-Attributes | Element Id (EI) | `Components/Attributes/ElementIdComponent.cs` | b21816ea-0ef6-4feb-a522-f9371f28faf6 | 92cb7203-eb55-4cc4-a322-8e4ccd2bed08 |
| 07-Attributes | Fixed Axial Force (FAF) | `Components/Attributes/FixedAxialForceComponent.cs` | a88a3d74-f64d-4062-8e6b-4d5d50657b5b | b0574cd0-d4c8-44b1-a85c-1658dd4fbfae |
| 07-Attributes | Fixed compensation (FC) | `Components/Attributes/FixedCompensationComponent.cs` | 7608ac1c-9e5a-4dbb-8806-dd6791a6aa35 | 2a20c8a1-9837-4b21-a0ec-70d22e92bb6e |
| 07-Attributes | Fixed curvature cable (FCC) | `Components/Attributes/ForceDensityCableComponent.cs` | 6ce47bc0-97a4-4f66-8358-a692c761e56e | 92d964b3-ad4d-4c30-a1c6-8e37ac0a7b85 |
| 07-Attributes | Fixed Length (FL) | `Components/Attributes/FixedLengthComponent.cs` | 5d89f883-1bbe-4f2e-81bc-62227bd42f59 | cb4a245d-aec7-441d-b7a2-0e98aae9e065 |
| 07-Attributes | Force Density (FD) | `Components/Attributes/ForceDensityComponent.cs` | 132f6d9d-8e99-4e36-bfed-ca3babd0e9c9 | 969824ee-693f-42be-977b-4f977b7af300 |
| 07-Attributes | Keep Position (KP) | `Components/Attributes/KeepPositionComponent.cs` | 86d016cf-0229-4569-ad7f-fbe4393fea08 | a5502518-f224-4d21-a275-705cbc41eda5 |
| 07-Attributes | Membrane type (M) | `Components/Attributes/ForceDensityMembraneComponent.cs` | b08e8e8e-493d-4f25-83ad-a7dddf1c412d | 6176f23d-8401-4c8d-ae54-ed469c846fcf |
| 07-Attributes | Restraints (NR) | `Components/Attributes/NodeRestraintsComponent.cs` | 79186ea8-b0db-4a86-8f95-2414b580759b | 1612ade6-396f-46f4-a57f-ddf28c164a3b |
| 08-Loads | Beam Distributed Load (BDL) | `Components/Loads/BeamDistributedLoadComponent.cs` | 3203a9e1-c15b-4761-a81d-682c8da941ce | 19f2292f-70a7-4fce-bc0d-2f8923829b69 |
| 08-Loads | Beam Point Load (BPL) | `Components/Loads/BeamPointLoadComponent.cs` | 08795ed9-9536-4f38-bc7f-960a3bf30266 | 7f58c892-a469-400c-bc46-e2c9c60ad280 |
| 08-Loads | Node Load (NL) | `Components/Loads/NodeLoadComponent.cs` | 554499f4-e448-46a5-a457-40e308be5c87 | 705f086f-9f47-46f9-92b0-9c20e9115891 |
| 13-Patterning | Borders (Bs) | `Components/Patterning/BordersComponent.cs` | 49141274-7cad-44b1-8c31-ba5d2c9d74dc | 810c4b07-ff37-5c08-b92e-a7a9a288ef41 |
| 14-Tools | Cable Net (CN) | `Components/Patterning/CableNetComponent.cs` | 53e1a59c-b28d-4619-9ab6-70d2bf72c379 | cb2b48b5-63b8-4af9-8f54-75ff5d506e5f |
| 13-Patterning | Compensation (C) | `Components/Patterning/CompensationComponent.cs` | c64f8be6-7169-46b2-a501-8a6fab04c724 | 51dce643-1c37-56ac-9eb1-9641e7bcdf1f |
| 14-Tools | Compound Net (CompNet) | `Components/Patterning/CompoundNetComponent.cs` | c5e1549c-7c80-4bb0-a067-117eb2d19240 | ca17f231-a85b-42dd-b48f-91c81e0916d7 |
| 13-Patterning | Decompensation (DC) | `Components/Patterning/DecompensationComponent.cs` | a9e861a6-31e7-46ea-b3de-f39ad896bd6a | 9bcc0da9-b5e3-5b52-9801-47c48ae166f1 |
| 13-Patterning | DecompensationDescriptor (DD) | `Components/Patterning/DecompensationDescriptorComponent.cs` | 4e5480bb-ac1e-441b-b0d8-c28aa5d92e50 | 88842b12-f723-5963-aa1f-5d80a18b50f5 |
| 13-Patterning | DecompensationDescriptorExt (DDE) | `Components/Patterning/DecompensationDescriptorExtComponent.cs` | d787c3ec-4659-4ba1-b2d5-a4d226f14bf8 | af556c97-1cca-549b-9094-36dd61c91595 |
| 13-Patterning | Deform ARAP (DA) | `Components/Patterning/DeformArapComponent.cs` | cba10d67-bfa1-4ceb-a61c-37c5e11e6fe7 | e0346aed-16ed-4ebb-8b59-a1cf04ddad67 |
| 13-Patterning | Flattening (F) | `Components/Patterning/FlatteningComponent.cs` | 29b336bf-5f02-44bc-bce9-90eefd10c433 | b8fa603b-1674-573c-9380-450724df4b24 |
| 13-Patterning | Flattening ARAP (FA) | `Components/Patterning/FlatteningArapComponent.cs` | b3f17427-3496-4000-b632-72b1f14ce789 | 6923d40f-6d82-4765-bc05-7c4381bdb996 |
| 13-Patterning | Geodesic Offsets (GO) | `Components/Patterning/GeodesicOffsetsComponent.cs` | c1e35ec9-caf9-4dcf-abc1-41b8dcd34a46 | c1e96689-970d-5d37-9fac-5f2fd2e5ebe3 |
| 13-Patterning | Line-Up (LU) | `Components/Patterning/LineUpComponent.cs` | 52e1aa90-a28d-4e3d-bac6-177c713f5a2f | dce9b365-6f46-5d47-a342-bc5a769d640d |
| 13-Patterning | Mesh Clean (MC) | `Components/Patterning/MeshCleanComponent.cs` | f77f438b-00d4-46dd-b428-bc3ec195cfd2 | ca2b11c4-55c9-5d47-8db1-af6ecf7efa07 |
| 13-Patterning | Mesh Divide (MD) | `Components/Patterning/MeshDivideComponent.cs` | 065060c5-cde9-489f-a9d4-348447b3be1f | 1af1264d-127a-5fc0-a8fc-93713840902e |
| 13-Patterning | Mesh Geodesic (MG) | `Components/Patterning/MeshGeodesicComponent.cs` | d9ad6d18-9b32-4958-9836-c980c22e381b | fe0db245-06c9-5b1c-92bd-6b7c8bff360d |
| 13-Patterning | Mesh Remap (MR) | `Components/Patterning/MeshRemapComponent.cs` | 4bc2da90-9230-4fb8-b044-7b70e826270c | fd929959-b16b-5f27-8026-cb7374c3922a |
| 13-Patterning | OrderBorders (OB) | `Components/Patterning/OrderBordersComponent.cs` | 335d8f38-48a3-485e-ae93-8b5f3d72992f | 085ff316-27ac-53fb-b09f-2f8039f82f68 |
| 14-Tools | Radial Cable Net (RCN) | `Components/Patterning/CableNetRadialComponent.cs` | 0f317607-a1e1-40ed-b988-c2157b8b3907 | f5b16b9e-f4e6-45c1-9430-7595f8b26cca |
| 13-Patterning | Remesh (R) | `Components/Tools/RemeshComponent.cs` | 4c4d90ee-ff74-4cf0-8fc7-b3a73e06512a | 7fd3c82c-a8ea-4d66-9147-c8b722b9b1bb |
| 13-Patterning | Split Mesh On Curves (SM) | `Components/Patterning/SplitMeshOnCurvesComponent.cs` | 6413cd87-824b-484f-89b4-115f928f96fd | f1efd4c3-1fac-5f78-b272-a0b71b9af9f6 |
| 13-Patterning | Subdivide Mesh (MC) | `Components/Patterning/SubdivideMeshComponent.cs` | 8dcc7742-7a42-40bb-a94f-27b77c085f24 | a9271e1c-81d2-58d2-843c-bee8a3e0aa8d |
| 13-Patterning | Unweld Mesh (UM) | `Components/Patterning/UnweldMeshComponent.cs` | 0c4b1071-f64a-4a93-981e-6b9de5b1b7ba | 23032209-c252-5a53-a96d-3a958c387f15 |
| 12-Analysis | Form Find (FF) | `Components/Analysis/FormFindComponent.cs` | 83be34af-f862-457b-8c03-109fadcd679e | f4c3d571-98d3-4bc9-926e-b3315a1444f3 |
| 14-Tools | Beam Filter (BF) | `Components/Tools/BeamFilterComponent.cs` | f19ab6c2-3624-48ad-9d49-a1e74dae489b | 8d3cb7f2-7de6-4594-a437-9756dc36b47c |
| 14-Tools | Curve2Mesh (C2M) | `Components/Tools/CurveToMeshComponent.cs` | 2bfbb9cf-09b0-4ce6-be33-31bdaafffe92 | 923d40fe-63a7-4d04-84f3-6b8f825cabb9 |
| 14-Tools | Node Filter (NF) | `Components/Tools/NodeFilterComponent.cs` | ee66aa2b-1b65-44ee-a638-248871d92b64 | f322b12d-ddb7-4287-aaa6-67813e6cc864 |
| 14-Tools | Plate Filter (PF) | `Components/Tools/PlateFilterComponent.cs` | 7bcdad25-17a2-4303-9ba6-3a5edc029a94 | 087ad97f-842c-41f7-826b-1a96656ffe77 |

I componenti Patterning della prima versione di Arachne mantengono i GUID già assegnati, tranne Flattening ARAP e Deform ARAP. Questi due hanno di nuovo le sole uscite FeMM e quindi un GUID nuovo.

## Componenti nuovi di Arachne

Non esistono in FeMM.

| Pannello | Componente | Cosa fa | GUID |
| --- | --- | --- | --- |
| 02-Materials | Membrane Material (MM) | Materiale di una membrana (tessuto spalmato o film) dai valori della scheda tecnica: peso in g/m², resistenze di ordito e trama in N/5cm, e facoltativi rigidezze E·t di ordito e trama e rigidezza a taglio G·t in kN/m, coefficiente di Poisson. Li salva in t, N e mm. Si collega a Plate Property. | 3653a652-deb4-4695-b52d-1fce5edfe629 |
| 12-Analysis | Solver Options (SO) | Le opzioni del Form Find, che in FeMM erano suoi ingressi, con gli stessi nomi: Gravity, Pressure, Pressure Load Case, Max Iter, Tolerance, Max Iter,s, ToleranceA,s, ToleranceB,s, Create mesh, Plate Property; in più Load Cases, i casi dei carichi da applicare. Predefiniti di FeMM, tranne le due tolleranze algebriche (1e-6, vedi Adattamenti). Senza questo componente il Form Find usa i predefiniti. | 4b18d9c1-d734-40c5-99d8-9d53333a76d5 |
| 09-Model | Check Model (CkM) | Controlla il modello prima del form finding: vincoli per ogni parte e direzione, nodi senza aste con forza, aste senza densità di forza, lunghezze nulle, aste doppie, valori non validi di lunghezza o forza fissa. Uscite: Valid, Errors, Warnings, Parts. | 6b9c4718-1b7d-485e-9293-31637d077e35 |
| 13-Patterning | Panel Distortion (PD) | Confronta un pannello 3D con il suo sviluppo (stessi vertici): allungamento di ogni lato in %, massimo e variazione d'area. | 44d42704-4c53-4dbc-a113-c2f9a3b6388d |
| 12-Analysis | Beam Forces (BF) | Linee, forze assiali (dal pre-carico salvato dal Form Find), densità di forza N/L, lunghezze e ID delle aste. | 5f9a26f9-c55b-496a-8153-459d8e9a56db |
| 12-Analysis | Support Reactions (SR) | Punti, reazioni dei vincoli (solo nelle direzioni vincolate), ID e risultante. Le reazioni sono calcolate dal Form Find. | 2df8465b-e430-4c4c-b077-6f64ee5150e9 |

## Bug corretti rispetto a FeMM

Form finding:

1. Linear step by step: il contatore delle iterazioni esterne non veniva mai incrementato. Senza convergenza il calcolo non terminava; ora si ferma al limite Max Iter con l'errore previsto da FeMM.
2. Uscite anticipate (limite di iterazioni dello step by step, tipo di solutore sconosciuto): il pulsante Run restava attivo e il componente continuava a ricalcolare.
3. Eccezione del solutore: veniva pubblicata la geometria iniziale come se fosse il risultato. Ora c'è un errore e nessuna uscita.
4. Aste senza Force Density: `First()` faceva fallire l'aggiornamento del pre-carico. Ora il pre-carico (forza assiale trovata) è salvato anche sulle aste governate solo da Fixed Axial Force o Fixed Length, nel loro caso di carico.
5. Divisioni per zero nello step by step: lunghezza fissa nulla, forza assiale fissa nulla, asta senza forza. Ora danno un errore chiaro.
6. Messaggi della preparazione del modello, prima ignorati: sono mostrati come Remark.
7. Più modelli in ingresso: tutte le iterazioni mostravano l'ultimo risultato. Ora ognuna mostra il proprio.
8. Somma parallela dei carichi nodali sullo stesso vettore: alcuni carichi andavano persi.
9. Nodi creati per estremi di aste o vertici di piastre non registrati: due elementi nello stesso punto ricevevano nodi distinti.
10. Avviso di forza assiale fissa duplicata calcolato sull'elenco delle lunghezze fisse.
11. Carichi distribuiti trapezi: la risultante era applicata a metà campata invece che nel baricentro.
12. Carichi "Projected": erano trattati come non proiettati. Le componenti in un sistema di riferimento diverso da Global erano lette come globali.
13. Schemi di carico distribuito non gestiti, momenti e assi locali: erano ignorati in silenzio. Ora i componenti di carico creano solo i carichi che il form finding gestisce (vedi Adattamenti).
14. Carichi sulle aste: la TensileLib li ripartiva sui nodi a ogni iterazione dividendo la distanza misurata sulla geometria iniziale per la lunghezza corrente; un'asta che si allunga spostava il carico (su un caso di prova il nodo riceveva quasi tre volte il carico). Corretto in TensileLib 2.2: il carico resta alla stessa frazione della lunghezza.
15. Pressione: la TensileLib la applicava secondo il verso di percorrenza delle piastre generate, che dipendeva dall'ordine delle aste; invertendo l'ordine la pressione cambiava verso. Corretto in TensileLib 2.2: le piastre di ogni parte sono orientate in modo che una pressione positiva agisca secondo la normale del piano Plane del Form Find (+Z con il piano predefinito), come la 2.0.5.3, che la spingeva sempre verso +Z.
16. Convergenza e precisione: tolleranze relative alle coordinate davano, lontano dall'origine (coordinate di cantiere in mm), iterazioni interrotte troppo presto ed errori crescenti con la distanza. Corretto in TensileLib 2.2: il modello è risolto centrato nell'origine e poi riportato al suo posto; i nodi vincolati tornano esattamente alle coordinate di ingresso.
17. Generazione delle piastre: se la prima asta non stava su una maglia chiusa (uno strallo, un'antenna, un cavo che collega due reti) la TensileLib non generava nessuna piastra, quindi né mesh né pressione, e generava solo la prima rete di un modello con reti separate. Corretto in TensileLib 2.2, insieme alle facce di 7-9 aste, al contorno delle reti piccole e ai tempi (10.000 nodi in meno di un secondo).

Modello:

18. Fixed Length, Fixed Axial Force, Fixed compensation e Keep Position creavano una copia dell'elemento con un GUID nuovo. Build Model, che unisce i rami paralleli tramite GUID, scartava una delle copie con i suoi attributi.
19. Build Model scartava in silenzio un nodo definito due volte nello stesso punto, insieme a vincoli e carichi. Ora li unisce.
20. Restraints: un valore booleano delle rotazioni veniva riletto come spostamento imposto, con errore.
21. Plate: errore o arresto con mesh di meno di tre vertici o senza bordo libero.
22. `IsValidWhyNot` di Beam e Plate restituiva i messaggi invertiti.
23. Serializzazione: il modello veniva scritto in JSON e riletto in binario, e `Read` di nodi, aste e costruzioni per fasi restituiva comunque errore. Erano inoltre difettosi:
    - `BeamPropertyModel` aggiungeva due volte le stesse chiavi;
    - `SectionModel` e `FunctionModel` non erano serializzabili;
    - `ForceDensityModel` scriveva `MembraneTypes` e leggeva `MembraneType`.

    I dati interni dei parametri ora si salvano e si rileggono.
24. Duplicate di un modello letto da file: generava un'eccezione per l'associazione dei GUID mancante.

Patterning e strumenti:

25. Compensation e Flattening modificavano la mesh in ingresso (la triangolavano).
26. Compensation: dopo un'eccezione del solutore veniva pubblicato il pannello iniziale.
27. Decompensation: interpolava sul parametro della polilinea invece che sulla lunghezza e modificava i descrittori condivisi.
28. Remesh: il bordo semplificato perdeva un angolo quando questo era il primo vertice della polilinea; un pannello rettangolare diventava un triangolo. Inoltre il componente non si poteva creare senza un documento Rhino aperto.
29. Mesh Remap, DecompensationDescriptorExt, Deform ARAP, Cable Net, Radial Cable Net e Compound Net: con dati incompatibili proseguivano e fallivano più avanti. Erano già stati corretti nella prima versione di Arachne; le correzioni sono mantenute.
30. Flattening: con Tolerance,s = 0, il valore predefinito di FeMM, il solutore ai minimi quadrati può fermarsi solo per errori di arrotondamento, che la TensileLib trattava come mancata convergenza: ogni pannello curvo finiva con "Solver not converged". Corretto in TensileLib 2.2, che accetta il punto migliore trovato; la tolleranza mantiene il significato di FeMM.
31. Node Filter e Plate Filter: l'ingresso Units non aveva un valore predefinito (Beam Filter sì, 2), quindi il filtro non faceva nulla finché Units non veniva collegato. Ora il predefinito è 2 (Nmm). L'ingresso resta per compatibilità: la tolleranza viene dalle impostazioni di Arachne.
32. Mesh Clean: saldava e ripuliva direttamente la mesh in ingresso, che Grasshopper condivide con i componenti a monte; dopo il calcolo anche la sorgente aveva i vertici coincidenti fusi (su un caso di prova da 8 a 7 vertici). Ora lavora su una copia.

Sezioni, materiali e proprietà:

33. Generic Section: l'anteprima delle aste (cerchio della stessa area) aveva il raggio moltiplicato per 0.001, quindi un cavo di 20 mm era disegnato spesso 0.02 mm. Ora il raggio è √(A/π) nelle unità del modello, come le dimensioni delle altre sezioni.
34. Plate Property: i dati interni salvati nel file perdevano materiale e spessori (venivano riletti solo Id e nome) e non si potevano rileggere, perché il tipo di dato non aveva il costruttore vuoto richiesto da Grasshopper. Ora si salva e si rilegge tutta la proprietà.
35. Custom Material con Kind = Rebar terminava con un'eccezione non gestita. Rebar, tipo SAP, non è più nell'elenco.
36. Materiale: la resistenza ultima a taglio leggeva e scriveva quella a trazione.

Comportamento invariato rispetto a FeMM: Form Find (Linear e Non-Linear), Flattening e Compensation pubblicano il risultato anche quando raggiungono il limite di iterazioni, con un avviso. Linear step by step, invece, termina con errore e senza risultato. Node, Beam e Plate Filter cercano per ID solo gli identificativi assegnati con Element Id, non la numerazione automatica.

## Adattamenti

- Ingressi tolti di proposito: Build Model non ha più Load Combinations, Named sets e Staged constructions; Beam Distributed Load non ha più P1 e P2. Beam Distributed Load accetta solo i carichi uniformi e trapezi; senza Coordinate System il carico è globale (in FeMM era nel sistema locale dell'asta, che il form finding non gestisce). Lo stesso vale per Beam Point Load.
- TensileLib 2.3: le due tolleranze algebriche di FeMM (TensileLib 2.0.6) ci sono di nuovo. ToleranceA,s: residuo relativo del gradiente coniugato (Linear) e residuo ai minimi quadrati |A^T r|/(|A| |r|) di LSQR (Non-Linear); ToleranceB,s: LSQR si ferma quando |r| <= B |b|. Predefinite a 1e-6, il valore predefinito di ALGLIB letto dalla DLL in uso (anche SciPy e MATLAB per LSQR: circa 6 cifre esatte). FeMM dava 0, che ALGLIB trasforma in 1e-6 solo se anche Max Iter,s è 0; altrimenti il solutore gira fino agli errori di arrotondamento. Non esiste più l'opzione CreateMesh del motore: senza piastre in ingresso, le piastre vengono generate dalle aste per ogni parte della rete.
- Peso proprio delle piastre (TensileLib 2.3), con Gravity diversa da 0: spessore × densità della Plate Property × gravità × area attuale, ricalcolato a ogni iterazione e verso -Z come per le aste. Per un Membrane Material il peso per area è quello della scheda tecnica (lo spessore si elide). La proprietà è quella delle piastre in ingresso o, per le piastre generate dalle aste, il Plate Property di Solver Options. Le reazioni lo comprendono.
- Casi di carico: il solutore prendeva tutti i carichi, qualunque fosse il loro caso. L'ingresso Load Cases di Solver Options sceglie i casi dei carichi nodali e sulle aste da applicare; vuoto: tutti, come prima. Gli attributi di form finding (densità di forza, lunghezze e forze fisse) non dipendono dai casi; il loro caso etichetta il pre-carico.
- Fixed compensation è una lunghezza fissa pari alla lunghezza dell'asta ridotta del C%. La Compensation la impone sul lato del pannello piano con gli stessi vertici (verificato: 10% su un lato di 2 dà 1.8); nel Form Find (Non-Linear e step by step) agisce come Fixed Length.
- Snellimento (0.3.0-rc.5), con GUID nuovi per i componenti con ingressi diversi da FeMM:
  - Load Case: tolto Gravity NSM (gravità sulla massa non strutturale, solo SAP);
  - Table Material: tolto (tabella sforzi-deformazioni per SAP); per le membrane c'è Membrane Material;
  - Plate Property: Name, Thickness e Material; tolti il tipo SAP (Shell-Thin, Membrane, Load Patch...) e il secondo spessore. Senza spessore l'anteprima della piastra è la superficie, come il Load Patch di FeMM;
  - Form Find: restano Model, Solver Type, Plane e Auto Run; le altre opzioni sono date da Solver Options. Se Options è collegato ma vuoto (per esempio una pressione senza caso di carico), il Form Find si ferma invece di usare i predefiniti;
  - Mesh Pipe: tolto;
  - Fixed curvature cable: nickname FCC, perché FC era anche quello di Fixed compensation;
  - pannelli: Node, Beam e Plate restano in 06-Model; Build, Decompose e Check Model sono in 09-Model, dopo attributi e carichi; il patterning è in 13-Patterning, dopo l'analisi; Cable Net, Radial Cable Net e Compound Net, che generano la rete, sono in 14-Tools.
- TensileLib 2.2 non modifica più le densità obiettivo delle aste (la densità trovata è `Beam.ForceDensity`): Linear step by step riparte a ogni passo dalle densità appena trovate, come prima.
- Non-Linear: i pesi di Keep Position e delle Force Density ora hanno effetto. Nella TensileLib precedente il calcolo ricadeva quasi sempre su un metodo che li ignorava (vedere il registro di TensileLib). I pesi di Fixed Length e Fixed Axial Force contano quando questi vincoli non si possono soddisfare tutti insieme.
- Il Form Find salva nel modello le reazioni dei vincoli (`NodeReactResultModel` di FeMM, caso "Form Finding"), calcolate dall'equilibrio dei nodi vincolati con le forze delle aste e i carichi applicati dal solutore (carichi nodali e delle aste, peso proprio di aste e piastre, pressione). Sono salvate solo se il calcolo converge e sono sostituite a ogni nuovo form finding. Le legge Support Reactions.
- Flattening ARAP e Deform ARAP usano il motore C# di `Arachne.Arap`. Quando non converge, il motore non restituisce la mesh (FeMM la pubblicava con un avviso); il componente mostra il motivo.
- Remesh compare nel pannello del patterning, come in FeMM, anche se il sorgente era nella cartella Tools.
