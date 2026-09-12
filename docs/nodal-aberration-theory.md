# Nodal aberration theory: what it would take here

Status: **stages 1 to 3 implemented**; stages 4 and 5 are still proposal. This file records what
was read, what the theory needs, what this program already supplies, and the order the work is
worth doing in.

| stage | what | state |
|---|---|---|
| 1 | vector algebra, the two frames, Seidel to `Wklm` | **done** - `Core/Nat/Vec2.cs`, `Conventions.cs`, `WaveCoefficients.cs` |
| 2 | Gu's as-built tolerance sensitivity, as a merit operand | **done** - `Core/Nat/Sensitivity.cs`, operand `ASBLT` |
| 3 | perturbation state, sigma, the field, the nodes, `--nat` | **done** - `Core/Nat/SigmaVector.cs`, `NatField.cs`; validated against Thompson (2009) Tables 4 and 5 |
| 4a | Zernike astigmatism and coma overlays | **done** - `ZERN` in `.align`, wired into `NatField`; Schmid's diagnostic tested |
| 4b | trefoil and higher overlays | proposal - needs stage 5 |
| 5 | fifth order | proposal; no longer blocked - the trilogy is in hand |

**Where a perturbation comes from: the `.align` sidecar**, uniform across all six formats.

    abcalc lens.zmx TILT "2 X 0.115"        degrees
    abcalc lens.zmx DEC  "2 Y 0.05"         lens units
    abcalc lens.zmx ZERN "1 Z5 0.0001"      Fringe Zernike, as surface SAG
    abcalc lens.zmx ALIGNLIST
    abcalc lens.zmx --nat

It is a file of its own rather than part of the lens, and the reason is not tidiness. A
perturbation is a statement about one BUILT INSTANCE - "this surface ended up fifty microns off"
- not about the design. Writing it into the prescription would corrupt the design record with a
build error, and for a `.lhlt`, which keeps its own variables inside the lens file, that is
exactly where the variables rule would have put it. Deleting `.align` restores the nominal
design exactly, which is the property an alignment engineer wants while trying hypotheses and
discarding them.

Two tests hold the line: applying an alignment changes no curvature, thickness, conic,
semi-diameter or glass, and the paraxial trace of a perturbed system is the nominal trace to the
bit. Nodal aberration theory treats the perturbation as a departure FROM that trace, not as
something the trace should carry.

**Still not done: folding a `.zmx` `COORDBRK` into the sidecar.** A coordinate break tilts
everything downstream until an inverse break, and guessing at the arrangements would mis-model a
design silently. The perturbation reaches the model through `.align` instead, which is the one
route that works for every format including the three that have no native tilt concept at all.

Nodal aberration theory (NAT) is the third-order aberration theory of a system whose
components are rotationally symmetric but whose *assembly* is not. Shack's observation, worked
out by Thompson, is that each surface still contributes exactly the rotationally symmetric
aberration field it always did - only that field is no longer centred on the field centre. It
is displaced by a vector, and the total is the sum of displaced fields rather than concentric
ones. The consequence is that the zeros of an aberration - the **nodes** - leave the axis and
scatter through the field in patterns characteristic of *what* went wrong. A total RMS number
says the telescope is soft. The node geometry says whether the secondary is decentred or the
primary is astigmatic, which is a different kind of answer.

## Why this program is a good host for it

**NAT consumes exactly what this program already produces, and nothing it does not.** Every
third-order NAT quantity is built from the paraxial marginal and chief rays and the per-surface
Seidel sums. `ParaxialTrace` gives the first, `SeidelCoefficients` the second, both per surface
and per wavelength.

The conversion to Thompson's wave coefficients is exact and is arithmetic:

    W040 = S1/8      W131 = S2/2      W222 = S3/2      W220P = S4/4      W311 = S5/2
    W220M = W220P + W222/2

That this is right can be checked without leaving the repository. Gu's Eq. (9) is

    W131 = -(1/2) i ibar y n^2 d(u/n)

and `SeidelCoefficients.cs` computes `s2 = -A * Abar * y * dUoverN` with `A = n i` and
`Abar = n ibar`, which is `-n^2 i ibar y d(u/n)`. So `W131 = S2/2` term for term, and the same
substitution against Gu's Eq. (14) gives `W222 = S3/2`. No notation bridge has to be invented,
which is not a thing that can be said of the fifth order - see **What this would not do**.

NAT then replaces the field vector `H` with a per-surface **effective field vector**

    H_Aj = H - sigma_j

and re-sums. An aligned system has every `sigma_j = 0` and the sums collapse to Seidel
identically. That collapse is the first test to write, and it can be exact to the bit.

### It is a different axis from the one this program already travels

This program generalises along **order** - third, fifth, seventh - with rotational symmetry
assumed throughout. NAT generalises along **symmetry**, with the order held at third. The two
are orthogonal and they compose: NAT's fifth-order extension is Thompson's multinodal trilogy,
and it wants exactly the per-surface fifth-order coefficients this program already computes, in
a notation it does not yet speak.

## What is missing here

1. **No vector aberration algebra.** NAT is written in a two-dimensional vector product that is
   complex multiplication in disguise, with a conjugate whose definition is not the obvious one.
   See below.
2. **No perturbation state.** `Surface` has no tilt and no decentre. `ZmxReader.cs` recognises
   `COORDBRK` and parks its five values in `Surface.Parameters[0..4]`; `ParaxialTrace` gives the
   surface zero power and otherwise ignores it. Nothing downstream can see a misalignment.
3. **No figure or freeform overlay on a surface.** Needed for both the Schmid (figure error) and
   Fuerschbach (freeform) halves of the theory.
4. **`SeidelCoefficients.cs` is outside the compile-twice scheme.** It is written in `double`
   rather than `Scalar`, and it is not among the `<CoreSource>` entries of
   `AberrationCalculator.Core.Ad.csproj`. Any NAT quantity that is to reach the optimiser has to
   be on the dual-number path, and putting it there is the whole cost: the mathematics is
   algebraic in `y, ybar, u, ubar, n, c`, so it differentiates for free once the arithmetic is
   aliased. This is a one-line `.csproj` change and a mechanical `double` to `Scalar` pass, and
   it is a prerequisite for Stage 2.

## The vector algebra, and the convention trap

Two-dimensional vectors multiply as complex numbers:

    a . b   = ax bx + ay by                                  scalar product
    (a b).x = ay bx + ax by                                  vector multiplication
    (a b).y = ay by - ax bx

with the two identities the NAT expansions rely on, from Thompson's Appendix A and quoted as
Eq. (11) and Eq. (23) of Fuerschbach 2014:

    A . (B C)          = A B* . C
    2 (A . B)(A . C)   = (A . A)(B . C) + A^2 . (B C)

**The conjugate negates the x component, not the y one:**

    B* = |B| exp(-i phi) = -Bx xhat + By yhat

because NAT measures its angle `phi` **clockwise from yhat**, so `y` plays the role of the real
axis. This is Fuerschbach 2014 Eq. (12) and 2012 Eq. (13), and it is the single most
load-bearing convention in the theory.

It matters because the *other* frame in play is the optical testing frame, in which the Fringe
Zernike set is defined with `theta` measured **counter-clockwise from xhat**. Every one of the
three Rochester papers spends a paragraph converting between them, and every one does it by
reversing the arctangent:

| quantity | test frame | NAT frame |
|---|---|---|
| astigmatism Z5/6 | `(1/2) atan(z6/z5)` | `(1/2)[pi/2 - atan(z6/z5)]` (F14 Eq. 7) |
| coma Z7/8 | `atan(z8/z7)` | `pi/2 - atan(z8/z7)` (F14 Eq. 18) |
| trefoil Z10/11 | `(1/3) atan(z11/z10)` | `(1/3) atan(z10/z11)` (F12 Eq. 8) |

Note the trefoil row: the arguments are **swapped**, not offset. Schmid Eq. (8) carries the same
reversal as a sign flip inside the arctangent.

This belongs in one file, `Nat/Conventions.cs`, with its own tests, and the tests should be
written against the published figures - Fuerschbach 2012 Fig. 3 and 2014 Fig. 3 both draw the
surface map with its peaks and valleys marked, which pins the orientation unambiguously. A NAT
implementation that is wrong by ninety degrees produces plausible-looking node patterns in the
wrong place, and no internal check will catch it.

## The sigma vector

`sigma_j` locates the centre of surface `j`'s aberration field in the image plane. There are two
routes to it and this program can take only one of them today.

### The paraxial route (Gu 2020, Appendix)

For a single perturbed surface `k`, with tilt vector `T_k` in radians, decentre vector `D_k`,
curvature `c_k`, index step `dn_k = n'_k - n_k`, Lagrange invariant `Hh`, and the incidence
angles `i = y c + u` and `ibar = ybar c + ubar` (slopes taken *before* the surface, as
`SeidelCoefficients` already takes them):

    sigma_k^(j) = 0                                            j < k
    sigma_k^(k) = (T_k + c_k D_k) / ibar_k
    sigma_k^(j) = (T_k + c_k D_k) xi_j / ibar_j                j > k

        xi_j = (i_j ybar_k - ibar_j y_k) dn_k / Hh

and several perturbed surfaces superpose, `sigma^(j) = sum_k sigma_k^(j)`. Note that `xi_j` mixes
the incidence angles at surface `j` with the ray heights at the perturbed surface `k`; that is
not a transcription slip, it falls out of Gu Eqs. (32)-(34).

`(T_k + c_k D_k)` is the **equivalent tilt**: for a spherical surface a decentre is a tilt about
the centre of curvature, so the two enter only in that combination. Conics and freeform overlays
break that equivalence, which is why Schmid Eq. (11) carries separate `sigma_SPH` and
`sigma_ASPH` vectors for the base sphere and the aspheric cap.

**A sign to pin down before anything is trusted.** Gu's Eq. (21) writes the general form with a
leading minus on `(T_j + c_j D_j)`, while his Eq. (27) gives `sigma_k^(k) = (T_k + c_k D_k)/ibar_k`
without one. The two cannot both be right. The overall sign of `sigma` does not affect the
magnitudes Stage 2 computes - each contribution is taken in modulus - but it reflects every node
location Stage 3 reports through the field centre.

**Resolved as far as it can be from theory, 2026-09-12.** Thompson (2005) Sec. 3 fixes the
CONVENTION beyond doubt - `sigma_j` points TO the displaced field centre, and `H_Aj = H - sigma_j`
is his Eq. (3.1) - and every displacement vector below follows that sense. What he does not give
is the paraxial expression in `T` and `D`; he defines sigma geometrically instead, as

> "the projection of a line connecting the center of the pupil for the surface of interest with
> the center of curvature of that surface to the image plane."

That is better than a formula for settling Gu's ambiguity, because it is **operational**: tilt
one surface by a known amount, work out where its centre of curvature projects, and compare. The
sign is now a test to write rather than a paper to find, and the same definition is what the
real-ray route of Thompson (2009) implements directly.

### The real-ray route (Thompson, Schmid, Cakmakci, Rolland 2009)

`sigma_j` is properly defined by where the **optical axis ray** - the ray from the field centre
through the centre of the stop - strikes each surface, relative to that surface's own centre of
curvature. The paraxial expressions above are the small-perturbation limit of that. For the large
tilts of an off-axis three-mirror or a freeform system the real-ray route is the only correct one,
and `RealRayTrace` is already here to do it. The paper is not.

## Stage 2 is the one worth building first

Gu's as-built model is the highest return in the folder and the lowest cost, for a reason worth
stating plainly: **it needs no perturbation state at all.** The input is not a misaligned system,
it is a tolerance - a scalar per surface, "this surface may be decentred 0.04 mm and tilted
9 arcmin". So it can be built before Stage 3 exists, and it exercises the same algebra.

The model (Gu Eqs. 1-2) is the RSS over surfaces and error types of the misalignment-induced coma
and astigmatism, averaged over the field and added in quadrature to the nominal.

### Do not form sigma explicitly

`sigma^(j)` carries `1 / ibar_j`, which is singular wherever the chief ray strikes a surface at
normal incidence. That singularity is **not real**: `ibar_j` cancels analytically in every term
that uses it, because `W131` carries one factor of `ibar` and `W222` carries two. Writing

    G_j = n_j^2 y_j d(u/n)_j

which is `-S1_j / i_j^2` and `-S2_j / (i_j ibar_j)` and `-S3_j / ibar_j^2`, the three sensitivity
kernels are

| term | kernel | source |
|---|---|---|
| field-constant coma | `i_j G_j / (4 sqrt2)` | Gu Eq. (10) |
| field-linear astigmatism | `ibar_j G_j / (2 sqrt6)` | Gu Eq. (15), first line |
| field-constant astigmatism | `G_j / (4 sqrt6)` | Gu Eq. (15), second line |

summed over `j >= k` with the `xi_j` factor, the first two linear in the equivalent tilt and the
third quadratic in it. There is no division anywhere in that, and `G_j` should be computed once
per surface rather than recovered from the Seidel arrays - going back through `S1`, `S2` or `S3`
reintroduces exactly the division that was just cancelled.

The RMS normalisations: `1/(2 sqrt2)` for coma has been verified here by direct integration over
the unit pupil. The two astigmatic factors are quoted as Gu prints them and have not been
re-derived.

### Where it lands

A new operand, monochromatic like Gu's, alongside `PRMSA` in `Operands/OperandType.cs` and
`OperandEvaluator.cs`. It belongs in the README's table of **things the predicted spot cannot
see**, as a fourth row: `PRMSA` measures the design that was drawn, not the one that will be
built. A design can be driven to a smaller predicted spot by making it more sensitive to the
tolerances it will actually be made to, and nothing currently in the merit function objects.

Gu's own result is that this costs about a tenth of a wave of nominal performance and buys back
nearly two tenths at the eightieth percentile of a two-thousand-sample Monte Carlo, with the
standard deviation at 64 per cent of the traditional design's.

## The freeform half

Fuerschbach's contribution is that a non-symmetric surface **at the stop** contributes a
field-constant aberration of its own order, and that as it moves off the stop the beam displaces
across it by

    dh = (ybar_j / y_j) H

so that replacing `rho` with `rho + dh` and expanding generates lower-order, field-dependent
terms. The whole of the 2014 paper is the table of what each Fringe Zernike pair generates.
Nothing new appears: every generated term is one NAT already had.

The field-constant vectors at the stop, with `n` and `n'` either side of the surface:

    FF_B222^2 = 2 (n' - n) z5/6 exp(i 2 phi_5/6)       astigmatism overlay   (F14 Eq. 9)
    FF_A131   = 3 (n' - n) z7/8 exp(i   phi_7/8)       coma overlay          (F14 Eq. 21)

and for a surface away from the stop the coma overlay additionally contributes, per Fuerschbach
2014 Table 1,

    A222  += (ybar_j / y_j) FF_A131,j    field-linear, field-asymmetric astigmatism
    A220M += (ybar_j / y_j) FF_A131,j    field-linear medial field curvature - a tilted focal surface

The trefoil case is the one with the result worth having: a three-point mount deformation on a
surface away from the stop produces, besides the expected field-constant trefoil, a **field
linear, field conjugate astigmatism** (2012 Eqs. 15, 16) whose orientation runs the opposite way
round the field from ordinary astigmatism. That is a signature no RMS number carries, and it is
how mount error is told apart from misalignment. The constant relating `C333^3` to the measured
`z10/11` is Eq. (10) of the 2012 paper, and the text layer of that equation in the PDF here is
damaged; it must be read off the page image before it is coded.

This stage needs `Surface.ZernikeCoefficients` and readers for Zemax Zernike Standard Sag and
CODE V `SPS ZRN`, which is the only new file-format work in the whole proposal.

## Third-order node structure

With `W131 = sum_j W131_j` and so on for the totals, and

    A131   = sum_j W131_j sigma_j        A222 = sum_j W222_j sigma_j
    B222^2 = sum_j W222_j sigma_j^2

the aligned forms are

    coma          W131 [ (H - a131) . rho ] (rho . rho)            a131 = A131 / W131
    astigmatism   (1/2) W222 [ (H - a222)^2 + b222^2 ] . rho^2     a222 = A222 / W222

    b222^2 = B222^2 / W222 - a222^2

giving **one** comatic node at `H = a131` and **two** astigmatic nodes at `H = a222 +/- i b222`.
Schmid's diagnostic follows directly: a figure error at the stop contributes to `B222^2` but
*not* to `a222`, so its two nodes stay symmetric about the field centre, while a secondary mirror
misalignment moves `a222` and carries the midpoint off with it. Those are different pictures, and
they are the point of the display.

Medial field curvature keeps a quadratic focal surface but moves its VERTEX, Thompson
Eqs. (4.27)-(4.31):

    A220M  = sum_j W220Mj sigma_j              a220M = A220M / W220M
    B220M  = sum_j W220Mj (sigma_j . sigma_j)  -- a SCALAR, a dot product not a vector square
    b220M  = B220M / W220M - a220M . a220M     -- also scalar

    -W20 = W220M (H - a220M) . (H - a220M) + b220M

Note that `B220M` uses the DOT product where astigmatism's `B222^2` uses the vector square; they
are different operations on the same sigma and swapping them is easy to do and hard to see.
Thompson adds the point worth keeping: the vertex of the medial surface generally does NOT
coincide with the coma node, "because each is weighted by a different set of surface-by-surface
aberration coefficients, even though the individual aberration field displacement vectors for
each aberration are identical."

That last clause is the implementation instruction: **sigma is computed once per surface and
reused by every aberration**. Only the weights differ.

Distortion is cubic in `H` and can carry up to three nodes; it does not degrade the spot, and
Thompson holds it back to his Section 7 for that reason.

**Provenance note, resolved 2026-09-12.** All of the above is now transcribed from Thompson
(2005) rather than reconstructed - coma from Eqs. (4.7)-(4.9), astigmatism from (4.15)-(4.22),
medial field curvature from (4.27)-(4.31). An earlier draft of this file guessed that field
curvature was tilted as well as displaced; it is not. The vector product was also written here
with `x` as the real axis, which contradicted the conjugate rule in the same section; Appendix A
settles it and the code was always right.

## Staging

1. **`Core/Nat/Vec2.cs`, `Conventions.cs`, `WaveCoefficients.cs`.** Vector algebra, the two
   frames, and the Seidel-to-`Wklm` conversion. Added to the Ad project's `<CoreSource>` list.
   Pure, testable, no new inputs and no new file-format work.
2. **`Core/Nat/Sensitivity.cs` and the as-built operand.** Gu 2020. Needs `SeidelCoefficients`
   moved onto the `Scalar` path. Reuses everything else unchanged.
3. **Perturbation state and `--nat`.** `Surface.Tilt` and `Surface.Decenter`, `COORDBRK` folded
   into them on read, `sigma` per surface, and a report: per-surface sigma, the aggregate `A131`,
   `A222`, `B222^2`, the node locations, and a full-field display as TSV - magnitude and
   orientation on a field grid, in the plain-text-and-TSV style the rest of the program already
   writes. Conics are admissible here: the README's own table has Buchdahl's third and fifth
   aspheric orders sound and only the seventh reconstructed, and NAT at third order is where the
   conic telescopes that are this theory's natural subject live.
4. **Freeform overlays.** As above. The largest stage and the only one touching the readers.
5. **Fifth order.** Not until the papers are in hand. See below.

## An open question, found by a test that failed

A uniform decentre - every surface moved by the same amount - is a rigidly translated lens. Its
optical axis moves bodily, so the optical axis ray moves with it, and by Thompson's geometric
definition every surface's field centre should land in the same place: **`sigma` constant across
surfaces**. That would be a sharp check of the transfer term `xi_j`, since nothing else forces a
sum over differently weighted surfaces to come out constant.

It does not. On the Cooke triplet with every surface decentred 0.05, `sigma_y` runs

    surf      1         2         3         4         5         6
    sigma  +0.0131   -0.0032   -0.0064   +0.0055   +0.0025   -0.0177

varying by a factor of five and changing sign.

**The premise has since been confirmed by real rays, which makes the paraxial route the
suspect.** `LocalFrameTests.AUniformlyDecentredLensImagesLikeTheNominalOneMovedOver` traces
bundles through a Cooke triplet with every surface decentred by `d` and finds the landings
identical to the nominal ones displaced by exactly `d` - sagittal unchanged, meridional moved
over, agreeing to ten decimal places across six surfaces and both transform directions. The
lens really is rigidly translated.

It follows that the optical axis ray runs at height `d` through the whole system, and every
surface's centre of curvature sits at height `d` as well, so the line joining them is parallel
to the axis and meets the image plane at `d`. **By Thompson's geometric definition `sigma` is
constant and equal to `d`.** The paraxial route does not give that.

The likely cause is the one named above: the equivalent tilt `c D` represents a decentre solely
by the motion of the centre of curvature and drops the vertex displacement, which is precisely
what makes a translation rigid. That is a hypothesis and not yet a finding.

## The real-ray sigma settles it, and against the paraxial route

`Core/Nat/RealSigma.cs` measures `sigma` from traced rays and nothing else. Thompson's geometric
definition has an operational reading - **`sigma_j` is the field at which the chief ray points
straight at surface `j`'s centre of curvature** - and the incidence `c (x, y) + (u_x, u_y)`,
taken on a real ray in the surface's own frame, vanishes exactly there. The optical axis ray is
aimed at the centre of the stop by Newton iteration (residual 1e-17), and

    sigma_j = - incidence_j(H = 0) / (d incidence_j / dH)

with both terms traced. No part of Gu's transfer expression appears in it.

### It disagrees with the paraxial route, on a case where the answer is known

Uniformly decentred Cooke triplet, every surface moved 0.05:

    surf         1         2         3         4         5         6
    paraxial  +0.0131   -0.0032   -0.0064   +0.0055   +0.0025   -0.0177
    real       0.0000    0.0000    0.0000    0.0000    0.0000    0.0000

**The real answer is zero, to 1e-16.** And zero is provably right, for a reason better than the
one guessed at above: Thompson locates the unperturbed field centre *by the optical axis ray*.
Translate the lens and the optical axis ray translates with it, every centre of curvature stays
on it, and nothing is displaced at all. The earlier reasoning here - that `sigma` should be
constant at `d` - was measuring against the mechanical axis instead, and was wrong.

A single tilted surface shows the same split differently. Surface 2 tilted 0.115 degrees:

    surf         1         2         3         4         5         6
    paraxial   0.0000   +0.0066   +0.0018   +0.0045   +0.0038   -0.0007
    real      -0.0030   +0.0076   +0.0034   +0.0034   +0.0034   +0.0034

The real route is **constant downstream of the perturbation**, which is what the geometry
requires: past surface 2 the optical axis ray is one definite ray in an aligned system, so it
corresponds to one definite field offset and every later surface must report it. The paraxial
route varies. The two also disagree upstream, where Gu's `xi_j` is zero by construction while
the traced ray is displaced at surface 1 - aiming at the stop through a tilted surface 2 changes
the ray before it as well.

### Resolved by Thompson (2009): the real-ray route is right, and the fault is the superposition

Thompson's Eq. (10) gives the paraxial sigma as

    sigma_sph = -ibar* / ibar = -[ ubar#_OAR + ybar#_OAR c - beta0# ] / [ ubar + ybar c ]

where `ibar*` is the angle of incidence of the OPTICAL AXIS RAY on the local surface, `beta0#`
is the equivalent tilt locating the centre of curvature relative to the mechanical axis, and
`ybar#_OAR`, `ubar#_OAR` are the height and inclination of the optical axis ray at that surface.
That is the same construction `RealSigma` implements, and **his Table 5 shows the paraxial and
real-ray routes agreeing to four or five figures on a perturbed Ritchey-Chretien**. They are not
different quantities; ours disagreeing was a fault.

Where the fault is. Put a uniform decentre `D` through Eq. (10): the centre of curvature moves,
so `beta0# = c D`; the optical axis ray moves with the lens, so `ybar#_OAR = D` and
`ubar#_OAR = 0`. Then

    ibar* = 0 + D c - D c = 0     and so     sigma = 0

exactly, matching the measurement. Gu's Eq. (7) has no `ybar#_OAR c` term because it is derived
for **one** perturbed surface with everything else nominal - and there the optical axis ray is
still on axis at that surface, so the term is zero and his form is correct. Summing his
single-surface result over every perturbed surface, which is what `SigmaVector` does, drops
exactly the term that makes a rigid translation come out zero.

`SigmaVector` has been rewritten on Eq. (10), accumulating `ybar#_OAR` and `ubar#_OAR` through
the system by Thompson's Eqs. (3)-(6) rather than superposing single-surface results. **It
reproduces his published Table 5 to seven figures** - see below - and the diagnosis above turned
out to be only half right.

### What the rewrite fixed, and what it did not

It did not change the uniformly decentred triplet at all: the accumulated form and the
superposed one agree there, so superposition was never the fault. The real cause is narrower and
sharper.

**The optical axis ray is defined by passing through the centre of the STOP.** When the stop
itself moves - as it does under a rigid translation - the ray is displaced before it reaches any
surface, and a forward accumulation that starts it on axis cannot know. On the telescope, where
the stop is the primary, a translation by `D` puts the stop centre at height `D`, so
`ybar#_OAR = D` at that surface and

    sigma = [ -0 - c D + c D ] / ibar = 0

which is the right answer and is not what a from-zero accumulation gives.

Thompson calls that term `sigma*`, and his Fig. 11 caption records that his example was built so
that "the stop is decentered with the primary mirror so that `sigma* = 0`". **That is why Table 5
agrees to seven figures and the rigid translation does not: they are the same fact seen twice.**
Locating the entrance pupil under perturbation is a boundary-value problem - start at the object
centre, end at the stop centre - rather than an initial-value one, and only the second is
implemented.

**`sigma*` is now implemented**, and it needed no iteration. Accumulate the perturbations ahead
of the stop, then solve for the initial pupil decentre that puts the ray on the stop's own
centre:

    ybar#_OAR(stop) = ybar_stop qAcc + y_stop (eAcc + e0)  =  decentre of the stop

One division, by the marginal ray height at the stop - a height that cannot vanish, since the
stop is where the marginal ray is at full aperture.
`ThompsonTelescopeTests.ARigidlyTranslatedTelescopeDisplacesNothing` now passes, and Table 5
still agrees to seven figures, which it must: Thompson's example has `sigma* = 0` by
construction, so the new term is exactly zero there.

### A published acceptance test, and a missing piece

Thompson's Tables 1-5 are a complete oracle of the kind this repository prefers: the
Ritchey-Chretien prescription, the applied perturbations, the paraxial and real ray traces, and
the resulting sigma vectors both ways. `sigma_sph` at the secondary is `(0.0220889, 0.0966082)`
paraxial against `(0.0220670, 0.0965643)` real ray. Any implementation can be checked against
those numbers on a lens whose prescription is printed beside them.

**An aspheric surface has TWO sigma vectors** - one for the spherical base curve and one for
the aspheric departure, treated as a zero-power plate after Burch - with

    sigma_asph = delta beta* / ybar                                    Eq. (11)

The aspheric one is not the spherical one: at the secondary of his telescope they are
`(0.0220889, 0.0966082)` and `(0.0540453, 0.108097)`, differing by more than a factor of two.
This is Schmid 2010's `sigma_SPH` and `sigma_ASPH`.

**Both are now implemented and `NatField` sums both.** A figured surface contributes two
displaced fields and the aspheric Seidel shares are kept apart by `SeidelCoefficients` so that
they can be. The aspheric share divides by the CHIEF ray height where the spherical one divides
by the chief-ray incidence - a different singularity, at a pupil rather than at normal incidence
- and it cancels the same way, because every aspheric share is one quantity times a power of
`ybar/y`:

    W131_a sigma_a   = S1a nu_a / (2 y)
    W222_a sigma_a   = S1a ybar nu_a / (2 y^2)
    W222_a sigma_a^2 = S1a nu_a^2 / (2 y^2)

with `S1a` going as `y^4`, so nothing is singular where the marginal ray vanishes. Petzval has
no aspheric share at all - it depends only on curvature and index step, and figuring changes
neither - so the medial term splits as `S4/4` plus the spherical half of `S3/4`.

## Validation

In the order the checks are worth anything, which is this repository's usual order:

1. **`sigma = 0` collapses to Seidel, bit for bit.** Costs nothing, catches most things.
2. **Gu's Cooke triplet is a complete, reproducible acceptance test.** Table 1 gives the starting
   prescription, Table 3 the optimised one, and the text gives every setting: `D = 0.0399 mm`,
   `T = 9.176'`, 546.1 nm, 25 field points over 14 by 14 degrees, EFL held at 50 mm, all
   curvatures and thicknesses variable. The optimiser here should be able to walk from one table
   to the other. Nothing else in the folder is that directly checkable, and it tests the operand,
   the derivative and the optimiser together.
3. **Published node geometries.** Schmid's Ritchey-Chretien - nodes symmetric about the field
   centre under figure error, one node pinned at centre for a coma-compensated secondary
   misalignment. Fuerschbach's two- and three-mirror telescopes - the equilateral node triad under
   trefoil, and the single off-axis node of Eq. (26) in the anastigmatic case.
4. **Full-field displays against real rays**, through the existing `zosapi/` harness: perturb in
   OpticStudio, fit Fringe Zernikes at a field grid, compare magnitude and orientation. The 2025
   paper's similarity threshold of 0.8 is the published precedent for what agreement means here.
5. **Monte Carlo against the analytic model**, for Stage 2 specifically. Gu's model is an
   expected value, not a bound, and the honest way to quote it is beside a sampled distribution.

## What this would not do

**No fifth-order NAT yet.** It is no longer blocked - see the section below for what is
established and what is left - but nothing fifth order is computed anywhere in the code.

**No real-ray sigma.** The paraxial route is the small-perturbation limit. It is the right tool
for tolerancing, which is what Stage 2 is, and the wrong one for a system with large deliberate
tilts, which is what Stage 4's customers are.

## Stage 5: the notation map, and what step one established

### The claim that was wrong

This file previously said fifth-order NAT was "blocked on a notation map" that "has not been
located". That was wrong on both halves. **Buchdahl paper VII Sec. 6 IS the map**, and its Eq.
(6.6) gives the nine fifth-order relations outright; the OCR of that page is unreadable, which is
why it went unnoticed. Its numerical check is in paper VI Table II, on the same triplet whose
Table I this repository already reproduces entry by entry.

The real blocker was always the physics - Thompson's multinodal trilogy, which says WHICH
fifth-order aberrations go multinodal and where the nodes land. Those are now in hand:

    I    J. Opt. Soc. Am. A 26(5), 1090 (2009)   spherical aberration
    II                      27(6), 1490 (2010)   the comatic aberrations
    III                     28(5),  821 (2011)   the astigmatic aberrations

### Step one: the monomial correspondence, verified

Buchdahl's wave-front deformation is a series in the three rotational invariants
`lambda = rho.rho`, `mu = rho.H`, `nu = H.H`, and his Eq. (4.6) - which he calls **exact** - is

    eps' = dD/dy

the transverse ray displacement as the gradient of the deformation. His third-order part is

    D3 = pi1 lambda^2 + pi2 lambda mu + pi3 lambda nu + pi4 mu^2 + pi5 mu nu + pi6 nu^2

and each monomial is one Hopkins term and only one:

    lambda^2   = rho^4                 ->  W040
    lambda mu  = H rho^3 cos(theta)    ->  W131
    lambda nu  = H^2 rho^2             ->  W220
    mu^2       = H^2 rho^2 cos^2(theta)->  W222
    mu nu      = H^3 rho cos(theta)    ->  W311
    nu^2       = H^4                   ->  piston, which is why pi6 is dropped

**So the deformation coefficients ARE Thompson's `Wklm`, one for one.** That is the whole
bridge, and it was checked rather than asserted: differentiating a wave front built from this
program's own third-order coefficients and comparing against the transverse polynomial
`Prms.Transverse` already computes, one Rimmer coefficient at a time, over a spread of pupil
radii, azimuths and field heights:

    B  -> W040     ratio 2.0     spread 2.2e-16
    F  -> W131     ratio 2.0     spread 2.2e-16
    C  -> W222     ratio 3.0     spread 1.5e-16
    Pi -> W220     ratio 2.0     spread 1.1e-16
    E  -> W311     absent

Every term maps **to machine precision**, which is what the spreads say: the shape is exactly
right in every variable at once.

Two of those rows need reading rather than glancing at.

**The 3.0 is not an error.** `Prms.Ey` writes third-order astigmatism as `{C: 3, Pi: 1}` -
Rimmer's transverse polynomial carries the TANGENTIAL combination `3C + Pi`, which is Johnson
(1973) Table I verbatim, where the gradient of `W222 mu^2` alone carries the plain term. The
factor of three is the tangential weighting, and it turns up exactly where the theory puts it
and nowhere else.

**The absent distortion is the documented behaviour**, not a gap: `E` and `E5` are not in Robb's
polynomial at all, because distortion displaces the image without resizing it.

### What step one implies for the fifth order, and what it does not

The same algebra forces the fifth-order assignment. Degree three in `(lambda, mu, nu)` has ten
monomials; drop the pure-field one as piston and nine remain, which is exactly Thompson's nine
names with nothing left over:

    sigma1 lambda^3      -> W060        sigma6 lambda nu^2   -> W420
    sigma2 lambda^2 mu   -> W151        sigma7 mu^3          -> W333
    sigma3 lambda^2 nu   -> W240        sigma8 mu^2 nu       -> W422
    sigma4 lambda mu^2   -> W242        sigma9 mu nu^2       -> W511
    sigma5 lambda mu nu  -> W331

Nine monomials, nine coefficients, no remainder - itself a check that Buchdahl's ordering and
Thompson's naming describe the same set.

**What is NOT established is the scale constants.** `Prms.Ey`'s fifth-order lines show the same
combination structure the third order had - `{M1, M2}` against `{M3}` at `cos3p`, `{N1, N2/2}`
against `{N2/2}` at `cos2`, `{C5: 5, Pi5: 1}` as the analogue of `3C + Pi` - and those groupings
are what `W240`/`W242`, `W331`/`W333` and `W420`/`W422` split into. Working them out is the next
step, and it is the part where a factor of three in the wrong place produces a plausible wrong
number rather than an obvious one. It should be done the way step one was: one coefficient at a
time, isolated, with the spread reported alongside the ratio, and then checked against paper VI
Table II.

**A trap to carry into that work.** Paper VII Sec. 7(a) gives scaling exponents - `A: -1, B: 0,
C: 1, S1: -1, S3,S4: 1, S5: 2, S6: 3`, with barred coefficients taking an extra factor - because
his `e` is not unity. A coefficient right and its power of `e` wrong looks like a plausible
number, not an error.

**The optimiser stays spherical-only.** Stage 2 changes nothing about that rule; it adds an
operand made of third-order quantities, and the refusal in `SphericalOnly.cs` is about Buchdahl's
reconstructed seventh-order aspheric increment, which NAT never touches. Stage 3's analysis
accepts conics precisely because it stops at third order.

**It does not replace tolerance analysis.** It makes tolerance sensitivity something the optimiser
can *see*, which is a different claim from predicting a yield.

## Stage 5, step two: the map is not a gradient, and Buchdahl prints it

Step one identified nine wavefront coefficients against Rimmer's twelve transverse ones and
guessed the bridge was a gradient. That guess was wrong, and it is worth recording exactly how
it failed, because the wrong version produces plausible numbers rather than obvious ones.

### The third order is a gradient, and that is what misled

Write `y = rho cos(th)`, `x = rho sin(th)`, take `eps = K dW/d(y,x)`, and expand into the slots
of `Prms.cs`. Note the mixed naming there: `cos2` is `Cos(2t)`, a double angle, while `cos3p` is
`Cos(t)^3`, a power. At third order every relation closes:

    B = 4K W040     F = K W131     C = K W222     Pi = 2K W220M - K W222

Three of these are over-determined - `W131` alone drives the `one`, `cos2` and `sin2` slots, and
all three agree - so this is four independent checks, not four definitions. Measured on the
fixtures, `S_i / Rimmer_i` is the same constant across all five coefficients to seven digits:

    KingslakeDG    -0.1233310      Ladder1_Sphere  -0.1245215
    CookeTriplet   -0.2000001      Ladder2_Sphere  -0.1969410

and that constant is **exactly `-1/FNumber`**, which `BuchdahlCoefficients.cs` says in its own
comment above the totals: *"Totals are transverse coefficients: unconverted sums times the
F/number."* There was never a physical scale constant to find at third order. It is bookkeeping.

### The fifth order is not a gradient

The same derivation forces three ratio constraints, each between coefficients carrying identical
powers of `rho` and `H`, so no normalisation of pupil, field or F/number can affect them:

    W151 drives both  one  and  cos2              =>   F1 : F2 = 3 : 2
    W240 -> M2 and M1+M2 ; W242 -> M3 and M1+M2   =>   M1 = M3
    W331 -> N3 and N1 ; W333 -> N1, N2 only       =>   N1 = N3

All three fail. `Ladder1_Sphere` is a **single surface**, so it has no induced terms at all, and
it still fails: `F1/F2 = 1.578`, `M1/M3 = 1.940`, `N1/N3 = 4.582`. The induced corrections are
not the cause.

The deficit does track how well corrected the system is - `KingslakeDG` is 0.1% off, the crude
ladders 5% to 24% - which is the signature of a correction built from third-order quantities.
A magnitude proxy across all 28 fixtures supports that direction but is far too crude to confirm
any particular form, and was not treated as confirmation.

### Buchdahl VII Sec. 6 gives the actual relation

Paper VII Sec. 6 is titled *The relations between W-coefficients and deformation coefficients*,
and its Eq. (6.6) is the fifth-order map, already solved in the direction needed:

    12 s1 = 2 S1 + 3A          2 s2 = 2 S1b + A + 2Ab
     8 s3 = 2 S3 + 2Ab + C
     2 s4 = S4 + 2A + 4Ab      2 s5 = S5 + 2Ab + C      2 s6 = S6
     3 s7 = S4b + 4Ab + Bb     2 s8 = S5b + Bb + C        s9 = S6b

The left side is the deformation (wavefront) expansion; the right side is the aberration
coefficients in W-coordinates. **Each wavefront coefficient is its aberration coefficient plus
third-order terms.** That is the missing piece, and it is linear in third order at fifth order -
at seventh it becomes quadratic, e.g. `64 t1 = 8 T1 + 20 S1 + 18 A^2 + 15 A`.

The counts settle the identification beyond doubt. Eq. (6.5) has **five** primary, Eq. (6.6)
**nine** secondary, Eq. (6.7) **fourteen** tertiary, and Buchdahl says so in the text:
*"it is effectively specified by 5+9+14 = 28 coefficients."* Those are exactly the numbers of
monomials `lambda^a mu^b nu^c` at each degree once the field-only piston term is dropped. So

    Buchdahl's s1..s9  IS  Thompson's  W060 W151 W240 W242 W331 W333 W420 W422 W511

and the right-hand `S1..S6, S1b..S6b` are twelve - Rimmer's twelve.

Sec. 6(b) also explains the apparent over-count: `pi2`, `s2`, `s4`, `s5` and six of the tertiary
coefficients each have two alternative expressions, and the 10 implied identities are exactly
those of paper VI (4.16-18). The naive gradient constraints above were the wrong identities.

### The verification route, and it needs no second program

Paper VII **Table I** tabulates the deformation `D` and retardation `R` coefficients of orders
3, 5 and 7 for the triplet `Sigma1` at focal length 1 - the same triplet whose paper III Table I
this program already reproduces:

    pi1  0.38571     s1 -18.34      s4 -5.9997    s7  0.1806
    pi2 -0.016143    s2 -26.905     s5  0.8986    s8  0.1471
    pi3  0.082148    s3  -2.547     s6 -0.20577   s9 -0.05877
    pi4 -0.016921
    pi5 -0.019674

(the `D` column; the `R` column differs at `s3`, `s5`, `s6`, `s8` and `s9`). **Thompson's `W` is
the wave aberration, so it is `R`, not `D`** - Eq. (3.4) relates them, and at `s6` the two differ
by 21%. Getting that wrong would produce a plausible number, not an obvious error.

Two conversions remain before the map can be coded, and both are in papers already on disk:

1. **VI (6.6-10)**, because this program computes *paracanonical* coefficients while Eq. (6.6)
   is stated for W-coordinates. VII says so explicitly: *"If paracanonical coefficients have
   been computed in the first place one need only use equations of the type VI(6.6-10)."*
2. **VII Eq. (3.4)**, deformation to retardation.

and the `e` scaling of Sec. 7(a) applies as usual when `e` is not unity.

### What this closes

The route through Robb's polynomial is abandoned. It was only ever a way to reach the wavefront
coefficients through the transverse ones this program happens to report, and Buchdahl reaches
them directly from the same per-surface machinery. The twelve-against-nine puzzle was never an
over-determination: twelve aberration coefficients plus the third-order ones map onto nine
wavefront coefficients, with the surplus absorbed by the ten identities.

One further point in Buchdahl's favour for NAT: the wavefront deformation is **additive over
surfaces**, which the transverse aberration coefficients are not - that is what the induced terms
exist to repair. Per-surface `W_klm`, which is exactly what NAT displaces by each surface's
`sigma_j`, is therefore the better-conditioned quantity of the two.

## Stage 5, step three: the conversions, and the chain closes on two published tables

`Nat/Deformation.cs` now carries VII Eq. (2.8), Eqs. (6.5-6) and Eq. (3.4). What follows is what
each one turned out to be, and what is still missing.

### Eq. (2.8) fixes the identification with no freedom left

    D = (pi1 L^2 + pi2 L M + pi3 L N + pi4 M^2 + pi5 M N)
      + (s1 L^3 + s2 L^2 M + s3 L^2 N + s4 L M^2 + s5 L M N + s6 L N^2
         + s7 M^3 + s8 M^2 N + s9 M N^2) + (fourteen tertiary terms) + O(10)

with `L, M, N` the invariants `lambda, mu, nu` of Eq. (2.3): `rho^2`, `rho H cos(th)`, `H^2`.
One monomial is one Hopkins term, `L^a M^b N^c -> W[k=b+2c, l=2a+b, m=b]`, so

    s1 s2 s3 s4 s5 s6 s7 s8 s9  =  W060 W151 W240 W242 W331 W420 W333 W422 W511

Note the order: `s6` is `W420` and `s7` is `W333`, which is **not** the order Thompson's
Appendix A lists them in. Reading the two lists off against each other in sequence would swap
field curvature with trefoil.

### Eq. (3.4), deformation to retardation

Thompson's `W` is a wave aberration, so it is the retardation `R`, not the deformation `D`.
Four of the nine secondary coefficients are unchanged; five pick up `-1/2 e^-2` times a primary
one:

    's3 = s3 - k pi1     's5 = s5 - k pi2     's6 = s6 - k pi3
    's8 = s8 - k pi4     's9 = s9 - k pi5          k = 1/(2 e^2)

    's1 = s1   's2 = s2   's4 = s4   's7 = s7     and every primary is unchanged.

The `e^-2` is the factor Eq. (3.3) carries on its `nu D` term. Eq. (3.4) itself is printed under
the Eq. (4.1) convention that `e` is the unit of length, so the code writes the factor out rather
than assuming unity - which matters, because Buchdahl's own Table I was computed with `e` not
equal to one.

**Table I checks this exactly where it is exact.** The four coefficients Eq. (3.4) leaves alone
are printed identically in the `D` and `R` columns - `s1 = -18.34`, `s2 = -26.905`,
`s4 = -5.9997`, `s7 = 0.1806` - and the five it corrects are not. Attaching the corrections to
the wrong coefficients fails that immediately. The five corrections themselves each imply the
same `e^-2`, between 1.0605 and 1.0655, which is the rounding spread of a four-figure table.

### Eqs. (6.5-6), aberration coefficients to deformation coefficients

    4 pi1 = A        pi2 = Ab      2 pi3 = C      2 pi4 = Bb        pi5 = Cb

    12 s1 = 2 S1 + 3A          2 s2 = 2 S1b + A + 2Ab
     8 s3 = 2 S3 + 2Ab + C
     2 s4 = S4 + 2A + 4Ab      2 s5 = S5 + 2Ab + C      2 s6 = S6
     3 s7 = S4b + 4Ab + Bb     2 s8 = S5b + Bb + C        s9 = S6b

### The chain closes against two tables in two papers

VI **Table II** gives Sigma1's aberration coefficients in *both* coordinate systems; VII
**Table I** gives the same system's deformation coefficients. So VI Table II (the W column), fed
through Eqs. (6.5-6) and the `e` scaling of Sec. 7(a), must reproduce VII Table I. It does.

`e` is not published. Solving for it from `pi1` alone leaves the other thirteen coefficients as
free checks on one fitted parameter, and all thirteen land within the printed precision - worst
relative error `1.2e-3`, on `s8`, which Table I prints as `0.1471`. The solved `e = 0.9688`
also agrees with the `e^-2 = 1.0636` implied independently by Eq. (3.4) against the `R` column.

That is the verification route this stage needed: no ray trace, no fit, no second program.
`DeformationTests.cs` holds it.

### The W-coordinate scheme, and the chain closes end to end

`BuchdahlTableI.Compute` now takes a `wCoordinates` flag which applies the modified rows of
VI Table I, p.536. The transcription of all thirty-nine rows is in
`C:\Research\Buchdahl\vi-table-i-w-coordinates.md`.

**The whole of the difference is where the reference point sits.** Paracanonical rows carry their
running sums back to the FIRST surface; W rows carry them forward to the IMAGE SPACE, which is
Buchdahl's double prime. The scheme shows it in one line:

    paracanonical:  t20 = (1/2)(t9 at surface 1 - t9) + t16
    W coordinates:  t20 = (t16 - (1/2)t9) + ((1/2)t9 - t16)''

So `X''` means a running sum taken over all surfaces instead of stopping at `j < i`, an angle
product built from the final primed angles, and a `[*]` row's recursion run backward from the
image end seeded at zero.

**Far fewer than thirty-nine rows were needed.** The fifth-order coefficients are `t41..t68`,
which are not themselves in the modified list but depend on `t20..t24` through `t25..t33`. So
patching the five primary rows moves all twelve secondary coefficients - and that alone put
eleven of the twelve onto VI Table II's W column. The remaining rows `t83..t98` and `t102..t114`
are a different family that feeds the tertiary order, and are not needed for fifth.

**One more row was needed, and VI (4.17) found it.** With the primary rows patched, S4 and S4b
were both wrong by the same ABSOLUTE amount, about -0.0107. An equal absolute shift in both
members of a pair is the signature of a missing additive row rather than a wrong product, and
`t55 = s4a dagger` is the fourth-pair row in the modified list. Reading VI Table I against the
`Secondary` helper,

    paracanonical:  t55 = 2(t51 - t50) + t54
    W coordinates:  t55 = 2(pi'' t11 + t51 - t50) + t54

so the entire modification is the one extra term `2 pi'' t11`, and `t55` enters `s4` additively
with coefficient one. That is why both members moved together.

The identities localised it before the cause was known. Of VI (4.17),

    S2 - 4 S1b = 2(Ab - A)     S4 - S2b = -A + Bb/2     S5 - 2 S3b = -2 Ab + Bb

the first and third were already clean at about 1e-4 while the second was out by 9.7e-3, which
pointed at S4 alone. They hold on Buchdahl's own published W column to 6.9e-5 once the powers of
`e` from Sec. 7(a) are applied, so that residual is the standard to meet - and applying the
e-scaling is what made them usable at all.

**What is verified.** `WCoordinateSchemeTests` checks four things:

1. the unpatched scheme reproduces VI Table II's `OT*` column, all seventeen coefficients - the
   baseline, checked first so that agreement on the W column cannot be a coincidence of two
   errors;
2. the patched scheme reproduces the `W` column, all seventeen;
3. the primary coefficients are identical in the two systems, asserted separately so a
   regression there cannot hide inside a looser tolerance on the secondary set;
4. **the whole chain, from a lens prescription to VII Table I** - through the W scheme, VII
   Eqs. (6.5-6), and Eq. (3.4) - reproducing both the `D` and the `R` columns:

       s1 -18.34034 / -18.34      s4  -6.00004 / -5.9997     s7   0.18050 / 0.1806
       s2 -26.90623 / -26.905     s5   0.89852 /  0.8986     s8   0.14699 / 0.1471
       s3  -2.54778 /  -2.547     s6  -0.20559 / -0.20577    s9  -0.05884 / -0.05877

   worst relative error 1.2e-3, and those nine ARE Thompson's `W060 W151 W240 W242 W331 W420
   W333 W422 W511`. No ray trace, no fit, no second program.

**One entry does not meet the rest.** `S5b` comes out 0.16014 against a printed 0.161, and misses
by the same amount in both columns (W: 0.1455 against 0.146). It is therefore a property of the
baseline scheme rather than of the patch, and the baseline is checked against paper III Table I
entry by entry elsewhere. Every other coefficient agrees to about 0.01 per cent. Recorded rather
than absorbed into a tolerance without comment.

**What is left of Sec. 6.** Nothing, for fifth order. VI (6.7) printed only three of its twelve
secondary relations, and that route is not needed: computing directly in W coordinates was always
the alternative VII Sec. 7(a) offered, and it turned out to need six rows rather than thirty-nine.
The tertiary rows remain unimplemented, which matters only if seventh-order NAT is ever wanted.

## Stage 5, step five: the per-surface split, and e stops being fitted

`Nat/WaveFront.cs` turns a scheme run in W coordinates into the per-surface and total wave front
coefficients NAT consumes.

### e is now computed

Every check up to here solved Buchdahl's `e` from `pi1`, which spent one of the fourteen
coefficients as a fitted parameter. It does not need to be fitted. The p ray reaches the axis at
the image and the q ray reaches it at the exit pupil, so

    e = (y_p v_q' - y_q v_p') / (v_p' v_q')

at the last surface, heights being continuous across a refraction. On Sigma1 that gives
**0.968799** against the **0.968801** his Table I implies - six significant figures - and the
whole chain still lands on Table I to 1.2e-3 with **nothing fitted anywhere**. All fourteen
coefficients are now free checks.

This also matters beyond the check: the fitted route only ever worked on the one system whose
answer was already published.

### The split is exact, and that is a property of the wave front

Every step from the scheme's per-surface contributions to the retardation coefficients is
**linear** - Eqs. (6.5-6) are linear in the aberration coefficients, the `e` scaling is one
factor per coefficient, and Eq. (3.4) adds a multiple of a primary coefficient to a secondary
one. So applying the chain surface by surface and summing gives the same answer as applying it to
the totals, to machine precision, with no apportioning judgement anywhere.

That is a property of the **wave front**, not of the transverse aberration. Wave front deformation
is additive over surfaces; the transverse coefficients are not, which is exactly what their
induced terms exist to repair. It is why NAT can decompose the way it does.

### Why the split is worth having

On Sigma1 the per-surface `W060` runs

    surface 1     35.49
    surface 2    113.46
    surface 3   -149.24
    surface 4    -19.74
    surface 5      0.00
    surface 6      1.68
    ---------------------
    system       -18.34

An eightfold cancellation. NAT displaces each of those contributions by its own `sigma_j` before
adding them, so a misalignment far too small to matter term by term still moves the sum a long
way. A decomposition that had quietly collapsed to "the system total, apportioned" would sum
correctly and lose the entire effect, so there is a test asserting the surfaces are much larger
than their sum as well as one asserting that they add up.

### What this does not settle

The per-surface contributions here are the scheme's own: intrinsic plus the induced part each
surface generates from the aberration already in the beam. Thompson's multinodal papers separate
the two at fifth order, because the induced part is generated by the PERTURBED third order and so
does not simply ride along on its surface's `sigma`. Nothing here decides that question - it
supplies the unperturbed per-surface coefficients, which are the input either way. Deciding it is
the next piece of work, and it is a question about NAT rather than about Buchdahl.

## Papers

The six PDFs read for this proposal, and the four that would be needed to finish it, are listed
in `references.md` under **Nodal aberration theory**. The short version: the folder holds the
application layer - figure error, mount error, freeform surfaces, tolerancing - and Gu's appendix
happens to reproduce the paraxial sigma derivation in full, which is what makes Stages 1 to 3
implementable today. What it does not hold is Thompson's foundational 2005 paper, whose
Appendix A defines the vector algebra every one of the six cites, and which is the authority on
the sign question raised above.
