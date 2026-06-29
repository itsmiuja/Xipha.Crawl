# SearchTerms — Search Keyword Strategy

## Why Prefixes?

The site `irc.fda.gov.ir` has no public API for retrieving all drugs at once. A search term must be provided. The chosen strategy is to send **short 2-character prefixes** that together cover the entire drug catalogue.

Example: searching `"am"` returns ampicillin, amiodarone, amlodipine, ...

---

## Latin Prefixes (105 total)

Cover English drug names and Latinised brand names:

```
ac  al  am  ap  ar  as  at  az
be  bi  bo  bu
ca  ce  ch  ci  cl  co
da  de  di  do  du
em  en  er  es  et  ez
fa  fe  fi  fl  fo  fu
ga  ge  gl
he  hy
ib  im  in  ip  ir  is
ke
la  le  li  lo
me  mi  mo
na  ni  no  ny
of  ol  om  on  os  ox
pa  pe  ph  pi  pr  py
qu
ra  ri  ro
sa  se  si  so  sp  su
ta  te  th  ti  to  tr
va  ve  vi
wa  zo
```

---

## Persian Prefixes (~125 total)

Cover drug names registered in Persian:

```
آ   اب  اد  ار  از  اس  اف  اک  ال  ام  ان  اه  ای
با  بت  بر  به
پر  پن  پو  پی
تر  تن  تو
دا  دک  دو  دی
ری  رو
سا  سد  سر  سف  سل  سم  سن  سو  سی
فر  فن
کا  کپ  کر  کل  کم  کن  کو  کی
لا  لو
ما  مت  مد  مر  مف  مل  من  مو  می
نا  نر  نو  نی
وا  هی  یو
```

---

## Combined Coverage

```
SearchTerms.Latin    → ~105 prefixes
SearchTerms.Persian  → ~125 prefixes
SearchTerms.All      → ~230 prefixes (Latin + Persian concatenated)
```

A full pipeline run uses `SearchTerms.All`.

---

## Limitations and Mitigations

### Duplicate results across terms
A single drug may appear under multiple prefixes (e.g. "Amlodipine" matched by both `"am"` and `"آم"`). The `INSERT OR IGNORE` mechanism in Storage handles this automatically — each `WebId` is stored only once.

### Prefixes with no results
If no drug matches a given prefix, `SearchResultParser` returns an empty list and the pipeline continues without error.

### Very short drug names
Drugs with names of 1–2 characters may not be caught by 2-character prefixes. Single-character prefixes (`a`, `b`, `آ`) can be added to `SearchTerms.Latin` or `SearchTerms.Persian` for better coverage.

---

## Adding a New Prefix

To add a prefix permanently:

```csharp
// In SearchTerms.cs
public static readonly IReadOnlyList<string> Latin =
[
    // ...existing entries...
    "xy",  // ← add here
];
```

To crawl a specific term in isolation at runtime:

```csharp
await pipeline.RunSearchPhaseAsync(["metformin", "enalapril"], ct);
```
