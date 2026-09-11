# Aberration Calculator

Opens a lens design written by any of the common optical design programs, prints its
prescription, evaluates its aberration coefficients, and optimizes the design by minimizing a
spot size computed from those coefficients.

First it tells you what a lens is: the surfaces and materials as stated, the refractive index
of every material at every wavelength in the file, the first-order layout, and the aberration
coefficients that follow from them. Then, if you ask it to, it will change the lens: **you** say
which curvatures and thicknesses may move and between what limits, it moves them to minimize the
merit function you wrote, and the result goes back into the file it came from.

**There are two optimizers**, and which you want depends on whether you are improving a design or
looking for a different one.

    abcalc lens.zmx --optimize                                  # the local optimizer
    abcalc lens.zmx --optimize_basin_hopping --save runs/       # the global search

The **local optimizer** finds the bottom of the valley the design starts in and stops. The step is
Dilworth's PSD III by default, with PSD II and Levenberg-Marquardt available through `--method`.
It is fast, it is deterministic, and it will never leave the form you gave it.

The **basin hopping** optimizer is the local one run many times over from kicked starting points,
with Metropolis acceptance deciding which valleys to keep walking from. Each hop is a Hooke-Jeeves
pattern search followed by the local optimizer, chains run in parallel - one per physical core by
default - and because chains land in different valleys it produces **one design per chain** rather
than a single answer. It is also the only one that can change the **glasses**: with
`--glass_substitution <catalogue>` a hop may swap a material for another from a named substitution
catalogue, which is a move no continuous optimizer can make.

**Every derivative the optimizer uses is analytic.** Not a difference quotient with a
well-chosen step - analytic, to machine precision, including through the predicted spot, which
is a quadratic form in thirty-seven Buchdahl coefficients reached through five thousand lines
of computing scheme. That is done by compiling the whole aberration chain **twice from one
source**: once in `double`, which is the analysis this program has always performed and which
its tests still guard bit-for-bit, and once with the arithmetic aliased to a forward-mode dual
number. Nothing is copied and nothing can drift, because they are the same files.

**The optimizer works on spherical surfaces only.** The coefficients come from Buchdahl's
closed-form scheme, which is the fastest route to a seventh-order coefficient there is; his
aspheric seventh order is a reconstruction real rays reject by up to a factor of four. So a
figured design is refused before the run rather than optimized against a number known to be
wrong, and there is not one test for figuring anywhere inside the evaluation loop. Reading and
analysing a figured design is unaffected - `--forbes` and the rest handle conics and even
aspheres at every order they report.

PSD recovers the curvature Gauss-Newton discards, from two successive **exact** Jacobians - a
secant that is only worth taking when both ends are real measurements, which is why nothing here
is ever Broyden-updated. Bounded variables are held by **reflection — not clamping, and not a
sigmoid**: a step past a limit is folded back inside, and the gradient keeps magnitude one. A
clamp would pin the variable and discard the rest of the step, leaving the search stalled against
a boundary with a gradient it cannot act on; a sigmoid's derivative vanishes *at* the bound, so a
variable pressed against a limit goes numb and nothing brings it back when the design later wants
it.

The report says what MOVED - surface by surface, in radii and thicknesses and glass names rather
than in the optimizer's own variables - together with the merit it started at and the merit it
reached, and which operand is holding whatever error is left.

### Reading and writing the settings

Settings are split by what they describe. The **merit function** says what the design should be
and can be carried from one design to another; the **variables** say what may change about it and
mean nothing away from the design they name.

| | `.lhlt` | every other format |
|---|---|---|
| variables, bounds, pickups | in the lens file | `<lens>.var` |
| merit function | `<lens>.mf` | `<lens>.mf` |

A `.lhlt` states which surfaces have variable curvatures and thicknesses, the bounds on them and
its pickups; all of that is read, honoured, and written back when the design is saved. Its own
**merit function is not read** - this tool optimizes a different one - and it is left untouched in
the file.

```
PRMSA,   1, TAR 0                        # the predicted spot
EFL,   100, TAR 50,          2           # focal length, in wavelength 2
EGT,    10, MIN 1,           2, 4        # glass edges over surfaces 2 to 4
DTRGT,  10, MIN 1.5, MAX 12, 2, 4        # diameter-to-thickness ratio
DISTF,  10, MIN -2, MAX 2,   0.7         # distortion at seven tenths of the field
RY,      1, TAR 0,           7, 1, 1, 0, 1
```

The second field is the weight. The numbers in that column here are only there to show that the
column exists - they are not a recommendation, for the reason at the end of this section.

```
VAR CV 1                                 # curvature of surface 1
VAR TH 2 MIN 1.0 MAX 25.0                # a thickness, bounded
PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1
```

`CV` and `TH` are the only variables, for the reason above. Limits are optional and **merge**, so
setting a maximum does not discard a minimum set a moment earlier; `FREE` takes them off again.
An operand with `TAR` is driven to it and weighed against everything else, while one with `MIN` or
`MAX` costs *exactly zero* - in the merit and in the Jacobian - until it is threatened, which is
what lets a lens carry a dozen manufacturability limits without any of them bending the answer.

### Three things the predicted spot cannot see

`PRMSA` is the obvious thing to ask for and it is not enough on its own. Robb's spot is the
variance of the ray intersection **at the Gaussian image plane**, **about its own centroid**, from
coefficients computed **per wavelength from that wavelength's own paraxial trace**. Each of those
costs the merit function something that might be assumed to be included:

| invisible to `PRMSA` | because | ask for it with |
|---|---|---|
| where the image surface is | the spot is referred to paraxial focus, wherever the file put the image | `PY` at the image surface |
| a focus shift between colours | each wavelength is measured at *its own* focus | `AXC` |
| an image-height shift between colours | a shift of the whole patch does not change its size | `LCF` |

```
PY,    1, TAR 0, 10, 2, 0, 0, 1          # marginal ray height at the image surface
AXC,   1, MIN -0.15, MAX 0.15            # axial colour
LCF,   1, MIN -0.01, MAX 0.01            # lateral colour, at the full field
```

**Focusing the image plane with `PY` is the effective way to do it.** Driving the paraxial
marginal ray to zero height at the image surface puts that surface at paraxial focus - which is
where the spot was being measured all along, so the operand does not pull the design anywhere. It
makes the last thickness *mean* something. Without it a variable back focus has exactly zero
gradient from `PRMSA`: on the double Gauss in `tests/fixtures/lenses`, moving the image surface
twenty millimetres leaves `PRMSA` at 0.042589, the same to every digit printed.

**Both colours, or neither.** Axial colour on that design starts at -0.0990 mm of focus shift -
at f/8 a blur radius near 0.0062 mm - while the on-axis predicted spot in blue reads 0.0011 mm.
Lateral colour is the one most often left out, and a run given `AXC` alone can come back
achromatic on axis and smeared in colour at the edge of the field with nothing in the merit
function having mentioned it.

**No advice is offered here on weights.** Where a requirement is a boundary, say it as `MIN`/`MAX`
and no weight has to be chosen at all. Where it must be a target, the right weight depends on the
design and the few examples in this repository are not enough to generalise from - and since
changing a weight changes the scale of the merit, two differently weighted runs cannot be compared
by their merit numbers. Judge them on the physical quantities the report prints.

Both files are also built **a command at a time**, and the command *is* the file line:

    abcalc lens.zmx VAR "TH 2 MIN 1.0 MAX 25.0"
    abcalc lens.zmx OP  "EFL, 100, TAR 50, 2"
    abcalc lens.zmx OPLIST            # numbered; OPREMOVE 3 takes number 3 out
    abcalc lens.zmx VARLIST           # likewise VARREMOVE, PICKUPLIST, PICKUPREMOVE
    abcalc HELP                       # every command and operand; HELP EFL for one

Each command reads the settings, changes them and writes them back, so a command line that exits
between every command still behaves like a program that remembers. A transcript of commands is a
valid settings file and a settings file is a script of commands - one grammar, not two dialects
for the same ideas. `VAR` lines merge, so setting a maximum does not discard a minimum set a
moment earlier; `VAR "TH 2 FREE"` is how the bounds come off deliberately.

Commands may be followed by a run in the same invocation:

    abcalc lens.zmx VAR "CV 1" VAR "CV 2" --optimize --save
    abcalc lens.zmx --optimize --saveas better.zmx
    abcalc lens.zmx --optimize_basin_hopping --glass_substitution CoreSet28 --save runs/

Nothing is overwritten unless overwriting is asked for by name: with neither `--save` nor
`--saveas` the result is written beside the original as `<name>.optimised.<ext>`. Basin hopping
lands its chains in different valleys and so produces one design **per chain** - it is refused
without a folder rather than keeping the lowest merit and discarding the rest.

Glasses for substitution come from a **named** catalogue in `catalogs/Substitution`, kept apart
from the catalogues a design is read through: a search free to pick from every vendor at once
settles on glasses nobody stocks. `CoreSet28` ships with it.

One command is not about a lens at all, and is the only one that can be given on its own:

    abcalc BASE "C:\lenses\project7"     bare names now mean this folder
    abcalc BASELIST                      show it, and where it came from
    abcalc BASEREMOVE                    forget it

It is kept until it is changed, so it holds in the next shell too. In a terminal that is a
convenience - `cd` already does most of it - but over MCP it is the difference between working
and not: an MCP server's working directory is whatever the client started it in, not anything the
user chose, so without a base every path has to be absolute. Four things can set it, most
specific first: `--dir` on the command line, `ABCALC_DIR` in the environment, the folder set with
`BASE`, and failing those the working directory - and `BASELIST` says which of the four is
answering. An absolute path is never re-rooted.

### What goes back into the file

An optimized design goes back **in the format it came from**, by editing that file rather than
regenerating it: only curvatures, thicknesses and glass names change, in the file's own units,
and everything this program does not model - solves, coatings, tolerances, somebody else's merit
function - survives untouched. That matters more than it sounds. The .zmx reader here recognises
twenty-three directives and a real `.zmx` has many times that, so a writer that rebuilt the file
from what it understood would quietly delete the rest of a design.

| format | reads | writes back |
|---|---|---|
| LensHH-LT `.lhlt` | yes | **yes**, including variables, bounds and pickups |
| ZEMAX `.zmx` | yes | **yes** |
| Optiland `.json` | yes | **yes** |
| CODE V `.seq` | yes | **yes** |
| OPTALIX `.otx` `.opt` | yes | **yes** |
| OSLO `.len` `.osl` | yes | **yes** |

Every format goes back the way it came, and each one asks for something different. A `.seq`
surface line is positional - `S radius thickness material` - so editing the radius means taking
the line apart and putting the other two back as the file spelled them. A `.len` writes nothing
for a property that has nothing to say, so bending a plane means *adding* the `RD` line it never
had, in the right block. An `.otx` gives the shape as a curvature rather than a radius, and marks
its image surface with a `-999` that is a flag and not a distance. A `.zmx` is UTF-16 where the
rest are plain text, and none of the plain-text three may come back with a byte order mark they
did not arrive with.

A format still outside the list says so plainly rather than producing a file that looks like the
design and is not; the optimized prescription is in the report, and the settings in the sidecar.

`docs/optimizer.md` has the merit-function format, every operand, and - at the end - what this
does not do.

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

**Distortion asks the same question of the coefficients, and reaches something a spot cannot.**

    abcalc <lensfile> --distortion-coefficients

**This is not a way to obtain a distortion figure.** Tracing one chief ray gives that exactly,
at the same speed, and does not degrade at the corner where the seventh order is out by a
third. What the coefficients give that a trace cannot is WHICH ORDER the distortion is —
third order is stop position and symmetry, the higher orders are not, and they answer to
different changes — and which SURFACE it comes from.

It reports both mappings, F-tan(theta) and F-theta with the exact relation between them, and
reconciles the paraxial image plane the coefficients live at with the image surface the file
defines, where a design program quotes. On a figured design the seventh-order term is taken
from Forbes' series trace, because the scheme's aspheric arrangement is a reconstruction the
rays reject; the report says which route it used.

At zero pupil radius the polynomial keeps three terms — `E h^3 + E5 h^5 + tau20 h^7` — and
they are separated by their power of the field alone, so each is measured against traced rays
**on its own** rather than inside a sum where errors cancel. It needs no fit and no model of
the other seventeen coefficients, and it is a check B7 cannot pass, having no field in it.
`docs/distortion-prediction.md` has the measurement, and it corroborates what
`CoefficientInversion` already establishes by the full twenty-coefficient inversion: on a
**figured** design this program's own `tau20` is out by up to a factor of four and Forbes' is
what the rays agree with — which convicts the one part of the scheme that had to be
reconstructed here, because Buchdahl never published it. On spherical designs the two routes
agree to roundoff and the rays back both.

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

## Formats it reads and writes

| Program | Extension |
|---|---|
| ZEMAX / OpticStudio | `.zmx` |
| CODE V | `.seq` |
| OPTALIX | `.otx`, `.opt` |
| OSLO | `.len`, `.osl` |
| Optiland | `.json` |
| LensHH-LT | `.lhlt` |

Every one of them is also written back, by editing the file rather than regenerating it - see
**What goes back into the file** above for what that means and why it is done that way.

Glasses are resolved from AGF catalogs. A material that is not a catalog name is read as a
six-digit glass code (`517642`, or `564.610` in the decimal form some files use), which fixes nd and Vd and
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
- the optimizer: PSD, Hooke-Jeeves and basin hopping over analytic derivatives, with the
  Jacobian checked operand by operand and variable by variable against central differences.
  Spherical surfaces only, on purpose (`docs/optimizer.md`)

There is no GUI. The tool writes plain text and TSV files that you can read, diff and
feed to something else.

## Layout

```
src/AberrationCalculator.Core   models, glass, ray trace, coefficients
src/AberrationCalculator.Core.Ad  the same source, compiled against a dual number
src/AberrationCalculator.Optimize variables, operands, PSD, Hooke-Jeeves, basin hopping
src/AberrationCalculator.IO     one reader per format, and the .lhlt writer
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
