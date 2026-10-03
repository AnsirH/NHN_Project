# ClayWarsFill verification — 2026-10-04

The project now uses `PROJECT:ClayWarsFill`, copied from Unity 6000.5.3f1's Default WebGL template. The canvas fills its containing window, the footer is removed, and the background is black. The loader macros, centered loading bar, and completion behavior are retained. Rendering follows CSS size with device pixel ratio capped at 2; clicking the canvas focuses it for keyboard input.

## Regression and build evidence

- The new builder test first failed with the Default template (RED), then all 13 builder tests passed. It checks both normal and exceptional template restoration.
- `Tools/CI/Invoke-WebGLCI.ps1` completed: 374 selected EditMode tests passed; 114 PlayMode tests passed, with 11 explicit visual-capture tools skipped. The existing two BalanceLab cross-runtime cases are outside the release gate, as documented in README.md.
- The first cold WebGL build hit the known Bee six-rescan limit; the existing bounded retry succeeded. Artifact validation passed (164.1 MB).
- The enhanced smoke rejected the old published Default template's 960×600 canvas and footer. It passed against the rebuilt artifact at 1280×900 and 980×551, including focus/DOM key delivery and zero runtime errors.

## Interactive acceptance

HTTP-served WebGL was exercised in Chromium with software WebGL, rather than testing a static template alone. Live resizing was checked at 1280×900, 980×551 (16:9), 1280×800 (16:10), and 900×600 (narrow landscape). Canvas origin was (0,0), CSS dimensions matched each viewport, the background was black, and no footer or document overflow was present.

The main menu, map selection, character selection, room graph, deployment, and battle were opened using canvas clicks. Deployment screenshots at 16:10 and narrow landscape retained both unit grids and action buttons; battle screenshots retained the arena, health display, pause/speed controls, and skill button inside the viewport. These observations cover representative screens, not every possible game state.

A real 980×551 iframe was tested with device pixel ratio 3. Its canvas filled the frame and rendered at 1960×1102, confirming the cap of 2. After clicking Start inside the iframe, two parent-page Escape key presses opened and closed the game's map-selection pause popup. Screenshots confirm actual Unity Input System behavior in addition to the smoke's DOM keyboard check. No JavaScript/runtime errors were recorded.

Local evidence is saved in ignored `Logs/`: `fill-template-red.xml`, `fill-template-green.xml`, `editmode.xml`, `playmode.xml`, `browser-default-red/results.json`, `browser-fill-1280x900/results.json`, `browser-fill-980x551/results.json`, and `fill-acceptance/` screenshots and measurements. Build output remains ignored under `Build/WebGL/`.

Only the WebGL template selection is changed persistently in ProjectSettings. Game code, scenes, rendering assets, compression, memory, and hash settings are not part of this change.
