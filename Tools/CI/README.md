# WebGL build and GitHub Pages

## Automatic builds

Every push to `main` or `dev` runs `.github/workflows/webgl.yml`.

- `main`: EditMode and PlayMode tests → Unity CLI WebGL build → downloadable artifact → GitHub Pages.
- `dev`: the same tests/build and downloadable artifact; the public site stays on `main`.
- Actions → **WebGL build and Pages** → **Run workflow** supports manual builds of `main`/`dev`.
- Public URL: https://ansirh.github.io/NHN_Project/
- Failed builds never replace the previously published site. Logs and test XML are retained for 14 days.
- `Build/WebGL/build-info.json` records the editor version, UTC build time, and source commit.

## Runner and license

The build job uses the Windows self-hosted runner **ClayWars-WebGL-user**, labelled `unity-webgl`.
It uses the existing Unity license and Unity CLI; no Unity account password is stored in GitHub.
The PC must be powered on, connected to the internet, and logged in with the licensed Windows account.
An offline runner leaves builds queued until it comes back online. This is not an always-on cloud builder.
The Pages deployment job runs on a GitHub-hosted Linux runner.

Only trusted `main`/`dev` source runs on the workstation. There is no pull-request event, and manual
execution of other branches is rejected. Do not add fork PR execution to this self-hosted workflow.
Runner credentials and working files are under `Tools/CI/.runner/`, which is ignored by Git.

Required local tools:

- Unity CLI `1.0.0-beta.9` or newer (`unity --version`).
- Unity `6000.5.3f1` with Web Build Support and a valid local license.
- Git with Git LFS, Windows PowerShell 5.1, and the registered GitHub Actions runner.

Start/restart the listener using:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CI/Start-Runner.ps1
```

The script starts the runner in a hidden background window and avoids starting a duplicate listener.
The configured workstation also has a **ClayWars WebGL Runner** shortcut in the Windows Startup folder
so the listener starts at user login. If the project is moved, update that shortcut.
To stop it, use Task Manager to stop the matching `Runner.Listener.exe` process and its launcher.

For a replacement machine, register a Windows x64 runner in repository Settings → Actions → Runners,
use label `unity-webgl`, install the matching Unity editor/module and activate its license. Point
`UNITY_CLI_PATH` at `unity.exe` if it isn't on PATH or at `%LOCALAPPDATA%/Unity/bin/unity.exe`.
Cloud-hosted builds require a separately valid unattended license strategy; service-account tokens
alone do not activate Personal/entitlement editor licenses.

## Local builds

Run the same tests and build as CI from a project that isn't open in another Unity process:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CI/Invoke-WebGLCI.ps1
```

To build only after tests already passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CI/Invoke-WebGLCI.ps1 -SkipTests
```

The underlying build command is:

```powershell
unity build . --target WebGL --execute-method ClayWars.Build.WebGLBuilder.Build --output-path Build/WebGL --no-tail
```

To preserve an already-open editor, build a separate worktree rather than closing it or sharing its
`Library`. The build script temporarily sets Gzip, Decompression Fallback, 256 MB initial memory, and
hashed files, then restores the prior settings even after failure. Generated output is replaced only
under `Build/<directory>`; junction/symlink output is refused. Android settings aren't rewritten.
Serve the output over HTTP; opening `index.html` with `file://` is insufficient.

## Tests and known baseline issue

The clean `47afc4c` main snapshot already fails
`BalanceLabCrossCheckTests.CliAndEditor_ProduceIdenticalResults("archer_general_volley_cross")`:
the recorded .NET BalanceLab result differs from the Unity Mono simulation. The first full run for
this task had 375 cases: 374 passed, 1 failed. The release gate explicitly selects all current
OutGame tests, WebGL builder tests, and the other five Simulation test fixtures. It does not execute
the two BalanceLab cross-runtime comparison cases. They remain in the repository, and the known
failure is not represented as a passing test. Future simulation fixtures must be added to the
release gate or the drift issue fixed and full-suite gating restored.

PlayMode capture tests may be skipped in batch mode because it has no rendered frames. Browser
checks of the actual WebGL artifact complement the tests. See `VERIFICATION.md` for measured results.

The unchanged main PlayMode suite also has 33 existing failures from stale prefab paths and legacy
`UnityEngine.UI.Text` expectations after UI/TextMeshPro changes (82 pass, 11 skip). The exact test
names are listed in `KnownPlayModeFailures.txt`. All PlayMode tests still execute and their failed
results are retained; any failure outside that explicit baseline stops deployment. Missing test
results, execution errors, and suite/setup failures also stop deployment. Remove names from the
baseline when the corresponding tests are repaired. This baseline does not certify those features.
`KnownPlayModeSignatures.json` also checks the known error messages and source locations: changed
failure reasons/locations stop the build. `Test-CIGuards.ps1` verifies rejection using synthetic reports.
Unity CLI beta.9 empirically returns code 2 for failed tests; newer versions document code 8. Both
require fresh, complete NUnit results before this baseline can be accepted.

## Troubleshooting

- **Queued**: confirm the workstation is online and the runner reports Listening for Jobs.
- **License error**: confirm the same Windows account can run the editor and its license is active.
- **Missing module**: `unity editors -i --format json` must list Web for `6000.5.3f1`.
- **Test/build error**: download `webgl-logs-<run-id>` from the failed Actions run.
- **Browser load error**: inspect HTTP failures and JavaScript/WebAssembly errors. Pages builds use
  `.unityweb` decompression fallback, so they don't require custom Content-Encoding headers.
- **Site not updated**: compare `build-info.json` revision with the successful main workflow SHA.

Official references: [Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli),
[Unity Web deployment](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/deploying),
[GitHub Pages workflows](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages).
