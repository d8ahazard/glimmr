import { useEffect, useState } from "preact/hooks";
import {
	system, sortedDevices, mode, setMode, setSystemProps, updateDevice,
	audioScenes, scenes, showToast, scanDevices, scanning, outputImage, setPreviewsActive
} from "../lib/store";
import { MODES, type DeviceData } from "../types";
import { IconChevron, IconRefresh, MODE_ICONS } from "../components/icons";
import { Toggle, Slider, SelectField } from "../components/ui";
import { ColorWheel } from "../components/ColorWheel";
import { DeviceSheet, deviceIcon } from "../components/device/DeviceSheet";
import { getHueSyncStatus, armHueSyncLink, unpairHueSync, selectScene } from "../lib/api";
import type { HueSyncStatus } from "../types";
import { PreviewImage } from "../components/PreviewImage";

function AudioPanel() {
	const sd = system.value;
	if (!sd) return null;
	const scenes = [...audioScenes.value].sort((a, b) => a.id - b.id);
	return (
		<>
			<div class="section-label">Audio scene</div>
			<div class="scene-chips">
				{scenes.map(s => (
					<button
						key={s.id}
						class={"scene-chip" + (sd.audioScene === s.id ? " active" : "")}
						onClick={() => setSystemProps({ audioScene: s.id })}
					>
						{s.name}
					</button>
				))}
			</div>
			<div class="section-label">Response</div>
			<div class="field">
				<label class="field-label">Gain</label>
				<Slider
					value={sd.audioGain ?? 0}
					min={0} max={2} step={0.01}
					onCommit={v => setSystemProps({ audioGain: v })}
				/>
			</div>
			<div class="field">
				<label class="field-label">Rotation speed</label>
				<Slider
					value={sd.audioRotationSpeed ?? 0}
					min={0.01} max={1} step={0.01}
					onCommit={v => setSystemProps({ audioRotationSpeed: v })}
				/>
			</div>
			<div class="field">
				<label class="field-label">Rotation sensitivity</label>
				<Slider
					value={sd.audioRotationTrigger ?? 0}
					min={0} max={255} step={1}
					onCommit={v => setSystemProps({ audioRotationTrigger: v })}
				/>
			</div>
			<div class="grid-2">
				<div class="field">
					<label class="field-label">Rotation lower</label>
					<Slider
						value={sd.audioRotationLower ?? 0}
						min={0} max={1} step={0.01}
						onCommit={v => setSystemProps({ audioRotationLower: v })}
					/>
				</div>
				<div class="field">
					<label class="field-label">Rotation upper</label>
					<Slider
						value={sd.audioRotationUpper ?? 1}
						min={0} max={1} step={0.01}
						onCommit={v => setSystemProps({ audioRotationUpper: v })}
					/>
				</div>
			</div>
		</>
	);
}

function pickScene(id: string) {
	const cur = system.value;
	if (cur) system.value = { ...cur, deviceMode: 8, sceneId: id, autoDisabled: false };
	void selectScene(id);
}

function ScenePanel() {
	const sd = system.value;
	if (!sd) return null;
	// "solid" is the static-color scene, driven by the color wheel below, not a chip.
	const list = [...scenes.value].filter(s => s.id !== "solid").sort((a, b) => a.name.localeCompare(b.name));
	return (
		<>
			<div class="section-label">Static color</div>
			<ColorWheel
				color={sd.ambientColor}
				onCommit={hex => {
					setSystemProps({ ambientColor: hex });
					if (sd.sceneId !== "solid" || sd.deviceMode !== 8) pickScene("solid");
				}}
			/>
			<div class="section-label">Scenes</div>
			{list.length === 0 && (
				<p class="field-hint" style={{ margin: "0 8px" }}>
					No scenes yet — create one in the Scenes tab.
				</p>
			)}
			<div class="scene-chips">
				{list.map(s => (
					<button
						key={s.id}
						class={"scene-chip" + (sd.sceneId === s.id ? " active" : "")}
						onClick={() => pickScene(s.id)}
					>
						{s.name}
					</button>
				))}
			</div>
		</>
	);
}

function StreamPanel() {
	const sd = system.value;
	if (!sd) return null;
	const dsDevices = sortedDevices.value.filter(d => d.tag === "DreamScreen");
	return (
		<>
			<div class="section-label">Streaming source</div>
			<SelectField
				label="Streaming Mode"
				value={sd.streamMode}
				options={[
					{ value: 0, label: "DreamScreen" },
					{ value: 1, label: "Glimmr" },
					{ value: 2, label: "FloW LED / WLED" }
				]}
				onChange={v => setSystemProps({ streamMode: parseInt(v) })}
			/>
			{sd.streamMode === 0 && (
				<SelectField
					label="Target DreamScreen"
					value={sd.dsIp ?? ""}
					allowEmpty
					options={dsDevices.map(d => ({ value: d.id, label: `${d.name} — ${d.id}` }))}
					onChange={v => setSystemProps({ dsIp: v })}
				/>
			)}
		</>
	);
}

function subtitle(dev: DeviceData): string {
	const parts: string[] = [];
	if (dev.ipAddress) parts.push(dev.ipAddress);
	const count = dev.multiZoneCount ?? dev.ledCount;
	if (count != null && dev.deviceTag !== "Lifx Bulb") parts.push(`${count} LEDs`);
	return parts.join(" · ");
}

function displayName(dev: DeviceData, duplicate: boolean): string {
	const name = dev.name ?? "";
	return duplicate && dev.ipAddress ? `${name} (${dev.ipAddress})` : name;
}

export function HueSyncPanel({ enablePolling = true }: { enablePolling?: boolean } = {}) {
	const [status, setStatus] = useState<HueSyncStatus | null>(null);
	const [pairing, setPairing] = useState(false);

	const refresh = () => {
		void getHueSyncStatus().then(setStatus);
	};

	useEffect(() => {
		refresh();
		if (!enablePolling) return;
		const t = setInterval(refresh, 5000);
		return () => clearInterval(t);
	}, [enablePolling]);

	const pair = async () => {
		const ok = await armHueSyncLink();
		if (ok) {
			setPairing(true);
			showToast("Hue Sync", "Link armed — connect from the Hue Sync app within 60 seconds.");
			setTimeout(() => setPairing(false), 60000);
		}
		refresh();
	};

	const state = !status
		? "Checking status…"
		: status.streaming
			? "Streaming"
			: status.paired
				? "Paired"
				: status.armed
					? "Waiting for app (armed)…"
					: "Not paired";

	return (
		<>
			<div class="section-label">Hue Sync bridge</div>
			<div class="rows">
				<div class="row-item">
					<div class={status?.paired || status?.streaming ? "glow-dot" : ""} />
					<div class="row-body">
						<div class="row-title">{state}</div>
						<div class="row-sub">{status ? `Bridge ID: ${status.bridgeId}` : ""}</div>
					</div>
				</div>
			</div>
			<div style={{ display: "flex", gap: "10px", margin: "10px 4px" }}>
				<button class="btn primary" style={{ flex: 1 }} disabled={pairing} onClick={pair}>
					{pairing ? "Waiting for app…" : "Pair Hue Sync App"}
				</button>
				<button
					class="btn"
					onClick={async () => {
						await unpairHueSync();
						showToast("Hue Sync", "Pairing forgotten.");
						refresh();
					}}
				>
					Forget
				</button>
			</div>
			<p class="field-hint" style={{ margin: "4px 8px" }}>
				Tap "Pair Hue Sync App", then search for a bridge in the Philips Hue Sync app and connect
				within 60 seconds. Glimmr appears as an emulated Hue Bridge — once paired, start syncing
				and Glimmr drives every enabled device in the room.
			</p>
		</>
	);
}

function DeviceQuickRow({
	dev,
	duplicateName,
	onOpen
}: {
	dev: DeviceData;
	duplicateName: boolean;
	onOpen: (id: string) => void;
}) {
	return (
		<div class="row-item tappable" onClick={() => onOpen(dev.id)}>
			<div class="row-icon"><img src={deviceIcon(dev)} alt="" loading="lazy" /></div>
			<div class="row-body">
				<div class="row-title">{displayName(dev, duplicateName)}</div>
				<div class="row-sub">{subtitle(dev)}</div>
				<div style={{ marginTop: "6px", paddingRight: "6px" }} onClick={e => e.stopPropagation()}>
					<Slider
						value={dev.brightness ?? 100}
						max={dev.maxBrightness ?? 100}
						onCommit={v => updateDevice({ ...dev, brightness: Math.round(v) })}
						label={`${dev.name} brightness`}
					/>
				</div>
			</div>
			<div class="row-trail">
				<Toggle
					on={dev.enable}
					onChange={v => updateDevice({ ...dev, enable: v })}
					label={`Enable ${dev.name}`}
				/>
				<span class="muted" style={{ display: "inline-flex" }}><IconChevron size={16} /></span>
			</div>
		</div>
	);
}

export function ControlScreen({ onOpenPreviewStudio }: { onOpenPreviewStudio?: () => void } = {}) {
	const sd = system.value;
	const m = mode.value;
	const [openDevice, setOpenDevice] = useState<string | null>(null);
	const devs = sortedDevices.value;
	const modeName = m === 3 ? "Scenes" : MODES.find(x => x.id === m)?.name ?? "Off";
	const busy = scanning.value;
	const groups = new Map<string, DeviceData[]>();
	for (const d of devs) {
		const key = d.deviceTag && d.tag === "Lifx" ? d.deviceTag : d.tag;
		const list = groups.get(key) ?? [];
		list.push(d);
		groups.set(key, list);
	}
	const nameCounts = new Map<string, number>();
	for (const d of devs) {
		const name = d.name ?? "";
		nameCounts.set(name, (nameCounts.get(name) ?? 0) + 1);
	}
	const previewEnabled = sd?.enablePreviewStudio ?? true;

	useEffect(() => {
		if (!previewEnabled) return;
		setPreviewsActive(true);
		return () => setPreviewsActive(false);
	}, [previewEnabled]);

	return (
		<div class="screen">
			<div class="hero">
				<div class="hero-glow" />
				<div class="hero-name">{sd?.deviceName ?? "Glimmr"}</div>
				<div class="hero-mode">{m === 0 ? "Lights off" : modeName}</div>
			</div>

			<div class="chips" role="tablist" aria-label="Lighting mode">
				{MODES.map(md => {
					const Icon = MODE_ICONS[md.icon];
					return (
						<button
							key={md.id}
							class={"chip" + (m === md.id ? " active" : "")}
							onClick={() => setMode(md.id)}
						>
							<Icon size={22} />
							<span>{md.name}</span>
						</button>
					);
				})}
			</div>

			{(m === 8 || m === 3) && <ScenePanel />}
			{(m === 2 || m === 4) && <AudioPanel />}
			{m === 5 && <StreamPanel />}
			{m === 7 && <HueSyncPanel />}
			{(sd?.enablePreviewStudio ?? true) && (
				<>
					<div class="section-label split">
						<span>Live preview</span>
						<button class="btn small" onClick={() => onOpenPreviewStudio?.()}>Full preview</button>
					</div>
					<button class="preview-card-btn" onClick={() => onOpenPreviewStudio?.()}>
						<PreviewImage signal={outputImage} alt="Live output preview" />
						<span>Tap to open Preview Studio</span>
					</button>
				</>
			)}

			<div class="section-label split">
				<span>Devices</span>
				<button class="btn small" disabled={busy} onClick={scanDevices}>
					<span class={busy ? "spin" : ""} style={{ display: "inline-flex" }}>
						<IconRefresh size={15} />
					</span>
					{busy ? "Scanning…" : "Scan"}
				</button>
			</div>
			{devs.length === 0 && (
				<div class="rows">
					<div class="row-item">
						<div class="row-body muted">
							{busy ? "Looking for devices…" : "No devices yet. Tap Scan to discover devices."}
						</div>
					</div>
				</div>
			)}
			{[...groups.entries()].map(([tag, list]) => (
				<div key={tag}>
					<div class="section-label">{tag}</div>
					<div class="rows">
						{list.map(d => (
							<DeviceQuickRow
								key={d.id}
								dev={d}
								duplicateName={(nameCounts.get(d.name ?? "") ?? 0) > 1}
								onOpen={setOpenDevice}
							/>
						))}
					</div>
				</div>
			))}

			{openDevice && <DeviceSheet deviceId={openDevice} onClose={() => setOpenDevice(null)} />}
		</div>
	);
}
