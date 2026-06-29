# Data Models

## DrugBasic

Basic information extracted from the **search results page**.

```
DrugBasic
├── PersianName         : Persian drug name
├── EnglishName         : English / brand name
├── BrandOwner          : brand owner company
├── LicenseHolder       : license holder
├── Price               : package price (IRR)
├── Packaging           : packaging string  →  "20 TABLET in 2 BLISTER PACK in 1 BOX"
├── ProductCode         : product code (کد فرآورده)
├── GenericCode         : generic code (کد ژنریک)
├── DetailUrl           : URL of the drug detail page
├── SearchTermUsed      : the prefix that returned this drug
├── IsEmergencyLicense  : true if the drug has an emergency license
└── WebId  (computed)   : last numeric segment of DetailUrl  →  /NFI/Detail/12345 = 12345
```

`WebId` is a computed property — it is derived from `DetailUrl` at runtime and does not have a backing field. It is persisted to the database as a separate column.

---

## DrugDetail

More complete information extracted from the **drug detail page**.

```
DrugDetail
├── WebId               : numeric ID (from URL)
├── DetailUrl           : full page URL
│
├── BrandName           : brand name
├── GenericName         : INN / generic name
├── DrugForm            : dosage form  →  tablet, capsule, syrup, ...
├── RouteOfAdmin        : route of administration  →  oral, injection, ...
├── LicenseHolder       : license holder
├── BrandOwner          : brand owner
├── Manufacturer        : manufacturer
├── LicenseExpiry       : license expiry date
│
├── PackagePrice        : package price (IRR)  ─── in model only, NOT in DrugDetails table
├── UnitPrice           : unit price (IRR)     ─── transferred to PriceHistory only
│
├── GTIN                : GTIN code
├── IRC                 : IRC code
├── Packaging           : packaging string (e.g. "20 TABLET in 2 BLISTER")
│
├── Composition         : drug composition
├── ATCCode             : primary ATC code
├── ATCHierarchy        : List<ATCLevel> — full ATC classification tree
└── ScrapedAt           : UTC timestamp of when the page was fetched
```

**Note:** `PackagePrice` and `UnitPrice` are **not** stored in the `DrugDetails` table — they are only forwarded to `PriceHistory`. This is intentional.

---

## ATCLevel

```
ATCLevel (record)
├── Code          : ATC code     →  e.g. "C09AA"
└── Description   : description  →  e.g. "ACE inhibitors"
```

Stored as JSON in the `ATCHierarchy` column:
```json
[
  { "Code": "C",     "Description": "Cardiovascular system" },
  { "Code": "C09",   "Description": "Agents acting on the renin-angiotensin system" },
  { "Code": "C09A",  "Description": "ACE inhibitors, plain" },
  { "Code": "C09AA", "Description": "ACE inhibitors, plain" }
]
```

---

## PriceRecord

A new row is written every time a drug's price changes.

```
PriceRecord
├── WebId         : drug identifier (FK to Drugs)
├── PackagePrice  : price per package (IRR)
├── UnitPrice     : price per unit (IRR)  — calculated or read directly from detail page
├── UnitCount     : number of units in the package
├── Source        : origin  →  "search" | "detail"
└── RecordedAt    : UTC timestamp of the record
```

### Source Comparison

| `Source` | Origin | Unit Price Accuracy |
|---|---|---|
| `"search"` | Search results page | Estimated — derived from PackagePrice ÷ UnitCount |
| `"detail"` | Drug detail page | Exact — read directly from the site |

---

## Model Relationships

```
DrugBasic ──────────────────────────► Drugs table
   │ (WebId)                              │
   └──► PriceHistory (Source="search")    │
                                          │ (WebId FK)
DrugDetail ─────────────────────────► DrugDetails table
   │ (WebId)                              │
   └──► PriceHistory (Source="detail")    │
                                          │
PriceRecord ────────────────────────► PriceHistory table
```

A single drug may be:
- Returned by **multiple search terms** (e.g. found by both `"am"` and `"آم"`) — but only one row is inserted into `Drugs` (UNIQUE WebId constraint)
- Have **one `DrugDetail`** row (INSERT OR REPLACE)
- Have **many `PriceRecord`** rows over time
