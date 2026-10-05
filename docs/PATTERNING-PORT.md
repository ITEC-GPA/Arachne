# Componenti patterning trasferiti

Le tre reti Cable Net, Radial Cable Net e Compound Net erano gia presenti. I seguenti 19 componenti completano tutti i 22 componenti della cartella Patterning di FeMM. I GUID nuovi permettono la coesistenza con FeMM.

I 19 componenti hanno prove funzionali in `tools/Arachne.ExampleWriter/PatterningChecks.cs`, con 31 controlli complessivi inclusi i casi negativi. La mappa riguarda i componenti Patterning; la ricerca di forma di Analysis e i relativi Attributes sono stati accorpati nei componenti del modello Arachne.

| Componente/attributo precedente | Equivalente Arachne |
| --- | --- |
| FormFind + FormFindingHelper | Model, Solver Settings, Form Find |
| ForceDensity, ForceDensityCable, ForceDensityMembrane | Cable: Kind, Target, Project, Direction e Target weight |
| FixedLength, FixedAxialForce, FixedCompensation | Cable Constraints: Length, Force, Compensation, Weight |
| KeepPosition e vincoli del nodo FEM | Node Conditions: vincoli XYZ e pesi per mantenere la posizione |
| Carichi sui cavi/nodi del modello FEM | Cable Load e Node Conditions, con modello dedicato senza dipendenza FEM |

Compensation accetta i Cable Constraints di Arachne come lunghezze prescritte ai bordi. Decompensation lavora su linee e polilinee; altre curve richiedono una conversione esplicita. Le scale dei descrittori restano frazioni `1 - ingresso`, mentre Compensation usa percentuali intere. Non e una conversione automatica inversa fra i due strumenti.

| Componente | GUID FeMM | GUID Arachne |
| --- | --- | --- |
| BordersComponent | 49141274-7CAD-44B1-8C31-BA5D2C9D74DC | 810c4b07-ff37-5c08-b92e-a7a9a288ef41 |
| CompensationComponent | c64f8be6-7169-46b2-a501-8a6fab04c724 | 51dce643-1c37-56ac-9eb1-9641e7bcdf1f |
| DecompensationComponent | a9e861a6-31e7-46ea-b3de-f39ad896bd6a | 9bcc0da9-b5e3-5b52-9801-47c48ae166f1 |
| DecompensationDescriptorComponent | 4e5480bb-ac1e-441b-b0d8-c28aa5d92e50 | 88842b12-f723-5963-aa1f-5d80a18b50f5 |
| DecompensationDescriptorExtComponent | d787c3ec-4659-4ba1-b2d5-a4d226f14bf8 | af556c97-1cca-549b-9094-36dd61c91595 |
| DeformArapComponent | cba10d67-bfa1-4ceb-a61c-37c5e11e6fe7 | e56e8131-f146-50bd-b8a8-e083e2c93558 |
| FlatteningArapComponent | b3f17427-3496-4000-b632-72b1f14ce789 | d0b5cbbf-f1c9-5042-9b67-8eeedebb1a69 |
| FlatteningComponent | 29b336bf-5f02-44bc-bce9-90eefd10c433 | b8fa603b-1674-573c-9380-450724df4b24 |
| GeodesicOffsetsComponent | c1e35ec9-caf9-4dcf-abc1-41b8dcd34a46 | c1e96689-970d-5d37-9fac-5f2fd2e5ebe3 |
| LineUpComponent | 52e1aa90-a28d-4e3d-bac6-177c713f5a2f | dce9b365-6f46-5d47-a342-bc5a769d640d |
| MeshCleanComponent | f77f438b-00d4-46dd-b428-bc3ec195cfd2 | ca2b11c4-55c9-5d47-8db1-af6ecf7efa07 |
| MeshDivideComponent | 065060c5-cde9-489f-a9d4-348447b3be1f | 1af1264d-127a-5fc0-a8fc-93713840902e |
| MeshGeodesicComponent | d9ad6d18-9b32-4958-9836-c980c22e381b | fe0db245-06c9-5b1c-92bd-6b7c8bff360d |
| MeshPipeComponent | b6b9a33b-b036-4e3d-97f6-898c3ced844f | dd2927ec-f681-56e7-ab75-f4916b2528d3 |
| MeshRemapComponent | 4bc2da90-9230-4fb8-b044-7b70e826270c | fd929959-b16b-5f27-8026-cb7374c3922a |
| OrderBordersComponent | 335d8f38-48a3-485e-ae93-8b5f3d72992f | 085ff316-27ac-53fb-b09f-2f8039f82f68 |
| SplitMeshOnCurvesComponent | 6413cd87-824b-484f-89b4-115f928f96fd | f1efd4c3-1fac-5f78-b272-a0b71b9af9f6 |
| SubdivideMeshComponent | 8dcc7742-7a42-40bb-a94f-27b77c085f24 | a9271e1c-81d2-58d2-843c-bee8a3e0aa8d |
| UnweldMeshComponent | 0c4b1071-f64a-4a93-981e-6b9de5b1b7ba | 23032209-c252-5a53-a96d-3a958c387f15 |
