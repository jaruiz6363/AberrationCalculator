! RAYINV - third-, fifth- and seventh-order aberration coefficients recovered from
! REAL TRACED RAYS, by inverting the transverse aberration rather than by any series.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! WHY THIS EXISTS, WHICH IS A NARROW AND SPECIFIC REASON
!
!   BUCH7 computes the coefficients from Buchdahl's arranged tables. FORBES computes
!   them from Forbes' series trace. On a system of spheres the two agree on all
!   thirty-seven to every printed digit, and FIFTHORD - which ships with OpticStudio
!   and needs nothing from this repository - agrees with both on eighteen of them.
!
!   On an ASPHERIC system that structure falls apart:
!
!     BUCH7 declines outright.
!     FIFTHORD reaches B7 and stops, so it covers the third order, the fifth, and
!       tau1 - eighteen quantities - and nothing above.
!     The Seidel analysis reaches the third order and stops.
!
!   So tau2 to tau20 on a figured lens - NINETEEN COEFFICIENTS, and precisely the
!   ones Buchdahl's aspheric arrangement gets wrong - have nothing to be checked
!   against except a second implementation of the same series method. That is not a
!   check; it is the same argument twice.
!
!   THIS MACRO IS THE THIRD OPINION. It uses no series at all. It traces real rays
!   through OpticStudio, measures where they land, and recovers the coefficients
!   from the measurements. Where it and FORBES agree, they agree by two routes with
!   nothing whatever in common except the definition of the coefficients.
!
! HOW IT WORKS
!
!   A traced ray gives no way to separate the seventh order from the ninth except by
!   watching how they shrink. So each ray shape is traced at a LADDER OF SCALES -
!   shrink the ray by s and the pupil goes to s rho, the field to s h - and an odd
!   polynomial in s is fitted through the landings. The coefficient of s^3, s^5 and
!   s^7 is then the third-, fifth- and seventh-order transverse aberration of that
!   shape. Seventy-five shapes give an overdetermined system for the coefficients.
!
!   THE LADDER IS THE WHOLE GAME AND IS NOT ARBITRARY. Measured against B7 on an
!   axial fan - where the degree-seven part is that coefficient alone and B7 is known
!   exactly - a ladder running out to s = 1 with powers to s^11 recovers it only to
!   about 1.5 per cent, because the ninth order and above are still large at full
!   aperture and the fit cannot separate them from the seventh. Twelve points out to
!   six tenths, with powers to s^15 to absorb the tail, recovers it to better than
!   one part in a million. Those two numbers are the reason for every constant below.
!
!   THE PARAXIAL IMAGE HEIGHT IS SUBTRACTED BEFORE FITTING. It is the whole of the
!   degree-one term, and leaving it in is fatal rather than untidy: at seven tenths
!   of the field it is some twelve millimetres sitting on a third-order term of two
!   hundredths, so the fit would have to cancel three orders of magnitude to reach
!   the aberration at all.
!
!   THE FIT IS DONE IN t = s / s_max, NOT IN s. The powers run to fifteen, so in s
!   the normal-equations matrix spans thirty orders of magnitude and its diagonal
!   ratio is 7E+06; rescaled it is 4. That turns a solve which needs a QR into one
!   an ordinary Gaussian elimination handles, which is what ZPL can carry. The
!   recovered coefficients are divided back by the corresponding power of s_max.
!
! THE MODEL IS SHARED WITH FORBES ON PURPOSE, AND THAT IS NOT A COMPROMISE
!
!   Both routes fit the same twenty coefficients through the same linear model -
!   Robb's Eq. (2) with one coefficient set to one at a time. The INDEPENDENCE lives
!   entirely in the right-hand side: series arithmetic there, OpticStudio's own ray
!   trace here. Were the two to use different bases they would not be computing the
!   same quantity and the comparison would mean nothing, so the shared model is what
!   makes the check a check.
!
! LIMITS
!
!   Infinite conjugate. The paraxial image height subtracted below is efl tan(field),
!   which is the infinite-conjugate form; FORBES carries both conjugates and this
!   does not.
!
!   Rotationally symmetric and sequential. It is SLOW: nine hundred real ray traces,
!   and each of the seventy-five shapes then takes two eight-by-eight solves.
!
!   It is less accurate than either series route and is meant to be. A traced ray
!   carries every order at once and the fit has to separate them, so the answer
!   arrives with a fitting residual where a series arrives exactly. Expect agreement
!   with FORBES to a few parts in ten thousand, not to machine precision - and read a
!   disagreement at that level as the fit, not as either route being wrong.
!
! THE ZPL TRAPS: see macros/README.md. All five apply. In particular every loop whose
! bound can fall below its start is guarded, because ZPL reports an empty range as an
! infinite loop rather than executing it zero times.
! ---------------------------------------------------------------------------------

eps = 1E-10
paraxmode = PMOD()

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! Storage.
!
!   VEC1  the fit: the scale ladder, the landings, the eight-by-eight normal matrix
!   VEC2  the shapes, the model rows, and the three coefficient systems
!   VEC4  the odd powers
! ---------------------------------------------------------------------------------

nsc = 12
npw = 8

! ZPL COMMENTS ARE WHOLE-LINE ONLY. There is no trailing comment: a ! after a
! statement is not the start of a comment, so the parser reads on and reports the
! next word as an unknown symbol. These eight lines carried their descriptions on
! the right and the macro would not run because of it.
!
!   vsc  the scale ladder, 12
!   vtt  the same rescaled to t = s / s_max, 12
!   vey  meridional landings, 12
!   vez  sagittal landings, 12
!   vfm  master fit normal matrix, 8 by 8
!   vfw  a working copy of it, since the solve destroys what it is given
!   vfb  the fit right-hand side, and afterwards the solution
!   vfx  the recovered odd coefficients, divided back into the s variable

vsc = 1
vtt = 21
vey = 41
vez = 61
vfm = 101
vfw = 201
vfb = 301
vfx = 321

mrowy = 3101
mrowz = 3121

PRINT "==================================================================="
PRINT "RAYINV - coefficients from REAL TRACED RAYS, no series"
PRINT "==================================================================="
PRINT " "

! ---------------------------------------------------------------------------------
! The system, and the ray basis.
! ---------------------------------------------------------------------------------

PARAXIAL ON
wl = PWAV()
ns = NSUR()
nsm = ns - 1

IF (nsm < 1)
   PRINT "RAYINV needs at least one optical surface."
   GOTO 900
ENDIF

IF (ABSO(THIC(0)) < 1E10)
   PRINT "RAYINV handles the infinite conjugate only. The paraxial image height it"
   PRINT "subtracts before fitting is efl tan(field), which is the collimated form."
   PRINT "FORBES.ZPL carries both conjugates and should be used for this lens."
   GOTO 900
ENDIF

RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 900
ENDIF
mgy = RAYY(1)
mgu = RAYM(nsm) / RAYN(nsm)
IF (ABSO(mgu) < eps)
   PRINT "The system is afocal; there is no focus to catch the rays on."
   GOTO 900
ENDIF

! The focal length, and the paraxial focus. A ray entering parallel to the axis at
! height y leaves with slope u and crosses the axis a focal length later, so both
! fall out of the marginal ray already traced.
efl = -mgy / mgu
gpfd = -RAYY(nsm) / mgu

RAYTRACE 0, 1, 0, 0, wl
IF (RAYE() != 0)
   PRINT "The paraxial chief ray will not trace. Is a field defined?"
   GOTO 900
ENDIF
hmax = RAYM(0) / RAYN(0)
IF (ABSO(hmax) < eps)
   PRINT "The chief ray carries no field. Is the field zero?"
   GOTO 900
ENDIF
amax = ATAN(hmax)

FORMAT 12.6
PRINT "Effective focal length     ", efl
PRINT "Paraxial focus distance    ", gpfd
PRINT "tan of the maximum field   ", hmax
PRINT " "

! ---------------------------------------------------------------------------------
! The ladder, and the fit matrix it induces. The matrix depends only on the scales,
! so it is built once and copied for each of the hundred and fifty solves.
! ---------------------------------------------------------------------------------

VEC4(1) = 1
VEC4(2) = 3
VEC4(3) = 5
VEC4(4) = 7
VEC4(5) = 9
VEC4(6) = 11
VEC4(7) = 13
VEC4(8) = 15

smax = 0
FOR jsc = 1, nsc, 1
   VEC1(vsc + jsc - 1) = 0.6 * jsc / nsc
   IF (VEC1(vsc + jsc - 1) > smax) THEN smax = VEC1(vsc + jsc - 1)
NEXT
FOR jsc = 1, nsc, 1
   VEC1(vtt + jsc - 1) = VEC1(vsc + jsc - 1) / smax
NEXT

FOR ji = 0, npw - 1, 1
   FOR jj = 0, npw - 1, 1
      vsum = 0
      FOR jsc = 0, nsc - 1, 1
         vsum = vsum + POWR(VEC1(vtt + jsc), VEC4(1 + ji)) * POWR(VEC1(vtt + jsc), VEC4(1 + jj))
      NEXT
      VEC1(vfm + ji * npw + jj) = vsum
   NEXT
NEXT

! ---------------------------------------------------------------------------------
! The seventy-five shapes, the same set FORBES uses.
! ---------------------------------------------------------------------------------

VEC2(3901) = 0.35
VEC2(3902) = 0.7
VEC2(3903) = 1.0

pival = 2 * ACOS(0)
VEC2(3911) = 0
VEC2(3912) = pival / 6
VEC2(3913) = pival / 4
VEC2(3914) = pival / 3
VEC2(3915) = pival / 2
VEC2(3916) = 2 * pival / 3
VEC2(3917) = 5 * pival / 6

nsh = 0
FOR jr = 1, 3, 1
   FOR jt = 1, 7, 1
      FOR jh = 1, 3, 1
         VEC2(3201 + nsh * 3) = VEC2(3900 + jr)
         VEC2(3202 + nsh * 3) = VEC2(3910 + jt)
         VEC2(3203 + nsh * 3) = VEC2(3900 + jh)
         nsh = nsh + 1
      NEXT
   NEXT
NEXT
FOR jr = 1, 3, 1
   FOR jt = 1, 3, 1
      VEC2(3201 + nsh * 3) = VEC2(3900 + jr)
      VEC2(3202 + nsh * 3) = VEC2(3911 + (jt - 1) * 2)
      VEC2(3203 + nsh * 3) = 0
      nsh = nsh + 1
   NEXT
NEXT
FOR jh = 1, 3, 1
   VEC2(3201 + nsh * 3) = 0
   VEC2(3202 + nsh * 3) = 0
   VEC2(3203 + nsh * 3) = VEC2(3900 + jh)
   nsh = nsh + 1
NEXT

FOR jr = 0, 24, 1
   VEC2(2001 + jr) = 0
NEXT
FOR jr = 0, 4, 1
   VEC2(2101 + jr) = 0
NEXT
FOR jr = 0, 143, 1
   VEC2(2201 + jr) = 0
NEXT
FOR jr = 0, 11, 1
   VEC2(2401 + jr) = 0
NEXT
FOR jr = 0, 399, 1
   VEC2(2501 + jr) = 0
NEXT
FOR jr = 0, 19, 1
   VEC2(3001 + jr) = 0
NEXT

PARAXIAL OFF

PRINT "Tracing 75 shapes at 12 scales - 900 real rays. This is slow."
PRINT " "

nbad = 0
FOR jsh = 0, nsh - 1, 1
   srho = VEC2(3201 + jsh * 3)
   sthe = VEC2(3202 + jsh * 3)
   shh = VEC2(3203 + jsh * 3)

   GOSUB shoot

   IF (sok == 1)
      GOSUB fitshape

      mord = 3
      nunk = 5
      gata = 2001
      gatb = 2101
      GOSUB mkmod
      ry = c3y
      rz = c3z
      GOSUB accum

      mord = 5
      nunk = 12
      gata = 2201
      gatb = 2401
      GOSUB mkmod
      ry = c5y
      rz = c5z
      GOSUB accum

      mord = 7
      nunk = 20
      gata = 2501
      gatb = 3001
      GOSUB mkmod
      ry = c7y
      rz = c7z
      GOSUB accum
   ELSE
      nbad = nbad + 1
   ENDIF
NEXT

PARAXIAL ON

IF (nsh - nbad < 25)
   PRINT "Too few shapes traced to determine twenty coefficients. Rays are being"
   PRINT "lost - check the apertures and the vignetting factors."
   GOTO 900
ENDIF

nunk = 5
gata = 2001
gatb = 2101
GOSUB gsolve

nunk = 12
gata = 2201
gatb = 2401
GOSUB gsolve

nunk = 20
gata = 2501
gatb = 3001
GOSUB gsolve

GOSUB showco

LABEL 900
IF (ABSO(paraxmode) > eps) THEN PARAXIAL ON
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF

END

! =================================================================================
! SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! shoot - trace the current shape at all twelve scales and store the transverse
! aberration, meridional in VEC1(vey..) and sagittal in VEC1(vez..).
!
! The field is set through the NORMALISED coordinate, so the angle wanted -
! atan(s h hmax) - is divided by the maximum field angle rather than handed over in
! degrees. The paraxial image height, efl tan(field), is subtracted here for the
! reason given at the head of this file.
!
! The landing is taken at the PARAXIAL FOCUS, not at the image surface the file
! happens to carry, so that the answer does not move when someone refocuses the lens.
! ---------------------------------------------------------------------------------
SUB shoot
   sok = 1
   FOR ki = 0, nsc - 1, 1
      ksc = VEC1(vsc + ki)
      kpy = ksc * srho * COSI(sthe)
      kpx = ksc * srho * SINE(sthe)
      ktan = ksc * shh * hmax
      khy = ATAN(ktan) / amax

      RAYTRACE 0, khy, kpx, kpy, wl
      IF (RAYE() != 0)
         sok = 0
      ELSE
         kdz = gpfd - RAYZ(nsm)
         kyy = RAYY(nsm) + kdz * RAYM(nsm) / RAYN(nsm)
         kxx = RAYX(nsm) + kdz * RAYL(nsm) / RAYN(nsm)
         VEC1(vey + ki) = kyy - efl * ktan
         VEC1(vez + ki) = kxx
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! fitshape - fit the odd polynomial through both sets of landings and read off the
! third-, fifth- and seventh-order coefficients.
!
! Fitted in t = s / s_max and divided back afterwards; see the head of the file for
! why that matters more than it looks.
! ---------------------------------------------------------------------------------
SUB fitshape
   fsrc = vey
   GOSUB fitone
   c3y = VEC1(vfx + 1)
   c5y = VEC1(vfx + 2)
   c7y = VEC1(vfx + 3)

   fsrc = vez
   GOSUB fitone
   c3z = VEC1(vfx + 1)
   c5z = VEC1(vfx + 2)
   c7z = VEC1(vfx + 3)
RETURN

! fitone - least squares of the data at fsrc onto the eight odd powers, leaving the
! coefficients in VEC1(vfx..) already divided back into the s variable.
SUB fitone
   FOR gi = 0, npw * npw - 1, 1
      VEC1(vfw + gi) = VEC1(vfm + gi)
   NEXT
   FOR gi = 0, npw - 1, 1
      gsum = 0
      FOR gj = 0, nsc - 1, 1
         gsum = gsum + POWR(VEC1(vtt + gj), VEC4(1 + gi)) * VEC1(fsrc + gj)
      NEXT
      VEC1(vfb + gi) = gsum
   NEXT

   nunk = npw
   gata = vfw
   gatb = vfb
   GOSUB gsolv1

   FOR gi = 0, npw - 1, 1
      VEC1(vfx + gi) = VEC1(vfb + gi) / POWR(smax, VEC4(1 + gi))
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! gsolv1 - Gaussian elimination with partial pivoting on VEC1. A second copy of
! gsolve because the fit lives in VEC1 and the coefficient systems live in VEC2, and
! ZPL has no way to say "whichever vector".
!
! Every loop whose bound can fall below its start is guarded: ZPL reports an empty
! range as an infinite loop rather than executing it zero times.
! ---------------------------------------------------------------------------------
SUB gsolv1
   FOR hc = 0, nunk - 1, 1
      hp = hc
      hbest = ABSO(VEC1(gata + hc * nunk + hc))
      IF (hc < nunk - 1)
         FOR hr = hc + 1, nunk - 1, 1
            hv = ABSO(VEC1(gata + hr * nunk + hc))
            IF (hv > hbest)
               hbest = hv
               hp = hr
            ENDIF
         NEXT
      ENDIF
      IF (hp != hc)
         FOR hk = 0, nunk - 1, 1
            htmp = VEC1(gata + hc * nunk + hk)
            VEC1(gata + hc * nunk + hk) = VEC1(gata + hp * nunk + hk)
            VEC1(gata + hp * nunk + hk) = htmp
         NEXT
         htmp = VEC1(gatb + hc)
         VEC1(gatb + hc) = VEC1(gatb + hp)
         VEC1(gatb + hp) = htmp
      ENDIF
      hd = VEC1(gata + hc * nunk + hc)
      IF (ABSO(hd) > 1E-300)
         IF (hc < nunk - 1)
            FOR hr = hc + 1, nunk - 1, 1
               hf = VEC1(gata + hr * nunk + hc) / hd
               IF (hf != 0)
                  FOR hk = hc, nunk - 1, 1
                     VEC1(gata + hr * nunk + hk) = VEC1(gata + hr * nunk + hk) - hf * VEC1(gata + hc * nunk + hk)
                  NEXT
                  VEC1(gatb + hr) = VEC1(gatb + hr) - hf * VEC1(gatb + hc)
               ENDIF
            NEXT
         ENDIF
      ENDIF
   NEXT
   FOR hr = nunk - 1, 0, -1
      hs = VEC1(gatb + hr)
      IF (hr < nunk - 1)
         FOR hk = hr + 1, nunk - 1, 1
            hs = hs - VEC1(gata + hr * nunk + hk) * VEC1(gatb + hk)
         NEXT
      ENDIF
      VEC1(gatb + hr) = hs / VEC1(gata + hr * nunk + hr)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! gsolve - the same, on VEC2, for the coefficient systems.
! ---------------------------------------------------------------------------------
SUB gsolve
   FOR gc = 0, nunk - 1, 1
      gp = gc
      gbest = ABSO(VEC2(gata + gc * nunk + gc))
      IF (gc < nunk - 1)
         FOR gr = gc + 1, nunk - 1, 1
            gv2 = ABSO(VEC2(gata + gr * nunk + gc))
            IF (gv2 > gbest)
               gbest = gv2
               gp = gr
            ENDIF
         NEXT
      ENDIF
      IF (gp != gc)
         FOR gk2 = 0, nunk - 1, 1
            gtmp = VEC2(gata + gc * nunk + gk2)
            VEC2(gata + gc * nunk + gk2) = VEC2(gata + gp * nunk + gk2)
            VEC2(gata + gp * nunk + gk2) = gtmp
         NEXT
         gtmp = VEC2(gatb + gc)
         VEC2(gatb + gc) = VEC2(gatb + gp)
         VEC2(gatb + gp) = gtmp
      ENDIF
      gd = VEC2(gata + gc * nunk + gc)
      IF (ABSO(gd) > 1E-300)
         IF (gc < nunk - 1)
            FOR gr = gc + 1, nunk - 1, 1
               gf = VEC2(gata + gr * nunk + gc) / gd
               IF (gf != 0)
                  FOR gk2 = gc, nunk - 1, 1
                     VEC2(gata + gr * nunk + gk2) = VEC2(gata + gr * nunk + gk2) - gf * VEC2(gata + gc * nunk + gk2)
                  NEXT
                  VEC2(gatb + gr) = VEC2(gatb + gr) - gf * VEC2(gatb + gc)
               ENDIF
            NEXT
         ENDIF
      ENDIF
   NEXT
   FOR gr = nunk - 1, 0, -1
      gs = VEC2(gatb + gr)
      IF (gr < nunk - 1)
         FOR gk2 = gr + 1, nunk - 1, 1
            gs = gs - VEC2(gata + gr * nunk + gk2) * VEC2(gatb + gk2)
         NEXT
      ENDIF
      VEC2(gatb + gr) = gs / VEC2(gata + gr * nunk + gr)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! accum - add one shape's contribution to the normal equations at gata and gatb.
! ---------------------------------------------------------------------------------
SUB accum
   FOR ai = 0, nunk - 1, 1
      VEC2(gatb + ai) = VEC2(gatb + ai) + VEC2(mrowy + ai) * ry + VEC2(mrowz + ai) * rz
      FOR aj = 0, nunk - 1, 1
         apr = VEC2(mrowy + ai) * VEC2(mrowy + aj) + VEC2(mrowz + ai) * VEC2(mrowz + aj)
         VEC2(gata + ai * nunk + aj) = VEC2(gata + ai * nunk + aj) + apr
      NEXT
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! mkmod - the model row for the current shape at order mord. This is Robb's Eq. (2)
! with one coefficient set to one at a time, keeping the terms of total degree mord,
! and it is DELIBERATELY the same model FORBES.ZPL uses - see the head of this file.
! The last column of each is distortion, which Robb's spot-size polynomial omits
! because it moves the patch without resizing it, and which a landing ray does carry.
! ---------------------------------------------------------------------------------
SUB mkmod
   mc = COSI(sthe)
   ms = SINE(sthe)
   mc2 = COSI(2 * sthe)
   ms2 = SINE(2 * sthe)
   mc4 = COSI(4 * sthe)
   ms4 = SINE(4 * sthe)
   mc3 = mc * mc * mc
   mcs = mc * mc * ms

   mr1 = srho
   mr2 = mr1 * srho
   mr3 = mr2 * srho
   mr4 = mr3 * srho
   mr5 = mr4 * srho
   mr6 = mr5 * srho
   mr7 = mr6 * srho
   mh1 = shh
   mh2 = mh1 * shh
   mh3 = mh2 * shh
   mh4 = mh3 * shh
   mh5 = mh4 * shh
   mh6 = mh5 * shh
   mh7 = mh6 * shh

   FOR mi = 0, 19, 1
      VEC2(mrowy + mi) = 0
      VEC2(mrowz + mi) = 0
   NEXT

   IF (mord == 3)
      VEC2(mrowy + 0) = mr3 * mc
      VEC2(mrowz + 0) = mr3 * ms
      VEC2(mrowy + 1) = 2 * mr2 * mh1 + mr2 * mh1 * mc2
      VEC2(mrowz + 1) = mr2 * mh1 * ms2
      VEC2(mrowy + 2) = 3 * mr1 * mh2 * mc
      VEC2(mrowz + 2) = mr1 * mh2 * ms
      VEC2(mrowy + 3) = mr1 * mh2 * mc
      VEC2(mrowz + 3) = mr1 * mh2 * ms
      VEC2(mrowy + 4) = mh3
   ENDIF

   IF (mord == 5)
      VEC2(mrowy + 0) = mr5 * mc
      VEC2(mrowz + 0) = mr5 * ms
      VEC2(mrowy + 1) = mr4 * mh1
      VEC2(mrowy + 2) = mr4 * mh1 * mc2
      VEC2(mrowz + 2) = mr4 * mh1 * ms2
      VEC2(mrowy + 3) = mr3 * mh2 * mc
      VEC2(mrowy + 4) = mr3 * mh2 * mc
      VEC2(mrowz + 4) = mr3 * mh2 * ms
      VEC2(mrowy + 5) = mr3 * mh2 * mc3
      VEC2(mrowz + 5) = mr3 * mh2 * mcs
      VEC2(mrowy + 6) = mr2 * mh3
      VEC2(mrowy + 7) = 0.5 * mr2 * mh3 + 0.5 * mr2 * mh3 * mc2
      VEC2(mrowz + 8) = mr2 * mh3 * ms2
      VEC2(mrowy + 9) = 5 * mr1 * mh4 * mc
      VEC2(mrowz + 9) = mr1 * mh4 * ms
      VEC2(mrowy + 10) = mr1 * mh4 * mc
      VEC2(mrowz + 10) = mr1 * mh4 * ms
      VEC2(mrowy + 11) = mh5
   ENDIF

   IF (mord == 7)
      VEC2(mrowy + 0) = mr7 * mc
      VEC2(mrowz + 0) = mr7 * ms
      VEC2(mrowy + 1) = mr6 * mh1
      VEC2(mrowy + 2) = mr6 * mh1 * mc2
      VEC2(mrowz + 2) = mr6 * mh1 * ms2
      VEC2(mrowy + 3) = mr5 * mh2 * mc
      VEC2(mrowz + 4) = mr5 * mh2 * ms
      VEC2(mrowy + 5) = mr5 * mh2 * mc3
      VEC2(mrowz + 5) = mr5 * mh2 * mcs
      VEC2(mrowy + 6) = mr4 * mh3
      VEC2(mrowy + 7) = mr4 * mh3 * mc2
      VEC2(mrowz + 8) = mr4 * mh3 * ms2
      VEC2(mrowy + 9) = mr4 * mh3 * mc4
      VEC2(mrowz + 9) = mr4 * mh3 * ms4
      VEC2(mrowy + 10) = mr3 * mh4 * mc
      VEC2(mrowy + 11) = mr3 * mh4 * mc3
      VEC2(mrowz + 12) = mr3 * mh4 * ms
      VEC2(mrowz + 13) = mr3 * mh4 * mcs
      VEC2(mrowy + 14) = mr2 * mh5
      VEC2(mrowy + 15) = mr2 * mh5 * mc2
      VEC2(mrowz + 16) = mr2 * mh5 * ms2
      VEC2(mrowy + 17) = mr1 * mh6 * mc
      VEC2(mrowz + 18) = mr1 * mh6 * ms
      VEC2(mrowy + 19) = mh7
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! showco - the coefficients, and what they should be held to.
! ---------------------------------------------------------------------------------
SUB showco
   PRINT " "
   PRINT "==================================================================="
   PRINT "Coefficients from real rays, transverse measure"
   PRINT "==================================================================="
   PRINT " "
   FORMAT 5.0 INT
   PRINT "Shapes traced              ", nsh - nbad
   IF (nbad > 0)
      PRINT "Shapes LOST to failed rays ", nbad
      PRINT "   Rays that will not trace are dropped rather than fudged. If many are"
      PRINT "   lost the remaining shapes may not span the coefficients."
   ENDIF
   PRINT " "
   FORMAT 15.6 EXP
   PRINT "Third order"
   PRINT "   B  ", VEC2(2101), "   F  ", VEC2(2102), "   C  ", VEC2(2103)
   PRINT "   Pi ", VEC2(2104), "   E  ", VEC2(2105)
   PRINT " "
   PRINT "Fifth order"
   PRINT "   B5 ", VEC2(2401), "   F1 ", VEC2(2402), "   F2 ", VEC2(2403)
   PRINT "   M1 ", VEC2(2404), "   M2 ", VEC2(2405), "   M3 ", VEC2(2406)
   PRINT "   N1 ", VEC2(2407), "   N2 ", VEC2(2408), "   N3 ", VEC2(2409)
   PRINT "   C5 ", VEC2(2410), "   Pi5", VEC2(2411), "   E5 ", VEC2(2412)
   PRINT " "
   PRINT "Seventh order, Buchdahl's tau. tau1 is B7."
   PRINT "   tau1 ", VEC2(3001), "  tau2 ", VEC2(3002), "  tau3 ", VEC2(3003)
   PRINT "   tau4 ", VEC2(3004), "  tau5 ", VEC2(3005), "  tau6 ", VEC2(3006)
   PRINT "   tau7 ", VEC2(3007), "  tau8 ", VEC2(3008), "  tau9 ", VEC2(3009)
   PRINT "   tau10", VEC2(3010), "  tau11", VEC2(3011), "  tau12", VEC2(3012)
   PRINT "   tau13", VEC2(3013), "  tau14", VEC2(3014), "  tau15", VEC2(3015)
   PRINT "   tau16", VEC2(3016), "  tau17", VEC2(3017), "  tau18", VEC2(3018)
   PRINT "   tau19", VEC2(3019), "  tau20", VEC2(3020)
   PRINT " "
   PRINT "WHAT THIS IS FOR"
   PRINT " "
   PRINT "Nothing here is a series. These came from real rays traced by OpticStudio,"
   PRINT "so where they agree with FORBES.ZPL the two agree by routes with nothing"
   PRINT "in common except the definition of the coefficients."
   PRINT " "
   PRINT "That matters in exactly one place. On a system of spheres FORBES can already"
   PRINT "be checked against BUCH7 and against FIFTHORD, which ships with OpticStudio."
   PRINT "On a FIGURED system BUCH7 declines, FIFTHORD reaches B7 and stops, and the"
   PRINT "Seidel analysis reaches the third order and stops - so tau2 to tau20 there"
   PRINT "have nothing else to be held to. THIS IS THAT CHECK."
   PRINT " "
   PRINT "EXPECT A FEW PARTS IN TEN THOUSAND, NOT MACHINE PRECISION. A traced ray"
   PRINT "carries every order at once and the fit has to separate them, so this"
   PRINT "arrives with a fitting residual where a series arrives exactly. A"
   PRINT "disagreement at that level is the fit. A disagreement at per cent level is"
   PRINT "not, and on an aspheric system it would be worth taking seriously, because"
   PRINT "that is the size of the error the aspheric Buchdahl arrangement carries."
RETURN
