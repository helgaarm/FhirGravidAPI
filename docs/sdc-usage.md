# Avgrensning mot SDC og Questionnaire

Dette repositoriet implementerer ikke SDC-forhåndsutfylling, `Questionnaire`, `QuestionnaireResponse`, `$populate`, `StructureDefinition`-oppslag eller en utfyllingsmotor.

FHIR-fasaden kjenner ikke spørreskjema-ID-er, `linkId` eller `Questionnaire.item.definition`. API-et har ingen `definition`-søkeparameter og ingen mapping fra spørreskjema til DHG.

Den implementerte flaten består av FHIR-operasjonene for `Patient`, `Observation`, `Encounter` og `CareTeam`. Støttede søkeparametere står i [CapabilityStatement og README](../README.md#løsningen), og kjøreklare kall står i [FHIR-eksemplene](../examples/fhir-queries.md).

Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`. Dette betyr at den aktive DHG-posten ikke inneholder et treff; det betyr ikke `false`. En eksplisitt `valueBoolean=false` beholdes. Feil returneres som `OperationOutcome`.
