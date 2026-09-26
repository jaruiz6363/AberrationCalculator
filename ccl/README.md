# buch7_asph.ccl - Buchdahl coefficients per surface in OSLO

`buch7_asph.ccl` is the OSLO port of [`macros/BUCH7_ASPH.ZPL`](../macros/BUCH7_ASPH.ZPL). It prints
the third-, fifth- and seventh-order Buchdahl coefficients of a system that may carry conics and even
aspheres, as system totals and **surface by surface**.

For each surface, the third order, the fifth order and B7 are split three ways:

| Part | What it is |
|---|---|
| **intrinsic** | what the surface generates out of its own curvature, indices and the two paraxial rays arriving at it |
| **figuring** | what its conic and its AD, AE, AF terms add; zero on a sphere |
| **induced** | what the aberration already accumulated ahead of the surface produces in it; there is none at third order |

Then each surface's induced share of the fifth-order magnitude it carries. Near 0, the surface is
on its own. Near 1, what it carries was handed to it, and the fix is upstream. Above 1, the
intrinsic and induced parts are cancelling each other there.

Last come the twenty seventh-order tau, as system totals and surface by surface. The per-surface
tau are totals: the seventh order is not split into intrinsic and induced, as the macro explains,
because no published derivation separates them. tau1 is printed both as B7 from the fifth-order
working and as Table I gives it; on a lens of spheres the two are identical.

## Installing and running it

1. Copy `buch7_asph.ccl` into OSLO's CCL folder, `private\ccl` under the OSLO data folder (for OSLO
   EDU, `C:\Users\Public\Documents\OSLO66 EDU\private\ccl`).
2. Compile it: **Tools > Compile CCL**, and check the window shows `No errors detected`. OSLO keeps
   running the version it last compiled.
3. Open a lens and type `buch7_asph`.

```
buch7_asph               per surface, current wavelength, report file
buch7_asph 0             system totals only
buch7_asph 1 2           at wavelength 2
buch7_asph 1 0 0         without the report file
```

Each run is also added to the end of `B7_REPORT`, set at the top of the file to
`C:/GIT/AberrationCalculator/ccl/buch7_asph.txt`, so several lenses can be run into one file. Change
it to a folder that exists on your machine.

## What it accepts

- **Surfaces:** plane, sphere, conic and OSLO's standard asphere (AD, AE, AF). AG, the r^10 term,
  cannot reach the seventh order and is noted and left out. Any other type (toric, spline, general
  asphere, perfect lens) is declined by name. A figured flat facing collimated light is declined for
  the seventh order; its third and fifth order still stand.
- **Size:** at most 16 surfaces between object and image. The arrays are sized to fit the 1 MB of
  global storage CCL shares with OSLO's own CCL.
- **Rays:** the paraxial marginal and chief rays are traced from OSLO's surface data. They are
  launched as AberrationCalculator launches them: EBR at the entrance pupil, and the field as ANG at
  infinity or OBH at a finite object. The report prints them beside the index after each surface, so
  they can be set against OSLO's own `pxt all`.
- **Mirrors:** a reflecting surface negates the index from there on, as in the C#.

## How it is built

The front end, meaning the lens data and the two paraxial rays, is written for OSLO. Everything from
the macro's stage A on is translated from the macro statement for statement by
[`tools/zpl2ccl`](../tools/zpl2ccl), so the arithmetic is the macro's own:

- every ZPL variable becomes a global `z_<name>`, since ZPL is case-insensitive and has no locals;
- each `SUB` becomes a function;
- CCL refuses a function past an internal size, so the macro's main body is cut into functions of
  at most 200 lines at statement boundaries.

After changing the macro, run `python tools/zpl2ccl/regen.py`, then compile in OSLO and check it.

## Checking it

`reference/` holds this program's own per-surface output (`surfaces.tsv`) for three lenses.
`tools/zpl2ccl/compare.py` checks one run of the report against it:

```
python tools/zpl2ccl/compare.py <one run of the report> ccl/reference/<lens>.surfaces.tsv
```

Run in OSLO EDU 6.6 in September 2026, every number agreed with the C# to the last printed digit:

| Lens | Route through the macro | Per-surface split | Per-surface tau | 38 system totals |
|---|---|---|---|---|
| `F6_triplet_two_aspheres.len`: triplet, conic + AD, and AD + AE | figured | 469 entries, worst 4.4e-7 | 140, worst 4.5e-7 | worst 4.2e-7 |
| `F4_parabolic_mirror.len` | figured, reflecting | 134, worst 2.5e-7 | 40, worst 4.5e-7 | worst 4.5e-7 |
| `KingslakeDG.len`: double Gauss, all spheres | Table I alone | 603, worst 4.6e-7 | 180, worst 4.1e-7 | worst 3.4e-7 |

The paraxial rays it traces agree with the C#'s at every surface.

On F4 the conic's figuring cancels the sphere's spherical aberration at every order: B, B5 and B7
come out zero, and so does tau1 by both routes. The F/number there is -2.5, not 2.5. After an odd
number of reflections the marginal ray leaves with the opposite sign of slope, and the F/number that
scales the coefficients keeps that sign, as the C# and the macro do.
