# What is verified, and by what

This program computes quantities that are easy to get wrong and hard to check by eye. This
records what has actually been established, by what means, and what has not.

The working log those checks were made in is kept outside this repository, with the author's
notes: it transcribes source material verbatim to reason against, which belongs where the
sources are and not in an MIT-licensed repository. What follows is the standing position
rather than the history of arriving at it.

## The order of evidence

Not every check is worth the same, and they are listed in the order they carry weight.

**1. Buchdahl's own printed numbers.** Paper III works Table I through for a specific
triplet. `BuchdahlPublishedTableTests` reproduces it entry by entry, and his published
totals for the tertiary. This is the oracle, and it is what found the errors in t100–t108.

**2. Closed-form analytic surfaces, which need no other program at all.** A parabolic mirror
images infinity onto its focus with no spherical aberration at any order, so every order must
cancel term for term. A single conic surface can be traced analytically and expanded as
`eps = a3 y^3 + a5 y^5 + a7 y^7 + ...`, giving the third, fifth and seventh orders as numbers.
`ParabolicMirrorTests` and `ExactConicSurfaceTests`. **This is what establishes the aspheric
third and fifth order**, and it had to: nothing else available computes them.

**3. Inverse real ray tracing.** `CoefficientInversion` recovers coefficients from the
landings of real traced rays, by scaling and an odd-polynomial fit. It is this repository's
own code. Eight test files use it as their reference, including every aspheric one.

**4. An independent implementation** of the third and fifth order, written in C++ by this
repository's author directly from the monograph — a different lineage from the same source.
It does not implement aspherics.

**5. Forbes' series trace**, from a separate published paper with no shared code. It agrees
with Buchdahl's scheme on all twenty tertiary coefficients to 2E-13 at both conjugates.

**And a cross-check anyone can repeat.** An independent commercial implementation's Seidel
analysis and the FIFTHORD macro agree with this program — 586 coefficient values across seven designs, worst residual 1.1E-12,
and 30 of 30 third-order values at a finite conjugate. That is recorded because a reader with
such a program can confirm it without trusting anything here. It is not the authority: neither
computes the seventh-order set this program exists for, so neither could be.

**That cross-check reaches the per-surface SPLIT, not only the totals.** The recorded reference is
what FIFTHORD reported for these designs, and it carries three things per fixture: what each
surface generates ON ITS OWN, the isolated ASPHERIC contribution of each figured surface, and the
system totals. `BuchdahlCoefficientsTests.MatchesTheRecordedReference` holds all three to 1E-9
relative on every build, over seven designs. So the intrinsic and figuring parts a merit function
can now target are checked against a second implementation of the same published method, not only
against their own sum.

**That cross-check has a measured boundary**, and it is the r-squared deformation term: every one
of the seven fixtures has `PARM 1` zero, and the agreement above holds only there. See
*The r-squared term* below.

The induced part is not in the reference, because FIFTHORD does not print it. It is reached the
other way about: the macro's totals are NOT the sum of the surface rows it prints, and
`TotalsAreNotTheSumOfTheIntrinsicParts` requires that gap to exist. Differencing a FIFTHORD run by
hand recovers the induced values this program reports to about 1.5E-4, which is what six roundings
of five printed digits costs - worth knowing, and weaker than everything above it.

**One statement in that reference is about a term being ABSENT.** FIFTHORD leaves the Petzval
column blank in every aspheric block, which says the figuring contributes nothing to the Petzval
sum - the sum depends on the vertex curvature and the indices, and a figured surface has the same
vertex sphere as the sphere it was figured from. Two implementations agreeing that a term is
absent is worth more than two agreeing about a value, and for a long time this one was not checked
at all: the fixtures simply omit `Pi` there, and a comparison written to skip what a fixture does
not state read the blank as no data rather than as zero. `TheFiguringContributesNothingToPetzval`
now asserts it.

## Standing results

| what | result |
|---|---|
| Table I, t1–t155, per surface | matches the reference implementation to 7E-16 |
| the twenty tau, Buchdahl against Forbes | 20 of 20 to 2E-13, both conjugates |
| seventh-order spherical aberration, two routes | identical to every printed digit |
| third order against an independent Seidel analysis, finite conjugate | 30 of 30 |
| fifth order against FIFTHORD, finite conjugate | 18 of 18 totals |
| the per-surface split | intrinsic and figuring against the FIFTHORD reference, 1E-9 over seven designs, every build; the figuring's Petzval asserted absent; and internally, intrinsic + figuring + induced = total to 1.8E-14 |
| E, E5 and tau20 against traced chief rays | each to under one per cent wherever the two routes agree, at both conjugates |
| the suite | over a thousand tests, and everything they read is in this repository |

## The aspheric arrangement, and how it was established

This section used to live under **What is not established** below, and it is worth saying why it
no longer does. Buchdahl gives the aspheric **scheme** — Secs. 65-66 for the D and L split,
(85.2)-(85.5) for the two passes — so the method was never in question and nothing about it was
guessed. What he never published is the arranged **table** for it, the way Table I arranges the
spherical case, and that arrangement had to be re-derived. It has been, and what follows is what
that rests on.

**The arrangement is established, and the exception that used to qualify this is closed too.** The
reconstruction is a separate routine, `BuchdahlAsphericScheme`, and
`TertiaryCoefficients.Attach` sends every figured system there; spheres keep Buchdahl's own
arrangement in `BuchdahlTableI`, bit for bit. Its default arrangement is four things, each
derived and each gated before it was adopted:

| piece | source | gate |
|---|---|---|
| barred q accumulations, members 1-5 | identities, M Sec. 22 | (22.42) against (22.53), 1E-13 on figured systems |
| barred q accumulation, member 6 | the dual run, paper XII Sec. 6 (swap the rays, negate the indices) | reproduces members 1-5 from the identities to 2E-13, and all six on spheres to 6E-13 |
| the figuring's D half in the hat pass | (60.3), (85.3) | in the secondaries AND in the M entries built from them |
| the D half on an exactly flat surface | its curvature limit | continuous with the R = 1e10 twin |
| a figured flat facing collimated light | the same formulas in Laurent series arithmetic, curvature as the variable | reproduces the double route on regular surfaces to 1E-10; Forbes on the flat to 1.2E-9 |

**Corroborated a second time, outside this program.** The same arrangement is transcribed into
`macros/BUCH7_ASPH.ZPL`, which runs inside OpticStudio and shares nothing with this code but the
published equations. On a lens of spheres it reproduces `BUCH7.ZPL` entry for entry — 155 Table I
entries over 9 surfaces, 1170 numbers, and all twenty tau. On figured designs it reproduces
`FORBES.ZPL` on all twenty tau, and the recorded FIFTHORD reference on all eighteen third- and
fifth-order totals, on a conic singlet, a conic carrying r⁴, r⁶ and r⁸ together, and a triplet
with two figured surfaces where one induces on the other. A transcription agreeing to the
printed digits is not proof of the arrangement, but it does exclude a whole class of
implementation error in this code, since a shared bug would have to have been made twice in two
languages.

Against Forbes, all twenty tau: between 2E-13 and 2E-10 relative on every figured design in the
ladder and on the three aspheric triplets (`BuchdahlAsphericSchemeTests.TheAsphericRoutineAgreesWithForbes`),
where the arrangement as it stood before was out by 17 to 467 per cent, with 6 to 19 of the twenty
beyond one per cent. Against real rays both routes now sit at the rays' own floor
(`ForbesCoefficientsTests.BothRoutesAgreeWithRealRaysOnFiguredDesigns`), and `tau20` comes back
from the traced chief rays to within the recovery's scatter.

**A figured flat facing collimated light** (`Ladder2_FlatFigured`, a corrector plate in a parallel
beam) needed one more step. There the marginal incidence is identically zero, q is infinite, and
the finite coefficients arrive only after terms carrying different powers of q cancel - the
arrangement was right (bend the surface to R = 100 and it agrees with Forbes to 1.6E-12) but
the flat branch dropped the figured tertiary and a numerical limit reached only 4.5E-4. So
`TertiaryCoefficients.Attach` runs such a system through `AberrationCalculator.Core.Series`, the
same source files compiled in Laurent series arithmetic with that surface's curvature as the
variable, and reads the answer at e^0. It is used only when it vouches for itself - two
truncations agreeing, nothing below the lowest carried order, negative orders cancelled - and
the design now agrees with Forbes to 1.2E-9, where it was 710 per cent out
(`FlatCollimatedSeriesTests`). That was the one exception, and it is closed: there is no figured
case left that this program declines to compute or computes differently from Forbes.

**And it is differentiated too, which it was not at first.** The series route lived in Core alone,
so the OPTIMISER refused such a design: in the differentiating build the call had no body and
compiled away, leaving a right value beside a silently wrong derivative. It now runs in
`AberrationCalculator.Core.Series.Ad`, the same sources again with `Scalar` a dual number whose
value and derivative are each a Laurent series - so value and derivative come out of one run, and
reading them off independently is legitimate because extracting the e^0 coefficient is linear.
Checked two ways on `Ladder2_FlatFigured`: the differentiated route reproduces the double route on
all thirty-seven coefficients, and the derivatives of tau2 to tau20 match central differences with
respect to a curvature, a thickness and the corrector's own r^4 term
(`FlatCollimatedDerivativeTests`). The first of those is the one that matters, because the second
compares a derivative against differences of its OWN route and would pass even if that route had
drifted away from the value the analysis reports.
**The rest of this section records how the defect looked while it was open**, and is kept rather
than deleted because how an error was found is worth more than the fact that it was. Everything
in it is in the past tense as a matter of fact, whatever tense it is written in.

Two cautions for anyone measuring this, both of which cost time here. Normalising the error by
the largest coefficient in the set hides it almost entirely — a small coefficient wrong by five
times is nothing beside the largest, and the design that does exactly that reports as 1.7 per
cent. And a predicted spot cannot see it at all; see below.

**A second ray route says the same thing about `tau20`.** The inversion above (evidence 3) is
the general one. This is a narrower instrument over the same rays: at zero pupil radius the
transverse polynomial has three terms separated by their power of the field alone, so `E`,
`E5` and `tau20` fall out by differencing, with no basis, no least-squares solve and no model
of the other seventeen coefficients. The two agree on `tau20` to between 0.03 and 1.2 per cent
across the figured fixtures. Wherever the two routes agree the
rays agree with both, four figured designs included; wherever they disagree by more than the
recovery's own error bar the rays land on Forbes, six designs, no exceptions, over gaps from
11 per cent to a factor of 3.8. One further design has a 3.2 per cent gap against a 5.1 per
cent error bar and settles nothing; it is reported as no verdict. `E` and `E5`
come back exactly throughout, which is what confines the reading to the seventh-order aspheric
arrangement. Which route is the wrong one is something a disagreement between the two could
not establish.

**On a purely spherical system the two never disagree, and this was checked rather than
assumed.** Across the seven all-spherical fixtures and the finite-conjugate triplet, the worst
departure over all twenty tau is 3E-15 of the largest of them — roundoff — on seven of the
eight, and 2E-08 on the near-degenerate flat fixture. The rays return `tau20` on all eight. So nothing above touches the part of the scheme Buchdahl actually published: what
it convicts is this repository's reconstruction of the aspheric arrangement he did not, and it
convicts it in one coefficient of the twenty. See `distortion-prediction.md`.

**A predicted spot cannot settle it, and this is measured rather than assumed.** On that same
design the two routes agree on the spot to one part in ten thousand while disagreeing on the
coefficients as above, because the disagreement sits in the smallest terms and a spot barely
weights them. Predicted-versus-traced spot agreement would have certified a tau15 that is
wrong by five times and points the wrong way. It is not used as a correctness metric here,
and `spot-prediction.md` says what it is used for instead.

## The r-squared term, and where the FIFTHORD cross-check stops

An even asphere's first coefficient, `PARM 1` in OpticStudio, multiplies r-squared. **It is not
figuring.** A surface of curvature `c` carrying `A2` is exactly the sphere of curvature `c + 2 A2`
carrying whatever is left over, so the term changes the surface's POWER and with it the focal
length of the system. Everything from r^4 upward is genuine departure from a sphere; r^2 is a
radius in disguise.

That distinction is the whole of this section, because programs differ on whether they notice it.

### What was measured

`tests/fixtures/coefficient-reference/F8_r2_conic_a4_a6_a8` exists for this. It is
`F3_conic_a4_a6_a8` with `PARM 1 = 1.0E-04` added and nothing else changed. The term is not a
perturbation: it moves the effective focal length from 78.037505 to 77.419426, the F/number from
3.9019 to 3.8710, `B` by three per cent and `B7` by 0.43.

| | notices `PARM 1` | EFL it works at |
|---|---|---|
| OpticStudio's own first-order data | yes | 77.419426 |
| this program | yes | 77.419426 |
| `macros/BUCH7_ASPH.ZPL` | yes | 77.419426 |
| FIFTHORD | **partly** | mixes both |

**This program is correct here, and it is not this program's own opinion of itself.** Four things
that share no arithmetic agree on that design: the Buchdahl aspheric scheme, which folds the term
into the vertex curvature and re-measures the figuring from that sphere; the Forbes series trace,
which does no folding at all and simply carries `A2` as the first coefficient of the sag series;
the ray inversion, which recovers the coefficients from real traced rays and knows nothing of
either; and OpticStudio's own focal length. The first three agree on the twenty tau to nine
significant figures, with the rays at their own ladder floor of 3E-06.

**BUCH7_ASPH agrees with this program to every printed digit on that file**, all eighteen totals
and all twenty tau. Its EFL comes from `GETSYSTEMDATA`, so that agreement also establishes the
thing the fixture was built to test: OpticStudio's paraxial data accounts for `PARM 1`, and the
macro's folded vertex curvature is therefore consistent with the pupil and focal length it reads
back.

### Why FIFTHORD is not accurate when A2 is non-zero

It was expected to return the answer for the lens with the term removed. **It does not.** Its `B`
comes to 2.5314E-02, against 2.7696E-02 with the term and 2.6888E-02 without it - neither. The
reason is that it takes its paraxial ray data from OpticStudio, which INCLUDES the r-squared
power, and then computes each surface's contribution from the BASE curvature. The two halves
describe different surfaces.

Two measurements locate that rather than infer it. On surface 2 of the fixture, which carries no
figuring, FIFTHORD and this program agree on every third- and fifth-order term to all five printed
digits - so nothing general is wrong with either. On surface 1, which carries the term, the
Petzval contribution differs by exactly the ratio of the two curvatures:

    FIFTHORD    -5.2159E-03  x  (0.0202 / 0.0200)  =  -5.268059E-03
    this program                                     -5.268041E-03

to 3.4E-06, which is the limit of FIFTHORD's five printed digits. Petzval depends on the surface
curvature and the indices alone, so it isolates which curvature each program used and nothing
else. The totals then differ by -8.6 per cent on `B`, -4.4 on `N1` and -1.65 on `B7`, while `E`,
`E5`, `N2` and `M2` are unmoved - the signature of a curvature error rather than of a dropped
term.

### The author flags it, and the flag understates it

The macro's header says:

> "Zemax uses a second-order aspheric deformation coefficient which is not used in this treatment.
> It may appear in a future version."

That is candid and it was written in 1998, and none of this is a criticism of a macro given away
freely. But it describes an omission, and a reader would reasonably take it to mean the result is
the one for the surface without that term - an incomplete answer, and a defensible thing to hand
back. What the macro actually returns is an inconsistent one: a lens whose rays come from one
surface and whose contributions come from another. **A note strong enough for what happens would
have to say that the coefficients are not to be used at all when `PARM 1` is non-zero**, rather
than that the term is not used.

### What this does and does not disturb

**It does not touch the 586-value agreement.** Those seven fixtures have `PARM 1` zero, which the
README beside them records as deliberate, and the agreement there stands at a worst residual of
1.1E-12. This section says where that agreement stops applying, which is a boundary on a
cross-check and not a defect in either program.

**Nothing in this repository needs fixing.** The term is read, folded, and carried correctly, and
`AsphericR2TermTests` already pins the equivalence of the two ways of writing the same surface.

**And the macro pair has now been run against each other on `F8`.** `FORBES.ZPL` reproduces
`BUCH7_ASPH.ZPL` on that file to every printed digit - all eighteen totals and all twenty tau,
`tau1 = 2.515701E-04` through `tau20 = -4.928042E-09` - and both agree with this program. The two
macros share no arithmetic and, on this term specifically, take opposite approaches: FORBES puts
`A2` straight into the sag series as the coefficient of `p` and folds nothing, while BUCH7_ASPH
folds it into the vertex curvature and re-measures the figuring from that sphere. Agreement
between those two is worth more than agreement between two implementations of the same treatment.

So on the r-squared term the count is five: two macros, two C# routes and real rays, against
FIFTHORD alone.

**And the refutation is now direct rather than inferred.** Both macros were also run on
`F3_conic_a4_a6_a8`, the same lens without the term, and they reproduce this program's numbers for
it exactly - `B = 2.688792E-02`, `tau1 = 2.526552E-04`, `tau20 = -4.894869E-09`. So the A2-free
answer is in hand and measured, and FIFTHORD's 2.5314E-02 on `F8` is not it. It was expected to
return that answer; it returns a third thing.

**Both macros now refuse the FIFTHORD comparison when they meet a non-zero `PARM 1`**, in place of
the invitation they used to print unconditionally - an invitation that would have sent a reader
hunting a fault in the wrong program. The guard was exercised both ways on the pair above: the
refusal on `F8`, the original text on `F3`.

## Figuring beyond r^8

The claim made about r^10 and above is stronger than "it is ignored": those terms **cannot
appear** in anything this program computes. A deformation `A_n r^n` first contributes at wave
order `n`, which is transverse order `n-1`, so r^4 reaches the third order, r^6 the fifth, r^8 the
seventh - and r^10 reaches the NINTH and nothing below it.

**Both halves of that are tested, and either alone would be worthless.** That the coefficients do
not move is half; on its own it would pass equally against code that threw the term away before it
reached the sag - which is a real bug, and the one the r^2 term actually had once. So
`AsphericBeyondR8Tests` also requires that the surface genuinely is different:

- **Nothing moves.** Adding r^10, r^12, r^14 and r^16 to a surface that is figured already leaves
  every coefficient and the focal length **bit-identical**, on four designs. In the Forbes route
  bit-identity holds even on a surface that was spherical, because its figure is a truncated power
  series in `p = r^2` and r^10 falls outside the truncation rather than being filtered out of a
  list - there is no special case to leave.
- **But the lens really has changed.** A real marginal ray lands elsewhere.
- **And the displacement is ninth order.** Halving the pupil divides it by 512, measured, not by
  the 128 an r^8 term would give or the 2048 of an r^12 one. That is what "it cannot reach the
  seventh order" means when it is put as a measurement rather than an assertion.

**And the rule is a rule, not a convenient exclusion.** The same pair of traces shows `r^10` dead
at the seventh order and live at the ninth: `ForbesTrace.Figure` builds the sag to `degree + 1`, so
degree 3 carries `r^2` to `r^8` and degree 4 carries `r^10`. The highest deformation that can reach
transverse order `2m+1` is `r^(2m+2)`, which is exactly what that line admits, so it is right at
every order rather than tuned for this one. At degree 3 an added `r^10` moves no monomial of `S` or
`T`; at degree 4 it moves the degree-four part and still nothing below it. So the term is excluded
where it cannot contribute and included where it can, by the truncation itself.

**One thing does change, and it is the route rather than the answer.** `Surface.IsFigured` is true
for any aspheric term, r^10 included - correctly, since the surface really is aspheric - and that
flag decides whether the tertiary coefficients come from the spherical scheme or the aspheric one.
So adding r^10 to a sphere moves the design onto the other route, and the two agree to one or two
parts in 1E16 rather than to the bit. The test says so in that case instead of demanding a
bit-identity that would be asserting something untrue about the implementation.

**The report now says it too.** An r^10 row in the aspheric table is marked `takes no part`, with a
note below giving the reason and the consequence: the surface is still that shape and real rays
still see it, so a design whose figuring lives mostly in those terms is not described by the
coefficients, however strong its aspherics look. Both ZPL macros already warned; the C# report
printed the row and said nothing, which invited the reader to assume it had gone in.

## What is not established

**How far the seventh order reaches.** It is a property of the lens and not a number. Of five
designs measured, one is described by third order alone, two need the full seventh to reach a
per cent, one is not well described at seventh, and one is not described at all. See
`spot-prediction.md`.

**Lateral color is not reachable from these coefficients at all, and this was measured rather
than assumed.** It is the chromatic difference of the chief ray's height, so it looks like the
distortion terms differenced between wavelengths — but distortion coefficients are referred to
each wavelength's OWN paraxial image plane, and lateral color is defined at one shared plane.
Carrying a chief ray between those planes needs its ANGULAR aberration, about 11 mrad at the
corner of the Cooke triplet, and the transverse polynomial has no angular term. The size of
what is missing settles it: between its own focus and the shared plane that ray moves 9.4E-02
mm, while the whole lateral color there is 4.0E-04 — a factor of two hundred. `tau20` and its
nineteen companions cannot answer this question and no care with them will.

It can be reached by developing the Forbes series to the shared plane, whose output base plane
is an input, and that was built and measured before being discarded: exact over the inner half
of the field, the wrong SIGN in the outer quarter at seventh order, and needing degree 7 - the
fifteenth order - to hold the corner to ten per cent. The reason is that lateral color is a
small residue of large cancelling terms, so what governs it is not the order but how completely
two errors far larger than the answer cancel. It was discarded because tracing two chief rays
gives the same figure exactly, in 4.7 microseconds against 142 milliseconds. Recorded here so
that the next attempt starts from the measurement rather than from the idea.
