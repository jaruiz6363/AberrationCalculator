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

## Standing results

| what | result |
|---|---|
| Table I, t1–t155, per surface | matches the reference implementation to 7E-16 |
| the twenty tau, Buchdahl against Forbes | 20 of 20 to 2E-13, both conjugates |
| seventh-order spherical aberration, two routes | identical to every printed digit |
| third order against an independent Seidel analysis, finite conjugate | 30 of 30 |
| fifth order against FIFTHORD, finite conjugate | 18 of 18 totals |
| the per-surface split | intrinsic + figuring + induced = total, to 1.8E-14 |
| E, E5 and tau20 against traced chief rays | each to under one per cent wherever the two routes agree, at both conjugates |
| the suite | 597 tests, and everything they read is in this repository |

## What is not established

**The aspheric tertiary arrangement — now established, with one exception.** Buchdahl gives the
aspheric scheme in Sec. 85 of the monograph but never published the arranged table for it. This
program's reconstruction of it is a separate routine, `BuchdahlAsphericScheme`, and
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
(`FlatCollimatedSeriesTests`). The paragraphs below record how the defect looked while it was
open.

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
