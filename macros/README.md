# ZPL macros

## BUCH7.ZPL

Third-, fifth- and seventh-order Buchdahl coefficients for a centred system of
**spherical** surfaces, per surface, with the intrinsic and induced parts separated.

### Why

`FIFTHORD.ZPL`, which ships with OpticStudio, gives the third and fifth orders plus
seventh-order spherical aberration, as system totals. What it does not give is the
seventh order entire, or any of it per surface, or the split between what a surface
generates on its own and what it generates by acting on the aberration handed to it.

That split is the useful part. On a Cooke triplet the induced share is the larger one on
four surfaces of six, and on one of them it is more than a thousand times the intrinsic -
a surface that is blameless in isolation and is nonetheless a large contributor. The cure
for that is upstream of the surface the total blames, and a table of totals cannot say so.

### Status

All four stages are written and all four pass, at either conjugate.

| stage | contents | agrees with | result |
|---|---|---|---|
| A | paraxial basis, per-surface primary, running sums | the Seidel analysis in OpticStudio | 30 of 30, both conjugates |
| B | fifth order per surface, intrinsic and induced, plus B7 | FIFTHORD | 18 of 18 totals, both conjugates |
| C | Buchdahl's Table I, t1 to t155, per surface | `reference/CookeTriplet_TableI.txt` | 155 of 155 to 7e-16 |
| D | the twenty tau, intrinsic and induced per surface | that file's tau block, and a Forbes series trace at 250 mm | 420 of 420; 20 of 20 to every printed digit |

### Where the numbers come from, and what merely agrees with them

The method is Buchdahl's and the authority for it is his published work. **The check that
settles the scheme is against his own printed numbers**: paper III prints Table I worked
through for a specific triplet, and `BuchdahlPublishedTableTests` reproduces it — entry by
entry for the scheme, and against his published totals for the tertiary. That is the oracle.
It is what found the errors in t100–t108. The reference file behind stages C and D is an
implementation held to it.

The second independent route is Forbes' series trace, *J. Opt. Soc. Am.* **73**, 782 (1983)
— a different paper, different mathematics, no shared code — which stage D is checked
against at both conjugates.

**OpticStudio's Seidel analysis and FIFTHORD are neither of those.** They are used here as
cross-checks *a reader can repeat without trusting anything in this repository*, which is
their whole value: two of the four stages can be confirmed without taking this macro's word
for anything. They are not the source of the method and they are not the authority for the
numbers. Neither computes the seventh-order set this macro exists for, so neither could be.

Both conjugates. The object position enters the scheme in exactly one place - the four
starting values of the p and q rays, M (13.4) - and paper III says so in a parenthesis:
"the choice of different coordinate systems reflects itself only in the starting values
of y_p, v_p, y_q, v_q (cf. M Secs. 12-13)". Those four were hard-coded to the
infinite-conjugate case, so every finite-conjugate system was quietly given the answer
for an object at infinity, identical to seven figures and with no other symptom.

On a 250 mm Cooke triplet the corrected macro agrees with three things that share no code
with each other: the Seidel analysis in OpticStudio at third order (30 of 30), FIFTHORD at
fifth (18 of 18), and a Forbes series trace on all twenty seventh-order coefficients, to
every printed digit. Seventh-order spherical aberration is reached twice over within the
macro itself, through the fifth-order working and through Table I and Table II, and both
give 2.305287E-03 — a check that needs nothing outside it.

Both checkers were confirmed by perturbing values and watching them report exactly the
perturbed ones. An earlier version of this work spent an afternoon chasing thirty-three
reported failures in the macro that were a bug in the script comparing it, so a checker
that says nothing is wrong is not believed until it has been shown able to say otherwise.

The two orders overlap in one number and disagree in none: seventh-order spherical
aberration is reached in stage B through the fifth-order working and in stage D through
Table I and Table II, and on the Cooke triplet both give 1.681450E-03.

Stage D prints in TRANSVERSE measure where A, B and C print unconverted per surface.
That is not an inconsistency to be tidied away: every third- and fifth-order coefficient
takes the same conversion factor, so one factor at the foot of the table serves them all,
but each tau multiplies a different power of aperture and field and so takes its own.
Printed unconverted, tau1 and tau20 could not be compared with each other. The one
overlap between the conventions, tau1 against B7, is computed by two different routes
and agrees, which is the check that ties the two halves of the listing together.

### The companion

`zosapi/` holds FORBES7, which computes the same three orders by G. W. Forbes' series
trace, *J. Opt. Soc. Am.* **73**, 782 (1983), as a C# program driving OpticStudio through
the ZOS-API. It is the better tool on a **figured** system, where this macro stops: it
carries conics and even aspheres at all three orders and separates what the figuring itself
contributes. This macro is the better tool everywhere else — it is one text file, it needs
nothing installed, and two of its four stages check against OpticStudio's own analyses.

The two agree, which is the point of having both. Seventh-order spherical aberration comes
out of this macro through Buchdahl's fifth-order working and out of FORBES7 through a power
series, and both give 1.681450E-03 on the Cooke triplet at infinite conjugate and
2.305287E-03 with the object at 250 mm.

### Limits, which the macro enforces rather than documents

Spherical surfaces only. A conic or an even-asphere term makes it stop with an
explanation instead of returning a plausible number. Buchdahl gives the aspheric scheme -
§65-66 and (85.2)-(85.5) - but never published the arranged table for it, and that
arrangement is the one part of this work with no printed answer to check against. The
aspheric case is handled by a different method entirely; see `docs/forbes.md`.

Object at infinity, rotationally symmetric, sequential, no mirrors.

### On the two ways it finds Buchdahl's p

`p` is the entrance pupil position in focal lengths, and everything else is built on it.
The macro takes it from the system data and, where there is a field to trace a chief ray
at, derives it a second way from that ray - because the two rest on different sign
conventions, and a silent disagreement would poison every number below it. If they
disagree it says so rather than picking one.

### Expected output, CookeTriplet - stage B

From the C# on the same lens. The eighteen system totals are in transverse measure and
must match FIFTHORD run on the same file.

```
Fifth order per surface, unconverted   (int / ind / tot)
 Surf                B5             F1             F2             M1             M2             M3
    1  int -4.092955E-004 -7.189070E-004 -4.060244E-004 -6.207642E-004 -1.279330E-003  1.550576E-003
      ind  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000
      tot -4.092955E-004 -7.189070E-004 -4.060244E-004 -6.207642E-004 -1.279330E-003  1.550576E-003
    2  int -1.436617E-004  1.323774E-003  8.769318E-004 -5.455176E-003 -2.730198E-003 -5.343197E-003
      ind -6.263158E-004  2.346151E-003  1.207026E-003 -2.329584E-003 -2.686517E-003 -3.792866E-004
      tot -7.699775E-004  3.669924E-003  2.083958E-003 -7.784760E-003 -5.416715E-003 -5.722484E-003
    3  int  1.492490E-003 -4.198192E-003 -1.867355E-003  5.832534E-003  3.806345E-004 -6.302961E-003
      ind  2.351401E-003 -7.311034E-003 -4.979961E-003  1.119313E-002  1.050506E-002  1.527106E-002
      tot  3.843891E-003 -1.150923E-002 -6.847316E-003  1.702566E-002  1.088569E-002  8.968097E-003
    4  int  9.013352E-004  5.537364E-003  3.898870E-003  1.417514E-002  9.738758E-003  1.363791E-002
      ind  2.132639E-003  1.463683E-003 -3.001885E-004 -6.689307E-003 -2.344094E-003 -1.814739E-002
      tot  3.033974E-003  7.001047E-003  3.598681E-003  7.485828E-003  7.394664E-003 -4.509476E-003
    5  int -9.293814E-006 -3.711552E-005 -3.685577E-007 -2.914493E-006  2.010163E-004  7.046019E-004
      ind -7.594452E-004 -3.672453E-003 -1.821558E-003 -6.553210E-003 -5.311631E-003  1.722267E-003
      tot -7.687390E-004 -3.709569E-003 -1.821926E-003 -6.556124E-003 -5.110615E-003  2.426868E-003
    6  int -3.213308E-003  6.895329E-003  5.083310E-003 -5.733068E-003 -6.361125E-003 -2.540855E-003
      ind -1.781624E-004  2.106077E-004 -4.365193E-004  1.647346E-003  4.213939E-003  6.191672E-003
      tot -3.391470E-003  7.105937E-003  4.646790E-003 -4.085721E-003 -2.147186E-003  3.650818E-003

 Surf                N1             N2             N3             C5            Pi5             E5
    1  int -9.779737E-004  4.123452E-003  1.469063E-003  5.615067E-004  3.214874E-003  2.886824E-003
      ind  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000
      tot -9.779737E-004  4.123452E-003  1.469063E-003  5.615067E-004  3.214874E-003  2.886824E-003
    2  int  8.491944E-003  3.323546E-002  8.308053E-003 -1.292058E-002  1.454949E-005  4.014262E-002
      ind -1.775234E-003  6.496174E-003  3.114606E-003 -1.220429E-003 -1.499338E-003 -1.235677E-002
      tot  6.716711E-003  3.973163E-002  1.142266E-002 -1.414101E-002 -1.484788E-003  2.778586E-002
    3  int -5.944409E-004  3.473054E-002  1.244357E-002 -9.716628E-003 -1.029460E-002  3.125174E-002
      ind -1.139817E-002 -7.073607E-002 -2.371288E-002  1.815842E-002  1.094115E-002 -4.451463E-002
      tot -1.199261E-002 -3.600552E-002 -1.126931E-002  8.441793E-003  6.465552E-004 -1.326289E-002
    4  int  1.770362E-002  5.629517E-002  1.575174E-002  1.431716E-002  8.843223E-004  2.763404E-002
      ind -1.436581E-002 -3.906255E-002 -6.825315E-003 -9.778304E-004  3.262900E-003  1.811926E-002
      tot  3.337814E-003  1.723263E-002  8.926420E-003  1.333933E-002  4.147222E-003  4.575330E-002
    5  int  7.948019E-004  7.823814E-003  2.518938E-003  4.979836E-003  3.133763E-003  3.208049E-002
      ind  2.306536E-003 -1.901303E-002 -7.939031E-003 -2.026191E-002 -4.117754E-003 -8.080477E-002
      tot  3.101338E-003 -1.118922E-002 -5.420093E-003 -1.528207E-002 -9.839916E-004 -4.872428E-002
    6  int  3.587108E-003  6.701073E-003  2.634129E-003 -7.427069E-004  6.512652E-004  5.156497E-005
      ind -6.687247E-003 -2.710175E-002 -1.080609E-002  5.215682E-003  5.274891E-003 -1.419352E-002
      tot -3.100139E-003 -2.040067E-002 -8.171964E-003  4.472975E-003  5.926156E-003 -1.414196E-002

 Surf                B7   (int / ind / tot)
    1 -1.427141E-005  0.000000E+000 -1.427141E-005
    2 -2.005394E-006 -4.437606E-005 -4.638145E-005
    3  8.043467E-005  2.010518E-004  2.814864E-004
    4  4.118292E-005  3.838727E-004  4.250556E-004
    5 -9.001096E-008 -1.298666E-004 -1.299567E-004
    6 -2.324267E-004  5.278437E-005 -1.796424E-004

System totals, transverse measure (what FIFTHORD prints)
   B   -3.480019E-002   F    6.174430E-003   C    4.453368E-002
   Pi  -1.285785E-001   E    9.292942E-003
   B5   7.691912E-003   F1   9.196030E-003   F2   6.270814E-003
   M1   2.732059E-002   M2   2.163254E-002   M3   3.182199E-002
   N1  -1.457431E-002   N2  -3.253849E-002   N3  -1.521610E-002
   C5  -1.303741E-002   Pi5  5.733012E-002   E5   1.484277E-003
   B7   1.681450E-003
```

### Stage B validated against FIFTHORD

Run on `CookeTriplet.zmx` at wavelength 2, all eighteen system totals match FIFTHORD to
every digit FIFTHORD prints, and every per-surface INTRINSIC row matches its per-surface
rows exactly - six surfaces, three groups, plus B7.

That comparison also settles what FIFTHORD's per-surface table is. It is the INTRINSIC
part alone. Its rows do not sum to its own totals, and cannot, because the induced part
is not in them:

    fifth-order spherical, B5, transverse measure
      sum of FIFTHORD per-surface rows   -6.908569E-03
      FIFTHORD reported system total      7.691900E-03
      sum of BUCH7 per-surface totals     7.691915E-03

The induced share is 1.460048E-02 - nearly twice the total, and opposite in sign to the
sum of the intrinsic parts. A designer reading the per-surface table alone would conclude
this triplet has negative fifth-order spherical aberration distributed over surfaces 1, 2
and 6, when the system has positive B5 and most of it is generated by surfaces acting on
aberration handed to them. That is not a criticism of FIFTHORD, which claims nothing
else; it is the gap this macro exists to close, now demonstrated with numbers rather than
asserted.

BUCH7's per-surface totals do sum to the system totals, as the same table shows.

### Stage C, and why it has a reference file

Stage C is not a continuation of stage B but a second arrangement. The fifth order is
built on the marginal and chief rays as traced; the seventh is built on Table I, which
uses its own basis - two rays p and q in units of the focal length, the p ray entering at
unit height parallel to the axis and the q ray at height p0 with unit angle. So the
paraxial basis is constructed again, and the entry numbers in the macro are Table I own.

It is also about a hundred and fifty entries, against the twenty or so of stage B. A
transcription that long, written where it cannot be executed, will contain mistakes; the
question is whether they can be found. So `reference/CookeTriplet_TableI.txt` carries
every non-zero entry, per surface, from the C# in this repository, and the macro will
print the same on request - set `dbg = 1` at the head of stage C. A wrong entry then
shows up as one wrong line rather than as a wrong answer at the bottom.

Built and CHECKED so far: t1 to t33 - the paraxial basis, the intrinsic primary, the
running sums and the dagger family. Every entry agrees with the reference on all six
surfaces of the Cooke triplet to 8.33E-17, machine precision, which is what a
transcription of the same arithmetic should give. The Table I basis is therefore proven,
and what follows is transcription against a known target rather than construction in the
dark.

Built and CHECKED: t1 to t98 - the paraxial basis, the primary, the secondary with its
barred and mid forms, the accumulated secondaries and the q-side. Every entry agrees with
the reference on all six surfaces of the Cooke triplet: t1 to t33 to 8.33E-17, t34 to t98
to 1.39E-17. That is machine precision, which is what a faithful transcription of the
same arithmetic gives.

Still to come: the tertiary chain t99 to t155, and the per-surface intrinsic and induced
split.

One limit taken deliberately. The barred secondary needs the lift q s, formed here as a
product. Buchdahl Sec. 84(e) carries the powers of q on the incidences instead, which
stays finite where q does not - a flat surface facing collimated space. That
regularisation is not transcribed, so such a surface is refused at the top rather than
got wrong quietly.

### Three ZPL traps this macro has already hit

Recorded because each produced something that looked like working code.

1. **THEN belongs only to the single-statement form.** `IF (cond) THEN statement` is
   one line; a block is `IF (cond)` ... `ENDIF` with no THEN. A trailing bare THEN is
   a syntax error. Not one line in the macros shipped with OpticStudio ends in one.

2. **Variable names are case-insensitive.** `sB` and `Sb` are the same variable. This
   one did not error - it produced a correct-looking table with one wrong total, which
   is far worse. There is an audit for it in the repository history: extract every
   assigned name, lowercase them, look for duplicates.

3. **FOR does not skip an empty range.** `FOR m = 1, 0, 1` is reported as an infinite
   loop rather than executing zero times. Any loop whose bound can fall below its start
   - a sum over preceding surfaces at the first surface, say - needs guarding.

### ZPL variable names are case-insensitive

Worth stating on its own, because it cost a wrong answer that looked right. The running
total `sB` and the chief-ray surface factor `Sb` are the SAME variable in ZPL, so the
accumulator was overwritten on every pass and the printed total for B came out as the
last surface's `Sb + Ba` - confirmed exactly, 1.425923E+00 against the C#. Every
per-surface number in the column above it was correct, which is precisely what made it
easy to miss: the table looked right and only the total was wrong.

Stages B and C carry many more quantities. Keep every name distinct without relying on
case, and check each printed total against the column above it.

### Provenance

Written from the implementation in `src/AberrationCalculator.Core`, which is this
project's own transcription of Buchdahl's scheme. `FIFTHORD.ZPL` was read as a reference
for the ZPL interface only - the keyword names and the paraxial conventions - and no code
from it is reproduced here. Every ZPL keyword used has been checked against the macros
shipped with OpticStudio rather than written from memory.

### Expected output, CookeTriplet

Run against `CookeTriplet.zmx` at wavelength 2 (0.55 um), these are the numbers the C#
in this repository gives for the same lens. They are what stage A must reproduce.

    EFL                49.999982
    Buchdahl p          0.230243     (= entrance pupil position / EFL)
    stop surface        4

     Surf              B              F              C             Pi              E
        1  -1.385463E-02  -1.059106E-02  -8.096248E-03  -5.772738E-02  -5.031834E-02
        2  -1.125614E-02   3.501083E-02  -1.088968E-01  -2.916251E-03   3.477808E-01
        3   5.211528E-02  -8.138899E-02   1.271060E-01   5.726770E-02  -2.879384E-01
        4   2.413498E-02   4.387382E-02   7.975611E-02   6.269015E-02   2.589462E-01
        5  -4.079989E-03  -1.613194E-02  -6.378433E-02  -1.594791E-02  -3.152546E-01
        6  -5.401955E-02   3.046222E-02  -1.717798E-02  -6.908203E-02   4.864296E-02
      SUM  -6.960041E-03   1.234886E-03   8.906738E-03  -2.571572E-02   1.858589E-03

The first draft of stage A reproduced only `B`, and only up to a constant factor. That
was diagnostic rather than mysterious: `B` carries no chief-ray incidence, `F` carries
one and `C` two, so a wrong normalisation of the chief ray shows up as a different
factor on each. The macro had been built on Table I's normalised basis, where the q ray
is scaled to unit angle at the first surface, while the coefficients are defined on the
physically scaled chief ray. Stage A now works from the marginal and chief rays as
traced, with the surface factors written as the C# writes them.

---

## STRESS.ZPL

Sasian's lens stress and relaxation parameters - the power distribution `W`, the symmetry
about the stop `S`, and the real-ray metric `R` - as system totals and, which he does not
tabulate, per surface.

J. Sasian, *Introduction to Lens Design*, Cambridge University Press, section 12.2. The
quantities are his (12.5) to (12.8); the reference values are his Table 12.1 and the
n sin(I) plot is his Figure 12.3. He says in that section that the W and S values and
those plots were produced by writing a macro inside a lens design program. This is one.

### Why

A lens acquires its power either gradually, spread over several weak surfaces, or
violently, by pitting strong positive against strong negative. The second buys degrees of
freedom - a flat field, mainly - and pays for them in higher-order aberration and in
tolerances. Nothing in a spot diagram distinguishes the two, and nothing in a Seidel table
does either: both report the aberration that survived, not the effort spent producing it.

`W` measures how hard the surfaces are working, `S` how far the lens departs from symmetry
about the stop, and `R` does what `W` does but on real rays, so it knows about the aperture
and field the lens is actually used over. Smaller is more relaxed in all three.

The normalisations are the interesting part and they are Sasian's. `W` carries the factors
`n_k' u_k'` and `(1 - m)` that make it independent of scale and of the conjugate. `S`
carries the stop factors `Abar_stop` and `y_stop` as well, which additionally make it
independent of field and of F/#. So these are numbers you can compare between two lenses
that share nothing.

### What this adds to the parameters as published

`W` and `S` are each one number for a whole lens. That tells you a lens is stressed
without telling you where. Both are RMS over surfaces, so the macro prints the per-surface
term going into each sum; the surface with the largest term is the one carrying the
stress. On the Cooke sample below, surface 4 carries the largest weighted power and
surface 3 the largest asymmetry, and they are not the same surface.

### Status

| what | checked against | result |
|---|---|---|
| W and S, and both per-surface tables | an independent implementation outside OpticStudio, from the same prescription | agrees; W = 1.055890, S = 0.917432 |
| the conventions behind them | Sasian's Table 12.1 | 1.06 and 0.92 here against his 1.12 and 0.89 - a different triplet |
| n sin(I), all four rays | an independent real ray trace, itself checked against OpticStudio's own ray intercepts to 2.3E-14 lens units | agrees to every digit printed |
| every ZPL function and keyword used | the ZPL reference in the OpticStudio help | 26 functions and 13 keywords, all present |

### What is actually proven, and what is not

**The arithmetic is checked; the absolute values are not, and cannot be from published
numbers.** Sasian's Table 12.1 gives W = 1.12 and S = 0.89 for *his* Cooke triplet. The
OpticStudio sample used here is a different lens, so the 1.06 and 0.92 obtained cannot be
compared with his entry by entry. What that near-agreement does settle is the
**conventions** - the sign of the chief-ray refraction invariant, whether the stop factors
sit in the numerator or the denominator, what `k` counts - because getting any of those
wrong moves the answer by a factor, not by six per cent. Two different triplets landing
within six per cent of each other is what a correct arrangement looks like; it is not what
a wrong one looks like.

The arithmetic itself is checked the only way it can be, against a second implementation
of the same equations written outside OpticStudio and driven from the same prescription.

One thing that check needed first: the independent computation used catalogue glasses, and
a glass name alone does not say whose glass it is. The indices OpticStudio actually used
were recovered from its own optical path lengths - optical path over geometric path,
segment by segment - and match the catalogue values to ten decimal places for both SK16
and F2. Without that, an agreement to four figures would have proved nothing about the
fourth figure.

### Two checks the macro makes on itself, every run

Both are printed, and both are the sort that can fail.

`w = phi*y` must equal `n'u' - nu`, which is Sasian's (12.5) and an identity of the
paraxial refraction equation. It holding says the index handling, the mirror sign flips
and the ray basis are mutually consistent. OpticStudio's sign convention makes it
`n'u' - nu = -phi*y`; Sasian writes them equal, which is the same statement in his.

`n sin(I)` from the `RAID` operand must equal `n sin(I)` worked out from the ray direction
and the surface normal, which come from a different part of the program. These part company
on a system with coordinate breaks or tilts, which is one of the things that makes such a
system not the sort this macro is for - so the check earns its place rather than merely
passing.

### The one arbitrary choice, made visible rather than buried

Sasian writes "a system of k surfaces" and does not say what to do about surfaces that are
not optical surfaces at all - a dummy plane, a stop floating in air. They contribute
nothing to either sum, since `phi` is zero and so is `delta(u/n)`, but counting them still
shrinks W and S by `sqrt(kept/counted)`.

This macro counts a surface when the index changes across it, so refraction and reflection
count and dummies do not, which makes W and S properties of the lens rather than of how the
file was typed. **The value on the other convention is printed underneath**, because the
choice is arbitrary enough to be worth seeing both ways. On a lens with no dummy surfaces
the two lines are equal; where they differ the reader decides. The Cooke sample has no
dummy surfaces, so it does not discriminate - a Petzval objective with a separate stop
surface would move by about five per cent.

### Limits

Rotationally symmetric and sequential; a non-axial system is refused. Not afocal, since
both W and S divide by `n_k' u_k'`. Unit magnification makes Sasian's `(1 - m)` factor
vanish and W and S with it - a property of the definition, not of the lens - so the macro
says so and prints the unnormalised RMS rather than dividing by zero.

**Aspheres are fine here**, which is worth stating because `BUCH7.ZPL` in this folder
refuses them. W and S are built on the paraxial trace and a conic or an even-asphere term
does not touch it: such a surface carries no vertex power, which is the same reason Sasian
gives for aspheres being absent from the Petzval sum. R is a real-ray quantity and
OpticStudio's own angle of incidence carries the figuring correctly.

Vignetting factors move the real rays, so they move R and the n sin(I) table but not W and
S. If factors are set, the `Py = +/-1` rays sit at the vignetted edge of the pupil rather
than the full one - usually what you want, never what you expect. The macro reports it.

### A fourth ZPL trap, which this macro found

The three in the BUCH7 section above all still apply. This one is new:

4. **`OPEV` traces rays of its own.** It evaluates an optimization operand, and the ray
   operands trace to do it, so `RAYL`, `RAYM`, `RANX` and the rest - which report the ray
   traced *last* - come back describing the operand's ray rather than yours. Reading them
   after an `OPEV` call returns plausible numbers for the wrong ray. Everything wanted from
   a `RAYTRACE` is therefore read out before the first `OPEV` of that pass, not interleaved
   with it.

The case-insensitivity trap deserves its restatement. Writing this macro it bit three times
in one sitting, twice in the PowerShell used to check the macro rather than in the macro
itself: `$I` silently clobbered a loop counter `$i` and produced an infinite loop, and `$t`
silently clobbered a thickness array `$T` and produced a ray trace that agreed with
OpticStudio on surface 1 and disagreed on every surface after it. Both looked like physics
bugs. Neither was.

### Expected output, Cooke 40 degree field

Run against `C:\ProgramData\Zemax\Samples\Sequential\Objectives\Cooke 40 degree field.zmx`
at the primary wavelength, 0.55 um. Stop at surface 4, object at infinity, so `m = 0` and
Sasian's `(1 - m)` factor is 1.

    Lagrange invariant         1.819851
    n_k' u_k'                 -0.100000
    Stop surface                      4
    Abar at the stop           0.478801
    Marginal y at the stop     3.800852

     Surf            phi              y        w = phi y         W term
        1  2.828288E-002  5.000000E+000  1.414144E-001 -1.414144E+000
        2  1.428784E-003  4.715974E+000  6.738109E-003 -6.738109E-002
        3 -2.807578E-002  3.825940E+000 -1.074163E-001  1.074163E+000
        4 -3.073415E-002  3.800852E+000 -1.168160E-001  1.168160E+000
        5  7.813498E-003  4.162261E+000  3.252182E-002 -3.252182E-001
        6  3.384596E-002  4.241508E+000  1.435579E-001 -1.435579E+000

     Surf           Abar     delta(u/n)              y         S term
        1  1.736295E-001 -5.371140E-002  5.000000E+000  2.562266E-001
        2  4.944718E-001 -9.444108E-002  4.715974E+000  1.210146E+000
        3  5.003551E-001  1.327002E-001  3.825940E+000 -1.395893E+000
        4  4.788009E-001  9.153203E-002  3.800852E+000 -9.153203E-001
        5  5.073451E-001 -5.953575E-002  4.162261E+000  6.908357E-001
        6  1.864152E-001 -1.165440E-001  4.241508E+000  5.063556E-001

    W  (index-changing surfaces)    1.055890
    S  (index-changing surfaces)    0.917432
    W  (all surfaces counted)       1.055890
    S  (all surfaces counted)       0.917432

     Surf   marginal(0,1)     chief(1,0)         (1,+1)         (1,-1)
        1   2.271324E-001  1.631584E-001  3.765930E-001  5.027624E-002
        2   1.613820E-001  4.690591E-001  3.125282E-001  6.119860E-001
        3   3.216885E-001  4.652921E-001  1.678046E-001  7.226106E-001
        4   2.717694E-001  4.778641E-001  6.982383E-001  2.172980E-001
        5   1.367177E-001  4.984117E-001  6.219992E-001  3.529278E-001
        6   3.306640E-001  1.395184E-001  1.614162E-001  4.432840E-001
        R   2.526545E-001  3.998682E-001  4.412349E-001  4.596190E-001

The two W and S pairs are equal because this file carries no dummy surfaces; on a file that
does, they will differ.

Sasian remarks of his own triplet that surfaces 3 and 4 carry the largest n sin(I). They do
here too - 0.72 on surface 3 for the `(1,-1)` ray and 0.70 on surface 4 for `(1,+1)` - on a
different triplet, which is the sort of agreement a structural claim about a lens form
should produce.

### Provenance

Written from Sasian's published equations. `FIFTHORD.ZPL` and the `Wavefront Aberrations
from Sasian.zpl` macro shipped with OpticStudio were read as references for the ZPL
interface only - keyword names, the paraxial conventions, the handling of mirrors through
`ISMS` - and no code from either is reproduced here. Every ZPL function and keyword used
was checked against the ZPL reference in the OpticStudio help rather than written from
memory.
