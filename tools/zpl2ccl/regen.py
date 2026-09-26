"""Regenerate the generated part of ccl/buch7_asph.ccl from macros/BUCH7_ASPH.ZPL.

    python tools/zpl2ccl/regen.py

The file has two parts. The OSLO front end - the lens data and the two paraxial rays, from
the top of the file through b7_prop, and b7_run and the command at the bottom - is written by
hand. Everything between the marker "// ---- generated from macros/BUCH7_ASPH.ZPL" and
"static int\\nb7_run(" is the macro, from its stage A on, translated statement for statement:

  zpl2ccl.py  the translation. Every ZPL variable becomes a global z_<name> (ZPL is
              case-insensitive and has no locals), each SUB a function, PRINT/FORMAT a line
              built with sprintf/strcat and written by b7_out(). The FIFTHORD and FORBES
              notes, which are about OpticStudio, are left out.
  split.py    CCL refuses a function past an internal size, so the macro's main body is cut
              into functions of at most 200 lines at statement boundaries; the macro's early
              exit becomes the flag z_stop.

The arrays are sized for 16 surfaces between object and image (ARRAYS in zpl2ccl.py), to fit
the 1 MB of global storage CCL shares with OSLO's own CCL; B7_MAXS in the front end is 18 to
match. Re-run this after changing the macro, then compile in OSLO and check with compare.py.
"""
import os, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(os.path.dirname(here))
ccl = os.path.join(root, "ccl", "buch7_asph.ccl")

gen = os.path.join(tempfile.mkdtemp(), "gen.ccl")
subprocess.run([sys.executable, os.path.join(here, "zpl2ccl.py"), gen], check=True)
t = open(gen, encoding="utf-8").read().replace(
    "System totals, transverse measure - these are what FIFTHORD prints", "System totals, transverse measure")
open(gen, "w", encoding="utf-8").write(t)
subprocess.run([sys.executable, os.path.join(here, "split.py"), gen], check=True)
g = open(gen, encoding="utf-8").read()

src = open(ccl, encoding="utf-8").read()
a = src.index("// ---- generated from macros/BUCH7_ASPH.ZPL")
b = src.index("static int\nb7_run(")
out = src[:a] + g + "\n" + src[b:]
if out == src:
    print("unchanged")
else:
    open(ccl, "w", encoding="utf-8").write(out)
    print("regenerated", ccl)
