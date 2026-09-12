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

**No fifth-order NAT, for now.** Thompson's multinodal fifth order wants `W060, W151, W240M,
W242, W331M, W333, W422, W511`. This program computes the fifth order in Rimmer's notation -
`B5, F1, F2, M1..M3, N1..N3, C5, Pi5, E5` - and the map between the two has not been located. The
2025 paper sidesteps it by taking Sasian's set instead, which is a third notation again. This is
the same class of problem, and the same kind of cost, as the Buchdahl-to-Rimmer gap recorded in
`references.md`; it should not be promised until the mapping is in hand.

**No real-ray sigma.** The paraxial route is the small-perturbation limit. It is the right tool
for tolerancing, which is what Stage 2 is, and the wrong one for a system with large deliberate
tilts, which is what Stage 4's customers are.

**The optimiser stays spherical-only.** Stage 2 changes nothing about that rule; it adds an
operand made of third-order quantities, and the refusal in `SphericalOnly.cs` is about Buchdahl's
reconstructed seventh-order aspheric increment, which NAT never touches. Stage 3's analysis
accepts conics precisely because it stops at third order.

**It does not replace tolerance analysis.** It makes tolerance sensitivity something the optimiser
can *see*, which is a different claim from predicting a yield.

## Papers

The six PDFs read for this proposal, and the four that would be needed to finish it, are listed
in `references.md` under **Nodal aberration theory**. The short version: the folder holds the
application layer - figure error, mount error, freeform surfaces, tolerancing - and Gu's appendix
happens to reproduce the paraxial sigma derivation in full, which is what makes Stages 1 to 3
implementable today. What it does not hold is Thompson's foundational 2005 paper, whose
Appendix A defines the vector algebra every one of the six cites, and which is the authority on
the sign question raised above.
