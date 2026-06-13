# Glimmr Web UI

Mobile-first SPA for Glimmr, built with [Preact](https://preactjs.com), [Vite](https://vite.dev) and TypeScript.
The build output is served as static files by the Glimmr ASP.NET host from `src/Glimmr/wwwroot/app`.

## Development

Run the Glimmr backend (listens on port 80), then:

```bash
npm install
npm run dev
```

The Vite dev server proxies `/api`, `/img` and `/socket` to `http://localhost:80`.
Point it at another Glimmr instance with `GLIMMR_HOST=http://192.168.1.50 npm run dev`.

## Building

```bash
npm run build
```

Type-checks and emits the production bundle into `../Glimmr/wwwroot/app`.
`dotnet publish` on the Glimmr project runs this automatically (skip with `-p:SkipGlimmrWeb=true`).

## Architecture

- `src/lib/store.ts` — typed SignalR store (`/socket` hub). Hydrates from the `olo`
  snapshot, applies live `device`/`stats`/`mode` events, exposes actions with a
  REST fallback when the socket is down.
- `src/lib/accent.ts` — drives the live UI accent color from the current mode /
  ambient color (`--accent` CSS variable).
- `src/components/maps.tsx` — SVG LED / sector maps over the master grid
  (`src/lib/maps.ts` holds the geometry + wrap-around range math).
- `src/components/device/DeviceSheet.tsx` — schema-driven device settings
  rendered from each device's `keyProperties`, plus device-specific editors
  (Hue light mapping, Nanoleaf panel layout, LIFX beam ordering, WLED segments).
- `src/screens/` — Control, Devices, Capture, Settings and the first-run
  setup wizard (gated on `SystemData.skipTour`).
