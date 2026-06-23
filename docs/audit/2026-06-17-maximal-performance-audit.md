# Maximal Performance-First Audit - 2026-06-17

## Scope completed
- `src/Glimmr` backend runtime and lifecycle hotspots
- `src/Glimmr.Web` state/render preview hotspots
- `rpi-ws281x-csharp` thread-safety and API guardrails
- deployment/update scripts and Linux installer/update reliability
- Home Assistant integration lifecycle correctness

## Implemented changes

### Phase 0 baseline/instrumentation
- Added runtime perf data model in `src/Glimmr/Models/Data/PerformanceData.cs`.
- Added `StatData.Perf` in `src/Glimmr/Models/Data/StatData.cs`.
- Added pipeline counters and per-target dispatch counters in `src/Glimmr/Services/ColorService.cs`:
  - frame processed/dropped
  - average/max update/compute/dispatch/preview ms
  - preview frames/bytes/encode+send latency
  - per-target calls/skips/timeouts/errors/in-flight/latency
- Wired perf snapshots into `src/Glimmr/Services/StatService.cs`.
- Added API endpoints:
  - `GET /api/Glimmr/stats/perf`
  - `POST /api/Glimmr/stats/perf/reset`
  in `src/Glimmr/Controllers/GlimmrController.cs`.
- Added benchmark harness script: `script/perf-smoke.ps1`.

### Phase 1 backend runtime hardening
- Replaced unbounded event fan-out wait with bounded per-target dispatch in `src/Glimmr/Services/ColorService.cs`:
  - timeout-controlled handler wait (`TargetSendTimeoutMs`)
  - skip frame when handler already in-flight (no overlap explosion)
  - per-target timeout/error accounting
- Added `SystemData.TargetSendTimeoutMs` in `src/Glimmr/Models/Data/SystemData.cs` with defaults.
- Instrumented splitter hot path in `src/Glimmr/Models/Frame/FrameSplitter.cs`:
  - drop counter when frame arrives while busy
  - stage timing for compute/send/preview.
- Removed blocking sleeps in hot async code:
  - `src/Glimmr/Services/ColorService.cs` flash routine (`Task.Delay` instead of `Thread.Sleep`)
  - `src/Glimmr/Controllers/GlimmrController.cs` scan wait (`Task.Delay`).
- Fixed lifecycle disposal issues:
  - `ColorService.StopAsync` now blocks until `StopServices` completes before shutdown.
  - `ControlService.StopAsync` now disposes actual agent values and awaits base stop.
- Removed sync-over-async fallback in store serialization:
  - `src/Glimmr/Models/Util/DataUtil.cs` now avoids `StatUtil.GetStats().Result`.

### Phase 2 frontend render/state improvements
- Added signal-scoped image component `src/Glimmr.Web/src/components/PreviewImage.tsx` to prevent top-level screen subscriptions to preview values.
- Reduced unnecessary preview streaming:
  - `Control.tsx` activates preview only when preview studio feature is enabled.
  - `Scenes.tsx` activates preview only when a scene is active in scene mode.
  - `Settings.tsx` activates preview only while Capture & Mapping section is expanded.
- Removed render-time `outputImage.value` dependency from scene tile screenshot button (read at click time only).
- Added UI control for backend dispatch timeout in settings:
  - `targetSendTimeoutMs` in `src/Glimmr.Web/src/types.ts` and `src/Glimmr.Web/src/screens/Settings.tsx`.
- Improved list rendering cost in `Control.tsx`:
  - precomputed name collision map
  - removed per-row global signal lookups.
- Preview Studio tuning:
  - debounced emitter position writes (`PreviewStudio.tsx`).
- Room 3D scene refactor:
  - `RoomScene3D.tsx` now initializes scene once, updates meshes incrementally, updates selected highlight without full rebuild, and pauses rendering when page hidden.

### Phase 3 WS281x and LED path hardening
- Added defensive bounds and lower-allocation reset logic in `rpi_ws281x/Controller.cs`.
- Added synchronization around render/brightness/reset/dispose in `rpi_ws281x/WS281x.cs`.
- Added disposed guard to prevent render/write after disposal.
- Verified wrapper project builds across target frameworks.
- Kept Glimmr package pin at `1.3.1` because `1.3.2` is not available on NuGet yet.

### Phase 4 deployment/image/installer reliability
- Updated `script/publish.ps1`:
  - fixed `$js` variable bug (`$javascript`).
  - aligned base file set with current app layout (`wwwroot/app/*`, scenes).
  - fixed web/css/js copy paths to Vite asset layout.
  - fixed restart condition for web-only pushes.
- Updated `script/setup_linux.sh` and `src/Glimmr/script/update_linux.sh`:
  - explicit unsupported 32-bit ARM path with clear error.
  - replaced permissive `chmod -R 777` with safer ownership and file/dir mode normalization.

### Phase 5 integration cleanup
- Home Assistant lifecycle fix:
  - `glimmr_ha/custom_components/glimmr/light.py` now awaits socket stop on entity removal.
- Removed duplicate update call in config flow:
  - `glimmr_ha/custom_components/glimmr/config_flow.py`.
- Relaxed pinned client requirement:
  - `glimmr_ha/custom_components/glimmr/manifest.json` uses `glimmr>=1.2.0`.

## Verification results
- Frontend build: `npm run build` in `src/Glimmr.Web` - passed.
- Backend tests: `dotnet test src/Glimmr.Tests` - passed (`61/61`).
- WS281x wrapper build: `dotnet build src/rpi_ws281x/rpi_ws281x.csproj` - passed.

## Residual risks and follow-ups
- `ColorSendEventAsync` still executes all handlers in-frame; timeouts prevent worst stalls, but a dedicated queued dispatcher (`OutputSchedulerService` integration) is still recommended.
- Several older discovery/network paths still use sync/blocking patterns (`.Result`) outside the main frame hot path; these should be migrated to async in a dedicated cleanup pass.
- ABI conformance for ws281x structs is still not formally asserted by tests. Add a struct-size validation test against the exact native header build.
- Pi 5 manual install still requires external kernel module build path for `rp1_ws281x_pwm`; image path remains preferred.
- Current warning set in backend build remains (pre-existing and non-blocking for this pass).
