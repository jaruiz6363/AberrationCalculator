# The optimizer

    abcalc <lensfile> --optimize

This repository could always say what a lens *is*. This says what it could be instead.

What makes it worth having rather than being one more least-squares loop is that the derivatives
are **analytic everywhere**, including through the predicted spot — which is a quadratic form in
thirty-seven aberration coefficients reached through some five thousand lines of Buchdahl's
computing scheme.

**It works on spherical surfaces only.** That is a deliberate restriction, and the reasoning is
below.

## The derivatives are exact

There is no finite difference anywhere in this optimizer. No step size is chosen, and no
quantity is ever formed as the difference of two nearly equal numbers.

The mechanism is one source compiled twice. Every file that computes a coefficient — the
paraxial recurrence, the skew ray trace, Buchdahl's scheme and its 155-entry table, the tertiary
cubics — is written in a type called `Scalar` rather than in `double`. In
`AberrationCalculator.Core` that alias **is** `double`, so the analysis computes exactly what it
always computed, to the bit; the 597 tests that guarded it before this work still guard it and
still pass. In `AberrationCalculator.Core.Ad` the *same files* are compiled with `Scalar` aliased
to a forward-mode dual number, and every quantity comes out carrying its own exact derivative.

    src/AberrationCalculator.Core/Numerics/ScalarAlias.cs     Scalar = double
    src/AberrationCalculator.Core.Ad/AdAlias.cs               Scalar = Dual

Nothing is copied. `AberrationCalculator.Core.Ad.csproj` *links* the same `.cs` files, so a
correction to Buchdahl's scheme lands in the derivative the moment it lands in the value, and the
two cannot drift apart.

**Why not .NET generic math.** `INumber<T>` is the modern answer and it is the wrong one here:
there are about 5,450 numeric literals in that chain, and every one would have to be written
`T.CreateChecked(...)`. A struct with an implicit conversion from `double` leaves all 5,450
exactly as Buchdahl and Rimmer wrote them.

**One derivative at a time, not a gradient vector.** A `Dual` is sixteen bytes — a value and one
derivative — so it lives in registers and allocates nothing. The *n* variables are *n*
independent passes, which run in parallel on as many cores as there are. Carrying a gradient
array instead would allocate on every one of the tens of thousands of arithmetic operations per
evaluation.

### How it is checked

`tests/AberrationCalculator.Tests/AnalyticDerivativeTests.cs` compares the analytic Jacobian
against a central difference of the same residuals, for **every operand** against **every
variable**. A central difference is accurate to about the two-thirds power of machine epsilon, so
agreement to five or six digits says the analytic derivative is right and the difference quotient
is the one carrying the error.

The step is sized so that the residuals move by about a part in a million, which matters more
than it sounds: a step that looks small in absolute terms can be enormous relative to a
variable's natural scale, and what comes back is then a secant rather than a derivative.

`PrmsaAgreesWithTheReport` closes the loop the other way: the PRMSA the optimizer descends is
equal to twelve decimals to the PRMSA `abcalc` prints.

### One place the derivative has to be taken deliberately

The ray-surface intersection is a Newton iteration, and an iteration converges on its *value*
long before a naive reading would give the right derivative. `RealRayTrace.Intersect` therefore
applies its final correction **after** deciding it has converged, not before. At a point where
the residual is zero that correction moves the value by nothing and evaluates to
`-(df/dp)/(df/dt)` — the implicit function theorem applied to `f(t, p) = 0`, exact whatever the
derivative was beforehand.

Returning one step early is the obvious optimisation and it is wrong. On a curved surface it
leaves every real-ray derivative one Newton step stale, which is small enough to look like
rounding. On a plane it is total: the flat-surface starting guess is already exact, the loop
returns on its first pass, and a plano surface being bent reports that bending it does not move
the ray at all. The finite-difference check caught it; nothing else would have.

## Spherical surfaces only

The coefficients come from **Buchdahl's computing scheme** — closed-form sums over the paraxial
ray data, with no trace, no fit and no linear solve. It is the fastest route to a seventh-order
coefficient there is, and it is why the predicted spot can be evaluated tens of thousands of
times in a search.

It handles a conic or an even asphere correctly at third and fifth order. At **seventh** order it
does not: that needs an aspheric arrangement Buchdahl never published, so this repository's is a
reconstruction, and real rays reject it — `docs/distortion-prediction.md` measures `tau20` out by
up to a **factor of four** on a figured design. An optimizer descending a quantity that is wrong
by a factor of four is not slow; it is pointed in the wrong direction.

So a figured design is **refused before anything runs**, and a conic or aspheric coefficient
cannot be declared a variable at all — `VariableKind` has no such member, and the merit-function
parser recognises `CC` and `A4` in order to *explain* the refusal rather than report them as a
typo.

**Why refuse rather than fall back.** Forbes' series trace gets the figured tertiary right and
could have been used instead. Carrying two routes means every evaluation asks which one it is on
— and worse, asks whether each surface is figured, in the middle of the arithmetic. Those
questions get answered once, at the door, or they get answered tens of thousands of times a
second for no benefit. Refusing up front keeps the evaluation loop free of them entirely: there
is not one test for figuring anywhere inside it.

That decision also disposes of a hazard rather than merely avoiding it. Buchdahl's chain is full
of sparse skips of the shape `if (coefficient == 0.0) continue;`, which are sound in ordinary
arithmetic and a trap under differentiation: a quantity that is zero *because nobody has moved it
yet* has value nothing and a derivative of something, and dropping its term leaves the value
perfectly right while the gradient goes silently short. With figuring out of scope, every one of
those skips is correct again exactly as written, and the fast path stays fast.

The hazard does not disappear entirely, because it is not only about conics. A **plane** surface
has zero curvature, so the conic term of its sag is skipped — and if that curvature is a variable
being bent, the term has value nothing and derivative `r^2/2`. The two places where that arises,
`Surface.Sag` and `RealRayTrace.SagSlope`, therefore ask `SMath.Vanishes` instead of `== 0.0`,
which is the same comparison in `double` and additionally asks about the derivative in the dual
build. `ACurvatureVariableStartingAtExactlyZeroKeepsItsGradient` is the test.

**The analysis side is unaffected.** `abcalc <lens>`, `--forbes`, `--screen` and
`--distortion-coefficients` handle conics and even aspheres at every order they report, exactly
as before. It is only the optimizer that is spherical.

## Three files

The settings are split by what they describe, not by convenience. The **merit function** says
what the design should be, and can be carried from one design to another. The **variables** say
what may change about it, and mean nothing away from the design they name.

| | `.lhlt` | every other format |
|---|---|---|
| variables, bounds, pickups | in the lens file | `<lens>.var` |
| merit function | `<lens>.mf` | `<lens>.mf` |

A `.lhlt` states its own variables and pickups, so they are read from it and written back to it.
Its own **merit function is deliberately not read** — this tool optimizes a different one — and it
is left untouched in the file. Sidecars are named for the lens *including* its extension
(`triplet.zmx.mf`), so a folder holding `triplet.zmx` and `triplet.seq` keeps their settings
apart.

    abcalc lens.lhlt --optimize              # from the lens and its .mf
    abcalc lens.zmx  --optimize other.mf     # or a merit function you name

## The merit function

Plain text, one operand per line, because a merit function is something a designer argues with.
It wants to be diffed against last week's, commented, and read out loud in a design review.

```
TYPE, WEIGHT, TAR x, INPUTS
TYPE, WEIGHT, MIN x, INPUTS
TYPE, WEIGHT, MAX x, INPUTS
TYPE, WEIGHT, MIN x, MAX x, INPUTS
```

```
PRMSA,   1, TAR 0                        # the predicted spot: no inputs
EFL,   100, TAR 50,          2           # focal length, in wavelength 2
TTL,     5, MAX 60                       # total track
EGT,    10, MIN 1,           2, 4        # glass edges over surfaces 2 to 4
EAT,    10, MIN 0.1,         2, 4        # and the air gaps
DTRGT,  10, MIN 1.5, MAX 12, 2, 4        # diameter-to-thickness ratio
AXC,     2, TAR 0                        # axial colour
LCF,     5, TAR 0,           1.0         # lateral colour at the full field
DISTF,  10, MIN -2, MAX 2,   0.7         # distortion at seven tenths of the field
RY,      1, TAR 0,           7, 1, 1, 0, 1
```

The second field is the weight, and the numbers in that column above are there to show that the
column exists rather than to recommend anything — see **What the predicted spot cannot see** below.

**Targets and boundaries are different things.** An operand with `TAR` is driven to it and weighed
against everything else. One with `MIN` or `MAX` costs *exactly zero* — in the merit and in the
Jacobian — while it is satisfied. It does not pull the design gently toward the middle of its
range; it is simply not there until it is threatened. That is what lets a lens carry a dozen
manufacturability limits without any of them bending the answer.

**Residuals are relative where they can be.** A target operand contributes
`sqrt(weight) x (value - target) / |target|`, so a weight means the same thing whether the operand
is a 50 mm focal length or a 20 micron spot. Where the target is zero — which is what asking for
no spot and no distortion looks like — there is nothing to be relative to and the residual is
absolute.

### Operands, and what each takes

The inputs are positional. Which ones an operand takes is stated in exactly one place —
`OperandInputs` — so the parser, the writer and the error messages cannot disagree about what
`RY, 1, TAR 0, 7, 1, 1, 0, 1` means. **Trailing inputs may be left off** and take their defaults,
so `RY, 1, TAR 0, 7` is surface seven at the reference colour, the full field and the chief ray.

| operand | | inputs |
|---|---|---|
| `PRMSA` | predicted RMS spot over every field and wavelength | none |
| `TTL` | total track, first surface to image | none |
| `AXC` | real axial colour | none |
| `EFL` | effective focal length | `wave` |
| `LCF` | real lateral colour | `hy` |
| `DISTF` | real distortion, per cent | `hy` |
| `EGT` | edge thickness of a glass element | `surface, surface2` |
| `EAT` | edge thickness of an air space | `surface, surface2` |
| `DTRGT` | diameter-to-thickness ratio | `surface, surface2` |
| `PX PY PZ PL PM PN` | paraxial ray position and direction cosines | `surface, wave, hy, px, py` |
| `RX RY RZ RL RM RN` | the same for a real ray | `surface, wave, hy, px, py` |

`hy` is a **fraction of the maximum field**, 0 on axis and 1 at the corner — not an index into the
field list, so a merit function can ask for seven tenths of the field whether or not the design
defines a point there. `px` and `py` are fractions of the pupil radius. `wave` is 1-based in the
order the lens file lists them. There is no zero: to use the design's reference colour, leave
the wavelength off.

`EGT`, `EAT` and `DTRGT` scan the span and produce **one residual per surface** they apply to,
rather than reducing it to the worst one. A minimum over surfaces has a gradient only at whichever
surface happens to be worst, so the optimizer fixes that one, the next becomes worst, and the
search chatters between them. They are measured at the **paraxial beam radius**, `|y| + |ybar|`,
not at the semi-diameter the file declared: a declared semi-diameter is a constant, and would tell
the optimizer that thinning a lens costs nothing at its edge when the beam is still the size it
was.

### What the predicted spot cannot see

`PRMSA` is the obvious thing to ask for and it is not sufficient on its own. Robb's spot is the
variance of the ray intersection **at the Gaussian image plane**, taken **about its own centroid**,
and one coefficient set is computed **per wavelength from that wavelength's own paraxial trace**.
Each of those three words costs the merit function something it might be assumed to have:

| invisible to `PRMSA` | because | ask for it with |
|---|---|---|
| where the image surface is | the spot is referred to the paraxial focus, wherever the file put the image | `PY` at the image surface |
| a focus shift between colours | each wavelength is measured at *its own* focus | `AXC` |
| an image-height shift between colours | a shift of the whole patch does not change its size | `LCF` |

The third row is the same reason distortion is absent from a spot radius, which Robb's own terms
show by vanishing identically. The second is not an approximation either — it is what computing
the coefficients per wavelength means.

The scale of it, on the Kingslake–Kidger double Gauss shipped in `tests/fixtures/lenses`: axial
colour at the start is −0.0990 mm of focus shift, which at f/8 is a blur radius near 0.0062 mm,
while the on-axis predicted spot in blue reads 0.0011 mm. The spot is not wrong; it is answering a
different question. And moving the image surface twenty millimetres leaves `PRMSA` at 0.042589 —
the same to every digit printed.

**So focus the image plane with `PY`, and ask for both colours.**

```
PY,    1, TAR 0, 10, 2, 0, 0, 1          # marginal ray height at the image surface
AXC,   1, MIN -0.15, MAX 0.15            # axial colour
LCF,   1, MIN -0.01, MAX 0.01            # lateral colour, at the full field
```

`PY` here is the paraxial marginal ray (`px` 0, `py` 1) on axis (`hy` 0) at surface 10, the image.
Driving its height to zero puts the image surface at paraxial focus, which is where the spot was
being measured all along — so the operand does not pull the design anywhere, it makes the last
thickness *mean* something. Without it, a variable back focus has exactly zero gradient from
`PRMSA` and will sit wherever it started or wander on whatever else touches it.

Lateral colour is worth stating separately because it is the one most often left out. A run given
`AXC` alone comes back achromatic on axis and can be smeared in colour at the edge of the field,
and nothing in the merit function will have mentioned it.

**This document does not advise on weights.** Where a requirement is a boundary — an edge
thickness, a colour tolerance, a distortion limit — say it as `MIN`/`MAX` and it costs exactly
zero until it is threatened, which needs no weight chosen for it. Where it must be a target, the
right weight depends on the design, and the handful of examples in this repository are not enough
to generalise from. Changing a weight changes the scale of the merit, so two runs weighted
differently are not comparable by their merit numbers at all — judge them on the physical
quantities the report prints.

## Variables and pickups

```
VAR CV 1                                 # curvature of surface 1, unbounded
VAR TH 2 MIN 1.0 MAX 25.0                # a thickness, bounded
VAR CV 4 MIN -0.05 MAX 0.05
PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1  # surface 2's thickness follows surface 1's
```

`CV` and `TH`, and nothing else — see **Spherical surfaces only** above.

**A `VAR` line merges into what is already known.** `VAR TH 2 MIN 1.0` followed by
`VAR TH 2 MAX 25.0` leaves both limits, not the second alone. For a file this program writes the
rule never comes up, since it emits one canonical line per variable; it matters when a *command*
sets one limit, because a command naming only the maximum would otherwise silently discard a
minimum set a moment earlier — the surprise `chmod u+x` exists to avoid. `FREE` drops the bounds
again, since merging means they can no longer be removed by omission.

Bounds are enforced by **reflection — not by clamping, and not by a sigmoid**. A step that would
carry a variable past a limit is folded back inside, as light off a mirror, and the variable keeps
its physical units throughout. Reflection has derivative of magnitude one everywhere: the sign
flips at each fold and nothing else changes.

Both of the usual alternatives lose that, in different ways. A **clamp** pins the variable at the
limit and throws the rest of the step away, so the Jacobian goes on describing a variable that is
not moving; the step is recomputed, clamped again, and the search stalls against the boundary with
a gradient it cannot act on. A **sigmoid** maps the interval onto an unbounded coordinate whose
derivative goes to zero *at* the bound — so a variable driven against a limit stops responding,
and no amount of gradient brings it back when the design later wants it. Either way a bounded
variable becomes numb exactly where the design is most likely to need it, which matters most in
the two places this optimizer lives: constrained descent, and a stochastic search that throws
large steps on purpose.

A variable that touches either end of a **pickup** is refused. Pickups are resolved when a file is
read and are not maintained afterwards, so optimizing one end of a cemented pair would part the
cement.

## The optimizers

### PSD — Dilworth's pseudo-second derivative

The merit function is a sum of squares whose exact second derivative is

    d2phi/dxj dxk = 2 [ (J'J)_jk + sum_i r_i d2r_i/dxj dxk ]

Gauss-Newton, and Levenberg-Marquardt after it, drop the second term entirely and damp with an
arbitrary multiple of the first. That is safe, and it is why LM is hard to break. But the
discarded term is not small on a lens — it is exactly the part that knows an aberration
coefficient is a strongly curved function of a curvature — and throwing it away is what makes a
least-squares run crawl once the residuals stop being small.

Dilworth's insight is that the term cannot be computed but its **diagonal can be estimated for
nothing**, from two gradients the optimizer already has:

    d2phi/dxj^2  ~  ( g_j(k) - g_j(k-1) ) / ( x_j(k) - x_j(k-1) )

Subtracting the Gauss-Newton diagonal from that leaves an estimate of precisely the part that was
dropped, and *that* — rather than a blind multiple of the identity — is what is added to the
normal equations. D. C. Dilworth, "Pseudo second derivative matrix and its application to
automatic lens design," *Applied Optics* **17**, 3372 (1978).

The secant is a difference of two gradients, so both ends have to be real measurements — which is
why this optimizer holds an **exact analytic Jacobian at every iteration and never Broyden-updates
one**. Differencing two approximated gradients measures the update rule rather than the design.
Here both ends are better than real, being analytic rather than differenced, so the estimate
carries no truncation error of its own.

`psd2` clips a negative estimate to zero and can therefore only ever be as bold as Marquardt.
`psd3` keeps the estimate **with its sign**: a negative estimate says the merit falls away along
that variable faster than Gauss-Newton believes, so the right step there is *longer*, and it is
floored at a fraction of the Gauss-Newton diagonal so the matrix cannot go indefinite. That
distinction is the whole of the difference between them.

On the Cooke triplet with eight variables and twelve operands, four hundred iterations:

| method | merit |
|---|---|
| `lm` | −23.4% |
| `psd2` | −31.7% |
| `psd3` | −30.8% |

**What is Dilworth's and what is not.** The per-variable secant curvature, and its use as the
damping diagonal, are his. The safeguards around it — clipping, smoothing between iterations,
answering a rejected step by increasing the damping — are ordinary practice, and the exact
division of labour between `psd2` and `psd3` here is this implementation's reading of the idea
rather than a transcription of his PSD-III. It has not been checked against the original paper.

### Hooke-Jeeves

    --method hj

A direct search that uses no derivatives at all, which sits inside a derivative-exact optimizer
because the two are good at different things. The least-squares step is built on a quadratic
model and is unbeatable while that model holds; it is helpless where it does not — at a boundary
operand's kink, where a residual switches on and its derivative jumps, or in the flat-bottomed
valleys a corrected lens sits in. Pattern search asks a cruder question — *is the merit lower over
there?* — and that question still has an answer in both places.

### Basin hopping

    --hops 300 --chains 8

Local minimisation, a kick, local minimisation again, and a rule for whether to stay. Every local
method finds the bottom of the valley it starts in and stops; a lens problem has a great many
valleys, most of them poor, and which one a design falls into is decided by the starting
prescription rather than by anything about the optics.

Accepting only improvements makes the walk a hill climb over basins and it sticks in the first
decent one, so a worse design is accepted with probability `exp(-dMerit/T)`. The temperature is
**autotuned from the merit the chain is working at**, because a fixed one set for the beginning
accepts everything by the end, and set for the end accepts nothing at the beginning.

Two rescues, and the difference between them matters. A chain that has gone a while without a new
best gets a **long jump** from its own best. Separately, a chain that is *both* stalled *and* far
worse than the global best may be handed that design — and both conditions are required, because
a Metropolis walk goes tens of hops between records while working perfectly well. Reseeding on a
stall alone collapses every chain onto the leader and throws away the independence that made
running several worth it.

Steps are in units of each variable's **natural scale**, computed from the Jacobian as the step
that moves the merit by a set amount, capped by what the parameter can plausibly do on this design
(curvatures against the focal length, thicknesses against the total track). A search that stepped
every variable by the same fraction would be meaningless: a curvature lives near 0.02 per
millimetre and a thickness near ten. This is a use exact derivatives can be put to that a
finite-difference optimizer cannot easily match, since it would have to spend a whole extra
Jacobian to find out how big a step to take.

**Glass is the discrete variable.** There is no derivative from N-BK7 to SF11, only a list, so a
glass change can only be proposed and judged. The continuous variables are re-minimised around the
new glass before the acceptance rule sees it, because a glass that is better in the right shape is
generally worse in the shape that suited the old one.

The list is a **substitution catalogue, named on the command line** — `CoreSet28`, say — and not
the catalogues a design is read through. The two are kept apart on purpose: a search free to pick
from every vendor catalogue at once settles on glasses nobody stocks, because the space is dense
enough that there is always something a shade better a few weeks' lead time away. `CoreSet28` is
twenty-eight glasses that are common, available and spread across the diagram, and it lives in
`catalogs/Substitution`. Nothing in the analysis side reads that folder, and nothing there is
loaded when a lens is opened.

## Commands

Setting up a run takes more than fits on one command line — a merit function is a dozen operands
and a variable list is a dozen more — so the settings are built **a line at a time**, and each
command reads the settings file, changes it and writes it back. A one-shot command line therefore
behaves like a program that remembers, and what it remembers survives a restart, can be opened in
an editor, and can be committed alongside the lens.

**The command IS the file line.** `VAR "TH 2 MIN 1.0 MAX 25.0"` writes `VAR TH 2 MIN 1.0 MAX 25.0`
and that is the whole of the translation. A transcript of commands is a valid settings file and a
settings file is a script of commands — one grammar to learn, one to document, one to test. The
alternative, inventing `--min` to stand for `MIN`, is a second dialect for the same ideas that has
to be kept in step with the first forever.

    abcalc lens.zmx VAR "TH 2 MIN 1.0 MAX 25.0"     declare a variable, or bound one
    abcalc lens.zmx VAR "TH 2 FREE"                 drop its bounds
    abcalc lens.zmx VARLIST                         list them, numbered
    abcalc lens.zmx VARREMOVE 2                     remove number 2

    abcalc lens.zmx PICKUP "TH 2 INDEX 1 SCALE 1 OFFSET -0.1"
    abcalc lens.zmx PICKUPLIST
    abcalc lens.zmx PICKUPREMOVE 1

    abcalc lens.zmx OP "EFL, 100, TAR 50, 2"        add an operand
    abcalc lens.zmx OPLIST
    abcalc lens.zmx OPREMOVE 3                      remove number 3

Several commands may be given in one invocation, and a run may follow them:

    abcalc lens.zmx VAR "CV 1" VAR "CV 2" OP "PRMSA, 1, TAR 0" --optimize --save

A `VAR` line **merges** with what is already there rather than replacing it, so
`VAR "TH 2 MAX 25"` after `VAR "TH 2 MIN 1"` keeps the minimum. Last-wins would be simpler to
implement and worse to use: naming a maximum is not a statement about the minimum, and silently
dropping it is the kind of surprise that costs an afternoon. `FREE` is how you say the opposite
deliberately.

Removing **renumbers**, so a `REMOVE` prints the new listing. A user removing two things in a row
would otherwise be working from numbers that no longer mean what they meant. An out-of-range
number is refused by number — `there is no 9: the numbers run from 1 to 3` — rather than clamped
to the last one, because deleting the wrong thing helpfully is still deleting the wrong thing.


### Asking what a command or an operand does

    abcalc HELP           every command and every operand, briefly
    abcalc HELP VAR       one command: what it does, and an example
    abcalc HELP EFL       one operand: what it measures, what it takes, a line to copy

`abcalc --help` is still the whole command line. `HELP` is the part you need while writing a
merit function, which is where the questions actually are: the inputs on an operand line are
**positional**, so `RY, 1, TAR 0, 7, 1, 1, 0, 1` is unreadable unless something tells you that
the five trailing numbers are surface, wave, hy, px and py — and `HELP RY` does, with an example
in the same shape.

All of it is **generated**, from the same tables the parser reads: the commands from the one
list that also drives the command line and the MCP, the operand signatures from the table the
parser, the writer and the error messages already share. So an operand cannot be added without
showing up here, and a signature cannot change without the help changing with it. A help page
kept by hand beside the thing it describes is a page that is wrong within a release or two.

An operand's example is checked in the test suite by **parsing it** — an example that does not
parse is worse than none, because it will be copied, and then it will fail.

### The base folder

One command is not about any particular lens, and is the only one that can be given on its own —
naming a folder is what stops you having to type the path to a lens in the first place.

    abcalc BASE "C:\lenses\project7"     bare names now mean this folder
    abcalc BASELIST                      show it, and where it came from
    abcalc BASEREMOVE                    forget it

It is **kept until it is changed**, so it holds in the next shell too. In a terminal that is a
convenience — `cd` already does most of it — but over MCP it is the difference between working
and not: an MCP server's working directory is whatever the client started it in, not anything the
user chose and not anything they can change, so without a base every path an assistant passes has
to be absolute. The `base_path` tool sets the same setting.

**Four things can set it, most specific first:** `--dir` on the command line, `ABCALC_DIR` in the
environment, the folder set with `BASE`, and failing all of those the working directory. Each is
easier to change than the one below it, which is the order that lets a stored base be overridden
for one run without being un-set — a quick look at another folder should not cost you your
setting. `BASELIST` says which of the four is in force, because *why is it looking there?* is the
only hard question this raises.

**An absolute path is never re-rooted.** Whatever the base is, `C:\elsewhere\L.zmx` means what it
says; a base that quietly redirected absolute paths would be a trap rather than a convenience.
The base applies to what a run **writes** as well as what it reads — `--saveas better.zmx` lands
in the base folder, not beside the shell.


## Command line

| | |
|---|---|
| `--optimize [mf]` | optimise once; optional merit-function file |
| `--optimize_basin_hopping` | search over basins instead, one design per chain |
| `--method lm\|psd2\|psd3\|hj` | default `psd3` |
| `--iterations <n>` | local iterations, or iterations per hop; default 200 |
| `--hops <n>` | hops per chain; default 300 under hopping |
| `--chains <n>` | default 0 = one per processor |
| `--seed <n>` | default 1234 |
| `--hop-sigma <s>` | size of a hop, in natural steps |
| `--glass_substitution <catalogue>` | let the hopping try glasses from that catalogue |
| `--dir <path>` | take bare names against this folder, for this run only |
| `--save` | overwrite the lens that was read |
| `--save <folder>` | under hopping, where the per-chain designs go |
| `--saveas <path>` | write the result somewhere new |

**Nothing is overwritten unless overwriting is asked for by name.** With none of those, the
original is left alone and the result is written beside it as `<name>.optimised.<ext>`, so a run
that made things worse costs nothing but the time — and the report says plainly when that is what
happened. `--save` with no path is the destructive form, and it is destructive only because it
was typed.

Basin hopping is **refused without `--save <folder>`**. It produces one design per chain, and
which of them is interesting is a judgement only a designer can make; keeping the single lowest
merit and discarding the rest throws away most of what the run paid for. Each chain's design is
written as `<name>.chainNN.<ext>` with its own `.mf` and `.var` beside it, so any of them can be
picked up and worked on further, and one report covers the run.

## Saving back

The optimised design goes back **in the format it came from**, by editing that file rather than
regenerating it. Only curvatures, thicknesses and glass names ever change, and they go back in
the file's own units — a design opened from a file written in inches returns in inches.

Editing rather than regenerating is the whole point. This program recognises twenty-three .zmx
directives and a real `.zmx` has many times that in solves, coatings, apertures, tolerances and
multi-configuration data; a writer that rebuilt the file from what it understood would quietly
delete the rest of somebody's design. So the original is read, the three things the optimiser can
move are moved, and every other byte is left alone — including the encoding and the line endings,
which for a `.zmx` are UTF-16 and CRLF. `PatchingAZmxKeepsItsEncodingAndChangesOnlyWhatMoved`
holds it to exactly one changed line.

The same reasoning governs the `.lhlt`: a merit function, vignetting settings and whatever the
next version of the program that wrote it adds all survive being written through this one.

**Every format this reads, it writes back**: `.lhlt`, `.zmx`, Optiland `.json`, CODE V `.seq`,
OPTALIX `.otx`/`.opt` and OSLO `.len`/`.osl`. Each needed working out separately, because each
says the same three things differently:

| format | shape | a plane | a property with nothing to say |
|---|---|---|---|
| `.zmx` | `CURV` curvature | curvature 0 | written anyway |
| `.json` | `radius`, plus an **absolute z** per surface | a very large radius | written anyway |
| `.seq` | radius, **positional** in the surface line | radius `0` | `AIR` in the third field |
| `.len` | `RD` radius | no `RD` line at all | **omitted** |
| `.otx` | `CUY` curvature | curvature 0 | **omitted** |

The two that omit are the awkward ones: bending a plane or substituting glass into an air space
means *adding* a line that was never there, in the block it belongs to, and the insertions are
applied from the bottom of the file upwards so no earlier position shifts under a later one.
Optiland is awkward differently — it records where each surface *is* rather than how far it is
from the one before, so moving one thickness moves everything after it, and it writes `Infinity`
as a bare word, which is not valid JSON and has to be carried through a parse without being
turned into a number.

`EveryFormatCarriesTheSameMoveBackUnchanged` runs the same three edits — a curvature, a
thickness, a glass — through all five and reads each back, which is the check that they agree.

The MCP server offers the same as `optimize`, taking the merit function inline as text, so an
assistant can compose one without writing a file. It writes nothing unless given `save_to`.

## What this does not do

- **Conics and even aspheres are out of scope**, for the reason given above. The design is
  refused before the run; the analysis side still handles them everywhere it did.
- **A variable that touches a pickup is refused.** Pickups are resolved when a file is read and
  are not maintained afterwards, so optimising one end of a cemented pair would part the cement.
  Constrain the pair with operands instead.
- **Glass moves only in the hopping.** Model glass `nd`/`Vd` are not continuous variables.
- **PRMSA is the *predicted* spot**, and inherits the accuracy of the prediction —
  `docs/spot-prediction.md` measures that at a few per cent on the spherical designs this
  optimizer accepts. On a design the series does not describe well, the optimizer will faithfully
  minimise a quantity that is not quite the spot.
- **Semi-diameters are not re-solved.** The clear aperture used by the edge and ratio operands is
  the paraxial beam, which follows the design, but vignetting is not modelled.

## Either conjugate

Every operand works with the object at infinity or at a finite distance. `PRMSA` and the paraxial
operands always did; the real-ray ones — `RX`–`RN`, `LCF`, `AXC`, `DISTF` — used to refuse a
finite conjugate, and that refusal was in the wrong place.

`RealRayTrace` propagates a ray given at surface one's vertex plane and **never asks where the
object is**. Only the *aiming* did: it built the ray direction from the field angle alone, which
is true of a collimated beam and of nothing else, because light from a finite object leaves at a
direction that depends on which pupil point it is heading for. Given the object distance both are
the same construction — the line through two known points — so the launch now takes the object
point, the pupil point, and draws the line between them.

The test that this is the *right* line, rather than merely a line, is that a real ray must
converge on the paraxial one as it shrinks toward the axis: scale field and pupil together and
the aberrations fall as the cube while the paraxial part falls linearly. A launch wrong at first
order — object on the wrong side, height read in the wrong convention, pupil at the wrong
distance — could not converge at all. `FiniteConjugateRayTests` measures that for both field
conventions, object height and object angle.

One caller still assumes infinity and now says so itself. `CoefficientInversion` measures the
field as `tan(theta)` and subtracts a paraxial height of `efl*tan(theta)`, neither of which means
anything at a finite conjugate; it used to be protected by the trace refusing, and it now carries
its own guard. That is the honest place for it — the assumption was always that method's, not the
tracer's.
