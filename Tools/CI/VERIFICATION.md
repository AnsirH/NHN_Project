# WebGL CI verification

Verification date: 2026-10-01. Base revision: `47afc4cd2808f9586a3664015aa7ecf29c03a979`.

## Local evidence

Tests/builds use the native Unity CLI `1.0.0-beta.9`, Unity `6000.5.3f1`, and a separate
worktree under `Temp/WebGLValidation`. The original editor and its unsaved scene remain open.

- TDD RED: 11 new builder contract cases failed before implementation (`Logs/webgl-red.xml`).
  The checkpoint commit subject mistakenly says 12; the recorded XML has 11.
- Initial GREEN: all 11 passed (`Logs/webgl-focused-green.xml`).
- Refined builder: 12 tests, including duplicate arguments and hashed-file setting restoration.
- Release EditMode suite: **374 passed, 0 failed** (`Logs/editmode.xml`).
- Full EditMode suite before the twelfth builder case: 375 cases, 374 passed, 1 pre-existing
  `archer_general_volley_cross` cross-runtime numerical drift failure (`Logs/webgl-green.xml`).
- Full PlayMode suite: **126 cases: 82 passed, 33 existing failures, 11 skipped** (`Logs/playmode.xml`).
  Runtime/test/prefab files are identical to the base revision. Failures are documented in the
  explicit baseline and checked against error messages/source locations. They are not counted as passing.
- CI report guard: known baseline accepted; cancelled/incomplete/suite/new/changed-reason/
  changed-location reports all rejected.
- Native CLI WebGL output: **164 MB**, artifact structure passed. First cold build reached the
  Bee six-rescan limit; the next full CI run completed successfully using the populated cache.
  The specific failure now has a single bounded retry, preserving the original log.
- Local Chromium smoke passed: no HTTP, network, JavaScript, or console errors; main menu rendered.
- Interactive WebGL acceptance: main menu -> map selection -> character selection -> room graph ->
  deployment -> rendered 3D battle -> room graph return. Gold changed from 0 to 20 and the completed
  battle node changed color. Formation popup opened and closed after return; no browser errors.
  Screenshots are under `Logs/gameplay/` in the validation worktree. The skill-button click happened
  after the battle had already ended, so it does not verify skill execution.

The release gate does not run the two BalanceLab cross-runtime cases. There is no measured
coverage percentage. Batch-mode capture skips do not substitute for browser verification.

## Deployment verification

GitHub Pages uses Actions with the `github-pages` environment restricted to `main`.
The registered Windows runner is `ClayWars-WebGL-user`, label `unity-webgl`; its listener
starts hidden at Windows user login. Browser smoke runs on GitHub-hosted Linux and checks
HTTP/JavaScript/Unity console errors at `/NHN_Project/`, captures the main menu, and reads metadata.

Automatic run evidence:

- First automatic main run: [36854209818](https://github.com/AnsirH/NHN_Project/actions/runs/36854209818).
  Native tests/build and downloadable artifact succeeded. Windows Pages packaging selected the
  WSL `bash.exe` without an installed distribution and failed. Packaging was moved to the Linux
  browser job, after runtime verification, to remove that Windows shell dependency.
- Corrected automatic main run: [36855875374](https://github.com/AnsirH/NHN_Project/actions/runs/36855875374),
  source `8d0e256a1d4c76af3ebb12647baa4166daf0973c`: **build, Linux Chromium smoke, Pages packaging,
  and deployment all succeeded**. Library cache saved: 2,286,973,179 bytes. The exact cold-cache
  Bee failure triggered the single retry and its original log was retained.
- Public [build metadata](https://ansirh.github.io/NHN_Project/build-info.json) returned HTTP 200
  with the same revision as the successful workflow. Public site: https://ansirh.github.io/NHN_Project/.

The follow-up remote cache restore became a multi-minute bottleneck. The self-hosted workflow now
preserves the dedicated runner's local Library while resetting/cleaning source and generated output.
The actual workflow cleanup script was exercised against an isolated Git fixture: tracked source
restored, root Library preserved, Build/Logs and an untracked nested Library removed. The exclude
pattern is anchored as `/Library/`. Superseded main/dev acceptance runs for `09fd990` were cancelled.
The earlier cloud cache remains historical evidence, not the final cache design. Final acceptance
checks local Library reuse on main/dev. The [workflow history](https://github.com/AnsirH/NHN_Project/actions/workflows/webgl.yml)
records their build/browser outcome and main-only deployment. Compare the live
metadata revision to the latest successful main run, since later pushes supersede the revision above.
