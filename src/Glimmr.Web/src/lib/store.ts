import { signal, computed, batch } from "@preact/signals";
import {
	HubConnection,
	HubConnectionBuilder,
	HubConnectionState,
	LogLevel
} from "@microsoft/signalr";
import type {
	AuthState,
	DeviceData,
	LogLine,
	Scene,
	SceneV2,
	StatData,
	StoreData,
	SystemData,
	ToastMsg
} from "../types";

// ---------------------------------------------------------------------------
// State signals
// ---------------------------------------------------------------------------

export const system = signal<SystemData | null>(null);
export const devices = signal<DeviceData[]>([]);
export const ambientScenes = signal<Scene[]>([]);
export const audioScenes = signal<Scene[]>([]);
export const scenes = signal<SceneV2[]>([]);
export const usbDevices = signal<Record<string, string>>({});
export const audioDevices = signal<{ id: string }[]>([]);
export const stats = signal<StatData | null>(null);
export const connected = signal(false);
export const everConnected = signal(false);
/** True only after the connection has been down for a grace period - drives the
 *  offline banner so sub-second reconnects don't flash warnings at the user. */
export const offline = signal(false);
export const inputImage = signal<string | null>(null);
export const outputImage = signal<string | null>(null);
export const logLines = signal<LogLine[]>([]);
export const toasts = signal<ToastMsg[]>([]);
export const authState = signal<AuthState>({ active: false, progress: 0, authorized: false, error: false });
export const scanning = signal(false);

export const mode = computed(() => {
	const sd = system.value;
	if (!sd) return 0;
	return sd.autoDisabled ? 0 : sd.deviceMode;
});

export const sortedDevices = computed(() =>
	[...devices.value]
		.filter(d => !(d.tag === "DreamScreen" && (d.deviceTag ?? "").includes("DreamScreen")))
		// Sort by name, then IP (numeric-aware) so the order is stable and enable/disable
		// can't reshuffle same-named devices.
		.sort((a, b) => {
			const byName = (a.name ?? "").localeCompare(b.name ?? "");
			if (byName !== 0) return byName;
			return (a.ipAddress ?? "").localeCompare(b.ipAddress ?? "", undefined, { numeric: true });
		})
);

let toastId = 0;
let previewConsumers = 0;

export function showToast(title: string, body: string) {
	const t = { id: ++toastId, title, body };
	toasts.value = [...toasts.value, t];
	setTimeout(() => {
		toasts.value = toasts.value.filter(x => x.id !== t.id);
	}, 5000);
}

// ---------------------------------------------------------------------------
// SignalR connection
// ---------------------------------------------------------------------------

let hub: HubConnection | null = null;

function loadStore(parsed: StoreData) {
	if (!parsed) return;
	batch(() => {
		if (parsed.systemData) system.value = parsed.systemData;
		if (parsed.devices) devices.value = parsed.devices;
		if (parsed.devUsb) usbDevices.value = parsed.devUsb;
		if (parsed.devAudio) audioDevices.value = parsed.devAudio as { id: string }[];
		if (parsed.ambientScenes) ambientScenes.value = parsed.ambientScenes;
		if (parsed.audioScenes) audioScenes.value = parsed.audioScenes;
		if (parsed.scenes) scenes.value = parsed.scenes;
		if (parsed.stats) stats.value = parsed.stats;
	});
	clearPreviewIfLightsOff(system.value);
}

let offlineTimer: number | undefined;

function setConnected(up: boolean) {
	connected.value = up;
	window.clearTimeout(offlineTimer);
	if (up) {
		offline.value = false;
		return;
	}

	// Only surface the banner if we stay down for a bit; quick reconnects are silent.
	offlineTimer = window.setTimeout(() => {
		if (!connected.value) offline.value = true;
	}, 2500);
}

function clearPreviewIfLightsOff(sd: SystemData | null) {
	if (!sd) return;
	if (!sd.autoDisabled && sd.deviceMode !== 0) return;
	inputImage.value = null;
	outputImage.value = null;
}

export function connect() {
	if (hub) return;
	hub = new HubConnectionBuilder()
		.withUrl("/socket")
		// Retry immediately, then back off. Combined with the server's 60s client
		// timeout this keeps blips short and rare instead of a banner every time.
		.withAutomaticReconnect({
			nextRetryDelayInMilliseconds: ctx => Math.min(500 * 2 ** ctx.previousRetryCount, 8000)
		})
		.configureLogging(LogLevel.Warning)
		.build();

	hub.serverTimeoutInMilliseconds = 60000;
	hub.keepAliveIntervalInMilliseconds = 10000;

	hub.on("olo", (parsed: StoreData) => loadStore(parsed));

	hub.on("mode", (m: number) => {
		const sd = system.value;
		if (!sd) return;
		const next = { ...sd, deviceMode: m, autoDisabled: false };
		system.value = next;
		clearPreviewIfLightsOff(next);
	});

	hub.on("stats", (s: StatData) => {
		stats.value = s;
	});

	hub.on("frames", (fps: Record<string, number>) => {
		const s = stats.value;
		if (s) stats.value = { ...s, fps };
	});

	hub.on("device", (json: string) => {
		try {
			const dev = JSON.parse(json) as DeviceData;
			if (!dev?.id) return;
			const list = devices.value;
			const idx = list.findIndex(d => d.id === dev.id);
			devices.value = idx >= 0
				? list.map((d, i) => (i === idx ? dev : d))
				: [...list, dev];
		} catch {
			// ignore malformed device payloads
		}
	});

	hub.on("deleteDevice", (id: string) => {
		devices.value = devices.value.filter(d => d.id !== id);
	});

	hub.on("toast", (title: string, body: string) => showToast(title, body));

	hub.on("auth", (state: string, value: number) => {
		switch (state) {
			case "start":
				authState.value = { active: true, progress: 0, authorized: false, error: false };
				break;
			case "update":
				authState.value = { ...authState.value, active: value < 30, progress: value / 30 };
				break;
			case "authorized":
				authState.value = { active: false, progress: 1, authorized: true, error: false };
				showToast("Device Linked", "Device linked successfully.");
				break;
			case "error":
			case "stop":
				authState.value = { ...authState.value, active: false, error: state === "error" };
				break;
		}
	});

	hub.on("inputImage", (b64: string) => {
		inputImage.value = "data:image/png;base64," + b64;
	});

	hub.on("outputImage", (b64: string) => {
		outputImage.value = "data:image/png;base64," + b64;
	});

	hub.on("log", (msg: string) => {
		try {
			const m = JSON.parse(msg);
			const ts: string = (m.Timestamp ?? "").split("T")[1]?.split(".")[0] ?? "";
			const line: LogLine = {
				level: m.Level ?? 2,
				ts,
				caller: m.Properties?.Caller?.Value ?? "",
				text: m.MessageTemplate?.Text ?? ""
			};
			const next = [line, ...logLines.value];
			if (next.length > 500) next.length = 500;
			logLines.value = next;
		} catch {
			// ignore unparseable log lines
		}
	});

	hub.onreconnecting(() => {
		setConnected(false);
	});

	hub.onreconnected(() => {
		setConnected(true);
		void hub?.invoke("Store");
	});

	hub.onclose(() => {
		setConnected(false);
		setTimeout(tryStart, 4000);
	});

	tryStart();
}

function tryStart() {
	if (!hub || hub.state !== HubConnectionState.Disconnected) return;
	hub.start()
		.then(() => {
			setConnected(true);
			everConnected.value = true;
		})
		.catch(() => {
			setConnected(false);
			setTimeout(tryStart, 4000);
		});
}

async function invoke(method: string, ...args: unknown[]) {
	if (hub && hub.state === HubConnectionState.Connected) {
		try {
			await hub.invoke(method, ...args);
			return;
		} catch (e) {
			console.error("Hub invoke failed", method, e);
		}
	}
	// REST fallback mirrors the legacy UI behavior.
	try {
		await fetch(`/api/Glimmr/${method}`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(args.length === 1 ? args[0] : args)
		});
	} catch (e) {
		console.error("REST fallback failed", method, e);
	}
}

// ---------------------------------------------------------------------------
// Actions
// ---------------------------------------------------------------------------

export function setMode(m: number) {
	const sd = system.value;
	if (sd) {
		const next = { ...sd, deviceMode: m, autoDisabled: false };
		system.value = next;
		clearPreviewIfLightsOff(next);
	}
	void invoke("Mode", m);
}

/** Update one or more SystemData properties optimistically and push the full object. */
export function setSystemProps(patch: Partial<SystemData>) {
	const sd = system.value;
	if (!sd) return;
	const next = { ...sd, ...patch };
	system.value = next;
	clearPreviewIfLightsOff(next);
	void invoke("SystemData", JSON.stringify(next));
}

export function updateDevice(dev: DeviceData) {
	const list = devices.value;
	const idx = list.findIndex(d => d.id === dev.id);
	devices.value = idx >= 0 ? list.map((d, i) => (i === idx ? dev : d)) : [...list, dev];
	void invoke("UpdateDevice", JSON.stringify(dev));
}

export function setDeviceProp(id: string, prop: string, value: unknown) {
	const dev = devices.value.find(d => d.id === id);
	if (!dev) return;
	updateDevice({ ...dev, [prop]: value });
}

export function deleteDevice(id: string) {
	devices.value = devices.value.filter(d => d.id !== id);
	void invoke("DeleteDevice", id);
}

export function scanDevices() {
	if (scanning.value) return;
	scanning.value = true;
	void invoke("ScanDevices");
	const timeout = (system.value?.discoveryTimeout ?? 10) * 1000;
	setTimeout(() => {
		scanning.value = false;
	}, timeout);
}

export function authorizeDevice(id: string) {
	authState.value = { active: true, progress: 0, authorized: false, error: false };
	void invoke("AuthorizeDevice", id);
}

export function flashDevice(id: string) {
	void invoke("FlashDevice", id);
}

export function flashSector(sector: number) {
	void invoke("FlashSector", sector);
}

export function flashLed(led: number) {
	void invoke("FlashLed", led);
}

export function systemControl(action: "shutdown" | "reboot" | "restart" | "update") {
	void invoke("SystemControl", action);
}

export function setPreviewsActive(active: boolean) {
	if (active) {
		previewConsumers++;
		if (previewConsumers === 1) {
			void invoke("SettingsShown", true);
		}
		return;
	}

	previewConsumers = Math.max(0, previewConsumers - 1);
	if (previewConsumers === 0) {
		void invoke("SettingsShown", false);
	}
}
