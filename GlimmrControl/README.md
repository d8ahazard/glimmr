# Glimmr app

A cross-platform companion app for [Glimmr](https://github.com/d8ahazard/Glimmr) ambient-lighting
servers, built with **.NET MAUI** (Android, iOS, Windows).

This is a ground-up rewrite of the old Xamarin.Forms app. It does real device onboarding and native
control instead of just embedding the device's web page.

## Features

- **Wi-Fi onboarding** – detect a brand-new Glimmr broadcasting its `glimmr-…` setup hotspot, join it,
  hand it your home Wi-Fi credentials (via the on-device comitup portal at `http://10.41.0.1`), then
  rediscover it once it's on your network.
- **Discovery** – finds Glimmr servers on the LAN via mDNS/Bonjour (`_glimmr._tcp`), or add one by IP.
- **Native control** – power/mode (Off, Video, Audio, A/V, Ambient, Stream, Hue Sync), ambient color +
  scenes, audio scene + response sliders, streaming source, Hue Sync pairing, and per-device
  brightness/enable. Updates stream live over the server's SignalR hub (`/socket`) with a REST
  fallback (`/api/Glimmr`).
- **Advanced settings** – the device's full web UI embedded in a WebView for everything else.

## Architecture

```
Views/ (XAML)  ─bind─>  ViewModels/ (CommunityToolkit.Mvvm)  ─>  Services/
                                                                  ├─ DiscoveryService  (Zeroconf mDNS)
                                                                  ├─ DeviceStore       (Preferences)
                                                                  ├─ GlimmrConnection  (SignalR + REST)
                                                                  ├─ GlimmrApiClient   (/api/Glimmr)
                                                                  ├─ OnboardingService ┐
                                                                  ├─ ComitupClient     ┤ Wi-Fi setup
                                                                  └─ IWifiService      ┘ (per-platform)
```

`Models/` mirror the server's camelCase JSON. `SystemData`/`DeviceData` keep a `[JsonExtensionData]`
bag so server-only fields round-trip untouched when the app pushes updates.

### Platform Wi-Fi notes

- **Android** scans for `glimmr-*` networks (`WifiManager`) and joins the open setup AP with a
  `WifiNetworkSpecifier`, binding the process to it so the comitup portal is reachable. Needs the
  location / `NEARBY_WIFI_DEVICES` permission (requested at runtime). Pre-Android 10 devices join the
  AP from system Wi-Fi settings and use the "I'm already connected" path.
- **iOS** cannot enumerate nearby SSIDs (Apple restriction), so onboarding asks for the `glimmr-…`
  network name and joins it with `NEHotspotConfiguration`. Requires the **Hotspot Configuration** and
  **Access Wi-Fi Information** entitlements (`Platforms/iOS/Entitlements.plist`).
- **Windows** uses the WinRT `WiFiAdapter`. Unpackaged desktop builds may have Wi-Fi control denied by
  the OS; the "Open setup page in browser" fallback always works.

## Build

Requires the .NET 9 SDK with the `maui` workloads (`android`, `ios`, `maui-windows`).

```powershell
# Windows
dotnet build src/GlimmrControl/GlimmrControl.csproj -f net9.0-windows10.0.19041.0

# Android
dotnet build src/GlimmrControl/GlimmrControl.csproj -f net9.0-android
```

**iOS** must be built/signed from (or paired to) a Mac with Xcode; the managed code lives in
`Platforms/iOS`. Configure your Apple provisioning profile with the two networking entitlements above.

## Icon

The Glimmr brand mark (the original `appIcon_512.png`) is reproduced as scalable vector art in
`Resources/AppIcon/` (mandala + signal glyph in the Glimmr cyan→violet→magenta palette on a near-black
backdrop), matching the web UI's dark theme.
