# DHG API to facade attribute mapping

Status: implementation-aligned as of 2026-09-02.

> Language: This low-level attribute catalog is maintained in English to match DHG JSON names and FHIR element names. The shorter operational and mapping documentation is maintained in Norwegian.

This document is the exhaustive attribute catalog for the read-only facade. It traces the
two DHG responses used at runtime through the facade's normalized population model and into
FHIR R4. The shorter [clinical mapping matrix](mapping.md) remains the terminology-oriented
view, while [DHG to FHIR resource mapping](dhg-fhir-resource-mapping.md) describes the
resource shapes and query surface.

The source contract was checked against the official NHN documentation for
[Status](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/statusmd),
[Resources](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd), and
[Metadata](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/metadatamd).
The implemented contract is defined by `DhgModels.cs`, the DHG-to-population transformation
by `DhgPopulationSnapshotFactory.cs`, and the population-to-FHIR transformation by
`FhirPopulationMapper.cs` and `PopulationCodes.cs`.

## Scope and interpretation

- DHG is the only runtime clinical data source.
- The FHIR layer receives normalized population objects and does not know DHG JSON paths.
- `DIRECT` means the explicit DHG fact is represented without changing its meaning.
- `PARTIAL` means only a semantically safe part is represented or the value is conditional.
- `CONTROL` means the attribute controls source selection, authorization, filtering, or
  consistency and is not itself returned as a FHIR data element.
- `UNSUPPORTED` means the DTO accepts the documented attribute for contract tolerance but
  the current facade does not expose it.
- `CONTAINER` means the attribute has no independent FHIR value; its child attributes are
  mapped separately.
- `null` remains unknown/not registered and normally produces no FHIR element or resource.
  A nullable clinical boolean is never collapsed to `false`.
- Text that is mapped is trimmed but not parsed. No diagnosis, medication, relationship,
  procedure, or other clinical fact is inferred from free text.
- A mapped resource with `metadata.enteredInError=true` is omitted in full.

## End-to-end flow and source gating

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG operation / JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR result</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GET /<wbr>status</code></td><td><code>DhgStatusResponse</code></td><td>None directly</td><td><code>CONTROL</code>: always called before the active record.</td></tr>
    <tr><td><code>status.<wbr>hasGivenConsent</code></td><td>Consent gate</td><td><code>OperationOutcome</code> on failure</td><td><code>CONTROL</code>: must be exactly <code>true</code>; <code>false</code> or <code>null</code> stops processing with HTTP 403.</td></tr>
    <tr><td><code>status.<wbr>deceased</code></td><td>Availability gate</td><td><code>OperationOutcome</code> on failure</td><td><code>CONTROL</code>: exactly <code>true</code> stops processing with HTTP 403; <code>false</code> and <code>null</code> do not themselves stop it.</td></tr>
    <tr><td><code>status.<wbr>hasActiveMaternityRecord</code></td><td>Active-record gate and <code>PopulationSnapshot.<wbr>HasActiveMaternityRecord</code></td><td>None directly</td><td><code>CONTROL</code>: must be exactly <code>true</code>; otherwise processing stops with HTTP 404.</td></tr>
    <tr><td><code>status.<wbr>latestRecordId</code></td><td>Record selector</td><td>DHG record URL only</td><td><code>CONTROL</code>: must be a nonblank UUID; selects <code>GET /<wbr>record/<wbr>{latestRecordId}</code> and must match <code>record.<wbr>metadata.<wbr>recordId</code>. It is never published as a patient identifier.</td></tr>
    <tr><td><code>status.<wbr>lastChangedDateTime</code></td><td><code>PopulationSnapshot.<wbr>SourceLastChanged</code></td><td>None in the current FHIR surface</td><td><code>CONTROL</code>: retained internally; falls back to <code>record.<wbr>metadata.<wbr>recordLastUpdated</code> when absent.</td></tr>
    <tr><td><code>GET /<wbr>record/<wbr>{latestRecordId}</code></td><td><code>DhgMaternityRecord</code></td><td><code>Patient</code>, <code>Observation</code>, <code>Encounter</code>, <code>CareTeam</code>, and search <code>Bundle</code> resources</td><td><code>CONTROL</code>: the selected current record is the sole clinical source.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordId</code></td><td>Consistency check and pregnancy context for <code>FetalPatientId</code></td><td>Indirect input to derived fetus <code>Patient.<wbr>id</code></td><td><code>CONTROL/<wbr>PARTIAL</code>: must match <code>status.<wbr>latestRecordId</code> and parse as UUID. It is combined with maternal logical ID and positive <code>fosterId</code>, SHA-256 hashed, and never exposed raw.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordStatus.<wbr>status</code></td><td>Active-record gate</td><td><code>OperationOutcome</code> on failure</td><td><code>CONTROL</code>: must equal <code>ACTIVE</code> case-insensitively; otherwise processing stops with HTTP 404.</td></tr>
  </tbody>
</table>

## Common structures

### Resource metadata

`<resource>` below means a mapped instance of `mother`, `currentPregnancy`,
`previousPregnancies`, `geneticDisorders`, `medicalConditions`, `medication`,
`lifestyleFactors`, `clinicalTests`, `rhesusDNegative`,
`vitalMeasurementsBeforePregnancy`, `symphysisFundalHeights[]`,
`antenatalAppointments[]`, `pointsOfContact`, or `birthStatus`.

<table>
  <thead>
    <tr>
      <th width="25%" scope="col">DHG JSON path</th>
      <th width="25%" scope="col">Facade handoff</th>
      <th width="25%" scope="col">FHIR output</th>
      <th width="25%" scope="col">Status and exact rule</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata</code></td>
      <td><code>DhgResourceMetadata</code></td>
      <td>None independently</td>
      <td><code>CONTAINER</code>: child attributes below apply.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>id</code></td>
      <td>Input to normalized resource ID</td>
      <td><code>Resource.id</code> for generated Observations, Encounters, and CareTeams</td>
      <td><code>PARTIAL</code>: combined with a stable field suffix, invalid FHIR ID characters become <code>-</code>, and values longer than 64 characters become a lowercase SHA-256 hex digest. Missing IDs use the internal <code>dhg-&lt;suffix&gt;</code> fallback.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>version</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>: the facade is read-only and does not expose DHG optimistic-concurrency versions.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdated</code></td>
      <td><code>Population*.<wbr>LastUpdated</code></td>
      <td><code>Resource.<wbr>meta.<wbr>lastUpdated</code></td>
      <td><code>DIRECT</code>: copied to every FHIR resource derived from that DHG resource. For a fetus observed more than once, the newest appointment timestamp wins.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>enteredInError</code></td>
      <td>Active-resource filter</td>
      <td>Entire derived FHIR resource set is absent</td>
      <td><code>CONTROL</code>: only explicit <code>true</code> filters the DHG resource; <code>false</code> and <code>null</code> do not.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>: provenance identity is not published by the current facade.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>userType</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>orgNr</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>; it is not substituted for a point-of-contact organization identifier.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>orgName</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>treatmentFacilityName</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>hprNr</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>; it is not substituted for a point-of-contact HPR number.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>hprRole</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>.</td>
    </tr>
    <tr>
      <td><code>&lt;resource&gt;.<wbr>metadata.<wbr>lastUpdatedBy.<wbr>name</code></td>
      <td>DTO only</td>
      <td>None</td>
      <td><code>UNSUPPORTED</code>.</td>
    </tr>
  </tbody>
</table>

### Record metadata

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>record.<wbr>metadata.<wbr>version</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: no DHG write/version surface is exposed.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordLastUpdated</code></td><td>Patient/source timestamp fallback</td><td>Maternal <code>Patient.<wbr>meta.<wbr>lastUpdated</code> when <code>mother.<wbr>metadata.<wbr>lastUpdated</code> is absent</td><td><code>PARTIAL</code>: also becomes <code>PopulationSnapshot.<wbr>SourceLastChanged</code> only when <code>status.<wbr>lastChangedDateTime</code> is absent.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>lastUpdated</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: the facade deliberately uses <code>recordLastUpdated</code> for record-wide fallback semantics.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>lastUpdatedBy</code> and all child fields</td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: record updater identity is not exposed.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordStatus.<wbr>deliveryDate</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: the facade population is the current active pregnancy only.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordStatus.<wbr>liveBirth</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>.</td></tr>
    <tr><td><code>record.<wbr>metadata.<wbr>recordStatus.<wbr>terminationDate</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>.</td></tr>
  </tbody>
</table>

### `CodeAndSystem`

These child rules apply only where a resource-specific row below accepts the expected code
system. A structurally valid code from the wrong system is not mapped.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON attribute</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>code</code></td><td><code>CodedValue.<wbr>Code</code></td><td><code>Coding.<wbr>code</code></td><td><code>DIRECT</code>: required by the mapper; a missing code drops the coded value.</td></tr>
    <tr><td><code>display</code></td><td><code>CodedValue.<wbr>Display</code></td><td><code>Coding.<wbr>display</code> and/or <code>CodeableConcept.<wbr>text</code></td><td><code>DIRECT</code>: optional source display is retained; no display is invented for source-defined lifestyle/language values.</td></tr>
    <tr><td><code>codeSystem</code></td><td>Normalized <code>CodedValue.<wbr>System</code></td><td><code>Coding.<wbr>system</code></td><td><code>PARTIAL</code>: known <code>VOLVEN_*</code> names used by the facade become their OID URNs; absolute URIs and numeric OIDs can be normalized, but each mapped field still enforces its expected system. Unknown strings are dropped.</td></tr>
    <tr><td>Any unmapped JSON member captured as <code>AdditionalProperties</code></td><td>DTO extension data</td><td>None</td><td><code>UNSUPPORTED</code>: forward-compatible deserialization does not imply clinical exposure.</td></tr>
  </tbody>
</table>

## Root resource coverage

<table>
  <thead>
    <tr><th width="33.33%" scope="col">DHG record attribute</th><th width="33.33%" scope="col">Facade result</th><th width="33.33%" scope="col">Status</th></tr>
  </thead>
  <tbody>
    <tr><td><code>mother</code></td><td>Maternal <code>PopulationPatient</code> plus social-history Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>currentPregnancy</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>previousPregnancies</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>geneticDisorders</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>medicalConditions</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>medication</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>lifestyleFactors</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>clinicalTests</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>rhesusDNegative</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>vitalMeasurementsBeforePregnancy</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>symphysisFundalHeights[]</code></td><td>Observations</td><td>Mapped per table below.</td></tr>
    <tr><td><code>antenatalAppointments[]</code></td><td>Encounters, Observations, and optional minimal fetus Patients</td><td>Mapped per table below.</td></tr>
    <tr><td><code>pointsOfContact</code></td><td>CareTeam with contained resources</td><td>Mapped per table below.</td></tr>
    <tr><td><code>birthStatus</code></td><td>Observations and optional minimal fetus Patients</td><td>Mapped per table below.</td></tr>
  </tbody>
</table>

## Mother

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>mother.<wbr>name</code></td><td><code>PopulationPatient.<wbr>Name</code></td><td><code>Patient.<wbr>name.<wbr>text</code></td><td><code>DIRECT</code>: surrounding whitespace is removed; the source is not split into given and family names.</td></tr>
    <tr><td><code>mother.<wbr>address</code></td><td><code>PopulationPatient.<wbr>Address.<wbr>Line</code></td><td><code>Patient.<wbr>address.<wbr>line</code></td><td><code>DIRECT</code>: emitted with the other available home-address fields.</td></tr>
    <tr><td><code>mother.<wbr>postNumber</code></td><td><code>PopulationPatient.<wbr>Address.<wbr>PostalCode</code></td><td><code>Patient.<wbr>address.<wbr>postalCode</code></td><td><code>DIRECT</code>: retained as text, including leading zeroes.</td></tr>
    <tr><td><code>mother.<wbr>postName</code></td><td><code>PopulationPatient.<wbr>Address.<wbr>City</code></td><td><code>Patient.<wbr>address.<wbr>city</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>mother.<wbr>employedLast6Months</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td>Social-history <code>Observation.<wbr>valueBoolean</code>; text-only code <code>Yrkesaktiv siste 6 måneder</code></td><td><code>DIRECT</code>: explicit <code>false</code> is retained.</td></tr>
    <tr><td><code>mother.<wbr>employmentPercentage</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Social-history <code>Observation.<wbr>valueQuantity</code>, UCUM <code>%</code></td><td><code>DIRECT</code>: source-contract values from 0 through 100 are retained, including zero.</td></tr>
    <tr><td><code>mother.<wbr>occupationAndIndustry</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td>Social-history <code>Observation.<wbr>valueString</code>; text-only code <code>Yrke og bransje</code></td><td><code>PARTIAL</code>: trimmed source text is retained without separating or coding occupation and industry.</td></tr>
    <tr><td><code>mother.<wbr>language.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td><td><code>PopulationPatient.<wbr>PreferredLanguage</code></td><td><code>Patient.<wbr>communication.<wbr>language</code>; <code>communication.<wbr>preferred=true</code></td><td><code>DIRECT</code>: emitted only for Volven 3303.</td></tr>
    <tr><td><code>mother.<wbr>countryOfBirth.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td><td><code>PopulationPatient.<wbr>CountryOfBirth</code></td><td>HL7 <code>patient-birthPlace</code> extension: code in <code>valueAddress.<wbr>country</code>, display in <code>valueAddress.<wbr>text</code></td><td><code>PARTIAL</code>: emitted only for Volven 9043; the FHIR R4 extension has no separate code-system element.</td></tr>
    <tr><td><code>mother.<wbr>needsLanguageInterpreter</code></td><td><code>PopulationPatient.<wbr>NeedsInterpreter</code></td><td>HL7 <code>patient-interpreterRequired</code> extension with <code>valueBoolean</code></td><td><code>DIRECT</code>: explicit <code>false</code> is retained; <code>null</code> omits the extension.</td></tr>
    <tr><td><code>mother.<wbr>cohabitingCoparent</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td>Social-history <code>Observation.<wbr>valueBoolean</code>; text-only code <code>Bor sammen med medforelder</code></td><td><code>DIRECT</code>: no relationship, parental responsibility, or household membership is inferred.</td></tr>
    <tr><td><code>mother.<wbr>cohabitingCoparentNote</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td>Social-history <code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: trimmed source text is retained without semantic parsing.</td></tr>
  </tbody>
</table>

The maternal `Patient.id` is not a DHG attribute. In protected GET flows it comes from the
short-lived patient context; in authenticated POST search it is a stable HMAC pseudonym.
Neither variant exposes the national identity number. The name and address values above come
directly from the active DHG mother resource.

## Current pregnancy

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>currentPregnancy.<wbr>dateLastPeriod</code></td><td><code>PopulationObservation(<wbr>DateValue)</code></td><td><code>Observation.<wbr>valueDateTime</code> (day precision), LOINC <code>8665-2</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>dueDate</code></td><td><code>PopulationObservation(<wbr>DateValue)</code></td><td><code>Observation.<wbr>valueDateTime</code>, SNOMED CT <code>289206005</code> plus LOINC <code>11778-8</code></td><td><code>DIRECT</code>: explicitly the estimate based on last period.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>dueDateBasedOnUltrasound</code></td><td><code>PopulationObservation(<wbr>DateValue)</code></td><td><code>Observation.<wbr>valueDateTime</code>, SNOMED CT <code>738070007</code> plus LOINC <code>11778-8</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>dueDateCorrectedDate</code></td><td><code>PopulationObservation(<wbr>DateValue)</code></td><td><code>Observation.<wbr>valueDateTime</code>; text-only code <code>Korrigert termindato</code></td><td><code>PARTIAL</code>: retained as a separate source fact; no clinical precedence or correction reason is inferred.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>hasPrenatalDiagnosticsTests</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only code <code>Gitt informasjon om fosterdiagnostikk</code></td><td><code>DIRECT</code>: represents whether information was provided, not whether a test occurred or its result.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>numberOfFetuses</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, SNOMED CT <code>246435002</code></td><td><code>PARTIAL</code>: only positive values are emitted.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>assistedConception</code></td><td><code>DhgAssistedConception</code></td><td>None independently</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>assistedConception.<wbr>hadAssistedConception</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>813541000000100</code></td><td><code>DIRECT</code>: explicit <code>false</code> is retained.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>assistedConception.<wbr>dateAssistedConception</code></td><td><code>PopulationObservation.<wbr>EffectiveDate</code></td><td>Assisted-conception <code>Observation.<wbr>effectiveDateTime</code> with day precision</td><td><code>PARTIAL</code>: used only when <code>hadAssistedConception=true</code>; it never creates an Observation or status by itself.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>birthPreparationTalk</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>702396006</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>currentPregnancy.<wbr>breastfeedingGuidance</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>243094003</code></td><td><code>DIRECT</code>.</td></tr>
  </tbody>
</table>

## Previous pregnancies

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>previousPregnancies.<wbr>numberOfPreviousPregnancies</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, SNOMED CT <code>246211005</code></td><td><code>DIRECT</code>: non-null source count; the facade does not calculate it from other outcomes.</td></tr>
    <tr><td><code>previousPregnancies.<wbr>numberOfPreviousLiveBirths</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, LOINC <code>11636-8</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>previousPregnancies.<wbr>spontaneousMiscarriages</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, SNOMED CT <code>248989003</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>previousPregnancies.<wbr>stillBirths22weeks</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, SNOMED CT <code>252112002</code></td><td><code>PARTIAL</code>: the DHG 22-week/500-g threshold remains a source-contract limitation.</td></tr>
    <tr><td><code>previousPregnancies.<wbr>numberOfEctopicPregnancies</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td><code>Observation.<wbr>valueInteger</code>, SNOMED CT <code>440537001</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>previousPregnancies.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no pregnancy outcome, diagnosis, or procedure is extracted.</td></tr>
  </tbody>
</table>

There is no explicit induced-abortion attribute. The facade never derives one as a residual
from the counters above.

## Genetic disorders

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>geneticDisorders.<wbr>noneKnown</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only code <code>Ingen kjente arvelige sykdommer</code></td><td><code>DIRECT</code>: <code>false</code> does not establish a disorder.</td></tr>
    <tr><td><code>geneticDisorders.<wbr>parentsAreRelatives</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>842009</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>geneticDisorders.<wbr>hipDysplasia</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only family-history code</td><td><code>PARTIAL</code>: affected relative and clinical diagnosis are unknown.</td></tr>
    <tr><td><code>geneticDisorders.<wbr>other</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only code <code>Annen arvelig sykdom</code></td><td><code>PARTIAL</code>: no disorder type is inferred.</td></tr>
    <tr><td><code>geneticDisorders.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no disorder, person, or relationship is extracted.</td></tr>
  </tbody>
</table>

## Medical conditions

All boolean rows retain explicit `false` and omit `null`.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>medicalConditions.<wbr>nothingParticular</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only code <code>Ingenting spesielt</code></td><td><code>DIRECT</code>: <code>false</code> does not identify a disease.</td></tr>
    <tr><td><code>medicalConditions.<wbr>heartDisease</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>56265001</code></td><td><code>DIRECT</code>: no subtype is inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>highBloodPressure</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>38341003</code></td><td><code>DIRECT</code>: no subtype is inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>kidneyUrinaryTractDiseases</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; exact text-only composite code</td><td><code>PARTIAL</code>: not split into kidney and urinary-tract conditions.</td></tr>
    <tr><td><code>medicalConditions.<wbr>diabetes</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>73211009</code></td><td><code>PARTIAL</code>: DHG does not distinguish pre-existing from gestational diabetes.</td></tr>
    <tr><td><code>medicalConditions.<wbr>allergiesAsthma</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; exact text-only composite code</td><td><code>PARTIAL</code>: not split into allergy and asthma.</td></tr>
    <tr><td><code>medicalConditions.<wbr>epilepsy</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>84757009</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>medicalConditions.<wbr>thrombosis</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>439127006</code></td><td><code>PARTIAL</code>: DHG combines thrombosis and/or treatment; treatment is not inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>autoimmuneDisease</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>85828009</code></td><td><code>DIRECT</code>: no subtype is inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>gynecologicalConditions</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; exact text-only composite code</td><td><code>PARTIAL</code>: disease, intervention, and surgery are not split.</td></tr>
    <tr><td><code>medicalConditions.<wbr>mentalHealth</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>74732009</code></td><td><code>PARTIAL</code>: no specific diagnosis is inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>other</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>; text-only code</td><td><code>PARTIAL</code>: no condition is inferred.</td></tr>
    <tr><td><code>medicalConditions.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no diagnosis, medication, procedure, or affected person is extracted.</td></tr>
  </tbody>
</table>

## Medication

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>medication.<wbr>medicationFrequency</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code <code>Hyppighet av legemiddelbruk</code></td><td><code>PARTIAL</code>: raw enum/string is retained without normalizing frequency or inferring a medication.</td></tr>
    <tr><td><code>medication.<wbr>drugAllergy</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>416098002</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>medication.<wbr>folate</code></td><td><code>DhgFolate</code></td><td>None independently</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>medication.<wbr>folate.<wbr>takenBefore</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>792807003</code>; note <code>Før svangerskapet</code></td><td><code>PARTIAL</code>: time context is an annotation; it is not inferred from <code>takenDuring</code>.</td></tr>
    <tr><td><code>medication.<wbr>folate.<wbr>takenDuring</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td><code>Observation.<wbr>valueBoolean</code>, SNOMED CT <code>792807003</code>; note <code>Under svangerskapet</code></td><td><code>PARTIAL</code>: time context is an annotation; it is not inferred from <code>takenBefore</code>.</td></tr>
    <tr><td><code>medication.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no medication, dose, indication, or instruction is extracted.</td></tr>
  </tbody>
</table>

## Lifestyle factors

One valid frequency creates one social-history Observation. The first-consultation and
week-36 objects can therefore create two Observations for one stimulus.

<table>
  <thead>
    <tr>
      <th width="25%" scope="col">DHG JSON path</th>
      <th width="25%" scope="col">Facade handoff</th>
      <th width="25%" scope="col">FHIR output</th>
      <th width="25%" scope="col">Status and exact rule</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><code>lifestyleFactors.<wbr>stimuli[]</code></td>
      <td>Iterated <code>DhgStimulus</code></td>
      <td>Zero to two Observations per entry</td>
      <td><code>CONTAINER</code>: invalid/missing stimulus coding drops the entry.</td>
    </tr>
    <tr>
      <td><code>lifestyleFactors.<wbr>stimuli[].<wbr>stimuliType.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td>
      <td>Dynamic <code>PopulationCode</code></td>
      <td><code>Observation.<wbr>code</code></td>
      <td><code>DIRECT</code>: only Volven 8536 is accepted.</td>
    </tr>
    <tr>
      <td><code>lifestyleFactors.<wbr>stimuli[].<wbr>stimuliFrequencyFirstConsultation</code></td>
      <td><code>DhgStimuliFrequency</code></td>
      <td>One Observation when its frequency is valid</td>
      <td><code>CONTAINER</code>: annotation identifies <code>Ved første konsultasjon</code>.</td>
    </tr>
    <tr>
      <td><code>...stimuliFrequencyFirstConsultation.<wbr>stimuliFrequency.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td>
      <td><code>CodedValue</code></td>
      <td><code>Observation.<wbr>valueCodeableConcept</code></td>
      <td><code>DIRECT</code>: only Volven 8537 is accepted.</td>
    </tr>
    <tr>
      <td><code>...stimuliFrequencyFirstConsultation.<wbr>dailyCount</code></td>
      <td><code>PopulationComponent(<wbr>IntegerValue)</code></td>
      <td><code>Observation.<wbr>component.<wbr>valueInteger</code>; text-only component code <code>Daglig antall</code></td>
      <td><code>PARTIAL</code>: only non-negative values are retained; no unit or clinical interpretation is invented.</td>
    </tr>
    <tr>
      <td><code>lifestyleFactors.<wbr>stimuli[].<wbr>stimuliFrequencyAtWeek36</code></td>
      <td><code>DhgStimuliFrequency</code></td>
      <td>One Observation when its frequency is valid</td>
      <td><code>CONTAINER</code>: annotation identifies <code>Ved uke 36</code>.</td>
    </tr>
    <tr>
      <td><code>...stimuliFrequencyAtWeek36.<wbr>stimuliFrequency.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td>
      <td><code>CodedValue</code></td>
      <td><code>Observation.<wbr>valueCodeableConcept</code></td>
      <td><code>DIRECT</code>: only Volven 8537 is accepted.</td>
    </tr>
    <tr>
      <td><code>...stimuliFrequencyAtWeek36.<wbr>dailyCount</code></td>
      <td><code>PopulationComponent(<wbr>IntegerValue)</code></td>
      <td><code>Observation.<wbr>component.<wbr>valueInteger</code></td>
      <td><code>PARTIAL</code>: only non-negative values are retained; no unit is invented.</td>
    </tr>
    <tr>
      <td><code>lifestyleFactors.<wbr>note</code></td>
      <td><code>PopulationObservation.<wbr>Note</code></td>
      <td><code>Observation.<wbr>note</code> on each emitted lifestyle Observation</td>
      <td><code>PARTIAL</code>: appended to the consultation/week context; no standalone Observation is created and the text is not parsed.</td>
    </tr>
  </tbody>
</table>

## Clinical tests

For every laboratory boolean below, `true` becomes Volven 8340 `T002 |Positiv|`, `false`
becomes `T008 |Negativ|`, and `null` creates no Observation. Numeric quantities are emitted
only when positive.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>clinicalTests.<wbr>hemoglobin</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>NLK <code>NOR05172</code>, <code>valueQuantity</code> UCUM <code>g/<wbr>dL</code></td><td><code>DIRECT</code>: first-trimester source fact.</td></tr>
    <tr><td><code>clinicalTests.<wbr>hemoglobinAt3rdTrimester</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>NLK <code>NOR05172</code>, <code>valueQuantity</code> UCUM <code>g/<wbr>dL</code>; third-trimester note</td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>ferritin</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>NLK <code>NPU19763</code>, <code>valueQuantity</code> UCUM <code>ug/<wbr>L</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>hbv</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>SNOMED CT <code>165806002</code>, coded positive/negative result</td><td><code>DIRECT</code>: source explicitly identifies HBV surface antigen.</td></tr>
    <tr><td><code>clinicalTests.<wbr>hbvCore</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>: no unverified analyte coding is invented.</td></tr>
    <tr><td><code>clinicalTests.<wbr>hiv</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>syphilis</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>aboRh</code></td><td><code>DhgAboRh</code></td><td>None independently</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>aboRh.<wbr>aboType</code></td><td><code>CodedValue</code></td><td>NLK <code>NPU58582</code> plus LOINC <code>883-9</code>; SNOMED CT coded blood group value</td><td><code>DIRECT</code>: only <code>A</code>, <code>B</code>, <code>AB</code>, and letter <code>O</code> are accepted.</td></tr>
    <tr><td><code>clinicalTests.<wbr>aboRh.<wbr>rhesusDType</code></td><td><code>CodedValue</code></td><td>NLK <code>NPU21917</code> plus LOINC <code>10331-7</code>; SNOMED CT RhD value</td><td><code>DIRECT</code>: <code>NEGATIVE</code>, documented <code>POSTIVE</code>, and corrected <code>POSITIVE</code> are accepted case-insensitively.</td></tr>
    <tr><td><code>clinicalTests.<wbr>bloodAntibodies</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>: antibody identity is not inferred.</td></tr>
    <tr><td><code>clinicalTests.<wbr>chlamydia</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>toxoplasmosis</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>: DHG can cover more than one analyte.</td></tr>
    <tr><td><code>clinicalTests.<wbr>rubellaAntigen</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>NLK <code>NPU12412</code> P-Rubellavirus IgG, coded positive/negative result</td><td><code>DIRECT</code>: mapping follows the documented meaning rather than the misleading JSON name.</td></tr>
    <tr><td><code>clinicalTests.<wbr>hepatitisC</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>mrsaVreEsbl</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only composite code, coded positive/negative result</td><td><code>PARTIAL</code>: organism/resistance mechanism is not inferred.</td></tr>
    <tr><td><code>clinicalTests.<wbr>bHbA1c</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>NLK <code>NPU27300</code>, <code>valueQuantity</code> UCUM <code>mmol/<wbr>mol</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>glucoseTolerance</code></td><td><code>DhgGlucoseTolerance</code></td><td>None independently</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>glucoseTolerance.<wbr>fastingGlucoseLevel</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>SNOMED CT <code>271062006</code>, <code>valueQuantity</code> UCUM <code>mmol/<wbr>L</code></td><td><code>DIRECT</code>: only positive values; test date becomes <code>effectiveDateTime</code> when present.</td></tr>
    <tr><td><code>clinicalTests.<wbr>glucoseTolerance.<wbr>post2hGlucoseLevel</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>SNOMED CT <code>49167009</code>, <code>valueQuantity</code> UCUM <code>mmol/<wbr>L</code></td><td><code>DIRECT</code>: only positive values; test date becomes <code>effectiveDateTime</code> when present.</td></tr>
    <tr><td><code>clinicalTests.<wbr>glucoseTolerance.<wbr>testDate</code></td><td><code>PopulationObservation.<wbr>EffectiveDate</code></td><td><code>Observation.<wbr>effectiveDateTime</code> on mapped fasting and two-hour results</td><td><code>PARTIAL</code>: does not create a resource without a mapped glucose result.</td></tr>
    <tr><td><code>clinicalTests.<wbr>gonorrhea</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>cytomegaloVirus</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>asymptomaticBacteriuria</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>groupBStreptococci</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Text-only analyte code, coded positive/negative result</td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>clinicalTests.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td><code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no analyte, result, diagnosis, or assessment is extracted.</td></tr>
  </tbody>
</table>

## Rhesus D negative

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>rhesusDNegative.<wbr>consentFetalRhesusTyping</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: consent is not converted into a clinical Observation; a FHIR Consent surface needs an explicit architecture and policy decision.</td></tr>
    <tr><td><code>rhesusDNegative.<wbr>fetusRhDPositiveAtWeek24</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Laboratory Observation with text-only aggregate code and Volven 8340 positive/negative value</td><td><code>PARTIAL</code>: <code>true</code> means at least one fetus is RhD-positive; <code>false</code> means all tested fetuses are RhD-negative. It is not assigned to one fetus.</td></tr>
    <tr><td><code>rhesusDNegative.<wbr>prophylaxisAtWeek28</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td>Therapy Observation, SNOMED CT <code>408783007</code>, <code>valueBoolean</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>rhesusDNegative.<wbr>dateForResult</code></td><td><code>PopulationComponent(<wbr>DateValue)</code></td><td>Text-only <code>Observation.<wbr>component.<wbr>valueDateTime</code> with day precision</td><td><code>PARTIAL</code>: included only on an emitted aggregate fetus-RhD result; not treated as specimen, effective, or issued time.</td></tr>
    <tr><td><code>rhesusDNegative.<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td>Laboratory <code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no result, diagnosis, treatment, or assessment is extracted.</td></tr>
  </tbody>
</table>

## Vital measurements before pregnancy

All three values are emitted only when positive. DHG provides no measurement timestamp, so
the facade does not construct `effective[x]` and does not claim a specialized Vital Signs
profile.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>vitalMeasurementsBeforePregnancy.<wbr>height</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>50373000</code> plus LOINC <code>8302-2</code>; UCUM <code>cm</code></td><td><code>PARTIAL</code>: source context is retained in <code>Observation.<wbr>note</code>.</td></tr>
    <tr><td><code>vitalMeasurementsBeforePregnancy.<wbr>prePregnancyWeight</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>27113001</code> plus LOINC <code>29463-7</code>; UCUM <code>kg</code></td><td><code>PARTIAL</code>.</td></tr>
    <tr><td><code>vitalMeasurementsBeforePregnancy.<wbr>bMI</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>60621009</code> plus LOINC <code>39156-5</code>; UCUM <code>kg/<wbr>m2</code></td><td><code>PARTIAL</code>.</td></tr>
  </tbody>
</table>

## Symphysis-fundal heights

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>symphysisFundalHeights[].<wbr>pregnancyWeek</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: currently not represented or used to derive the measurement date.</td></tr>
    <tr><td><code>symphysisFundalHeights[].<wbr>measurement</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>364253002</code>; UCUM <code>cm</code></td><td><code>DIRECT</code>: only positive measurements are emitted.</td></tr>
    <tr><td><code>symphysisFundalHeights[].<wbr>measurementDate</code></td><td><code>PopulationObservation.<wbr>EffectiveDate</code></td><td><code>Observation.<wbr>effectiveDateTime</code> with day precision</td><td><code>PARTIAL</code>: emitted only with a valid positive measurement.</td></tr>
  </tbody>
</table>

## Antenatal appointments and fetus findings

Every appointment not marked `enteredInError=true` creates one `PopulationEncounter`, even
if all clinical attributes are absent. Appointments are sorted by `appointmentDate`; missing
dates sort first. Every appointment-derived Observation references that Encounter.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>antenatalAppointments[].<wbr>appointmentDate</code></td><td><code>PopulationEncounter.<wbr>Date</code> and <code>EffectiveDate</code></td><td><code>Encounter.<wbr>period.<wbr>start/<wbr>end</code> set to the same day; appointment Observations use <code>effectiveDateTime</code></td><td><code>DIRECT</code>: when absent, Encounter remains with no period and Observations have no <code>effective[x]</code>.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>pregnancyWeek</code></td><td>Input to gestational-age <code>QuantityValue</code></td><td>LOINC <code>18185-9</code>, UCUM <code>d</code></td><td><code>PARTIAL</code>: must be positive and is combined with a valid day offset as <code>week * 7 + day</code>.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>daysAfterFullPregnancyWeek</code></td><td>Input to gestational-age <code>QuantityValue</code></td><td>Same Observation as pregnancy week; original <code>week+day</code> is retained in <code>Observation.<wbr>note</code></td><td><code>PARTIAL</code>: must be <code>0.<wbr>.<wbr>6</code>; <code>null</code> is treated as zero only when pregnancy week is valid.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>motherWeight</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>27113001</code> plus LOINC <code>29463-7</code>; UCUM <code>kg</code></td><td><code>DIRECT</code>: only positive values.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>bloodPressure</code></td><td>Parsed into two <code>PopulationComponent</code> values</td><td>LOINC <code>85354-9</code> panel; systolic/diastolic SNOMED CT plus LOINC components; UCUM <code>mm[Hg]</code></td><td><code>PARTIAL</code>: only whitespace-tolerant <code>NN/<wbr>NN</code> or <code>NNN/<wbr>NNN</code> with positive components is emitted; no inference from other text.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>proteinInUrineTestResult</code></td><td><code>CodedValue</code></td><td>NLK <code>NPU04206</code>, <code>Observation.<wbr>valueCodeableConcept</code></td><td><code>DIRECT</code>: exact mappings are <code>Neg-&gt;T008</code>, <code>Spor-&gt;T052</code>, <code>1+-&gt;T048</code>, <code>2+-&gt;T049</code>, <code>3+-&gt;T050</code> in Volven 8340; other values are omitted.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>edema</code></td><td><code>PopulationObservation(<wbr>IntegerValue)</code></td><td>Exam Observation with text-only code and raw <code>valueInteger</code></td><td><code>PARTIAL</code>: only <code>0.<wbr>.<wbr>3</code>; scale-step meaning is not inferred.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>fetusesVitalSigns</code></td><td>Iterated fetus findings</td><td>Optional fetus Patients and fetus-focused Observations</td><td><code>CONTAINER</code>: child attributes are mapped below.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>medication</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td>Encounter-scoped <code>Observation.<wbr>valueBoolean</code>; text-only code</td><td><code>PARTIAL</code>: no medication, dose, indication, or treatment status is inferred.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>employmentRate</code></td><td>DTO only</td><td>None</td><td><code>UNSUPPORTED</code>: employment is outside the current facade surface.</td></tr>
    <tr><td><code>antenatalAppointments[].<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td>Encounter-scoped <code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no diagnosis, medication, procedure, measurement, or assessment is extracted.</td></tr>
    <tr><td><code>.<wbr>.<wbr>.<wbr>fetusesVitalSigns[].<wbr>fosterId</code></td><td>Optional <code>PopulationFetusPatient.<wbr>LogicalId</code> and Observation focus ID</td><td>Minimal fetus <code>Patient.<wbr>id</code>; <code>Observation.<wbr>focus=Patient/<wbr>{derived-id}</code></td><td><code>PARTIAL</code>: only positive IDs. The ID is SHA-256-derived from maternal logical ID, active record UUID, and source fetus ID. Raw <code>fosterId</code> is not exposed. Missing/non-positive IDs do not suppress findings but produce no fetus Patient/focus.</td></tr>
    <tr><td><code>.<wbr>.<wbr>.<wbr>fetusesVitalSigns[].<wbr>fetalHeartRate</code></td><td><code>PopulationObservation(<wbr>QuantityValue)</code></td><td>Vital-signs Observation; SNOMED CT <code>364075005</code> plus LOINC <code>55283-6</code>; UCUM <code>{beats}/<wbr>min</code></td><td><code>DIRECT</code>: only positive values; maternal Patient remains <code>subject</code>, optional fetus Patient is <code>focus</code>.</td></tr>
    <tr><td><code>.<wbr>.<wbr>.<wbr>fetusesVitalSigns[].<wbr>fetalPresentationLie.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td><td><code>PopulationObservation(<wbr>CodedValue)</code></td><td>Exam Observation with text-only question code and Volven 8534 <code>valueCodeableConcept</code></td><td><code>DIRECT</code>: only Volven 8534 is accepted.</td></tr>
    <tr><td><code>.<wbr>.<wbr>.<wbr>fetusesVitalSigns[].<wbr>motherFeelsBabyMovements</code></td><td><code>PopulationObservation(<wbr>BooleanValue)</code></td><td>Survey Observation, LOINC <code>57088-7</code>, <code>valueBoolean</code></td><td><code>DIRECT</code>: explicit <code>false</code> is retained; maternal Patient remains subject.</td></tr>
    <tr><td><code>.<wbr>.<wbr>.<wbr>fetusesVitalSigns[].<wbr>note</code></td><td><code>PopulationObservation(<wbr>TextValue)</code></td><td>Exam <code>Observation.<wbr>valueString</code>; text-only code</td><td><code>PARTIAL</code>: no diagnosis or finding is extracted.</td></tr>
  </tbody>
</table>

Fetus Patients contain only `id` and optional `meta.lastUpdated`. The facade does not invent
NIN, identifier, name, gender, or birth date.

## Points of contact

When at least one supported value is present, the facade creates one active `CareTeam` with
the maternal Patient as `subject`. Practitioner, PractitionerRole, and Organization resources
are contained within that CareTeam. No Grunndata or directory lookup is performed.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>pointsOfContact.<wbr>generalPractitioner</code></td><td><code>PopulationCareTeamMember</code></td><td>Contained Practitioner/Organization/PractitionerRole as applicable</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>generalPractitioner.<wbr>name</code></td><td><code>PopulationCareTeamMember.<wbr>Name</code></td><td>Contained <code>Practitioner.<wbr>name.<wbr>text</code></td><td><code>DIRECT</code>: trimmed; no external lookup.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>generalPractitioner.<wbr>organizationName</code></td><td><code>PopulationCareTeamMember.<wbr>OrganizationName</code></td><td>Contained <code>Organization.<wbr>name</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>generalPractitioner.<wbr>organizationId</code></td><td><code>PopulationCareTeamMember.<wbr>OrganizationId</code></td><td>Contained <code>Organization.<wbr>identifier</code> with ENH OID system</td><td><code>DIRECT</code>: source-provided value only.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>generalPractitioner.<wbr>hprNr</code></td><td><code>PopulationCareTeamMember.<wbr>HprNumber</code></td><td>Contained <code>Practitioner.<wbr>identifier</code> with HPR OID system</td><td><code>DIRECT</code>: source-provided value only.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>midwife</code></td><td><code>PopulationCareTeamMember</code></td><td>Contained Practitioner/Organization/PractitionerRole as applicable</td><td><code>CONTAINER</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>midwife.<wbr>name</code></td><td><code>PopulationCareTeamMember.<wbr>Name</code></td><td>Contained <code>Practitioner.<wbr>name.<wbr>text</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>midwife.<wbr>organizationName</code></td><td><code>PopulationCareTeamMember.<wbr>OrganizationName</code></td><td>Contained <code>Organization.<wbr>name</code></td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>midwife.<wbr>hprNr</code></td><td><code>PopulationCareTeamMember.<wbr>HprNumber</code></td><td>Contained <code>Practitioner.<wbr>identifier</code> with HPR OID system</td><td><code>DIRECT</code>.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>midwife.<wbr>organizationId</code></td><td>DTO contract-tolerance property only</td><td>None</td><td><code>UNSUPPORTED</code>: NHN documents the midwife shape without organization ID, and the facade does not expose it if present.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>birthInstitute</code></td><td><code>PopulationCareTeam.<wbr>BirthInstitute</code></td><td>Contained Organization name/type and CareTeam participant role <code>Fødeinstitusjon</code></td><td><code>DIRECT</code>: trimmed source name; no identifier is invented.</td></tr>
    <tr><td><code>pointsOfContact.<wbr>maternityHealthcareCentre</code></td><td><code>PopulationCareTeam.<wbr>MaternityHealthcareCentre</code></td><td>Contained Organization name/type <code>Helsestasjon</code> and CareTeam participant</td><td><code>DIRECT</code>: trimmed source name; no identifier is invented.</td></tr>
  </tbody>
</table>

The contained PractitionerRole text is `Fastlege` or `Jordmor`, based only on the explicit
DHG relationship. Period, specialty, services, and managing responsibility are not inferred.

## Birth status

NHN requires birth status to be registered before the maternity record is changed from active
to delivered. The facade therefore maps explicit birth-status entries found in the selected
active record.

<table>
  <thead>
    <tr><th width="25%" scope="col">DHG JSON path</th><th width="25%" scope="col">Facade handoff</th><th width="25%" scope="col">FHIR output</th><th width="25%" scope="col">Status and exact rule</th></tr>
  </thead>
  <tbody>
    <tr><td><code>birthStatus.<wbr>metadata</code></td><td>Resource metadata</td><td>Derived Observation IDs and <code>meta.<wbr>lastUpdated</code>; active-resource filter</td><td><code>CONTAINER</code>: the common metadata rules above apply to the whole birth-status resource.</td></tr>
    <tr><td><code>birthStatus.<wbr>birthStatus[]</code></td><td>Iterated <code>DhgBirthStatusEntry</code></td><td>Zero or one social-history Observation per entry</td><td><code>CONTAINER</code>: an entry is emitted when it has an accepted status or an explicit delivery timestamp.</td></tr>
    <tr><td><code>birthStatus.<wbr>birthStatus[].<wbr>fosterId</code></td><td>Pregnancy-scoped fetus key</td><td>Minimal fetus <code>Patient</code> and <code>Observation.<wbr>focus</code></td><td><code>PARTIAL</code>: a positive value creates or correlates the same pseudonym fetus used by antenatal findings. A missing or non-positive value creates no Patient or <code>focus</code>; the outcome remains maternal-subject data.</td></tr>
    <tr><td><code>birthStatus.<wbr>birthStatus[].<wbr>status.<wbr>{code,<wbr>display,<wbr>codeSystem}</code></td><td><code>CodedValue</code></td><td><code>Observation.<wbr>valueCodeableConcept</code></td><td><code>DIRECT</code>: only Volven 8522 is accepted. Foreign or missing coding is omitted without discarding an explicit delivery timestamp.</td></tr>
    <tr><td><code>birthStatus.<wbr>birthStatus[].<wbr>datetime</code></td><td><code>EffectiveDateTime</code></td><td><code>Observation.<wbr>effectiveDateTime</code></td><td><code>DIRECT</code>: the complete source timestamp and offset are retained.</td></tr>
  </tbody>
</table>

The Observation uses text-only code `Fødselsstatus`, category `social-history`, and the mother
as `subject`. It does not derive sex, diagnosis, viability, death time, or any outcome beyond
the explicit Volven value.

## Normalized facade fields to common FHIR elements

This final projection is independent of DHG JSON structure.

<table>
  <thead>
    <tr><th width="50%" scope="col">Normalized facade field</th><th width="50%" scope="col">FHIR R4 element</th></tr>
  </thead>
  <tbody>
    <tr><td><code>PopulationPatient.<wbr>LogicalId</code></td><td><code>Patient.<wbr>id</code></td></tr>
    <tr><td><code>PopulationPatient.<wbr>LastUpdated</code></td><td><code>Patient.<wbr>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td><code>PopulationFetusPatient.<wbr>LogicalId</code></td><td>Fetus <code>Patient.<wbr>id</code></td></tr>
    <tr><td><code>PopulationFetusPatient.<wbr>LastUpdated</code></td><td>Fetus <code>Patient.<wbr>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Id</code></td><td><code>Observation.<wbr>id</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>LastUpdated</code></td><td><code>Observation.<wbr>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Code</code></td><td><code>Observation.<wbr>code</code>; supplemental safe codings may be added by <code>PopulationCodes.<wbr>CodingsFor</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Category</code></td><td><code>Observation.<wbr>category</code> using the standard observation-category system</td></tr>
    <tr><td><code>PopulationObservation.<wbr>Value</code></td><td>Matching <code>valueBoolean</code>, <code>valueInteger</code>, <code>valueDecimal</code>, <code>valueDateTime</code>, <code>valueString</code>, <code>valueCodeableConcept</code>, or <code>valueQuantity</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Effective</code></td><td><code>Observation.<wbr>effectiveDateTime</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Components</code></td><td><code>Observation.<wbr>component</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>EncounterId</code></td><td><code>Observation.<wbr>encounter</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>FocusPatientId</code></td><td><code>Observation.<wbr>focus</code></td></tr>
    <tr><td><code>PopulationObservation.<wbr>Note</code></td><td><code>Observation.<wbr>note</code></td></tr>
    <tr><td>Maternal logical ID for every Observation</td><td><code>Observation.<wbr>subject=Patient/<wbr>{maternal-id}</code></td></tr>
    <tr><td><code>PopulationEncounter.<wbr>Id</code></td><td><code>Encounter.<wbr>id</code></td></tr>
    <tr><td><code>PopulationEncounter.<wbr>LastUpdated</code></td><td><code>Encounter.<wbr>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td><code>PopulationEncounter.<wbr>Date</code></td><td>Same day in <code>Encounter.<wbr>period.<wbr>start</code> and <code>.<wbr>end</code></td></tr>
    <tr><td>Facade Encounter constants</td><td><code>Encounter.<wbr>status=unknown</code>, <code>Encounter.<wbr>class=AMB</code>, maternal Patient as <code>subject</code></td></tr>
    <tr><td><code>PopulationCareTeam.<wbr>Id</code></td><td><code>CareTeam.<wbr>id</code></td></tr>
    <tr><td><code>PopulationCareTeam.<wbr>LastUpdated</code></td><td><code>CareTeam.<wbr>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td>Facade CareTeam constants</td><td><code>CareTeam.<wbr>status=active</code>, maternal Patient as <code>subject</code></td></tr>
  </tbody>
</table>

Search Bundles add no DHG attributes. They wrap the mapped resources as FHIR `searchset`
entries, calculate `Bundle.total`, stamp the response time, and optionally construct `fullUrl`
from the facade service base.

## Maintenance rule

When the DHG contract or facade mapping changes, update this catalog in the same change as:

1. `DhgModels.cs` for source-contract changes;
2. `DhgPopulationSnapshotFactory.cs` and mapping tests for normalization changes;
3. `PopulationCodes.cs` and terminology evidence for coding changes; and
4. `FhirPopulationMapper.cs`, FHIR examples, and contract tests for output-shape changes.

An attribute appearing in NHN Swagger or `AdditionalProperties` is not automatically safe to
publish. It needs an explicit row, semantic review, implementation, and test before it becomes
part of the facade contract.
