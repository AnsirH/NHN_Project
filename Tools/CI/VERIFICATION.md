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

The release gate does not run the two BalanceLab cross-runtime cases. There is no measured
coverage percentage. Batch-mode capture skips do not substitute for browser verification.

## Deployment verification

GitHub Pages uses Actions with the `github-pages` environment restricted to `main`.
The registered Windows runner is `ClayWars-WebGL-user`, label `unity-webgl`; its listener
starts hidden at Windows user login. Browser smoke runs on GitHub-hosted Linux and checks
HTTP/JavaScript/Unity console errors at `/NHN_Project/`, captures the main menu, and reads metadata.

Build, browser, and deployment results are recorded below after the actual runs complete.
