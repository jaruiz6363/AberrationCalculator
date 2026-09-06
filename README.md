# Aberration Calculator

Opens a lens design written by any of the common optical design programs, prints its
prescription, and evaluates its aberration coefficients.

It does not optimize, and it does not design. It reads a file someone else wrote and tells
you what that lens is: the surfaces and materials as stated, the refractive index of every
material at every wavelength in the file, the first-order layout, and the aberration
coefficients that follow from them.

## How far the coefficients get you

Predicting a spot from coefficients costs a small fraction of tracing rays for one, which is
what makes it attractive early in a design, before the shape is settled enough to be worth a
full evaluation. The question is where the prediction stops being worth quoting.
`docs/spot-prediction.md` measures that on five lenses, at both conjugates, against rays this
program traces itself.

Cooke triplet, f/5, 20° half-field, as error in the predicted RMS spot radius:

| field | 3rd | 3rd + 5th | + 7th-order spherical | full 7th |
|---|---|---|---|---|
| 0.7 | +79.1% | +6.6% | +5.2% | +1.6% |
| 0.8 | +97.7% | +9.2% | +8.2% | +1.4% |
| 0.9 | +127.0% | +14.5% | +13.9% | **+0.4%** |

The `+ 7th-order spherical` column adds that one coefficient on its own - the one tertiary
quantity already available elsewhere, and it barely moves the off-axis error. What closes the
gap is the other nineteen coefficients.

**The order a design needs is a property of that design, not a general rule.** Of the five
lenses measured, one is described by third order alone, two need the full seventh to reach a
per cent, one is not well described at seventh, and one - a hard-corrected asphere - is not
described at all. That last case is examined rather than glossed: the series is still
converging there, and what runs out is the extraction at seventh order rather than the method.

## Two things for OpticStudio users

Beside the calculator itself, the repository holds two programs that report third-, fifth-
and seventh-order aberration coefficients **per surface**, split into what each surface
generates on its own and what it generates by acting on the aberration already present when
light reaches it. A table of totals cannot say which surface to change; that split can.

| | `macros/BUCH7.ZPL` | `zosapi/` FORBES7 |
|---|---|---|
| method | Buchdahl's computing scheme | Forbes' series trace, JOSA 73, 782 (1983) |
| form | one ZPL macro | C# driving OpticStudio through the ZOS-API |
| to install | nothing | build one solution, run FixBinaries once |
| spherical surfaces | yes | yes |
| conics and even aspheres | third and fifth order only | all three orders, with the figuring separated |
| object at infinity or finite | either | either |

They share no code and agree: seventh-order spherical aberration is reached through
Buchdahl's fifth-order working in one and through a power series in the other, and both
give 1.681450E-03 on a Cooke triplet at infinite conjugate, 2.305287E-03 with the object
at 250 mm. FORBES7 prints that comparison itself, every run.

The seventh order is also reachable without OpticStudio at all. It needs no ray tracer and
no other program - only the lens file:

    abcalc <lensfile> --forbes

and the MCP server offers the same as `seventh_order`, so an assistant can ask for it
directly. All three print the identical report from one formatter.

What either of them buys over the orders already available is the section above, and
`docs/spot-prediction.md` in full.

See `macros/README.md` and `zosapi/README.md`. Note the repository holds **two solution
files**: `AberrationCalculator.sln` is .NET 8, and `ForbesAberrationCalculatorZOSAPI.sln`
is .NET Framework, which is what the ZOS-API requires — so `dotnet build` and
`dotnet test` need the solution named.

## Formats it reads

| Program | Extension |
|---|---|
| ZEMAX / OpticStudio | `.zmx` |
| CODE V | `.seq` |
| OPTALIX | `.otx`, `.opt` |
| OSLO | `.len`, `.osl` |
| Optiland | `.json` |
| LensHH-LT | `.lhlt` |

Glasses are resolved from AGF catalogs. A material that is not a catalog name is read as a
six-digit glass code (`517642`, or `564.610` as OSLO writes it), which fixes nd and Vd and
gives a model dispersion; anything still unresolved is reported rather than quietly treated
as air.

## What it computes

- **Prescription** - radius, thickness, material, semi-diameter, conic and aspheric terms,
  as the file states them.
- **Refractive indices** - per material, per wavelength.
- **First order** - effective and back focal length, entrance and exit pupils, F/number,
  paraxial image height and magnification.
- **Aberration coefficients** - third order (Seidel), fifth and seventh order
  (Buchdahl/Rimmer).
- **PRMS** - the RMS spot size predicted from those coefficients, per field and per
  wavelength, and **PRMSA**, the weighted composite over all of them.

## Status

Working, and validated in `docs/verification.md`:

- `abcalc`, the command-line tool, and the six file readers
- bundled glass catalogs and index resolution - nothing to point at, nothing to install
- the paraxial trace, which agrees with an independent implementation to every
  digit it prints
- Seidel third-order coefficients
- Buchdahl/Rimmer fifth- and seventh-order coefficients, split into intrinsic,
  aspheric and induced parts, per surface and totalled
- PRMS and PRMSA, the RMS spot radius predicted from those coefficients with no
  rays traced
- the re-normalised per-aberration and per-surface contributions to that spot

There is no GUI. The tool writes plain text and TSV files that you can read, diff and
feed to something else.

## Layout

```
src/AberrationCalculator.Core   models, glass, ray trace, coefficients
src/AberrationCalculator.IO     one reader per format
src/AberrationCalculator.Cli    abcalc - the command-line tool
catalogs/Glass                  bundled AGF glass catalogs
tests/                          unit tests
tools/smoke                     command-line harness used during development
```

The method is not original work: it is Buchdahl's aberration coefficients in Rimmer's
notation, Robb's analytic integration of them into a spot size, and Rosete-Aguilar and
Rayces's re-normalisation of them into comparable quantities. `docs/references.md` gives
the full chain, says which papers have actually been read, and records where the
implementation came from.

## Using it

```
abcalc mylens.zmx
```

Prints a report and writes it alongside the lens file, together with tab-separated data
files a script or spreadsheet can read: prescription, refractive indices, first-order
quantities, and the paraxial marginal and chief rays at every surface. `abcalc --help`
lists the options.

Glass catalogs ship in `catalogs/Glass` and are found automatically - there is nothing to
configure. `--glass <dir>` overrides them if you have your own.

## Building

```
dotnet build AberrationCalculator.sln
dotnet test  tests/AberrationCalculator.Tests/AberrationCalculator.Tests.csproj
```

.NET 8, so Windows, Linux and macOS.

## Licence

MIT - see `LICENSE`. Copyright Javier Ruiz; authors in `AUTHORS`.

The file readers began as the MIT-licensed readers from
[LensHH-LT](https://github.com/SynapseOptics/LensHH-LT); everything else here is written
for this program.
