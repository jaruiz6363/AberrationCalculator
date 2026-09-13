# References

The method this program implements is not original work. It is Buchdahl's aberration
coefficients in Rimmer's notation, Robb's analytic spot-size integration over them, and
Rosete-Aguilar and Rayces's re-normalisation of them into comparable quantities. This
file records what each source contributes and whether it has been read.

Status: **[have]** the paper is in hand. **[wanted]** it is cited downstream but has not
been obtained here.

Note on the scanned papers: the text layer on the 1958, 1970 and 1976 scans is 1950s-70s OCR
and is unusable for anything mathematical - the tables and equations come out as noise. Their
prose is readable; their formulae are not. Working from them requires page images. Everything
quoted from Sands (1970) below is prose for that reason; his Eqs. (33)-(34) and (45) and his
Sec. VIII identities have been located but not transcribed.

## Primary sources

**[wanted] Buchdahl, H. A.**, *Optical Aberration Coefficients* (Oxford University Press,
London, 1954). Dover reprint, New York, 1968.
The origin of the coefficients this whole program computes. Never cited in this repo
until now, which was an omission - the coefficients carry his name on every screen.

**The Dover reprint carries the whole journal series as an appendix**, which changes what
acquiring it is worth. Sands (1970), who cites the Dover edition as his reference 1,
describes "the thirteen papers under the same general title and reprinted at the end of
OAC" and cites them by number throughout - OACIII, OACVI, OACVII, OACXII. So the series
runs to at least twelve papers and the reprint holds all of them.

The consequence for this file is that `### The rest of Buchdahl's series` below is not a
shopping list of separate items. **One book discharges it, along with every monograph
section the tertiary work needs** - Secs. 65-66, 80, 81, 84, 85, 22(b) with Eq. (22.27),
218(a), and the Chap. 3 and Eq. (21.6) identities Sands says Table I was built on. It is
the highest-value single acquisition on this page and it is not close.

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

The series runs to at least fourteen papers, and I to XIII are reprinted at the end of the Dover
monograph - see the note under it above. Ten are now in hand. The numbering is exact, from
footnote 2 of XII and footnote 1 of XIV:

| | citation | subject | |
|---|---|---|---|
| I | *J. Opt. Soc. Am.* **46**, 941 (1956) | tertiary spherical aberration - B7's origin | wanted |
| II | **48**, 563 (1958) | the tertiary intrinsic coefficients | have |
| III | **48**, 747 (1958) | computing the tertiary - Table I, Table II | have |
| IV | **48**, 757 (1958) | quaternary (ninth-order) spherical | have |
| V | **49**, 1113 (1959) | on the quality of predicted displacements | have |
| VI | **50**, 534 (1960) | coordinates lying partly in the image space - the W coordinates | have |
| VII | **50**, 540 (1960) | deformation and retardation of the wave front | have |
| VIII | **50**, 678 (1960) | spherical aberration of order eleven | have |
| IX | **51**, 608 (1961) | theory of reversible optical systems | have |
| X, XI, XIII | ? | ? | wanted, citations unpinned |
| XII | **55**, 641 (1965) | remarks relating to aberrations of any order | have |
| XIV | **59**, 1422 (1969) | simplified computational form of the iteration equations | have |

Neither XII nor XIV cites X, XI or XIII, so their citations need a JOSA index; nothing yet
suggests they bear on this work.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. VII. The Primary, Secondary, and
Tertiary Deformation and Retardation of the Wave Front," *J. Opt. Soc. Am.* **50**, 540 (1960).
**The bridge between this program's transverse coefficients and the WAVE-FRONT coefficients
nodal aberration theory is written in.** Its Sec. 6 is titled "The Relations Between
W-Coefficients and Deformation Coefficients" and gives them explicitly at all three orders:
Eq. (6.5) the five primary, Eq. (6.6) the nine secondary, Eq. (6.7) fourteen tertiary.

This page is why the note at the top of this file matters. Its OCR is noise, and the paper sat
here looking like a curiosity until it was rendered as a page image - see
`reading-scanned-pdfs.md` in the working notes. `nat-development.md` records what it
established and what it did not.

Three things it supplies beyond the relations themselves:

- **Eq. (4.6), `eps' = dD/dy`, which he calls exact**: the transverse ray displacement is the
  gradient of the wave-front deformation. That is the mechanism, and it is what makes the
  correspondence checkable against coefficients this program already computes.
- **Redundancy.** The derivation yields two expressions for several coefficients, and he notes
  that "the 10 identities between the W coefficients of the first three orders so implied are
  exactly those given by the equations of VI (4.16-18)". Self-checking, as his tertiary
  identities are.
- **A published numerical answer.** Sec. 7(b): Table I gives the deformation and retardation
  coefficients of the first three orders for the triplet of III Sec. 3 - the same lens whose
  Table I this repository already reproduces - with the W coefficients themselves in VI Table II.

And one trap, Sec. 7(a): his `e` is not unity, so the coefficients carry powers of it -
`A: -1, B: 0, C: 1, S1: -1, S3,S4: 1, S5: 2, S6: 3`, barred coefficients taking an extra factor.
A coefficient right and its power of `e` wrong reads as a plausible number rather than an error.

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

**[have] Cruickshank, F. D. and Hills, G. A.**, "Use of Optical Aberration Coefficients
in Optical Design," *J. Opt. Soc. Am.* **50**, 379-387 (1960).
Robb states his derivation is based on this paper.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. V. On the Quality of
Predicted Displacements," *J. Opt. Soc. Am.* **49**, 1113-1121 (1959).
On how well a truncated coefficient series predicts real ray displacements - the
question this program's field-accuracy caveat is about.

**[have] Hopkins, G. W.**, "Proximate Ray Tracing and Optical Aberration Coefficients,"
*J. Opt. Soc. Am.* **66**, 405-410 (1976).
A different algorithm for the same coefficients. Useful as an independent check.

**[have] Sands, P. J.**, "Aberration Coefficients and Unusual Coordinates for Specifying
Rays," *Appl. Opt.* **9**(4), 828-836 (1970).
Robb points at its section VI for designs with large pupil aberrations. It turns out to
carry rather more than that, and three of the four things it settles were open questions
in this file.

**1. It names the cause of the accuracy limit this program measured.** Sands found a system
whose seventh-order series failed at a half-field of only 35 degrees while others held to
almost 45, and the difference was not field, aperture or figuring:

> "Close inspection revealed that for the system in question the pupil aberrations were
> large, whereas for systems in which the predictions were good over an extended range of
> field angles, the pupil aberrations were quite small."

`spot-prediction.md` reports the same behaviour from the other end - "the order a design
needs is a property of that design, not a general rule", with one of five lenses not
described at seventh order at all. Sands says which property. That makes it a testable
prediction rather than an observation: compute the pupil aberration of those five lenses
and see whether it sorts them in the order their series accuracy does. **Nothing in this
repository has tried that**, and it is the cheapest open question here.

**2. It is a caveat on the distortion report, and on per-aberration attribution generally.**
When rays are specified by the paraxial entrance pupil (Buchdahl's SPC, which is what this
program uses) and the pupil aberrations are large, the ray `S = 0` does not pass through
the centre of the stop. Sands' conclusion is blunt: the coefficients "are not the
coefficients of distortion", and for coma "the image patch obtained under the same
circumstances need bear no resemblance to the familiar comatic image. Strictly speaking in
this case, the coefficients in question do not govern linear coma and their usual
interpretation is invalid."

This bears directly on `--distortion-coefficients`, whose entire claim is that it says
WHICH ORDER the distortion is, and on `ContributionAnalysis`, which attributes spot size
per named aberration. Neither is wrong, and on the designs measured here neither is in
danger. But the condition under which the naming stops meaning anything is now known, is
checkable, and is not currently checked or stated.

**3. The fix, and what it would cost.** Section VI introduces *aperture coordinates* -
specify a ray by where it crosses the physical stop rather than the paraxial entrance
pupil. Vignetting becomes exactly `|S| <= 1`, the usual interpretation of the coefficients
is restored, and the new quantities are nearly free: `A*a = 0` and `A*b = -Gb` at the stop
surface, Eqs. (33)-(34), "their computation is somewhat trivial".

The cost is architectural and it is not small. With SPC the coefficients can be computed
**surface by surface**, which is exactly how `BuchdahlTableI` is written. With aperture
coordinates they cannot:

> "before the exact nth-order surface contributions can be computed at any surface, the
> intermediate coefficients of all lower orders must be known at least up to the aperture
> stop ... the computation of the aberration coefficients must proceed order by order, in
> contrast to the case of SPC."

Sands adds that aperture coordinates are a special case of Buchdahl's GPC and are identical
with the W coordinates of paper VI.

**4. A lead on Table I.** Sands states that "explicit use of the identities OAC Eq. (21.6)
was made when constructing OACIII, Table 1", and that the second set of identities - those
from the invariance of `E*` - were used there "in particular in lines 20-24 and 81-100 in
Table 1". Both sets change form under non-linear coordinates, and Sec. VIII gives the
modified versions.

Worth noting beside the record above that Buchdahl's own printed numbers found errors in
this program's `t100..t108`. Sands independently identifies lines 81-100 as the block built
on those identities. That is a coincidence of ranges and nothing more until someone looks,
but it is the kind of lead that is cheap to follow and expensive to have missed.

**[wanted] Sands, P. J.**, Thesis, Australian National University (1967).
Reference 2 of the above. Two things in it that nothing else here covers: an expansion of
the aberration function about the **principal ray** rather than the axis - which is a
generalisation of Buchdahl's theory, and is conceptually the same move nodal aberration
theory makes with its optical axis ray - and, in Chapter 10, a method for determining the
shape of the actual entrance pupil as a function of field angle "correct to any order",
which Sands says is widely believed to require extensive ray tracing.

**[wanted] Buchdahl, H. A.**, *An Introduction to Hamiltonian Optics* (Cambridge University
Press, New York, 1970), Sec. 37.
Reference 3 of the above. Cited by Sands for the ideal-wave-surface coordinates of his
Sec. VII.

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

## Nodal aberration theory

**None of this is implemented.** These are the sources for the proposal in
`nat-development.md`, and they are listed here so the reading is not lost and so the
gap between what is in hand and what the work needs is on the record.

NAT is a different axis of generalisation from the rest of this file. Everything above extends
the aberration expansion in **order**, with rotational symmetry assumed. NAT extends it in
**symmetry**, with the order held at third. The two compose, and NAT wants as input exactly the
per-surface coefficients this program already computes.

### The foundation

**[have] Thompson, K. P.**, "Description of the third-order optical aberrations of
near-circular pupil optical systems without symmetry," *J. Opt. Soc. Am. A* **22**(7),
1389-1401 (2005). DOI 10.1364/JOSAA.22.001389 (OSA pattern - verify on retrieval).
The paper every one of the six below cites, as [2], [4] or [13], and the foundation of the
whole subject. `Core/Nat/Vec2.cs` implements its Appendix A.

What it settled here:

- **The vector algebra, term for term.** Appendix A prints the component forms - (A1) and (A9)
  for the product, (A6) for the conjugate, (A7) for the product with a conjugate, (A10) for the
  squared vector, and identities (A11) to (A13). `NatTests` checks all of them against the
  implementation rather than against a restatement of it. An earlier draft of
  `nat-development.md` had the product written with `x` as the real axis, which
  contradicted the conjugate rule three lines above it; the appendix settles it.
- **The third-order node structure**, which had been assembled here from Schmid and reasoning:
  coma (4.7)-(4.9), astigmatism (4.15)-(4.22), medial field curvature (4.27)-(4.31). The last
  of those corrected a guess - the medial focal surface is displaced, not tilted, and its
  `B220M` is a DOT product where astigmatism's `B222^2` is a vector square.
- **`W220M = W220 + W222/2`**, his Eq. (4.11), which `Nat/WaveCoefficients.cs` had already
  implemented from the same reasoning and is now sourced.
- **The sigma convention**, though not the paraxial formula for it. Sec. 3 defines sigma
  geometrically, as the projection of the line joining the pupil centre to the surface's centre
  of curvature, with `H_Aj = H - sigma_j` as Eq. (3.1). That resolves the direction Gu's
  Eqs. (21) and (27) disagree about into something testable.
- One implementation instruction, from the remark under Eq. (4.31): the displacement vectors
  "for each aberration are identical", so sigma is computed once per surface and only the
  weights differ.

**[have] Thompson, K. P., Schmid, T., Cakmakci, O. and Rolland, J. P.**, "Real-ray-based
method for locating individual surface aberration field centers in imaging optical systems
without rotational symmetry," *J. Opt. Soc. Am. A* **26**(6), 1503-1517 (2009).
DOI 10.1364/JOSAA.26.001503 (OSA pattern - verify on retrieval).
The authority on the sigma vector, and it settled an open question here rather than merely
informing one.

- **Eq. (10)** gives the paraxial sigma as `-ibar* / ibar`: the angle of incidence of the
  OPTICAL AXIS RAY on the local surface, over the nominal chief-ray incidence. `RealSigma.cs`
  measures exactly that quantity from traced rays.
- **Table 5 shows the two routes agreeing** to four or five figures, so they are not different
  quantities - which established that this repository's disagreement between them was a fault
  and not a convention. The fault is diagnosed in `nat-development.md`: Gu's expression
  is derived for ONE perturbed surface and superposing it over several drops the term that
  makes a rigid translation come out zero.
- **Tables 1 to 5 are a published oracle** - a Ritchey-Chretien prescription, the perturbations
  applied to it, both ray traces, and the resulting sigma vectors. The same standard as
  Buchdahl's Table I: numbers printed beside the lens they were computed on.
- **Eq. (11) gives a SECOND sigma vector for an aspheric surface**, from the aspheric departure
  treated as a zero-power plate after Burch, distinct from the one for the spherical base. At
  the secondary of his telescope the two differ by more than a factor of two. This is Schmid
  2010's `sigma_SPH` and `sigma_ASPH`, and it is not implemented here - so `--nat` is currently
  right only for spherical surfaces.

**[have] Thompson, K. P.**, the multinodal fifth-order trilogy, *J. Opt. Soc. Am. A*:

- I, "spherical aberration", **26**(5), 1090 (2009)
- II, "the comatic aberrations", **27**(6), 1490 (2010)
- III, "the astigmatic aberrations", **28**(5), 821 (2011)

Fifth-order nodal aberration theory. Three things they settle:

- **Appendix B of paper I is the GENERAL pattern**, not a list of special cases:

      Wklm  = sum_j Wklm,j
      Aklm  = sum_j Wklm,j sigma_j        ->  aklm  = Aklm/Wklm,  Hklm = H - aklm
      Bklm  = sum_j Wklm,j (sigma_j . sigma_j)  ->  bklm  = Bklm/Wklm - aklm . aklm
      B2klm = sum_j Wklm,j sigma_j^2      ->  b2klm = B2klm/Wklm - a2klm

  which is exactly what `NatField` already does at third order, scalar-versus-vector
  distinction included. **The fifth order is the same machinery with more coefficients**, so
  the structural work is done and what remains is supplying them.
- **Appendix A of paper I names the fifth-order set**: `W060, W151, W240, W242, W331, W333,
  W420, W422, W511`. Nine wavefront coefficients against Rimmer's twelve transverse ones,
  because a transverse coefficient is a derivative of a wavefront one and the two do not
  correspond term for term. Johnson (1973) Table I, already recorded above, bridges them
  through the NAMED aberrations - "5th oblique spherical, tangential = M1+M2+M3" and the rest -
  which is a second route to the map alongside Buchdahl paper VII Sec. 6.
- **An erratum for Thompson (2005)**: its Appendix A Eq. (A5) is misprinted and should read
  `A B = |A||B| exp(i(alpha + beta))`. `Vec2` implements the corrected form - orientations ADD -
  and `NatTests.MultiplicationAddsOrientations` pins it. The printed error was noticed here
  when the OCR of the 2005 appendix disagreed with its own Eq. (A1); it is now confirmed as a
  misprint by the author rather than a reading difficulty. Thompson also corrects Fig. 10 of
  that paper, where the lower arrow should be labelled `+ib311`.

**[wanted] Shack, R. V. and Thompson, K. P.**, "Influence of alignment errors of a telescope
system on its aberration field," *Proc. SPIE* **251**, 146-153 (1980).
Where the idea starts. Cited by all six papers below; of historical rather than implementation
value, since the 2005 paper supersedes it as a source of equations.

**[wanted] Buchroeder, R. A.**, "Tilted component optical systems," Ph.D. dissertation
(University of Arizona, 1976), and **Thompson, K. P.**, "Aberration fields in tilted and
decentered optical systems," Ph.D. dissertation (University of Arizona, 1980).
The two theses the whole field rests on. Gu's appendix credits Buchroeder with the optical axis
ray tracing method it uses.

### The application layer - all in hand

These six are the PDFs read for the proposal. Between them they cover figure error, mount error,
freeform surfaces and tolerancing, and one of them happens to carry the derivation that makes
the first three stages implementable without the 2005 paper.

**[have] Gu, Z., Wang, Y. and Yan, C.**, "Optical system optimization method for as-built
performance based on nodal aberration theory," *Opt. Express* **28**(6), 7928-7942 (2020).
DOI 10.1364/OE.385089 (verify on retrieval - OSA moved to manuscript-number DOIs around
this date, so the volume.page pattern used above does not apply). Open access.
**The most directly usable paper in the set, and the one Stage 2 implements.** Its appendix
reproduces the paraxial sigma-vector derivation in full - Eqs. (17) to (34) - which is why
third-order NAT can be built here before the 2005 paper arrives.

Two things make it a good fit for this repository specifically. It needs no ray tracing beyond
the paraxial marginal and chief rays, which are already computed; and it is algebraic in those
rays, so it lands on the dual-number path and the optimiser gets analytic derivatives of
tolerance sensitivity for free.

It also supplies a complete acceptance test, which almost nothing else in this file does:
Table 1 is the starting Cooke triplet, Table 3 is the optimised one, and the text gives every
setting needed to reproduce the run. An implementation that can walk from one to the other has
tested the model, the derivative and the optimiser at once.

Reported outcome: about a tenth of a wave of nominal performance given up, nearly two tenths
recovered at the eightieth percentile of a 2000-sample Monte Carlo, standard deviation at
64 per cent of the traditionally optimised design's. Against Zemax's TOLR, comparable quality in
one to two minutes rather than eight hours.

**[have] Schmid, T., Rolland, J. P., Rakich, A. and Thompson, K. P.**, "Separation of the
effects of astigmatic figure error from misalignments using Nodal Aberration Theory (NAT),"
*Opt. Express* **18**(16), 17433-17447 (2010). DOI 10.1364/OE.18.017433.
The access point by which a non-symmetric surface enters NAT at all: a Zernike Z5/6 error at the
stop is added as a field-constant `B222^2`, and everything follows. Gives the binodal solution
`H = +/- i sqrt(B222^2 / W222)` and the diagnostic that matters - figure error keeps the node
midpoint at the field centre, secondary-mirror misalignment does not. Its Eqs. (12)-(16) are the
node algebra transcribed into `nat-development.md`.

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Extending Nodal Aberration
Theory to include mount-induced aberrations with application to freeform surfaces,"
*Opt. Express* **20**(18), 20139-20155 (2012). DOI 10.1364/OE.20.020139.
Takes the non-symmetric surface off the stop, where the beam displacement `dh = (ybar/y) H`
makes the contribution field dependent. The result worth having: three-point mount trefoil
produces **field linear, field conjugate astigmatism** as well as the expected trefoil - the
first time a conjugate field dependence was tied to an observable. Also introduces the freeform
sigma vector for an overlay decentred from the optical axis ray.

Caution for anyone implementing from the PDF in hand: the text layer of its Eq. (10), which is
the constant relating `C333^3` to the measured `z10/11`, is damaged. Read it off the page image.

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Theory of aberration fields for
general optical systems with freeform surfaces," *Opt. Express* **22**(22), 26585-26606 (2014).
DOI 10.1364/OE.22.026585.
The complete table, and the reference for Stage 4. Every Fringe Zernike pair through Z17/18
(tetrafoil), what field-constant vector it contributes at the stop, and which existing NAT term
it extends when the surface is away from the stop. Its conclusion is the useful one: **there are
no new aberration types**, only field dependences NAT already described but which were too small
to notice in tilted-and-decentred systems and dominate in freeform ones.

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Nodal Aberration Theory Applied
to Freeform Surfaces," *Proc. SPIE* (2014).
The conference version of the above, with the aberration-generating Schmidt telescope built and
measured. Shorter and more readable; the 2014 Optics Express paper is the one to implement from.

**[have] Jiang, Y., Wang, L., Zeng, X., Liu, Y., Hu, J. and Li, W.**, "Aberration field
distribution characterization and tolerance analysis based on nodal aberration theory,"
*Opt. Express* **33**(23), 49313-49330 (2025). DOI 10.1364/OE.582222 (verify on retrieval).
Open access.
The current state of the art, and the source of the validation threshold used in the proposal:
full-field displays from the analytic model compared against real-ray tracing by a similarity
measure, with agreement above 0.8 taken as validation.

Extends to fifth order and to off-axis pupils, and is a caution as much as a source: it takes
its fifth-order coefficients from **Sasian's** set rather than Thompson's or Rimmer's, so the
notation problem noted above has three sides to it and not two. Cites Zhang for the induced
fifth-order components and for the exact freeform decentre-and-tilt formulas; neither has been
obtained here.

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
`ExactConicSurfaceTests`. **This is what establishes the ASPHERIC third and fifth order** - they were available first and
they need no other program at all. (An earlier version of this line added "and it had to,
because no other implementation available here computes them", which is wrong: FIFTHORD handles
aspheres. It was not the ESTABLISHING check, but it was never unable to be a corroborating one.
See **On FIFTHORD** below.)

**3. Inverse real ray tracing.** `CoefficientInversion` recovers coefficients from the
landings of real traced rays by scaling and an odd-polynomial fit. It is this repository's
own code and owes nothing to any other program. Eight test files use it as their reference,
including every aspheric one.

**4. An independent implementation of the third and fifth order**, written in C++ by this
repository's author directly from Buchdahl's book - a different lineage from the same
source, described below. It does not implement aspherics.

**4a. The same C++ codebase supplies the tilted-surface transform.** `RayTrace/LocalFrame.cs`
is a port of its `coordinate_break_C`, and the conventions it fixes - degrees, the minus sign
on the x tilt where the y tilt has none, the composition order, decentre-before-rotate - are
copied rather than re-derived, because they are the error-prone part and a tested version
existed. The INVERSE is derived here; `LocalFrameTests` guards the derivation by requiring the
round trip to be the identity, and the whole transform by requiring a uniformly decentred lens
to image exactly like the nominal one moved over. No third-party program is involved at any
point.

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
agreement was an outcome, not a guide. FIFTHORD does not compute the twenty tertiary
coefficients, so it could not have settled those.

**Correction, 2026-09-12. This section previously said FIFTHORD computes neither the aspheric
coefficients nor the tertiary ones. The aspheric half of that is wrong: FIFTHORD does handle
aspheres.** The claim was never checked, and the page contradicted itself two paragraphs later
by listing "the aspheric r^8 handling" among the things this program departs from it on - a
departure that presupposes something to depart from. What the sentence was reaching for is
narrower and still true: the macro was not what established the aspheric third and fifth order
here, because the closed-form analytic surfaces above were available first and are stronger.
But it could have corroborated them, and saying it could not was a misstatement.

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
