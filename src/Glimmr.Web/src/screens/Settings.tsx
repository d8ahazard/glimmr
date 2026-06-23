import type { ComponentChildren } from "preact";
import { useEffect, useState } from "preact/hooks";
import {
	system, stats, setSystemProps, systemControl, logLines, showToast,
	usbDevices, audioDevices, inputImage, outputImage, setPreviewsActive
} from "../lib/store";
import {
	SelectField, TextField, ToggleRow, Sheet, confirmAction, CountCell
} from "../components/ui";
import { downloadDatabase, downloadLogs, uploadFile } from "../lib/api";
import { TIMEZONES } from "../lib/timezones";
import { HueSyncPanel } from "./Control";
import { LedMap, SectorMap } from "../components/maps";
import { PreviewImage } from "../components/PreviewImage";

const UPDATE_HOURS = Array.from({ length: 24 }, (_, h) => ({
	value: h,
	label: `${h % 12 === 0 ? 12 : h % 12} ${h < 12 ? "AM" : "PM"}`
}));

function LogViewerSheet({ onClose }: { onClose: () => void }) {
	const [levels, setLevels] = useState<Set<number>>(new Set([1, 2, 3, 4]));
	const lines = logLines.value.filter(l => levels.has(l.level));
	const names: Record<number, string> = { 1: "DBG", 2: "INF", 3: "WRN", 4: "ERR" };

	const toggle = (lvl: number) => {
		const next = new Set(levels);
		if (next.has(lvl)) next.delete(lvl);
		else next.add(lvl);
		setLevels(next);
	};

	return (
		<Sheet
			title="Live log"
			onClose={onClose}
			head={
				<div style={{ display: "flex", gap: "6px" }}>
					{[1, 2, 3, 4].map(l => (
						<button
							key={l}
							class={"scene-chip" + (levels.has(l) ? " active" : "")}
							style={{ padding: "5px 10px", fontSize: "11px" }}
							onClick={() => toggle(l)}
						>
							{names[l]}
						</button>
					))}
				</div>
			}
		>
			<div class="log-pane">
				{lines.length === 0 && <div class="muted">Waiting for log output…</div>}
				{lines.map((l, i) => (
					<div key={i} class={`log-${l.level}`}>
						[{l.ts} {names[l.level]}]{l.caller} {l.text}
					</div>
				))}
			</div>
		</Sheet>
	);
}

function FileUploadRow({
	label, hint, endpoint, accept
}: { label: string; hint: string; endpoint: "database" | "importAmbientScene" | "importAudioScene"; accept?: string }) {
	const [busy, setBusy] = useState(false);
	return (
		<div class="row-item">
			<div class="row-body">
				<div class="row-title">{label}</div>
				<div class="row-sub" style={{ whiteSpace: "normal" }}>{hint}</div>
			</div>
			<div class="row-trail">
				<label class="btn small" style={{ position: "relative", overflow: "hidden" }}>
					{busy ? "Uploading…" : "Choose file"}
					<input
						type="file"
						accept={accept}
						style={{ position: "absolute", inset: 0, opacity: 0, cursor: "pointer" }}
						disabled={busy}
						onChange={async e => {
							const file = (e.target as HTMLInputElement).files?.[0];
							if (!file) return;
							setBusy(true);
							const ok = await uploadFile(endpoint, file);
							setBusy(false);
							showToast(label, ok ? "Upload complete." : "Upload failed.");
							(e.target as HTMLInputElement).value = "";
						}}
					/>
				</label>
			</div>
		</div>
	);
}

function SectionGroup({
	title, defaultOpen = true, onToggle, children
}: {
	title: string;
	defaultOpen?: boolean;
	onToggle?: (open: boolean) => void;
	children: ComponentChildren;
}) {
	return (
		<details
			class="section-group"
			open={defaultOpen}
			onToggle={ev => onToggle?.((ev.currentTarget as HTMLDetailsElement).open)}
		>
			<summary>{title}</summary>
			<div class="section-group-body">{children}</div>
		</details>
	);
}

const CONTROL_WARNINGS: Record<string, string> = {
	shutdown: "This will shut down the device. You will need to manually turn it back on.",
	reboot: "This will restart the device.",
	restart: "This will restart the Glimmr TV service.",
	update: "This will update and restart Glimmr TV."
};

export function SettingsScreen({ onRunWizard }: { onRunWizard: () => void }) {
	const sd = system.value;
	const st = stats.value;
	const [showLog, setShowLog] = useState(false);
	const [captureSectionOpen, setCaptureSectionOpen] = useState(true);
	// Capture/preview settings now live in this screen. Only stream previews while that
	// section is expanded to avoid unnecessary encode/send overhead.
	useEffect(() => {
		if (!captureSectionOpen) return;
		setPreviewsActive(true);
		return () => setPreviewsActive(false);
	}, [captureSectionOpen]);
	if (!sd) return <div class="screen" />;
	const usb = usbDevices.value;
	const showUsb = sd.captureMode === 2 || (sd.captureMode === 1 && sd.camType === 1);

	const control = (action: "shutdown" | "reboot" | "restart" | "update") => {
		if (confirmAction(`Warning: ${CONTROL_WARNINGS[action]} Continue?`)) {
			systemControl(action);
		}
	};

	return (
		<div class="screen">
			<div class="screen-title">Settings</div>
			<p class="screen-sub">System, automation and maintenance.</p>

			<div class="section-label">About</div>
			<div class="rows">
				<div class="kv"><b>IP Address</b><span>{sd.ipAddress}</span></div>
				<div class="kv"><b>Version</b><span>{sd.version}</span></div>
				<div class="kv"><b>Uptime</b><span>{st?.uptime ?? "—"}</span></div>
			</div>

			<SectionGroup title="Capture & Mapping" onToggle={setCaptureSectionOpen}>
				<p class="screen-sub" style={{ marginBottom: "8px" }}>
					What Glimmr watches and how it maps your room lighting grid.
				</p>
				<div class="section-label">Preview</div>
				<div class="preview-pair">
					<figure>
						<PreviewImage signal={inputImage} alt="Input preview" />
						<figcaption>Input</figcaption>
					</figure>
					<figure>
						<PreviewImage signal={outputImage} alt="Output preview" />
						<figcaption>Output</figcaption>
					</figure>
				</div>
				<SelectField
					label="Preview Overlay"
					value={[0, 1, 2].includes(sd.previewMode) ? sd.previewMode : 0}
					options={[
						{ value: 0, label: "None" },
						{ value: 1, label: "LEDs" },
						{ value: 2, label: "Sectors" }
					]}
					onChange={v => setSystemProps({ previewMode: parseInt(v) })}
				/>
				<TextField
					label="Preview FPS"
					hint="Lower = lighter CPU, higher = smoother live preview."
					type="number"
					min={1}
					max={10}
					step={1}
					value={sd.previewFrameRate ?? 3}
					onCommit={v => setSystemProps({ previewFrameRate: Math.max(1, Math.min(10, parseInt(v) || 3)) })}
				/>
				<TextField
					label="Target send timeout (ms)"
					hint="Maximum wait for one device send before moving on to keep frame output responsive."
					type="number"
					min={20}
					max={500}
					step={5}
					value={sd.targetSendTimeoutMs ?? 120}
					onCommit={v =>
						setSystemProps({ targetSendTimeoutMs: Math.max(20, Math.min(500, parseInt(v) || 120)) })}
				/>

				<div class="section-label">Input source</div>
				<SelectField
					label="Input Source"
					value={sd.captureMode}
					options={[
						{ value: 1, label: "Camera" },
						{ value: 2, label: "HDMI Capture" },
						{ value: 3, label: "Screen" }
					]}
					onChange={v => setSystemProps({ captureMode: parseInt(v) })}
				/>
				{sd.captureMode === 1 && (
					<SelectField
						label="Camera Type"
						value={sd.camType}
						options={[
							{ value: 0, label: "Raspberry Pi Camera" },
							{ value: 1, label: "USB Web Camera" }
						]}
						onChange={v => setSystemProps({ camType: parseInt(v) })}
					/>
				)}
				{showUsb && (
					<SelectField
						label="USB Device"
						value={sd.usbSelection}
						allowEmpty
						options={Object.entries(usb).map(([k, v]) => ({ value: k, label: String(v) }))}
						onChange={v => setSystemProps({ usbSelection: parseInt(v) || 0 })}
					/>
				)}
				<div class="section-label">Input tuning</div>
				<div class="grid-2">
					<TextField
						label="Black Level"
						hint="Minimum brightness for colors to display. Default 7."
						type="number" min={0} max={255} step={1}
						value={sd.blackLevel}
						onCommit={v => setSystemProps({ blackLevel: parseInt(v) || 0 })}
					/>
					<TextField
						label="Gamma"
						hint="Gamma correction for wired LEDs (1.0–4.0)."
						type="number" min={1} max={4} step={0.1}
						value={sd.gammaCorrection}
						onCommit={v => setSystemProps({ gammaCorrection: parseFloat(v) || 1 })}
					/>
				</div>
				<SelectField
					label="Audio Input Device"
					value={sd.recDev ?? ""}
					allowEmpty
					options={audioDevices.value.map(d => ({ value: d.id, label: d.id }))}
					onChange={v => setSystemProps({ recDev: v })}
				/>

				<div class="section-label">Crop & detection</div>
				<div class="rows">
					<ToggleRow
						label="Letterbox cropping"
						hint="Crop black bars from the top and bottom."
						on={sd.enableLetterBox}
						onChange={v => setSystemProps({ enableLetterBox: v })}
					/>
					<ToggleRow
						label="Pillarbox cropping"
						hint="Crop black bars from the left and right."
						on={sd.enablePillarBox}
						onChange={v => setSystemProps({ enablePillarBox: v })}
					/>
				</div>
				<div class="grid-2 mt">
					<TextField
						label="Cropping Delay"
						hint="Seconds to wait before cropping."
						type="number" min={1} max={60} step={1}
						value={sd.cropDelay}
						onCommit={v => setSystemProps({ cropDelay: parseInt(v) || 1 })}
					/>
					<TextField
						label="Crop Black Level"
						hint="Non-black pixels allowed per line."
						type="number" min={8} max={40} step={1}
						value={sd.cropBlackLevel}
						onCommit={v => setSystemProps({ cropBlackLevel: parseInt(v) || 8 })}
					/>
				</div>

				<div class="section-label">LED grid</div>
				<p class="field-hint" style={{ margin: "0 8px 10px" }}>
					Total LEDs around the perimeter of your screen — the master grid every device maps onto.
				</p>
				<div class="count-grid">
					<CountCell label="Top" value={sd.topCount} onCommit={v => setSystemProps({ topCount: v })} />
					<CountCell label="Bottom" value={sd.bottomCount} onCommit={v => setSystemProps({ bottomCount: v })} />
					<CountCell label="Left" value={sd.leftCount} onCommit={v => setSystemProps({ leftCount: v })} />
					<CountCell label="Right" value={sd.rightCount} onCommit={v => setSystemProps({ rightCount: v })} />
				</div>
				<LedMap caption={`${sd.ledCount} LEDs total — tap one to flash it.`} />

				<div class="section-label">Sector grid</div>
				<p class="field-hint" style={{ margin: "0 8px 10px" }}>
					Sectors for single-color devices like bulbs and panels. Re-map devices after changing.
				</p>
				<div class="count-grid">
					<CountCell label="Horiz" value={sd.hSectors} onCommit={v => setSystemProps({ hSectors: v })} />
					<CountCell label="Vert" value={sd.vSectors} onCommit={v => setSystemProps({ vSectors: v })} />
				</div>
				<div class="rows">
					<ToggleRow
						label="Include center"
						hint="Sample the middle of the screen too, not just the edges."
						on={sd.useCenter}
						onChange={v => setSystemProps({ useCenter: v })}
					/>
				</div>
				<SectorMap caption={`${sd.sectorCount} sectors — tap one to flash it.`} />
			</SectionGroup>

			<SectionGroup title="Core configuration">
				<div class="section-label">General</div>
			<TextField
				label="Device Name"
				value={sd.deviceName}
				onCommit={v => setSystemProps({ deviceName: v })}
			/>
			<SelectField
				label="Theme"
				value={sd.theme}
				options={[
					{ value: "dark", label: "Ambient Dark" },
					{ value: "light", label: "Light" }
				]}
				onChange={v => setSystemProps({ theme: v })}
			/>
			<SelectField
				label="Time Zone"
				value={sd.timeZone ?? ""}
				allowEmpty
				options={TIMEZONES}
				onChange={v => setSystemProps({ timeZone: v })}
			/>
			<SelectField
				label="Temperature Units"
				value={sd.units}
				options={[
					{ value: 0, label: "Fahrenheit" },
					{ value: 1, label: "Celsius" }
				]}
				onChange={v => setSystemProps({ units: parseInt(v) })}
			/>
			<div class="rows">
				<ToggleRow
					label="Skip demo animation"
					hint="Disable the rainbow wipe on startup."
					on={sd.skipDemo}
					onChange={v => setSystemProps({ skipDemo: v })}
				/>
				<ToggleRow
					label="Enable Preview Studio"
					hint="Show the full 3D preview experience on the Control screen."
					on={sd.enablePreviewStudio ?? true}
					onChange={v => setSystemProps({ enablePreviewStudio: v })}
				/>
				<ToggleRow
					label="Enable phone calibration"
					hint="Allow live phone-camera calibration sessions."
					on={sd.enablePhoneCalibration ?? true}
					onChange={v => setSystemProps({ enablePhoneCalibration: v })}
				/>
			</div>
			<button class="btn mt" onClick={onRunWizard}>Run setup wizard</button>

			<div class="section-label">Updates</div>
			<div class="rows">
				<ToggleRow
					label="Automatically install updates"
					on={sd.autoUpdate}
					onChange={v => setSystemProps({ autoUpdate: v })}
				/>
			</div>
			<SelectField
				label="Update time"
				hint="When Glimmr checks for and installs updates (if enabled)."
				value={sd.autoUpdateTime}
				options={UPDATE_HOURS}
				onChange={v => setSystemProps({ autoUpdateTime: parseInt(v) })}
			/>

			<div class="section-label">Discovery</div>
			<div class="grid-2">
				<TextField
					label="Discovery time (s)"
					hint="Seconds allowed for each scan."
					type="number" min={1} max={60} step={1}
					value={sd.discoveryTimeout}
					onCommit={v => setSystemProps({ discoveryTimeout: parseInt(v) || 10 })}
				/>
				<TextField
					label="Discovery interval (min)"
					hint="How often auto-discovery runs."
					type="number" min={1} max={120} step={1}
					value={sd.autoDiscoveryFrequency}
					onCommit={v => setSystemProps({ autoDiscoveryFrequency: parseInt(v) || 15 })}
				/>
			</div>
			<SelectField
				label="Serial Port Speed"
				hint="Baud rate for Adalight discovery."
				value={sd.baudRate}
				options={[75, 110, 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200].map(b => ({
					value: b, label: String(b)
				}))}
				onChange={v => setSystemProps({ baudRate: parseInt(v) })}
			/>

			<div class="section-label">Automation</div>
			<div class="rows">
				<ToggleRow
					label="Auto-delete old devices"
					hint="Remove devices not seen during discovery for a while."
					on={sd.autoRemoveDevices}
					onChange={v => setSystemProps({ autoRemoveDevices: v })}
				/>
				<ToggleRow
					label="Auto-sleep lights"
					hint="Stop streaming when no input is detected."
					on={sd.enableAutoDisable}
					onChange={v => setSystemProps({ enableAutoDisable: v })}
				/>
				<ToggleRow
					label="Auto-adjust LED brightness"
					hint="Prevent dropouts and flicker on wired strips."
					on={sd.enableAutoBrightness}
					onChange={v => setSystemProps({ enableAutoBrightness: v })}
				/>
			</div>
			<div class="grid-2 mt">
				<TextField
					label="Auto-delete after (days)"
					type="number" min={1} max={60} step={1}
					value={sd.autoRemoveDevicesAfter}
					onCommit={v => setSystemProps({ autoRemoveDevicesAfter: parseInt(v) || 7 })}
				/>
				<TextField
					label="Auto-sleep delay (s)"
					type="number" min={0} max={3000} step={1}
					value={sd.autoDisableDelay}
					onCommit={v => setSystemProps({ autoDisableDelay: parseInt(v) || 0 })}
				/>
				<SelectField
					label="PSU Voltage"
					value={String(sd.ablVolts)}
					options={[
						{ value: "5", label: "5v" },
						{ value: "5.1", label: "5.1v (Canakit Pi PSU)" }
					]}
					onChange={v => setSystemProps({ ablVolts: parseFloat(v) })}
				/>
				<TextField
					label="PSU Amperage"
					type="number" min={1} max={100} step={0.1}
					value={sd.ablAmps}
					onCommit={v => setSystemProps({ ablAmps: parseFloat(v) || 1 })}
				/>
			</div>

			<div class="section-label">OpenRGB</div>
			<div class="grid-2">
				<TextField
					label="IP Address"
					value={sd.openRgbIp ?? ""}
					onCommit={v => setSystemProps({ openRgbIp: v })}
				/>
				<TextField
					label="Port"
					type="number"
					value={sd.openRgbPort ?? 6742}
					onCommit={v => setSystemProps({ openRgbPort: parseInt(v) || 6742 })}
				/>
			</div>

			<HueSyncPanel enablePolling={false} />
			</SectionGroup>

			<SectionGroup title="Maintenance" defaultOpen={false}>
				<div class="section-label">Backup & import</div>
			<div class="rows">
				<div class="row-item">
					<div class="row-body">
						<div class="row-title">Database backup</div>
						<div class="row-sub">Download the current settings database.</div>
					</div>
					<div class="row-trail">
						<button class="btn small" onClick={downloadDatabase}>Download</button>
					</div>
				</div>
				<FileUploadRow
					label="Database restore"
					hint="Upload a previously downloaded database."
					endpoint="database"
				/>
				<FileUploadRow
					label="Ambient scene"
					hint="Import an ambient scene JSON file."
					endpoint="importAmbientScene"
					accept=".json"
				/>
				<FileUploadRow
					label="Audio scene"
					hint="Import an audio scene JSON file."
					endpoint="importAudioScene"
					accept=".json"
				/>
			</div>

			<div class="section-label">Logging</div>
			<SelectField
				label="Log Level"
				value={sd.logLevel}
				options={[
					{ value: 0, label: "Debug" },
					{ value: 1, label: "Information" }
				]}
				onChange={v => setSystemProps({ logLevel: parseInt(v) })}
			/>
			<div style={{ display: "flex", gap: "10px", margin: "0 4px" }}>
				<button class="btn" style={{ flex: 1 }} onClick={() => setShowLog(true)}>Live log</button>
				<button class="btn" style={{ flex: 1 }} onClick={downloadLogs}>Download log</button>
			</div>

				<div class="section-label">System</div>
			<div style={{ display: "flex", gap: "10px", margin: "0 4px", flexWrap: "wrap" }}>
				<button class="btn" style={{ flex: 1 }} onClick={() => control("restart")}>Restart service</button>
				<button class="btn" style={{ flex: 1 }} onClick={() => control("update")}>Update</button>
				<button class="btn" style={{ flex: 1 }} onClick={() => control("reboot")}>Reboot</button>
				<button class="btn danger" style={{ flex: 1 }} onClick={() => control("shutdown")}>Shut down</button>
			</div>
			</SectionGroup>

			{showLog && <LogViewerSheet onClose={() => setShowLog(false)} />}
		</div>
	);
}
