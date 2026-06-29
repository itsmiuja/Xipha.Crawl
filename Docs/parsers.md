# Parsers

## SearchResultParser

### Target HTML Structure

Each search result is a `div` with the class `RowSearchSty`:

```html
<div class="RowSearchSty">
  <a href="/NFI/Detail/12345">Persian Drug Name</a>
  <span class="titleSearch-Link-ltrAlter">EnglishName</span>

  <label>صاحب برند</label>
  <span class="txtSearch1">Company Name</span>

  <label>صاحب پروانه</label>
  <span class="txtSearch1">License Holder Name</span>

  <label>کد فرآورده</label>
  <span class="txtSearch1">XXXXX</span>

  <label>کد ژنریک</label>
  <span class="txtSearch1">XXXXX</span>

  <span class="priceTxt">1,234,567</span>

  <bdo>20 TABLET in 2 BLISTER PACK in 1 BOX</bdo>
</div>
```

### Extracted Fields

| Field | XPath selector |
|---|---|
| `PersianName` | `//a[contains(@href,'/NFI/Detail/')]` inner text |
| `DetailUrl` | `href` of the same link, prefixed with `BaseUrl` |
| `EnglishName` | `//span[contains(@class,'titleSearch-Link-ltrAlter')]` |
| `BrandOwner` | label `صاحب برند` + sibling `span.txtSearch1` |
| `LicenseHolder` | label `صاحب پروانه` + sibling `span.txtSearch1` |
| `ProductCode` | label `کد فرآورده` + sibling `span.txtSearch1` |
| `GenericCode` | label `کد ژنریک` + sibling `span.txtSearch1` |
| `Price` | `span.priceTxt` (commas stripped, parsed as `long`) |
| `Packaging` | `<bdo>` tag inner text |
| `IsEmergencyLicense` | presence of text `پروانه فوریتی` anywhere in the row |

### GetLabeledValue Logic

For label-based fields:
1. Find the `<label>` element containing the target text
2. Move up to its `parentNode`
3. Find `span.txtSearch1` within that same parent

This relies on the site's HTML pattern where a label and its value share a common container element.

---

## DetailPageParser

### Extracted Fields

| Field | Extraction method |
|---|---|
| `WebId` | Last path segment of the URL, parsed as `int` |
| `BrandName` | `ByLabel(doc, "نام :")` |
| `GenericName` | `ByLabelBdo(doc, "نام عمومی")` — value is inside a `<bdo>` |
| `DrugForm` | `ByLabel(doc, "شکل دارویی")` |
| `RouteOfAdmin` | `ByLabelBdo(doc, "نحوه مصرف")` |
| `LicenseHolder` | `ByLabelClass(doc, "صاحب پروانه", "txtSearch1")` |
| `BrandOwner` | `BrandOwnerClean()` — text nodes only, child elements excluded |
| `Manufacturer` | `ByLabel(doc, "تولید کننده")` |
| `LicenseExpiry` | `ByLabel(doc, "تاریخ اعتبار پروانه")` |
| `Packaging` | `ByLabelBdo(doc, "تعداد در بسته")` |
| `Composition` | `ByLabel(doc, "ترکیبات")` |
| `PackagePrice` | first `span.priceTxt` |
| `UnitPrice` | second `span.priceTxt` |
| `GTIN` | label with class `txtSearchEnglish-ltr` containing `":GTIN"` |
| `IRC` | label with class `txtSearchEnglish-ltr` containing `":IRC"` |
| `ATCCode` | `label.graphLabelSearch` inner text |
| `ATCHierarchy` | all `div.graphBox` elements — parsed as ATC level list |

### Why ByLabelBdo for some fields?

Certain fields on the site are wrapped in a `<bdo dir="ltr">` tag (for correct LTR rendering of names like INN names). The `ByLabelBdo` method searches for `following-sibling::bdo` instead of `following-sibling::span`.

### BrandOwnerClean

The brand owner field contains unwanted child elements. This method concatenates only direct `Text` nodes, skipping all child elements:

```csharp
node.ChildNodes
    .Where(n => n.NodeType == HtmlNodeType.Text)
    .Select(n => n.InnerText)
```

### ParseATCHierarchy

The ATC classification hierarchy is extracted from multiple `div.graphBox` elements. Each box contains:
- `div.colorMediumPurple > a` → ATC level description
- `span.txtEnglish-ltr1 > a` → ATC code

Output: `List<ATCLevel(Code, Description)>`

**Clinical fields** (indications, side effects, contraindications) are intentionally **not extracted** — they are identical across all drugs sharing the same generic name and would inflate the database unnecessarily.

---

## PackagingParser

### Purpose
Extracts the unit count from a packaging string so the unit price can be calculated.

### Logic
```
"20 TABLET in 2 BLISTER PACK in 1 BOX"  →  first number = 20
"100 ML in 1 BOTTLE in 1 BOX"            →  first number = 100
"1 VIAL in 1 BOX"                        →  first number = 1
""  (empty)                              →  default = 1
```

Regex used: `^(\d+)` applied to the trimmed string.

### Unit Price Calculation
```csharp
UnitPrice = PackagePrice / UnitCount
// if UnitCount == 1, unit price equals package price
```
