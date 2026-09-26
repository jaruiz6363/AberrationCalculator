# buch7_asph.ccl - Buchdahl coefficients per surface in OSLO

`buch7_asph.ccl` is the OSLO port of stages A and B of
[`macros/BUCH7_ASPH.ZPL`](../macros/BUCH7_ASPH.ZPL). It prints the third- and fifth-order Buchdahl
coefficients and the seventh-order spherical B7, as system totals and **surface by surface**. Each
surface is split into three parts:

| Part | What it is |
|---|---|
| **intrinsic** | what the surface generates out of its own curvature, indices and the two paraxial rays arriving at it |
| **figuring** | what its conic and its AD, AE, AF terms add; zero on a sphere |
| **induced** | what the aberration already accumulated ahead of the surface produces in it; there is none at third order |

A final table gives each surface's induced share of the fifth-order magnitude it carries. Near 0 the
surface is on its own. Near 1, what it carries was handed to it, and the fix is upstream. Above 1,
the intrinsic and induced parts are cancelling each other there.

The twenty seventh-order tau (stages C and D of the macro) are not in this port.

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

The report is also written to `B7_REPORT`, set at the top of the file to
`C:/GIT/AberrationCalculator/ccl/buch7_asph.txt`. Change it to a folder that exists on your machine.

## What it accepts

- **Surfaces:** plane, sphere, conic and OSLO's standard asphere (AD, AE, AF). AG, the r^10 term,
  cannot reach the seventh order and is noted and left out. Any other type (toric, spline, general
  asphere, perfect lens) is declined by name.
- **Rays:** the paraxial marginal and chief rays are traced from OSLO's surface data. They are
  launched as AberrationCalculator launches them: EBR at the entrance pupil, and the field as ANG at
  infinity or OBH at a finite object. The report prints them beside the index after each surface, so
  they can be set against OSLO's own `pxt all`.
- **Mirrors:** a reflecting surface negates the index from there on, as in the C#.

## Checking it

`buch7_asph_F6_expected.txt` and `buch7_asph_F4_expected.txt` hold what it must print per surface
for `tests/fixtures/coefficient-reference/F6_triplet_two_aspheres.len`, a triplet with two figured
surfaces, and `F4_parabolic_mirror.len`. They come from this program's C# (`surfaces.tsv`), scaled
by the same signed F/number the C# scales its transverse totals by.

Run in OSLO EDU 6.6 in September 2026:

| Lens | Per-surface entries | System totals |
|---|---|---|
| F6, triplet with a conic and AD, and AD with AE | 469 of 469 identical to every printed digit | 18 of 18 to the last printed digit (3e-7) |
| F4, parabolic mirror | 134 of 134 identical | 18 of 18 to the last printed digit |

The paraxial rays it traces agree with the C#'s at every surface.

On F4 the conic's figuring cancels the sphere's spherical aberration exactly: B, B5 and B7 are
+0.1, 2.25e-3 and 4.625e-5 intrinsic against the same negated as figuring. The F/number there is
-2.5, not 2.5. After an odd number of reflections the marginal ray leaves with the opposite sign of
slope, and the F/number that scales the coefficients keeps that sign, as the C# and the macro do.
