# Coefficient reference fixtures

Seven small aspheric designs and the third-, fifth- and seventh-order coefficients they
should produce: per surface, the isolated aspheric part of each surface, and the system
totals.

The designs are purpose-built for testing and are this repository's own. Each is a single
wavelength (0.5875618 um), an infinite object and a non-zero field - the chief ray is traced
at full field, so a zero field would collapse the Lagrange invariant and there would be
nothing to check.

| fixture | what it exercises |
|---|---|
| F1_conic_singlet | one conic surface |
| F2_a4_equivalent | an r^4 term chosen to match that conic |
| F3_conic_a4_a6_a8 | a conic and three polynomial terms together |
| F4_parabolic_mirror | a mirror, where k = -1 and every (1-k) factor becomes 2 |
| F5_doublet_rear_asphere | figuring behind a powered surface |
| F6_triplet_two_aspheres | two figured surfaces, so one induces on the other |
| F7_conic_as_polynomial | the same surface written the other way round |

## Where the numbers came from

The reference values were produced by running the FIFTHORD macro (Rimmer 1962, via
M. MacFarlane 1998, with the mirror index-sign correction of T. A. Mitchell 2003 and the
Lagrange-invariant correction of J. Sasian 2019) on these seven designs, and recording what
it reported.

**Neither that macro nor anything derived from it is in this repository, and neither will
be.** It ships with a commercial program and is not ours to redistribute. What is here is the
numerical output for designs that are ours - the same status as any measurement.

`PARM 1`, the r^2 deformation term, is zero in every fixture on purpose: the macro ignores
that term while this program's sag includes it, so a non-zero value would put the two on
different surfaces and the comparison would mean nothing.

## What this reference is, and is not

It is a cross-check. Two implementations of the same published method agreeing across 586
values is worth having, and a reader with that program can reproduce it without trusting
anything here.

It is **not** the authority for these coefficients, and the test that reads it is not named
as though it were. What establishes them is Buchdahl's own printed table, closed-form conic
surfaces that need no other program at all, this repository's inverse ray tracing, an
independent implementation written from the book, and Forbes' series trace. See
`docs/references.md`.
