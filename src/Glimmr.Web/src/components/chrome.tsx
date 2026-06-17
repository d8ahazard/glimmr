import type { JSX } from "preact";
import { toasts, stats, offline, everConnected, system } from "../lib/store";
import { IconControl, IconDevices, IconCapture, IconSettings, IconScenes } from "./icons";

export type Tab = "control" | "scenes" | "devices" | "capture" | "settings";

const TABS: { id: Tab; label: string; icon: (p: { size?: number }) => JSX.Element }[] = [
	{ id: "control", label: "Control", icon: IconControl },
	{ id: "scenes", label: "Scenes", icon: IconScenes },
	{ id: "devices", label: "Devices", icon: IconDevices },
	{ id: "capture", label: "Capture", icon: IconCapture },
	{ id: "settings", label: "Settings", icon: IconSettings }
];

export function TabBar({ tab, onChange }: { tab: Tab; onChange: (t: Tab) => void }) {
	return (
		<nav class="tabbar">
			{TABS.map(t => {
				const Icon = t.icon;
				return (
					<button
						key={t.id}
						class={tab === t.id ? "active" : ""}
						onClick={() => onChange(t.id)}
						aria-label={t.label}
					>
						<Icon size={24} />
						<span>{t.label}</span>
					</button>
				);
			})}
		</nav>
	);
}

export function StatStrip() {
	const s = stats.value;
	if (!s) return null;
	const sd = system.value;
	const unit = sd?.units === 1 ? "°C" : "°F";
	const fps = s.fps?.source ?? s.fps?.Source ?? 0;
	const throttled = (s.throttledState ?? []).some(
		t => t === "Currently throttled" || t === "Under-voltage detected"
	);
	return (
		<div class="statstrip">
			<span>{fps} FPS</span>
			<span class={throttled ? "danger" : ""}>{s.cpuTemp}{unit}</span>
			<span>CPU {s.cpuUsage}%</span>
			<span>MEM {s.memoryUsage}%</span>
		</div>
	);
}

export function Toaster() {
	const list = toasts.value;
	if (!list.length) return null;
	return (
		<div class="toaster">
			{list.map(t => (
				<div key={t.id} class="toast">
					<div class="toast-title">{t.title}</div>
					{t.body && <div class="toast-body">{t.body}</div>}
				</div>
			))}
		</div>
	);
}

export function OfflineBanner() {
	if (!offline.value || !everConnected.value) return null;
	return <div class="offline-banner">Connection lost — reconnecting…</div>;
}
