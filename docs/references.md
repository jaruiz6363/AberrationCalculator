# References

The method this program implements is not original work. It is Buchdahl's aberration
coefficients in Rimmer's notation, Robb's analytic spot-size integration over them, and
Rosete-Aguilar and Rayces's re-normalisation of them into comparable quantities. This
file records what each source contributes and whether it has been read.

Status: **[have]** the paper is in hand. **[wanted]** it is cited downstream but has not
been obtained here.

Note on the scanned papers: the text layer on the 1958 and 1976 scans is 1950s-70s OCR and
is unusable for anything mathematical - the tables and equations come out as noise. Their
prose is readable; their formulae are not. Working from them requires page images.

## Primary sources

**[wanted] Buchdahl, H. A.**, *Optical Aberration Coefficients* (Oxford University Press,
London, 1954). Dover reprint, New York, 1968.
The origin of the coefficients this whole program computes. Never cited in this repo
until now, which was an omission - the coefficients carry his name on every screen.

**[wanted] Rimmer, M.**, M.S. Thesis, Institute of Optics, University of Rochester,
Rochester, New York, 1963.
Recast Buchdahl's coefficients into the notation used at the Institute of Optics, which
is the notation this program reports (B, F, C, Pi, E, B5, F1, F2, M1-M3, N1-N3, C5, Pi5,
E5, B7). Cited as reference [5] of Rosete-Aguilar and Rayces (1995).

Note: earlier versions of this repo cited Rimmer as "1962, University of Rochester Summer
School in Optics". That attribution came in with the implementation and could not be
verified. The primary literature gives the 1963 M.S. thesis, and that is what is cited
here now.

**[have] Robb, P. N.**, "Analytic merit function based on Buchdahl's aberration
coefficients," *J. Opt. Soc. Am.* **66**(10), 1037-1041 (1976).
DOI 10.1364/JOSA.66.001037 (OSA pattern - verify on retrieval).
`Prms.cs` implements this paper. Eq. (2) is the ray intersection polynomial; Eq. (4) is
its variance over the pupil. Two statements in it are load-bearing here:

- Distortion terms are identically zero in the spot size, because distortion moves the
  image without resizing it. This is why `E` and `E5` are absent from `Prms.Terms`.
- The Conclusions state that the image plane "ceases to become a design variable", that
  optimising the last thickness "will not have the slightest effect on the solution and
  will only consume computing time or cause the optimization algorithm to become
  unstable", and that focus must be adjusted afterwards by the method of Sands (1973).
  This is the defocus-blindness of PRMSA, documented by its author in 1976.

**[have] Rosete-Aguilar, M. and Rayces, J.**, "Re-normalization of Buchdahl-Rimmer
aberration coefficients to RMS expressions," *Proc. SPIE* **2730**, 499-502 (1996).
ISBN 0-8194-2111-1.
`ContributionAnalysis.cs` implements the geometric half of this paper - coefficients
re-normalised to RMS spot size, so that two aberrations with equal re-normalised values
do equal damage. The paper also covers the wave-aberration half in terms of the Strehl
ratio, which this program does not implement.

**[have] Rosete-Aguilar, M. and Rayces, J.**, "Renormalization of the Buchdahl-Rimmer
third- and fifth-order geometric aberration coefficients to rms wave aberration function
expressions," *J. Mod. Opt.* **42**(12), 2435-2445 (1995). DOI 10.1080/713824341.
The wave-aberration treatment, published a year earlier. Not implemented here; cited
because it is the fuller derivation of the re-normalisation idea.

**Welford, W. T.**, *Aberrations of Optical Systems* (Adam Hilger, Bristol), ch. 8.
The Seidel sign and normalisation convention `SeidelCoefficients.cs` follows. Edition and
year still to be pinned down for a proper citation.

## Cited by the sources above, not yet read

These are the references Robb and Rosete-Aguilar rely on. Two of them bear directly on
open questions in this program.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. III. The Computation of
the Tertiary Coefficients," *J. Opt. Soc. Am.* **48**, 747-756 (1958).
The route to seventh order. "Tertiary" is Buchdahl's word for it.

Robb assumed a COMPLETE seventh-order polynomial. He used "the first three polynomials
(of orders 3, 5, and 7)", and his Table I gives "Equations for the 37 S(J) coefficients",
of which S(5), S(17) and S(37) are carried but identically zero because distortion moves
the image without resizing it. Those three indices are the last of three blocks - 5
primary, 12 secondary, 20 tertiary - which is how his 37 partitions. His equations are
written in primary terms, secondary terms and tertiary terms, matching that split.

CONFIRMED. Robb Eq. (2), p.1038, names them: sigma_1..sigma_5, mu_1..mu_12, tau_1..tau_20,
with the distortion terms sigma_5, mu_12 and tau_20 falling at positions 5, 17 and 37.
No longer an inference.

This program supplies 18 of those 37: five primary, twelve secondary, and B7. The other
nineteen tertiary coefficients are simply absent, which is why full-field PRMS is the
weakest number the report prints. It is a coefficient-supply gap, not a limit of Robb's
method.

What this paper provides:

- The full computing scheme for primary, secondary AND tertiary coefficients, as Table I.
  192 entries per surface. Buchdahl notes his assistants took about three hours per
  surface on desk calculators, which is a fair proxy for how much arithmetic it is.
- **Spherical surfaces only.** Aspheric figuring would still need separate treatment, as
  this program already does at third and fifth order.
- **Six identities between the tertiary coefficients** (Sec. 6), which Buchdahl uses as
  his own final check. That is a self-test independent of any oracle - worth having when
  transcribing a 192-entry table.
- Seventh order has eight aberration types: the six traditionally named ones plus, in
  Buchdahl's words, "two unnamed types". This program carries one of the eight.
- A correction to the monograph: "Eqs. M (81.3) contain one (and only one) misprint. In
  fact, the product ApBF in the factor multiplying b in the equation for t5 should have a
  minus sign instead of a plus sign before it." Anyone implementing from the 1954 book
  alone inherits that error.

The paper is NOT self-contained. It defers to the monograph for the equations behind the
scheme - M Sec. 81 (Eqs. 81.3), Sec. 84, Sec. 22(b) and Eq. (22.27), and Sec. 218(a).
Those sections are needed before the scheme can be implemented.

Note that the 1954 book predates this paper by four years, and the Dover 1968 reprint is
normally unchanged, so the book alone may not carry the tertiary computation.

### The rest of Buchdahl's series

**[wanted] Buchdahl, H. A.**, *J. Opt. Soc. Am.* **46**, 941 (1956). Paper I of the series.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. II. The Tertiary Intrinsic
Coefficients," *J. Opt. Soc. Am.* **48**, 563-568 (1958).
Gives the tertiary intrinsic coefficients of spherical surfaces in closed form: ten
quantities z1..z10, then t_1p..t_10p as polynomials in q and the z, with the barred ten
following as tbar = q*t. "The ten unbarred intrinsic coefficients ... require a total of
only thirteen entries per surface."

Its introduction also states the field-versus-aperture limitation this program measured,
sixty-eight years earlier: the limitation of stopping at fifth order "appears to be the
relatively poor agreement between predicted and actual aberrations in the outer parts of
the field, rather than the inaccuracies of the predictions for large apertures", caused by
"the intractable behavior of the coefficients of oblique spherical aberration".

It also explains why B7 is the one seventh-order coefficient in common tooling: "the only
tertiary coefficient considered in detail so far has been that of spherical aberration",
which was the subject of paper I.

**[wanted] Cruickshank, F. D.**, *Australian J. Phys.* **11**, 41 (1958).
The analytical initial-design method Buchdahl pairs his coefficients with. Not needed for
the coefficients themselves.

**[wanted] Sands, P. J.**, "Aberration Coefficients and Surfaces of Best Focus," *J. Opt.
Soc. Am.* **63**, 582-588 (1973).
The focus-adjustment method Robb points to for exactly the defocus-blindness problem.
The highest-value item on this list for anyone optimising against PRMSA.

**[wanted] Cruickshank, F. D. and Hills, G. A.**, "Use of Optical Aberration Coefficients
in Optical Design," *J. Opt. Soc. Am.* **50**, 379-387 (1960).
Robb states his derivation is based on this paper.

**[wanted] Buchdahl, H. A.**, "Optical Aberration Coefficients. V. On the Quality of
Predicted Displacements," *J. Opt. Soc. Am.* **49**, 1113-1121 (1959).
On how well a truncated coefficient series predicts real ray displacements - the
question this program's field-accuracy caveat is about.

**[wanted] Hopkins, G. W.**, "Proximate Ray Tracing and Optical Aberration Coefficients,"
*J. Opt. Soc. Am.* **66**, 405-410 (1976).
A different algorithm for the same coefficients. Useful as an independent check.

**[wanted] Sands, P. J.**, "Aberration Coefficients and Unusual Coordinates for Specifying
Rays," *Appl. Opt.* **9**, 828-836 (1970).
Robb notes that section VI of this paper handles designs with large pupil aberrations,
where the plain seventh-order treatment becomes inexact.

**[wanted] Woodruff, C. J.**, "A Comparison, Using Orthogonal Coefficients, of Two Forms
of Aberration Balancing," *Opt. Acta* **22**, 933-941 (1975).

**[have] Johnson, R. B.**, "Polynomial Ray Aberrations Computed in Various Lens Design
Programs," *Appl. Opt.* **12**, 2079-2082 (1973).
The cross-program notation map Robb points at. It confirms that sigma/mu/tau IS the
standard nomenclature, and its Table I gives the published name for each coefficient
combination:

| aberration | base term | in this program's names |
|---|---|---|
| 3rd spherical | sigma1 | B |
| 5th spherical | mu1 | B5 |
| 7th spherical | tau1 | B7 |
| 3rd linear coma | 3 sigma2 | 3F |
| 5th linear coma | mu2 + mu3 | F1 + F2 |
| 3rd linear astigmatism, tangential | 3 sigma3 + sigma4 | 3C + Pi |
| 3rd linear astigmatism, sagittal | sigma3 + sigma4 | C + Pi |
| 5th linear astigmatism, tangential | mu10 | 5 C5 + Pi5 |
| 5th linear astigmatism, sagittal | mu11 | C5 + Pi5 |
| 3rd distortion | sigma5 | E |
| 5th distortion | mu12 | E5 |
| 5th oblique spherical, tangential | mu4 + mu6 | M1 + M2 + M3 |
| 5th oblique spherical, sagittal | mu5 | M2 |
| 5th elliptical coma, tangential | mu7 + mu8 | N1 + N2 |
| 5th elliptical coma, oblique | mu9 | N3 |
| 3rd Petzval | sigma4 | Pi |

Two historical points it settles:

- Across all six programs surveyed in 1972, **tau1 is the only seventh-order term exposed**.
  That is the same situation this program inherited, and it is why B7 stands alone.
- Yet FLAIR 43, from the Institute of Optics at Rochester - Rimmer's own institution - had
  a subroutine SWORD which "computes all 37 coefficients comprising the third, fifth and
  seventh orders". So a program computing the full set existed in 1972. The capability was
  not exposed, not absent.

Johnson's purpose is also a caution: he found "significant variances in term definitions"
between programs and reports "several anomalous term computations". Comparing a named
aberration across tools without checking its definition is unsafe.

**[wanted] Rayces, J.**, *Optica Acta* **11**, 85 (1964).
Cited by Rosete-Aguilar and Rayces for the conversion between the reduced geometric
components and the wave aberration function.

**[wanted] Nijboer, B. R. A.**, *Physica* **10**, 679 (1943).
The wave aberration function used in the 1995 paper. Only needed if the wave-aberration
re-normalisation is ever implemented here.

## Books that shaped the approach

These did not supply the equations - Buchdahl, Rimmer, Robb and Forbes above did that - but
they shaped what this program tries to be. They are listed because a reference list of
papers alone would misrepresent where the thinking came from.

**[have] Kidger, M. J.**, *Intermediate Optical Design* (SPIE Press, Bellingham, 2004).
SPIE Press Monograph PM134, ISBN 978-0-8194-5217-7. Published posthumously; his
*Fundamental Optical Design* (SPIE Press, 2001, PM92) precedes it.
Aberration theory as a designer actually uses it rather than as a subject to be surveyed.
The stance this program takes from it is that per-surface contributions are the useful
form: a total says a design is wrong, and a breakdown says where to go and look.

**[have] Dilworth, D. C.**, *Lens Design: Automatic and quasi-autonomous computational
methods and techniques* (IOP Publishing, Bristol, 2018; second edition 2020).
That a program should be an active participant in the design rather than a calculator the
designer drives - that it should be capable of saying what to change and not only what is
wrong. Both halves of that are here now: the optimiser is his PSD, and the
intrinsic-and-induced split exists because "which surface, and is it that surface's own
fault" is the question a designer needs answered before deciding what to do.

**[have] Dilworth, D. C.**, "The Ascendency of Numerical Methods in Lens Design",
*J. Imaging* **4**(12), 137 (2018). DOI 10.3390/jimaging4120137. Open access.

**The strongest published argument against the premise of this program, and it is here
because of that.** Dilworth holds that aberration theory has been overtaken in design work:

> "The authors of recent textbooks on lens design invariably instruct the reader to first
> work up a third-order solution by hand before submitting it to computer optimization.
> Even that idea is also now obsolete, in my opinion."

> "I argue that the theoretical approach has collapsed under its own weight. One simply
> cannot, in spite of generations of mathematical genius, design lenses according to a set of
> algebraic statements."

He credits the success of SYNOPSYS to two things and neither is aberration theory: the
PSD III optimiser, and a binary search over the SIGNS of element powers - a five-element
lens is 32 cases rather than a mesh of 200,000 nodes, and each is optimised numerically.

The answer this repository would give is not a rebuttal, and what it has since become makes
the agreement larger rather than smaller. It now optimises and it now searches - with his
PSD as the local step and a basin hopping above it - so the disagreement has narrowed to one
thing: what the merit function is MADE OF. Here it is made of aberration coefficients rather
than of traced rays, and the claim made for that is a modest one, which is that it gets close
enough for real ray tracing to finish the job. It is not offered as a way to arrive at a
starting point, which is the ground he is arguing on and where he is right.

The other half stands unchanged. "Surface 5 contributes almost nothing of its own and nearly
all of what it carries was induced upstream" is not a statement a merit function makes, at
any speed, and it is what tells a designer where to go and look. Dilworth's own case is that
the computer should say what to change; the remaining disagreement is over whether a number
the designer can reason about is worth having on the way there.

*A note on how this entry came to be written.* It was first recorded here, on a
recollection, as saying the opposite - that SYNOPSYS uses aberration theory early because it
is fast against real ray tracing. Reading the paper showed that it argues close to the
reverse. The recollection may still hold for the BOOK, or for how DSEARCH forms its first
merit function; neither has been checked, and neither should be cited until it is.

## Implementation provenance

The published sources above are the authority for the method. What establishes that this
program realises it correctly is, in the order the checks are worth anything:

**1. Buchdahl's own printed numbers.** Paper III works Table I through for a specific
triplet. `BuchdahlPublishedTableTests` reproduces it entry by entry, and his published
totals for the tertiary. That is the oracle, and it is what found the errors in t100-t108.

**2. Closed-form analytic surfaces, which need no other program at all.** A parabolic mirror
images infinity onto its focus with no spherical aberration at any order, so every order
must cancel term for term. A single conic surface can be traced analytically and expanded as
`eps = a3 y^3 + a5 y^5 + a7 y^7 + ...`, which gives the third, fifth and seventh order
coefficients as numbers - no series, no table, no macro. See `ParabolicMirrorTests` and
`ExactConicSurfaceTests`. **This is what establishes the ASPHERIC third and fifth order**,
and it had to, because no other implementation available here computes them.

**3. Inverse real ray tracing.** `CoefficientInversion` recovers coefficients from the
landings of real traced rays by scaling and an odd-polynomial fit. It is this repository's
own code and owes nothing to any other program. Eight test files use it as their reference,
including every aspheric one.

**4. An independent implementation of the third and fifth order**, written in C++ by this
repository's author directly from Buchdahl's book - a different lineage from the same
source, described below. It does not implement aspherics.

**5. Forbes' series trace** for the seventh order, from a separate published paper with no
shared code. See `forbes.md`.

### On FIFTHORD

The FIFTHORD macro by M. MacFarlane (1998) - with the mirror index-sign correction of
T. A. Mitchell (2003) and the Lagrange-invariant correction of J. Sasian (2019) - realises
the same published method, and its results agree with this program's: 586 coefficient values
across seven designs, to a worst-case residual of 1.1e-12.

That agreement is recorded because it is worth recording, and because a reader with that
macro can repeat it without trusting anything here. **It is not what determined that
these results are correct.** The checks above did, and they were available first; the
agreement was an outcome, not a guide. FIFTHORD computes neither the aspheric coefficients
nor the twenty tertiary coefficients, so it could not have settled either.

The macro is not redistributed here and no part of it is included in this repository. Where
this program departs from it - the aspheric r^8 handling, the F/number sign convention, the
analytic pupil integration in place of a table of constants - `verification.md` says so.

### Independent cross-check available

A second implementation of the third- and fifth-order coefficients exists, written in C++
by this repository's author directly from Buchdahl's book. It is his own work and is
available to check against.

It is worth using because it is an independent derivation. It takes Buchdahl's own route
rather than Rimmer's: a p/q paraxial ray pair as the basis instead of marginal and chief,
reduced angles, k = n/n' and k1 = 1 - k, everything scaled to unit focal length, and a
numbered auxiliary table transcribed from the text. Agreement between it and the macro
would corroborate 17 of the 18 coefficients from two lineages rather than one, which the
present 586-value validation cannot do - it is a single lineage, so a shared misconception
would not surface.

It does not compute seventh order. The function carries a `compute_tertiary` flag that
sets a status field and gates nothing, so `B7` would remain corroborated by the macro
alone.

**It reproduces the values of the triplet example Buchdahl published.** Confirmed
independently here: extracted standalone and run on `TRIPLET_BUCHALD_EFL50.lhlt`, it
agrees with paper III Table I to 5.3e-6 on the stop parameter, 4.3e-5 on the primary
coefficient ap, and 2.6e-5 on the secondary s1p - residuals at the level seven-digit
inputs support. The prescription identifies itself: the first curvature scaled by the
focal length, 0.09648784 x 50 = 4.824392, is exactly Buchdahl's tabulated t3 = 4.82439.
The extraction and the run are kept with the author's working notes, outside this
repository.

An earlier note here suggested it might carry the misprint that paper III corrects. That
was wrong: the misprint is in the equation for t5 of (81.3), which is a TERTIARY formula.
This code computes primary and secondary only, so the misprint cannot reach it. The
correction matters when (81.3) is implemented, not before.

Three points in that code to settle against the book if the cross-check is done:

- It computes `N * (1/c) * i1_p`, guarded to zero when the curvature is near zero. As
  c -> 0 that quantity diverges rather than vanishing, and a plano surface does contribute
  aberration. This program never divides by curvature at all, so the two differ here.
- `k / k1^2` is guarded to zero at a same-index interface, and the product it feeds also
  diverges rather than vanishing.
- In the block computing its terms 103 to 108, one column of the recurrence runs
  61, 59, 60, 62, 63, 64 where the neighbouring columns march monotonically. That may be
  a genuine irregularity in the book's formulae or a transcription slip; it is cheap to
  check and expensive to miss.

### Status of the tertiary work

Working notes are kept outside this repository,
because they transcribe material from copyrighted papers. Summary of where it stands:

The chain from paraxial data to the twenty tertiary coefficients is now sourced end to end
except for one link. Paper II gives the intrinsic coefficients in closed form; the
monograph's Eqs. (81.3) give the totals; Sec. 80 gives the aspheric correction to the
intrinsic quantities.

That missing link has since been found. Robb's Eq. (2), p.1038, writes the ray
displacement explicitly in Buchdahl's coefficients - sigma_1..sigma_5 primary,
mu_1..mu_12 secondary, tau_1..tau_20 tertiary. Five plus twelve plus twenty is thirty-seven,
his stated total, and the three distortion terms sigma_5, mu_12, tau_20 fall at positions
5, 17 and 37 - exactly the S(J) he reports as identically zero. The earlier partition was
inferred; it is now confirmed.

Two things follow. First, the notational bridge asserted in `Prms.cs` is correct: every
radial power, field power and theta function in its Ey/Ez lists matches Eq. (2) term for
term, so the implementation is faithful and is simply fed tau_1 alone. Second, the PRMS
side is now fully specified - the eighteen additional seventh-order terms and their theta
functions are written down in the working notes, and need only two new theta functions,
cos4 and sin4.

What remains is the VALUES of tau_2..tau_20, which is the Buchdahl (81.3) work. The two
halves of the problem are now cleanly separated.

One finding changes the scope. Eqs. (81.3) are written in Buchdahl's own quantities -
A, B, C and S1..S6 with their barred and p/q variants - whereas this program computes
fifth order in Rimmer's notation. Adding seventh order is therefore not "nineteen more
coefficients on top of what we have"; it needs a Buchdahl-notation pipeline for primary
and secondary as well, or a notation map that has not yet been located.

The three numeric tables of paper II have been transcribed and verified against Buchdahl's
own column identity - every column sums to zero except the last, which sums to unity. That
is 160 integers checked by an independent test rather than by re-reading. Paper III Sec. 6
provides six further identities between the tertiary coefficients themselves, so an
implementation would be self-testing throughout.

### Phase 0 resolved: the tau mapping is published

Paper III p.753 closes the last open question. Its Table II gives all twenty of Robb's
tertiary coefficients in terms of Buchdahl's, with numerical values for his own triplet -
`tau1 = T1`, `tau2 = Tbar1 + T2/2`, up to `tau20 = Tbar10`, where `T1 = T_1pk'/(N_k' v_pk')`
and analogously for the rest, that division converting augmented coefficients to actual
ones. Twenty published expected values on a lens whose prescription is also published, so
any implementation has a complete acceptance test.

The same page gives the tertiary displacement equations, and they are Robb's Eq. (2)
seventh-order lines verbatim - Robb took his seventh order straight from Buchdahl.

It also publishes the triplet's constitution. A reconstruction of it
reproduces every curvature, separation and index ratio to the printed digits, and the
oracle's stop parameter 0.113227601 matches the published p = 0.113227 directly rather than
by inference.

The largest consequence is that the 692-term Eqs. (81.3) are not needed. Table I's later
entries produce the tertiary coefficients directly - t121..t130 are exactly paper II's
z1..z10, and t131 onward are t1p, tbar1p, t2p and the rest. Table I is the condensed scheme
Sec. 84 describes, 192 entries per surface as a linear list of formulae, and it was the
point of the paper. That replaces the largest and riskiest phase of the work with a
transcription about a quarter the size. The trade is that Table I is spherical surfaces
only, so seventh order would initially be valid for all-spherical designs and the report
would have to say so.

Worth keeping in view, from p.753: in this triplet the large tertiary elliptical coma "is
due not so much to that of the seventh order, but due rather to the ninth and higher
orders". Seventh order will improve the full-field prediction. It will not make it exact.
