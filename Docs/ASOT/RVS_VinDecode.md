# RVS — VIN Decode

**Version:** 1.0 · October 10, 2026
**Scope:** How the intake wizard turns a VIN, serial number or plate photo into the vehicle fields on Step 4: where each value comes from, which source wins, and why. Covers what is built (`#854`). Storing the body type on the request, packet and Manager is not built (`#865`).

Product canon is `../RVS_Overview.md`, `../RVS_Spec.md`, `../RVS_Plan.md`. Requirements referenced here as `Spec A-3` and `Spec A-10` live in `../RVS_Spec.md` section A.

---

## Why this exists

The NHTSA vPIC decode looks authoritative and is not. vPIC's `Make` is the brand registered to the manufacturer's WMI (the first three VIN characters), so for makers with several brands it returns a sibling brand or a product line, **even on a clean decode** (`ErrorCode` 0):

| VIN | WMI | vPIC `Make` | Actual brand | What happened |
| --- | --- | --- | --- | --- |
| Arctic Fox 2021 | `4N1` | NASH | Arctic Fox | Northwood registers one make for all its brands. `Model` came back as "Northwood Investments Corporation". |
| Brinkley Model G 2024 | `7T0` | MODEL G | Brinkley RV | Brinkley filed its product lines as makes. The real brand is only in `Manufacturer`. |

The plate printed on the unit is the better source for brand and model. The decode is a fallback for them and the primary source for type.

---

## Decision flow

### 1. Getting the identifier (Step 3)

- The customer types, speaks or photographs it.
- A photo read at confidence >= 0.7 fills the field, along with any brand, model and year the plate prints. At >= 0.9 a VIN also triggers the lookup automatically (`Spec A-10`).
- Only a well-formed 17-character VIN is decoded. A serial number goes straight to Step 4 for manual entry.

### 2. Looking up the VIN (vPIC `DecodeVinValues`)

- No result, no `Make`, an HTTP error or a timeout: the lookup fails. Step 4 says no information was found and the customer enters the details. Submission still succeeds.
- Otherwise the decode is **exact** when `ErrorCode` is `0`, and **partial** for any other code.

### 3. Settling each field

The plate wins wherever it has a value.

| Field | Rule |
| --- | --- |
| **Brand** | Plate value if there is one. The decode only fills a blank. vPIC `Make`, uppercase as returned. |
| **Model** | Plate value if there is one. The decode only fills a blank, from `Model`, then `Series`, then `Trim`. |
| **Year** | An exact decode overrides the plate. A partial decode only fills a blank. A blank decoded year is 0 and is ignored. |
| **Type** | Comes from the decode only. See below. |

### 4. Inferring the body type

`RvBodyTypeClassifier` (Domain) reads four vPIC fields. The first rule that matches wins. When none does, the type is left blank rather than guessed.

| Order | Field | Match | Result |
| --- | --- | --- | --- |
| 1 | `OtherTrailerInfo` (maker's free text) | "toy hauler" | Toy Hauler |
| | | "fifth wheel" or "5th wheel" | Fifth Wheel |
| | | "pop-up", "popup", "tent" or "folding" | Pop-Up Camper |
| | | "park model" | Park Model |
| | | "travel trailer" | Travel Trailer |
| 2 | `TrailerBodyType` is "Camping or Travel Trailer", then `TrailerType` (the **hitch**) | Fifth Wheel, Gooseneck or Kingpin | Fifth Wheel |
| | | any other or blank hitch | Travel Trailer |
| 3 | `TrailerBodyType` | "House Trailer" | Park Model |
| 4 | `BodyClass` (motorized) | "Incomplete - Motor Home Chassis" or "Incomplete - Stripped Chassis" | Class A Motorhome |
| | | "Incomplete - Cutaway" or "Incomplete - Chassis Cab ..." | Class C Motorhome |
| | | "Van" or "Cargo Van" | Class B Camper Van |

Notes:

- `TrailerType` is the hitch, not the kind of RV. A bumper-pull trailer is not typed "Bumper Pull".
- "Not Applicable" and blank values are treated as absent.
- A bare `BodyClass` of "Motorhome" does not say which class, so it gets no type.
- Cargo, boat and utility trailers get no type.
- **Truck Camper** exists as a category but is never inferred: a truck camper carries a serial number, not a VIN.

### 5. Confirmation

- After any lookup the customer lands on Step 4 with "Check the brand, model and year below and change anything that's wrong." That includes when they returned to Step 3 from Review: Continue stops at Step 4, then returns to Review (`IntakeWizardState.ContinueFromEditAsync`).
- All fields stay editable.
- The inferred type is shown next to the VIN. It is not submitted.

---

## Where it lives

| Piece | Location |
| --- | --- |
| vPIC client, `Model` fallback, `IsExactMatch`, partial-decode log | `RVS.API/Integrations/NhtsaVinDecoderClient.cs` |
| Body-type rules | `RVS.Domain/Integrations/RvBodyTypeClassifier.cs`, `RvBodyType.cs` |
| Decode result and response DTO | `RVS.Domain/Integrations/IVinDecoderService.cs`, `RVS.Domain/DTOs/VinDecodeResponseDto.cs` |
| Field precedence, Step 4 stop | `RVS.Blazor.Intake/State/IntakeWizardState.cs` (`ApplyVinDecode`, `ContinueFromEditAsync`) |
| Real vPIC response used as a test fixture (VIN masked) | `Tests/RVS.API.Tests/Integrations/VpicFixtures.cs` |

The response DTO carries the type as a display name ("Fifth Wheel"). Logging on a partial decode records `Make`, vPIC `Manufacturer` and `ErrorCode` with the VIN suffix only.

---

## Coverage

| Requirement | State | Note |
| --- | --- | --- |
| A-3 VIN decode: make, model, year | **Built** | Plate wins for brand and model; decode fills blanks |
| A-3 type | **Built, not stored** | Inferred and shown on Step 4; not on the request, packet or Manager (`#865`) |
| A-10 decode vs. plate precedence | **Built** | Year follows exact vs. partial; brand and model never override the plate |
| Brand correction for makers whose `Make` is a product line | **Not built** | Brinkley returns "MODEL G"; with a photo the plate brand wins, without one Step 4 asks the customer to check |
| WMI-to-brand override table, other VIN providers | **Out of scope** | Listed out of scope in `#854` |
| Class A vs. Class C when vPIC says only "Motorhome" | **Not possible** | vPIC does not give chassis data |
