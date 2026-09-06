# Aberration Calculator

Opens a lens design written by any of the common optical design programs, prints its
prescription, and evaluates its aberration coefficients.

It does not optimize, and it does not design. It reads a file someone else wrote and tells
you what that lens is: the surfaces and materials as stated, the refractive index of every
material at every wavelength in the file, the first-order layout, and the aberration
coefficients that follow from them.

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

How much any of it buys is measured in `docs/spot-prediction.md`, against rays this program
traces itself. On a Cooke triplet at nine tenths of the field, third and fifth order together
predict the spot to +14.5 per cent; adding seventh-order spherical aberration alone - the one
tertiary quantity that was already available elsewhere - gets to +13.9; adding the other
nineteen coefficients gets to **+0.4**. On axis the position reverses and spherical aberration
is the whole of it. The order a design needs is a property of that design.

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
- the paraxial trace, which agrees with LensHH-LT to every digit it prints
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
