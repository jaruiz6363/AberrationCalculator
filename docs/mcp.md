# The MCP server

`abcalc-mcp` exposes the calculator over the Model Context Protocol, so an assistant can read
a lens file and ask for its aberration coefficients directly. It is a thin layer over the same
`ReportWriter` the command line uses, so the two cannot give different answers.

## Registering it, the easy way

    dotnet build tools/mcpsetup -c Release
    tools/mcpsetup/bin/Release/net8.0-windows/mcp-setup.exe

A small window. It finds the built server, or builds it for you; it says which Claude
clients it found; and it writes the registration.

**It edits a file you did not write and probably care about.** Your Claude configuration
usually holds other MCP servers, so `mcp-setup` copies the file aside with a timestamp
before every write, merges rather than replaces, leaves every key it does not own alone,
and prints the list of registered servers before and after so you can see for yourself that
nothing went missing. If it ever does, it says which one and tells you to restore the
backup. `ClaudeConfigTests` covers all of that; every test in it is a test that something
was left alone.

There is a **Remove** button too, so trying it is not a commitment.

**Choose the scope.** Claude Code keeps three, and the window defaults to `user` rather than
the CLI's own default of `local`:

| scope | where the server is available |
|---|---|
| `user` | everywhere, for you |
| `local` | this folder only |
| `project` | this folder, and committed to the repository as `.mcp.json` |

`local` is the CLI default and it is rarely what someone wants from a lens tool: registering
it inside the repository and then finding it absent everywhere else looks exactly like the
registration having failed.

**Restart afterwards.** A client reads its MCP servers once, when it starts. Until then it
will not list the server and nothing is wrong - which is a confusing enough half-hour that
the window now says so in a box you cannot miss, rather than in a line at the bottom.

## Registering it by hand

    dotnet build src/AberrationCalculator.Mcp -c Release

Then point the client at the built executable. For Claude Code:

    claude mcp add abcalc -- <repo>/src/AberrationCalculator.Mcp/bin/Release/net8.0/abcalc-mcp

or, in a client that takes JSON:

    {
      "mcpServers": {
        "abcalc": {
          "command": "<repo>/src/AberrationCalculator.Mcp/bin/Release/net8.0/abcalc-mcp"
        }
      }
    }

## The tools

Every REPORTING tool takes `lens_file` and an optional `glass_dir`. Most return tab-separated
tables, so a caller can parse a number rather than scrape prose; the four marked *text* are
verdicts and breakdowns that read far better ruled than flattened into one row per cell.

Two tools do not report on a lens and so carry their own arguments, listed separately below:
`optimize`, which CHANGES a design, and `base_path`, which says what folder bare file names are
taken against.

| tool | what it gives |
|---|---|
| `analyse_lens` | the whole analysis, formatted to read (*text*) |
| `prescription` | one row per surface |
| `first_order` | focal length, pupils, F-number, track |
| `paraxial_rays` | marginal and chief ray at every surface |
| `indices` | each material at each wavelength |
| `seidel` | third-order coefficients, per surface and totalled |
| `buchdahl` | third, fifth and seventh order |
| `rms_spot` | predicted RMS spot per field and wavelength, and PRMSA |
| `contributions` | which aberration is costing the design its performance |
| `surface_breakdown` | intrinsic, aspheric and induced, per surface |
| `surface_share` | each surface's share of the spot, and how much of it is induced |
| `seventh_order` | third, fifth and seventh order per surface, intrinsic and induced, the seventh by the Forbes series trace - the one that handles aspheres (*text*) |
| `aspheric_screen` | whether a design would exercise the aspheric seventh-order path hard enough to test it (*text*) |
| `distortion_from_coefficients` | how far the coefficients can be trusted for distortion, against rays - NOT the way to get a distortion figure, for which the traced column beside them is the answer (*text*) |

### `optimize`

Optimises a lens against a merit function given inline as text, and reports what changed.
Every derivative it uses is analytic - see `docs/optimizer.md` - including through PRMSA, the
predicted spot. **Spherical surfaces only:** the coefficients come from Buchdahl's closed-form
scheme, whose aspheric seventh order is a reconstruction real rays reject, so a figured design -
or a conic asked to be a variable - is refused before the run rather than optimised against a
number known to be wrong. The reporting tools above are unaffected and handle figuring
throughout.

| argument | |
|---|---|
| `lens_file` | the lens to optimise. It is read, never written |
| `merit` | the merit function as text, or `merit_file` for a path |
| `variables` | variables and pickups as text, in the `.var` format. A `.lhlt` carries its own |
| `method` | `lm`, `psd2`, `psd3` (default) or `hj` |
| `iterations` | local iterations, or iterations per hop |
| `hops`, `chains`, `seed` | basin hopping, off by default |
| `glass_substitution` | name of a substitution catalogue the hopping may take glasses from, e.g. `CoreSet28` |
| `save_to` | where to write the result. Under hopping this is a **folder**, and one design per chain goes into it. **Nothing is written without it** |

**The lens on disk is never modified.** A run that made the design worse costs nothing, and
the report says so rather than handing back something nobody asked for.

Formats are taken from the extension: `.zmx`, `.seq`, `.otx`, `.opt`, `.len`, `.osl`, `.json`
(Optiland) and `.lhlt`.


### `base_path`

Sets the folder that bare file names are taken to mean, so `lens_file` can be `L.zmx` rather
than a full path.

| argument | |
|---|---|
| `path` | the folder. Omit to report the base in force without changing it |
| `clear` | forget the stored base |

**This matters more here than on the command line.** A terminal has a working directory the user
chose; this server has whatever the client started it in, which the user cannot see and cannot
change - so without a base, every path has to be absolute. Set it once at the start of a session
and every tool above resolves against it.

It is the same setting as the command line's `BASE`, kept between runs, and it reports where the
base came from as well as what it is - `--dir`, `ABCALC_DIR`, `BASE`, or the working directory,
in that order of precedence. An absolute path is never re-rooted.

## Two things worth knowing

**A glass name does not say whose glass it is.** Some formats carry a catalog and some do
not. Optiland's JSON, for instance, gives only a name, so "F2" resolves against whatever
catalogs happen to be loaded - and on the Cooke triplet in `tests/fixtures` the wrong vendor's
F2 moves the focal length from 50.000 to 49.063 with no error anywhere. Pass `glass_dir` when
it matters.

**Unresolved materials are reported, not thrown.** If a glass cannot be found the analysis
still runs and still returns, with a warning at the top of the result: every quantity that
depends on the missing glass is meaningless, and the caller has no other way to learn that.

## The protocol

JSON-RPC 2.0 over stdin and stdout, newline-delimited: `initialize`, `tools/list`,
`tools/call`, `ping`. It is implemented directly rather than through a package - three methods
is less than a preview dependency would cost, in a program whose point is that its numbers can
be traced to something. stdout carries the protocol and nothing else; diagnostics go to stderr,
because a stray line on stdout shows up at the client as a parse error rather than as whatever
actually went wrong.
