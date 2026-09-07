! FORBES - aberration coefficients by G. W. Forbes' Lagrangian series trace.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! G. W. Forbes, "Order doubling in the computation of aberration coefficients,"
! J. Opt. Soc. Am. 73(6), 782 (1983), Sec. 3(a).
!
! STATUS: STAGES 1 AND 2. The truncated series ring, and the trace built on it. It
! prints the four series S, T, V, W that carry the whole aberration content of a
! system - but NOT the twenty named coefficients, which need the inversion of stage
! 4 and are not a printing step. See "Staging" below for what that means.
!
! The ring checks ITSELF on algebraic identities every run, before the trace, and
! declines to trace if any of them fails.
!
! WHY THIS METHOD, GIVEN BUCH7 ALREADY EXISTS
!
!   BUCH7 refuses a conic or an aspheric term, and the refusal is honest: Buchdahl
!   gives the aspheric scheme but never published an ARRANGED TABLE for it the way
!   Table I arranges the spherical case, so there is no printed answer to check a
!   transcription against.
!
!   Forbes removes the difficulty rather than solving it. He writes the ith surface as
!
!       x = f_i(y . y)
!
!   with f_i a power series. A sphere, a conic and an even asphere differ ONLY in the
!   coefficients of f_i and are traced by identical code. There is no D and L split,
!   no hat and check pass, no carrying ratio - the entire class of fault the aspheric
!   Buchdahl arrangement is prone to cannot be expressed in this formulation.
!
!   So this is not a faster BUCH7. It is the route that reaches the systems BUCH7
!   declines, and on the systems both reach they must agree, which is the check.
!
! THE ONE IDEA THE WHOLE THING RESTS ON
!
!   For a rotationally symmetric system every quantity carried through a ray trace is
!   either a scalar function of the three rotational invariants
!
!       p = y0 . y0 ,    k = y0 . b0 ,    u = b0 . b0
!
!   or a vector S y0 + T b0 with S and T such scalars, where y0 is the ray's height at
!   the input plane and b0 its direction. So the entire trace reduces to ARITHMETIC ON
!   ONE DATA TYPE: a power series in p, k and u, truncated at some total degree.
!
!   Each invariant is quadratic in the ray coordinates, so a term of degree m here
!   multiplies y0 or b0 to give order 2m + 1. Degree 1 is the primary aberrations,
!   degree 2 the secondary, degree 3 the tertiary - the seventh order. Twenty
!   monomials at degree three, which is why a series is twenty numbers.
!
!   Forbes' headline result, order doubling, is not used. It gets the characteristic
!   function to order 2M from a trace carried to order M, which is an economy for
!   orders far beyond the seventh; here the concatenation machinery it needs would be
!   the larger part of the work and the larger part of the risk.
!
! HOW A SERIES IS HELD
!
!   Twenty consecutive slots of VEC1. A "series" in what follows is the INDEX OF ITS
!   FIRST SLOT, and the subroutines take those indices in the globals sa, sb, sc.
!   The monomial order is fixed and is the one the C# in this repository uses, so the
!   two can be compared coefficient by coefficient:
!
!       m :  0  1  2  3   4  5  6  7  8  9  10 11 12 13 14 15 16 17 18 19
!       p :  0  1  0  0   2  1  1  0  0  0   3  2  2  1  1  1  0  0  0  0
!       k :  0  0  1  0   0  1  0  2  1  0   0  1  0  2  1  0  3  2  1  0
!       u :  0  0  0  1   0  0  1  0  1  2   0  0  1  0  1  2  0  1  2  3
!
!   Built once into VEC4 by the subroutine mktab, with an index table for the reverse
!   lookup (a, b, c) -> m that the multiply needs.
!
! SUBROUTINES AND THE ONE TRAP IN THEM
!
!   ZPL has GOSUB, SUB and RETURN, and up to a hundred subroutines - which is what
!   makes this feasible at all, since a series multiply is called dozens of times per
!   surface and inlining it would be unreadable.
!
!   BUT ALL ZPL VARIABLES ARE GLOBAL. A subroutine has no locals, so its working
!   variables are visible to, and clobberable by, everything else. Two rules keep
!   that safe here and both are checked in the audit that accompanies this file:
!
!     1. Every subroutine's private variables carry a prefix of its own - q for smul,
!        n for sinv, r for ssqrt, and so on - and no other subroutine uses them.
!
!     2. A SUBROUTINE THAT CALLS ANOTHER SUBROUTINE COPIES ITS OWN PARAMETERS FIRST.
!        sa, sb and sc are the argument registers; a nested call overwrites them, so
!        sinv begins by taking nsrc = sa and ndst = sb and never reads sa again.
!
!   The second rule is the one that would produce a plausible wrong answer rather
!   than an error, which is why it is stated before the code rather than after it.
!
! STAGING - this is stage 1 of four, and the stages are from docs/forbes.md
!
!   1. THE RING. Multiply, inverse, square root, and the monomial tables. Checked on
!      ALGEBRAIC identities that need no optics and no reference implementation:
!      difference of squares, s * (1/s) = 1, sqrt(s)^2 = s, the binomial coefficients
!      of (1 - u)^(1/2) term by term, and the truncation actually truncating.
!      *** DONE, and re-checked on every run before the trace starts. ***
!
!   2. THE TRACE, spherical surfaces only. Transfer and refraction, carrying S, T, V
!      and W from the input base plane to the paraxial focus.
!      *** DONE. Checked against ForbesTrace in src/AberrationCalculator.Core, all
!      four series and all twenty coefficients of each, to 7E-15 relative on the
!      Cooke triplet. Two of those numbers can also be checked without leaving the
!      printout: T(0,0,0) is the focal length and -1/V(0,0,0) is the same thing by a
!      different route. ***
!
!   3. Aspheres. Extra coefficients in f_i and nothing else changes - mkfig already
!      has the shape for it, and the refusal at the top is the only thing in the way.
!
!   4. THE TWENTY NAMED COEFFICIENTS. These are NOT simply read off S, T, V and W.
!      The transverse aberration is built as a series in the ray SCALE, its
!      degree-seven part taken exactly, and the twenty coefficients recovered from a
!      set of ray shapes by a linear inversion. That needs a second series type - a
!      univariate one in the scale, to order seven - and a solve in twenty unknowns.
!      It is a piece of work in its own right and it is not written here.
!
!   So this file does not yet replace BUCH7 for anything, and the comparison that
!   would prove it does - Forbes' twenty tau against Buchdahl's - waits on stage 4.
!
! ---------------------------------------------------------------------------------

eps = 1E-10

! Read before anything changes it, and restored at label 900 - including on the
! paths that leave early, which is why it is set here and not beside the trace.
paraxmode = PMOD()

! Degree three is the tertiary. Twenty monomials: (d+1)(d+2)(d+3)/6.
ndeg = 3
nterm = 20

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! The heap. Every base below is the FIRST slot of a twenty-slot block.
!
! Blocks 1 to 6 are the caller's to use. The blocks from 201 on are private to the
! subroutine named, and nothing outside it may touch them - that is what allows sinv
! and ssqrt to call smul without either losing its own working values.
! ---------------------------------------------------------------------------------

! --- the trace's own state and working blocks, VEC1 ---
bser = 1
bter = 21
bver = 41
bwer = 61

bp0 = 81
bk0 = 101
bu0 = 121

bph = 141
buh = 161
bal = 181
bia = 201
bfh = 221

bsn = 241
btn = 261
bpt = 281
bst = 301

bpa = 321
bfp = 341
bsl = 361
bgg = 381
bhh = 401
bih = 421
bci = 441
bla = 461
bfc = 481

bx1 = 501
bx2 = 521
bx3 = 541
bx4 = 561

! --- private to sdot ---
bd1 = 581
bd2 = 601
bd3 = 621
bd4 = 641

! --- private to scomp ---
bc1 = 661
bc2 = 681
bc3 = 701

! --- private to sinv and ssqrt; nothing else may touch these ---
bn1 = 1001
bn2 = 1021
bn3 = 1041
bn4 = 1061

br1 = 1101
br2 = 1121
br3 = 1141
br4 = 1161

! --- the stage 1 self-tests' operands, kept clear of everything above ---
ba = 1201
bb = 1221
bc = 1241
bd = 1261
be = 1281
bf = 1301

! --- univariate series in p alone, VEC3. Five coefficients each, degrees 0 to 4.
! The surface figure needs one degree beyond the trace, and it involves only p, so a
! full twenty-term invariant series would be the wrong shape as well as wasteful. ---
nfig = 5
fu1 = 1
fu2 = 11
fu3 = 21
fu4 = 31
fu5 = 41
fu6 = 51
fud = 61

ui1 = 101
ui2 = 111
ui3 = 121
ui4 = 131

uq1 = 201
uq2 = 211
uq3 = 221
uq4 = 231

GOSUB mktab

! ---------------------------------------------------------------------------------
! The self-tests. These need no optics and no reference numbers: they are identities
! that any correct implementation of a commutative ring with square roots satisfies,
! and they fail loudly if the monomial tables, the truncation or the recursions are
! wrong. Each prints the worst coefficient discrepancy it found.
! ---------------------------------------------------------------------------------

tol = 1E-12
nfail = 0

PRINT "==================================================================="
PRINT "FORBES - stage 1, the truncated series ring in p, k and u"
PRINT "Forbes, J. Opt. Soc. Am. 73, 782 (1983), Sec. 3(a)"
PRINT "==================================================================="
PRINT " "
FORMAT 5.0 INT
PRINT "Degree kept        ", ndeg
PRINT "Monomials          ", nterm
PRINT " "
PRINT "These are algebraic identities, not comparisons with stored numbers."
PRINT " "

! ---- table round trip -------------------------------------------------------------
! Every monomial must map to an index and back to itself. A wrong index table would
! otherwise show up only as a wrong answer much later.

worst = 0
FOR xm = 0, nterm - 1, 1
   xa = VEC4(101 + xm)
   xb = VEC4(121 + xm)
   xc = VEC4(141 + xm)
   xk = VEC4(1 + xa * 16 + xb * 4 + xc) - 1
   IF (xk != xm) THEN worst = 1
   IF (VEC4(161 + xm) != (xa + xb + xc)) THEN worst = 1
NEXT

! scopy is a ring primitive stage 2 leans on, so it is exercised here rather than
! left untested until something depends on it.
GOSUB mkopa
sa = ba
sb = bf
GOSUB scopy
sa = bf
sb = ba
GOSUB sdiff
IF (gmax > tol) THEN worst = 1

GOSUB report1

! ---- 1. difference of squares -----------------------------------------------------
! (A + B)(A - B) must equal A*A - B*B. This exercises the multiply and the truncation
! in a way that a single product cannot: the cross terms have to cancel exactly.

GOSUB mkopa
GOSUB mkopb

sa = ba
sb = bb
sc = bd
GOSUB sadd
sa = ba
sb = bb
sc = be
GOSUB ssub
sa = bd
sb = be
sc = bc
GOSUB smul

sa = ba
sb = ba
sc = bd
GOSUB smul
sa = bb
sb = bb
sc = be
GOSUB smul
sa = bd
sb = be
sc = bf
GOSUB ssub

sa = bc
sb = bf
GOSUB sdiff
worst = gmax
GOSUB report2

! ---- 2. the inverse ---------------------------------------------------------------
! A * (1/A) must be one. A's constant term is one, which is the only case the trace
! ever meets and the only case sinv accepts.

sa = ba
sb = bc
GOSUB sinv
sa = ba
sb = bc
sc = bd
GOSUB smul

sa = bf
GOSUB szero
VEC1(bf) = 1
sa = bd
sb = bf
GOSUB sdiff
worst = gmax
GOSUB report3

! ---- 3. the square root -----------------------------------------------------------
! sqrt(A) squared must be A again.

sa = ba
sb = bc
GOSUB ssqrt
sa = bc
sb = bc
sc = bd
GOSUB smul

sa = bd
sb = ba
GOSUB sdiff
worst = gmax
GOSUB report4

! ---- 4. the binomial series of (1 - u)^(1/2) --------------------------------------
! This is not an arbitrary series: it is the direction cosine a = (1 - u)^(1/2), which
! is exactly what the refraction takes the square root of. Its coefficients are known
! in closed form - 1, -1/2, -1/8, -1/16 - so this pins the recursion against
! arithmetic rather than against another implementation.

sa = bc
GOSUB szero
VEC1(bc) = 1
VEC1(bc + 3) = -1

sa = bc
sb = bd
GOSUB ssqrt

worst = ABSO(VEC1(bd) - 1)
xv = ABSO(VEC1(bd + 3) + 0.5)
IF (xv > worst) THEN worst = xv
xv = ABSO(VEC1(bd + 9) + 0.125)
IF (xv > worst) THEN worst = xv
xv = ABSO(VEC1(bd + 19) + 0.0625)
IF (xv > worst) THEN worst = xv
GOSUB report5

FORMAT 15.6 EXP
PRINT " "
PRINT "   (1-u)^(1/2) coefficients, which must be 1, -1/2, -1/8, -1/16:"
PRINT "   ", VEC1(bd), VEC1(bd + 3), VEC1(bd + 9), VEC1(bd + 19)
PRINT " "

! ---- 5. the truncation actually truncates -----------------------------------------
! p^2 times p^2 is p^4, which is past degree three and must vanish entirely. Without
! this a multiply that silently kept high terms would pass every identity above,
! because those identities hold at any truncation.

sa = bc
GOSUB szero
VEC1(bc + 4) = 1
sa = bc
sb = bc
sc = bd
GOSUB smul

worst = 0
FOR xm = 0, nterm - 1, 1
   xv = ABSO(VEC1(bd + xm))
   IF (xv > worst) THEN worst = xv
NEXT
GOSUB report6

! ---- verdict ----------------------------------------------------------------------

PRINT " "
FORMAT 5.0 INT
IF (nfail == 0)
   PRINT "All six checks pass. The ring is sound at degree ", ndeg
   PRINT " "
   PRINT "The ring is what everything below rests on, so it is re-checked on every run"
   PRINT "rather than taken on trust from the last one. The trace follows."
ELSE
   PRINT "FAILURES: ", nfail
   PRINT " "
   PRINT "The ring is wrong and nothing built on it would mean anything. The most"
   PRINT "likely cause is the monomial index table in mktab, which every other"
   PRINT "operation reads."
   PRINT " "
   PRINT "The trace below is not run, because it would only propagate the fault."
   GOTO 900
ENDIF

! =================================================================================
! STAGE 2 - THE TRACE.
!
! Forbes Sec. 3(a). A general ray leaves an input base plane at height y0 with
! direction b0, and at every surface the state is held as
!
!     y_i = S y0 + T b0 ,     b_i = V y0 + W b0                              (3.1)
!
! with S, T, V and W series in p, k and u. The whole trace is the four of them being
! carried forward, and nothing else is ever needed - which is the economy the
! invariants buy.
!
! The input base plane is the first surface's vertex and the output base plane is the
! paraxial focus, so that what comes out is directly comparable with the aberration
! coefficients computed elsewhere in this repository.
! =================================================================================

PARAXIAL ON
wl = PWAV()
ns = NSUR()
nsm = ns - 1

IF (nsm < 1)
   PRINT "FORBES needs at least one optical surface."
   GOTO 900
ENDIF
IF (nsm > 60)
   PRINT "FORBES is arranged for up to 60 surfaces and this system has more."
   GOTO 900
ENDIF

! Stage 2 is the spherical case. A conic or an aspheric term is carried by exactly
! the same code - that is Forbes' whole point and it is what stage 3 turns on - but
! it has not been checked yet, and an unchecked answer that looks plausible is worse
! than a refusal.
asph = 0
FOR gi = 1, nsm, 1
   IF (ABSO(CONI(gi)) > eps) THEN asph = 1
   FOR gj = 1, 8, 1
      IF (ABSO(PARM(gj, gi)) > eps) THEN asph = 1
   NEXT
NEXT
IF (asph == 1)
   PRINT " "
   PRINT "This system carries a conic or an aspheric term. Forbes' formulation handles"
   PRINT "those with the same code as a sphere - they are only more coefficients in the"
   PRINT "surface figure - but that path is stage 3 and has not been checked yet, so it"
   PRINT "is refused rather than guessed at."
   GOTO 900
ENDIF

! The paraxial focus, where the output base plane goes. Same construction as the one
! ROBB.ZPL uses: the marginal ray's -y/u at the last surface.
RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 900
ENDIF
gy = RAYY(nsm)
gu = RAYM(nsm) / RAYN(nsm)
IF (ABSO(gu) < eps)
   PRINT "The system is afocal, so there is no paraxial focus to catch the ray on."
   GOTO 900
ENDIF
gpfd = -gy / gu

! ---------------------------------------------------------------------------------
! The figures. Figure 0 is the input base plane, 1 to nsm the optical surfaces, and
! nsm+1 the output base plane. Each is five numbers: the vertex position on the axis
! and the coefficients of p, p^2, p^3, p^4 in the sag.
!
! The figure is carried ONE DEGREE BEYOND the trace on purpose. The refraction needs
! df/dp itself accurate to the trace's degree, and the p^3 term of df/dp comes from
! the p^4 term of f. Everywhere else df/dp is multiplied by something that vanishes
! with the ray, so the shortfall would stay hidden and cost exactly the top order.
! ---------------------------------------------------------------------------------

FOR gi = 0, nsm + 1, 1
   FOR gj = 0, nfig - 1, 1
      VEC2(1 + gi * 8 + gj) = 0
   NEXT
NEXT

gz = 0
FOR gi = 1, nsm, 1
   ccur = CURV(gi)
   ccon = CONI(gi)
   cbase = 1 + gi * 8
   cvz = gz
   GOSUB mkfig
   gzl = gz
   gz = gz + THIC(gi)
NEXT
VEC2(1 + (nsm + 1) * 8) = gzl + gpfd

! ---------------------------------------------------------------------------------
! The invariants themselves, and the initial state: y = y0, b = b0, so S = 1, T = 0,
! V = 0, W = 1.
! ---------------------------------------------------------------------------------

sa = bp0
GOSUB szero
VEC1(bp0 + 1) = 1
sa = bk0
GOSUB szero
VEC1(bk0 + 2) = 1
sa = bu0
GOSUB szero
VEC1(bu0 + 3) = 1

sa = bser
GOSUB szero
VEC1(bser) = 1
sa = bter
GOSUB szero
sa = bver
GOSUB szero
sa = bwer
GOSUB szero
VEC1(bwer) = 1

! ---------------------------------------------------------------------------------
! The loop over steps. Step gi is the move from figure gi into figure gi+1.
! ---------------------------------------------------------------------------------

FOR gi = 0, nsm, 1

   ! pHere = y.y and uHere = b.b at the figure we are leaving.
   da1 = bser
   db1 = bter
   da2 = bser
   db2 = bter
   dres = bph
   GOSUB sdot
   da1 = bver
   db1 = bwer
   da2 = bver
   db2 = bwer
   dres = buh
   GOSUB sdot

   ! alpha, the axial direction cosine, is sqrt(1 - u). This is the square root the
   ! ring's (1-u)^(1/2) self-test exists for.
   sa = bx1
   GOSUB szero
   VEC1(bx1) = 1
   sa = bx1
   sb = buh
   sc = bal
   GOSUB ssub
   sa = bal
   sb = bx1
   GOSUB ssqrt
   sa = bx1
   sb = bal
   sfac = 1
   GOSUB sscal
   sa = bal
   sb = bia
   GOSUB sinv

   ! f at the figure we are leaving, as a series.
   cser = bph
   cfig = 1 + gi * 8
   cres = bfh
   GOSUB scomp

   ! ---- transfer, Forbes (3.2)-(3.3) ------------------------------------------
   ! It is IMPLICIT: how far the ray travels along the axis depends on where it
   ! lands, which is what it is being solved for. Forbes' resolution is to solve it
   ! BY ORDER - with S and T known to degree m the argument of f is known to degree
   ! m+1, and (3.3) then gives S and T to degree m+1 - so iterating past the
   ! truncation reaches it exactly. One extra pass is run so that the last one is
   ! provably a fixed point rather than merely the one we stopped at.

   sa = bser
   sb = bsn
   GOSUB scopy
   sa = bter
   sb = btn
   GOSUB scopy

   FOR gpass = 0, ndeg + 1, 1
      da1 = bsn
      db1 = btn
      da2 = bsn
      db2 = btn
      dres = bpt
      GOSUB sdot

      cser = bpt
      cfig = 1 + (gi + 1) * 8
      cres = bx2
      GOSUB scomp

      sa = bx2
      sb = bfh
      sc = bx3
      GOSUB ssub
      sa = bx3
      sb = bia
      sc = bst
      GOSUB smul

      sa = bver
      sb = bst
      sc = bx3
      GOSUB smul
      sa = bser
      sb = bx3
      sc = bsn
      GOSUB sadd

      sa = bwer
      sb = bst
      sc = bx3
      GOSUB smul
      sa = bter
      sb = bx3
      sc = btn
      GOSUB sadd
   NEXT

   sa = bsn
   sb = bser
   GOSUB scopy
   sa = btn
   sb = bter
   GOSUB scopy

   ! The last step lands on the output base plane, which catches the ray rather
   ! than refracting it.
   IF (gi < nsm)
      GOSUB refract
   ENDIF
NEXT

GOSUB shows

LABEL 900
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF

END

! =================================================================================
! SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! mktab - the monomial tables, built once.
!
!   VEC4(1 + a*16 + b*4 + c)  the monomial number of p^a k^b u^c, plus one, or zero
!                             if that monomial is past the truncation
!   VEC4(101 + m)             a of monomial m
!   VEC4(121 + m)             b
!   VEC4(141 + m)             c
!   VEC4(161 + m)             total degree a + b + c
!
! The order is Forbes-neutral - any consistent order works - but it is deliberately
! the order the C# in src/AberrationCalculator.Core/Forbes uses, so that the two can
! be compared coefficient by coefficient rather than as sets.
! ---------------------------------------------------------------------------------
SUB mktab
   FOR tt = 1, 64, 1
      VEC4(tt) = 0
   NEXT
   tm = 0
   FOR tdeg = 0, ndeg, 1
      FOR ta = tdeg, 0, -1
         FOR tb = tdeg - ta, 0, -1
            tc = tdeg - ta - tb
            VEC4(1 + ta * 16 + tb * 4 + tc) = tm + 1
            VEC4(101 + tm) = ta
            VEC4(121 + tm) = tb
            VEC4(141 + tm) = tc
            VEC4(161 + tm) = tdeg
            tm = tm + 1
         NEXT
      NEXT
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! szero - the series at sa becomes zero.
! ---------------------------------------------------------------------------------
SUB szero
   FOR zi = 0, nterm - 1, 1
      VEC1(sa + zi) = 0
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! scopy - sb := sa.
! ---------------------------------------------------------------------------------
SUB scopy
   FOR ci = 0, nterm - 1, 1
      VEC1(sb + ci) = VEC1(sa + ci)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sadd - sc := sa + sb. Aliasing is harmless here.
! ---------------------------------------------------------------------------------
SUB sadd
   FOR di = 0, nterm - 1, 1
      VEC1(sc + di) = VEC1(sa + di) + VEC1(sb + di)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! ssub - sc := sa - sb. Aliasing is harmless here.
! ---------------------------------------------------------------------------------
SUB ssub
   FOR ei = 0, nterm - 1, 1
      VEC1(sc + ei) = VEC1(sa + ei) - VEC1(sb + ei)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sscal - sb := sa * sfac, a plain number times a series.
! ---------------------------------------------------------------------------------
SUB sscal
   FOR fi = 0, nterm - 1, 1
      VEC1(sb + fi) = VEC1(sa + fi) * sfac
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! smul - sc := sa * sb, truncated at degree ndeg.
!
! sc MUST NOT be sa or sb: the result is accumulated into sc as the pass proceeds, so
! aliasing would read values already overwritten. Callers use a scratch block.
!
! The truncation is what makes the whole scheme finite. A term of degree i is skipped
! against every term of degree greater than ndeg - i, which is the "room" below.
! ---------------------------------------------------------------------------------
SUB smul
   FOR qi = 0, nterm - 1, 1
      VEC1(sc + qi) = 0
   NEXT
   FOR qi = 0, nterm - 1, 1
      qx = VEC1(sa + qi)
      IF (qx != 0)
         qroom = ndeg - VEC4(161 + qi)
         FOR qj = 0, nterm - 1, 1
            qy = VEC1(sb + qj)
            IF (qy != 0)
               IF (VEC4(161 + qj) <= qroom)
                  qa = VEC4(101 + qi) + VEC4(101 + qj)
                  qb = VEC4(121 + qi) + VEC4(121 + qj)
                  qc = VEC4(141 + qi) + VEC4(141 + qj)
                  qk = VEC4(1 + qa * 16 + qb * 4 + qc) - 1
                  VEC1(sc + qk) = VEC1(sc + qk) + qx * qy
               ENDIF
            ENDIF
         NEXT
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sinv - sb := 1 / sa, for a series whose constant term is not zero.
!
! Writing sa = c0 (1 + v) with v of zero constant term, the geometric series in v
! TERMINATES at the truncation, so the result is exact for the degree kept rather
! than approximate. Every quantity the trace inverts has a constant term of one, so
! a zero constant term here means a quantity has been built wrongly and not that a
! singularity has been met - hence the refusal rather than a guard value.
! ---------------------------------------------------------------------------------
SUB sinv
   nsrc = sa
   ndst = sb
   nc0 = VEC1(nsrc)
   IF (ABSO(nc0) < eps)
      PRINT "FORBES: sinv on a series with no constant term. In this scheme that"
      PRINT "means a quantity has been built wrongly, not that a singularity was met."
      nfail = nfail + 1
   ELSE
      ! v = sa/c0 - 1, in bn1
      sa = nsrc
      sb = bn1
      sfac = 1 / nc0
      GOSUB sscal
      VEC1(bn1) = VEC1(bn1) - 1

      ! term = 1, sum = 1
      sa = bn2
      GOSUB szero
      VEC1(bn2) = 1
      sa = bn3
      GOSUB szero
      VEC1(bn3) = 1

      FOR ni = 1, ndeg, 1
         ! term := term * (-v)
         sa = bn2
         sb = bn1
         sc = bn4
         GOSUB smul
         sa = bn4
         sb = bn2
         sfac = -1
         GOSUB sscal
         ! sum := sum + term
         sa = bn3
         sb = bn2
         sc = bn3
         GOSUB sadd
      NEXT

      sa = bn3
      sb = ndst
      sfac = 1 / nc0
      GOSUB sscal
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! ssqrt - sb := sqrt(sa), for a series with a positive constant term.
!
! The same reduction as sinv with the binomial series for (1 + v)^(1/2). The only
! square root a symmetric trace takes is of a quantity that is one on the axis - the
! direction cosine (1 - u)^(1/2) - so a non-positive constant term is again a fault
! rather than a singularity.
! ---------------------------------------------------------------------------------
SUB ssqrt
   rsrc = sa
   rdst = sb
   rc0 = VEC1(rsrc)
   IF (rc0 <= eps)
      PRINT "FORBES: ssqrt on a series whose constant term is not positive. The only"
      PRINT "square roots a symmetric trace takes are of quantities that are one on axis."
      nfail = nfail + 1
   ELSE
      ! v = sa/c0 - 1, in br1
      sa = rsrc
      sb = br1
      sfac = 1 / rc0
      GOSUB sscal
      VEC1(br1) = VEC1(br1) - 1

      sa = br2
      GOSUB szero
      VEC1(br2) = 1
      sa = br3
      GOSUB szero
      VEC1(br3) = 1

      rbin = 1
      FOR ri = 1, ndeg, 1
         rbin = rbin * (0.5 - (ri - 1)) / ri
         ! term := term * v
         sa = br2
         sb = br1
         sc = br4
         GOSUB smul
         sa = br4
         sb = br2
         sfac = 1
         GOSUB sscal
         ! sum := sum + binom * term
         sa = br2
         sb = br4
         sfac = rbin
         GOSUB sscal
         sa = br3
         sb = br4
         sc = br3
         GOSUB sadd
      NEXT

      sa = br3
      sb = rdst
      sfac = SQRT(rc0)
      GOSUB sscal
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! sdiff - gmax := the largest absolute difference between the series at sa and sb.
! ---------------------------------------------------------------------------------
SUB sdiff
   gmax = 0
   FOR wi = 0, nterm - 1, 1
      wv = ABSO(VEC1(sa + wi) - VEC1(sb + wi))
      IF (wv > gmax) THEN gmax = wv
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! mkopa, mkopb - two arbitrary test operands. A has constant term one so that it can
! be inverted and square-rooted; B is unconstrained. Both carry terms at every degree
! so that no part of the truncation goes unexercised.
! ---------------------------------------------------------------------------------
SUB mkopa
   sa = ba
   GOSUB szero
   VEC1(ba) = 1
   VEC1(ba + 1) = 0.3
   VEC1(ba + 2) = -0.7
   VEC1(ba + 3) = 0.11
   VEC1(ba + 5) = 0.23
   VEC1(ba + 9) = -0.41
   VEC1(ba + 14) = 0.17
   VEC1(ba + 19) = -0.29
RETURN

SUB mkopb
   sa = bb
   GOSUB szero
   VEC1(bb) = -0.4
   VEC1(bb + 2) = 0.9
   VEC1(bb + 4) = -0.13
   VEC1(bb + 6) = 0.37
   VEC1(bb + 11) = 0.53
   VEC1(bb + 16) = -0.19
RETURN

! ---------------------------------------------------------------------------------
! The six verdicts. Each prints the worst discrepancy it found so that a pass is a
! number rather than a word.
! ---------------------------------------------------------------------------------
SUB report1
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  monomial table round trip           ", worst
   ELSE
      PRINT "   FAIL  monomial table round trip           ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report2
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  (A+B)(A-B) = A*A - B*B              ", worst
   ELSE
      PRINT "   FAIL  (A+B)(A-B) = A*A - B*B              ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report3
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  A * (1/A) = 1                       ", worst
   ELSE
      PRINT "   FAIL  A * (1/A) = 1                       ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report4
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  sqrt(A)^2 = A                       ", worst
   ELSE
      PRINT "   FAIL  sqrt(A)^2 = A                       ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report5
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  (1-u)^(1/2) binomial coefficients   ", worst
   ELSE
      PRINT "   FAIL  (1-u)^(1/2) binomial coefficients   ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report6
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  p^2 * p^2 truncates to nothing      ", worst
   ELSE
      PRINT "   FAIL  p^2 * p^2 truncates to nothing      ", worst
      nfail = nfail + 1
   ENDIF
RETURN

! =================================================================================
! STAGE 2 SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! The univariate series in p alone. Five coefficients, degrees 0 to 4. These exist
! only to build a surface figure: the sag is a function of p by itself, so carrying
! it in the full twenty-term invariant ring would be the wrong shape as well as four
! times the work - and the figure needs degree four where the ring keeps three.
! ---------------------------------------------------------------------------------

! uzero - the univariate series at ua becomes zero.
SUB uzero
   FOR ai = 0, nfig - 1, 1
      VEC3(ua + ai) = 0
   NEXT
RETURN

! umul - uc := ua * ub, truncated at degree nfig - 1. uc must not alias ua or ub.
SUB umul
   FOR bi = 0, nfig - 1, 1
      VEC3(uc + bi) = 0
   NEXT
   FOR bi = 0, nfig - 1, 1
      bx = VEC3(ua + bi)
      IF (bx != 0)
         FOR bj = 0, nfig - 1 - bi, 1
            by = VEC3(ub + bj)
            IF (by != 0) THEN VEC3(uc + bi + bj) = VEC3(uc + bi + bj) + bx * by
         NEXT
      ENDIF
   NEXT
RETURN

! uinv - ub := 1 / ua. Same terminating geometric series as sinv.
SUB uinv
   csrc = ua
   cdst = ub
   cc0 = VEC3(csrc)
   IF (ABSO(cc0) < eps)
      PRINT "FORBES: uinv on a figure series with no constant term."
      nfail = nfail + 1
   ELSE
      FOR ci2 = 0, nfig - 1, 1
         VEC3(ui1 + ci2) = VEC3(csrc + ci2) / cc0
      NEXT
      VEC3(ui1) = VEC3(ui1) - 1
      ua = ui2
      GOSUB uzero
      VEC3(ui2) = 1
      ua = ui3
      GOSUB uzero
      VEC3(ui3) = 1
      FOR ci2 = 1, nfig - 1, 1
         ua = ui2
         ub = ui1
         uc = ui4
         GOSUB umul
         FOR ci3 = 0, nfig - 1, 1
            VEC3(ui2 + ci3) = -VEC3(ui4 + ci3)
            VEC3(ui3 + ci3) = VEC3(ui3 + ci3) + VEC3(ui2 + ci3)
         NEXT
      NEXT
      FOR ci2 = 0, nfig - 1, 1
         VEC3(cdst + ci2) = VEC3(ui3 + ci2) / cc0
      NEXT
   ENDIF
RETURN

! usqrt - ub := sqrt(ua), constant term positive. Binomial series, as ssqrt.
SUB usqrt
   dsrc = ua
   ddst = ub
   dc0 = VEC3(dsrc)
   IF (dc0 <= eps)
      PRINT "FORBES: usqrt on a figure series whose constant term is not positive."
      nfail = nfail + 1
   ELSE
      FOR di2 = 0, nfig - 1, 1
         VEC3(uq1 + di2) = VEC3(dsrc + di2) / dc0
      NEXT
      VEC3(uq1) = VEC3(uq1) - 1
      ua = uq2
      GOSUB uzero
      VEC3(uq2) = 1
      ua = uq3
      GOSUB uzero
      VEC3(uq3) = 1
      dbin = 1
      FOR di2 = 1, nfig - 1, 1
         dbin = dbin * (0.5 - (di2 - 1)) / di2
         ua = uq2
         ub = uq1
         uc = uq4
         GOSUB umul
         FOR di3 = 0, nfig - 1, 1
            VEC3(uq2 + di3) = VEC3(uq4 + di3)
            VEC3(uq3 + di3) = VEC3(uq3 + di3) + dbin * VEC3(uq4 + di3)
         NEXT
      NEXT
      dr0 = SQRT(dc0)
      FOR di2 = 0, nfig - 1, 1
         VEC3(ddst + di2) = VEC3(uq3 + di2) * dr0
      NEXT
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! mkfig - the figure of one surface, from its vertex position cvz, its curvature
! ccur and its conic ccon, written into VEC2 at cbase.
!
! The sag of a conic is  c p / (1 + sqrt(1 - (1 + K) c^2 p)), built here through the
! univariate series rather than from hand-expanded coefficients, so that the
! expansion is the one the ring's own tests already check. A sphere is K = 0 and an
! even asphere would add A_j p^j - the same code, which is Forbes' whole point and
! what stage 3 turns on.
! ---------------------------------------------------------------------------------
SUB mkfig
   ua = fu6
   GOSUB uzero
   IF (ABSO(ccur) > eps)
      ua = fu1
      GOSUB uzero
      VEC3(fu1) = 1
      VEC3(fu1 + 1) = -(1 + ccon) * ccur * ccur
      ua = fu1
      ub = fu2
      GOSUB usqrt
      ua = fu3
      GOSUB uzero
      FOR ei2 = 0, nfig - 1, 1
         VEC3(fu3 + ei2) = VEC3(fu2 + ei2)
      NEXT
      VEC3(fu3) = VEC3(fu3) + 1
      ua = fu3
      ub = fu4
      GOSUB uinv
      ua = fu5
      GOSUB uzero
      VEC3(fu5 + 1) = ccur
      ua = fu5
      ub = fu4
      uc = fu6
      GOSUB umul
   ENDIF
   VEC2(cbase) = cvz
   FOR ei2 = 1, nfig - 1, 1
      VEC2(cbase + ei2) = VEC3(fu6 + ei2)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sdot - the inner product of two vectors held in the (y0, b0) basis, reduced to the
! invariants. With v1 = a1 y0 + b1 b0 and v2 = a2 y0 + b2 b0,
!
!     v1 . v2 = a1 a2 p + (a1 b2 + b1 a2) k + b1 b2 u
!
! because y0.y0 = p, y0.b0 = k and b0.b0 = u. This is the step that makes the whole
! trace scalar: no vector is ever carried componentwise.
!
! Parameters da1, db1, da2, db2 and dres. dres must not be bd1 to bd4.
! ---------------------------------------------------------------------------------
SUB sdot
   ea1 = da1
   eb1 = db1
   ea2 = da2
   eb2 = db2
   eres = dres

   sa = ea1
   sb = ea2
   sc = bd1
   GOSUB smul
   sa = bd1
   sb = bp0
   sc = bd2
   GOSUB smul

   sa = ea1
   sb = eb2
   sc = bd1
   GOSUB smul
   sa = eb1
   sb = ea2
   sc = bd3
   GOSUB smul
   sa = bd1
   sb = bd3
   sc = bd4
   GOSUB sadd
   sa = bd4
   sb = bk0
   sc = bd1
   GOSUB smul
   sa = bd2
   sb = bd1
   sc = bd2
   GOSUB sadd

   sa = eb1
   sb = eb2
   sc = bd1
   GOSUB smul
   sa = bd1
   sb = bu0
   sc = bd3
   GOSUB smul
   sa = bd2
   sb = bd3
   sc = eres
   GOSUB sadd
RETURN

! ---------------------------------------------------------------------------------
! scomp - substitute the invariant series at cser, which must have no constant term,
! into the univariate figure at cfig, leaving the result at cres.
!
! This is how a surface enters the trace: Forbes writes it as x = f(y . y), and y . y
! is itself a series in the invariants. Because cser vanishes on the axis its jth
! power has degree at least j, so the sum terminates at the truncation.
! ---------------------------------------------------------------------------------
SUB scomp
   hser = cser
   hfig = cfig
   hres = cres

   sa = bc1
   GOSUB szero
   VEC1(bc1) = VEC2(hfig)

   sa = bc2
   GOSUB szero
   VEC1(bc2) = 1

   FOR hj = 1, ndeg, 1
      sa = bc2
      sb = hser
      sc = bc3
      GOSUB smul
      sa = bc3
      sb = bc2
      sfac = 1
      GOSUB sscal
      hcf = VEC2(hfig + hj)
      IF (hcf != 0)
         sa = bc2
         sb = bc3
         sfac = hcf
         GOSUB sscal
         sa = bc1
         sb = bc3
         sc = bc1
         GOSUB sadd
      ENDIF
   NEXT

   sa = bc1
   sb = hres
   sfac = 1
   GOSUB sscal
RETURN

! ---------------------------------------------------------------------------------
! refract - Snell at figure gi+1, Forbes M (3.4).
!
! The surface normal is (1, -2 f' y) over its length, both components taken with the
! axial one positive, so with
!
!     G = alpha - 2 f' (b . y)    and    H = sqrt(1 + 4 p f'^2)
!
! the cosine of incidence is G / H. Preserving the tangential component gives
!
!     n' cos I' = sqrt(n'^2 - n^2 + n^2 cos^2 I)
!
! whose constant term is n' and so is safely rooted, and the refracted direction
! follows from n' b' = n b + (n' cos I' - n cos I) times the normal.
! ---------------------------------------------------------------------------------
SUB refract
   krn = INDX(gi)
   krnp = INDX(gi + 1)
   kfig = 1 + (gi + 1) * 8

   ! df/dp of that figure, one degree shorter than f itself.
   FOR ki = 0, nfig - 1, 1
      VEC2(fud + ki) = 0
   NEXT
   FOR ki = 0, nfig - 2, 1
      VEC2(fud + ki) = (ki + 1) * VEC2(kfig + ki + 1)
   NEXT

   da1 = bser
   db1 = bter
   da2 = bser
   db2 = bter
   dres = bpa
   GOSUB sdot

   cser = bpa
   cfig = fud
   cres = bfp
   GOSUB scomp

   da1 = bver
   db1 = bwer
   da2 = bser
   db2 = bter
   dres = bsl
   GOSUB sdot

   ! G = alpha - 2 f' (b . y)
   sa = bfp
   sb = bsl
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bx2
   sfac = 2
   GOSUB sscal
   sa = bal
   sb = bx2
   sc = bgg
   GOSUB ssub

   ! H = sqrt(1 + 4 p f'^2), and its inverse
   sa = bfp
   sb = bfp
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bpa
   sc = bx2
   GOSUB smul
   sa = bx2
   sb = bx1
   sfac = 4
   GOSUB sscal
   VEC1(bx1) = VEC1(bx1) + 1
   sa = bx1
   sb = bhh
   GOSUB ssqrt
   sa = bhh
   sb = bih
   GOSUB sinv

   sa = bgg
   sb = bih
   sc = bci
   GOSUB smul

   ! lambda = n' cos I' - n cos I
   sa = bci
   sb = bci
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bx2
   sfac = krn * krn
   GOSUB sscal
   VEC1(bx2) = VEC1(bx2) + krnp * krnp - krn * krn
   sa = bx2
   sb = bx3
   GOSUB ssqrt
   sa = bci
   sb = bx1
   sfac = krn
   GOSUB sscal
   sa = bx3
   sb = bx1
   sc = bla
   GOSUB ssub

   ! factor = (2/n') lambda f' / H
   sa = bla
   sb = bfp
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bih
   sc = bx2
   GOSUB smul
   sa = bx2
   sb = bfc
   sfac = 2 / krnp
   GOSUB sscal

   ! V := (n/n') V - factor S ,  W := (n/n') W - factor T
   sa = bver
   sb = bx1
   sfac = krn / krnp
   GOSUB sscal
   sa = bfc
   sb = bser
   sc = bx2
   GOSUB smul
   sa = bx1
   sb = bx2
   sc = bx3
   GOSUB ssub

   sa = bwer
   sb = bx1
   sfac = krn / krnp
   GOSUB sscal
   sa = bfc
   sb = bter
   sc = bx2
   GOSUB smul
   sa = bx1
   sb = bx2
   sc = bx4
   GOSUB ssub

   sa = bx3
   sb = bver
   GOSUB scopy
   sa = bx4
   sb = bwer
   GOSUB scopy
RETURN

! ---------------------------------------------------------------------------------
! shows - print the four series, with the constant terms called out because those
! three can be checked against first-order optics at a glance and need no reference.
! ---------------------------------------------------------------------------------
SUB shows
   PRINT " "
   PRINT "==================================================================="
   PRINT "STAGE 2 - the trace"
   PRINT "==================================================================="
   PRINT " "
   FORMAT 5.0 INT
   PRINT "Optical surfaces           ", nsm
   FORMAT 12.6
   PRINT "Paraxial focus distance    ", gpfd
   PRINT " "
   PRINT "First-order checks on the constant terms, which need no reference:"
   PRINT "   T(0,0,0) is the focal length      ", VEC1(bter)
   PRINT "   -1 / V(0,0,0) is the same thing   ", -1 / VEC1(bver)
   PRINT "   S(0,0,0) is zero at focus         ", VEC1(bser)
   PRINT " "
   PRINT " m   p  k  u              S              T              V              W"

   ! FORMAT n.0 INT PRINTS A ZERO AS BLANK. The first draft of this table used it and
   ! came out with the m = 0 row unlabelled and every zero power missing, so that
   ! p^0 k^0 u^3 read as though it were blank blank 3. Fixed format has no such
   ! trouble - the header above prints -0.000000 for a quantity of 1E-16 - so the
   ! integer columns are printed as fixed with no decimals instead.
   FOR mi = 0, nterm - 1, 1
      FORMAT 4.0
      PRINT mi, VEC4(101 + mi), VEC4(121 + mi), VEC4(141 + mi),
      FORMAT 15.6 EXP
      PRINT VEC1(bser + mi), VEC1(bter + mi), VEC1(bver + mi), VEC1(bwer + mi)
   NEXT
   PRINT " "
   PRINT "Stage 2 ends here. The twenty named coefficients are read off these four by"
   PRINT "a linear inversion over a set of ray shapes - that is stage 4, and it is a"
   PRINT "piece of work in its own right rather than a printing step."
RETURN
