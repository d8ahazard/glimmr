import { devices, updateDevice, deleteDevice, flashDevice } from "../../lib/store";
import type { DeviceData, KeyProperty } from "../../types";
import { Sheet, Slider, Toggle, TextField, SelectField, ToggleRow, confirmAction } from "../ui";
import { LedMap, SectorMap, SegmentLedMap, deviceLedRange } from "../maps";
import { LinkPane } from "./LinkPane";
import { NanoLayout } from "./NanoLayout";
import { HueLights } from "./HueLights";
import { BeamEditor } from "./BeamEditor";
import { IconFlash, IconTrash } from "../icons";

function lcFirst(s: string) {
	return s.charAt(0).toLowerCase() + s.slice(1);
}

function deviceIcon(dev: DeviceData): string {
	let tag = dev.tag ?? "led";
	if (dev.deviceTag && (tag === "Dreamscreen" || tag === "Lifx")) tag = dev.deviceTag;
	return `/img/${tag.toLowerCase().replace(" ", "")}_icon.png`;
}

function setDevicePropCoerced(dev: DeviceData, prop: string, raw: string | number | boolean) {
	let value: unknown = raw;

	// WLED per-segment fields are flattened into keyProperties as
	// segmentOffset{N} / segmentMultiplier{N}; fold them back into segments.
	const segOffset = /^segmentOffset(\d+)$/.exec(prop);
	const segMult = /^segmentMultiplier(\d+)$/.exec(prop);
	if ((segOffset || segMult) && dev.segments) {
		const idx = parseInt((segOffset ?? segMult)![1]);
		const key = segOffset ? "offset" : "multiplier";
		const segments = dev.segments.map((s, i) =>
			i === idx ? { ...s, [key]: parseInt(String(raw)) || 0 } : s
		);
		updateDevice({ ...dev, segments });
		return;
	}

	const current = dev[prop];
	if (typeof current === "number" && typeof raw === "string") {
		value = raw.includes(".") ? parseFloat(raw) : parseInt(raw);
		if (Number.isNaN(value)) return;
	}
	if (prop === "stripMode" || prop === "offset") value = parseInt(String(raw));
	updateDevice({ ...dev, [prop]: value });
}

function segmentValue(dev: DeviceData, prop: string): string | number {
	const segOffset = /^segmentOffset(\d+)$/.exec(prop);
	const segMult = /^segmentMultiplier(\d+)$/.exec(prop);
	if ((segOffset || segMult) && dev.segments) {
		const idx = parseInt((segOffset ?? segMult)![1]);
		const seg = dev.segments[idx];
		if (seg) return segOffset ? seg.offset : (seg.multiplier ?? 1);
	}
	return (dev[prop] as string | number) ?? "";
}

function PropControl({ dev, prop }: { dev: DeviceData; prop: KeyProperty }) {
	const name = lcFirst(prop.valueName);
	const value = segmentValue(dev, name);

	switch (prop.valueType) {
		case "check":
			return (
				<ToggleRow
					label={prop.valueLabel}
					hint={prop.valueHint || undefined}
					on={!!dev[name]}
					onChange={v => setDevicePropCoerced(dev, name, v)}
				/>
			);
		case "select":
			return (
				<SelectField
					label={prop.valueLabel}
					hint={prop.valueHint || undefined}
					value={String(value)}
					options={Object.entries(prop.options ?? {}).map(([k, v]) => ({ value: k, label: String(v) }))}
					onChange={v => setDevicePropCoerced(dev, name, v)}
				/>
			);
		case "number":
			return (
				<TextField
					label={prop.valueLabel}
					hint={prop.valueHint || undefined}
					type="number"
					min={prop.valueMin}
					max={prop.valueMax}
					step={prop.valueStep}
					value={value}
					onCommit={v => setDevicePropCoerced(dev, name, v)}
				/>
			);
		case "ledMultiplier":
			return (
				<TextField
					label="LED Scale"
					hint="Multiply or divide the number of LEDs pulled from the grid."
					type="number"
					min="-5"
					max="5"
					step="0.125"
					value={value}
					onCommit={v => setDevicePropCoerced(dev, name, v)}
				/>
			);
		case "text":
		default:
			return (
				<TextField
					label={prop.valueLabel}
					hint={prop.valueHint || undefined}
					value={value}
					onCommit={v => setDevicePropCoerced(dev, name, v)}
				/>
			);
	}
}

const MAP_TYPES = ["ledmap", "beamMap", "sectorLedMap", "sectormap", "nanoleaf", "hue"];

/** Self-contained mapping editors (these manage their own split layouts). */
function DeviceMaps({ dev, types }: { dev: DeviceData; types: string[] }) {
	const out = [];

	if (types.includes("nanoleaf")) {
		const linked = !!dev.token && !!dev.layout?.positionData;
		out.push(<LinkPane key="link" deviceId={dev.id} type="nanoleaf" linked={linked} />);
		if (linked) out.push(<NanoLayout key="nano" dev={dev} />);
	}

	if (types.includes("hue")) {
		const linked = !!dev.token;
		out.push(<LinkPane key="link" deviceId={dev.id} type="hue" linked={linked} />);
		if (linked) out.push(<HueLights key="hue" dev={dev} />);
	}

	if (types.includes("beamMap")) {
		out.push(<BeamEditor key="beam" dev={dev} />);
	}

	return <>{out}</>;
}

/** Master-grid maps that pair with the device settings column. */
function GridMaps({ dev, types }: { dev: DeviceData; types: string[] }) {
	const out = [];

	if (types.includes("ledmap")) {
		const range = deviceLedRange(dev);
		let first: number | undefined;
		if (range.size) {
			const sorted = [...range].sort((a, b) => a - b);
			first = dev.reverseStrip ? sorted[sorted.length - 1] : sorted[0];
		}

		out.push(
			<LedMap
				key="led"
				highlight={range}
				firstLed={first}
				reversed={!!dev.reverseStrip}
				onSelectLed={i => updateDevice({ ...dev, offset: i })}
				caption="Tap an LED to set this strip's start (offset)."
			/>
		);
	}

	if (types.includes("sectorLedMap")) {
		out.push(<SegmentLedMap key="segled" dev={dev} />);
	}

	if (types.includes("sectormap")) {
		const target = dev.targetSector ?? -1;
		out.push(
			<SectorMap
				key="sector"
				checked={target > 0 ? new Set([target]) : undefined}
				onSelect={s => {
					updateDevice({ ...dev, targetSector: s });
				}}
				caption={`Tap the sector your ${dev.tag} should mirror.`}
			/>
		);
	}

	return <>{out}</>;
}

/** Overlap warning for strip devices sharing master-grid LEDs. */
function LedOverlapWarning({ dev }: { dev: DeviceData }) {
	const range = deviceLedRange(dev);
	const overlaps: string[] = [];
	for (const other of devices.value) {
		if (other.id === dev.id || !other.enable) continue;
		if (!other.keyProperties?.some(p => p.valueType === "ledmap")) continue;
		const otherRange = deviceLedRange(other);
		let hits = 0;
		range.forEach(i => {
			if (otherRange.has(i)) hits++;
		});
		if (hits > 0) overlaps.push(`${other.name} (${hits} LEDs)`);
	}

	if (overlaps.length === 0) return null;
	return (
		<div class="map-warning" role="alert">
			This strip overlaps {overlaps.join(", ")} on the master grid — overlapping LEDs
			will be driven by multiple devices.
		</div>
	);
}

const GRID_MAP_TYPES = ["ledmap", "sectorLedMap", "sectormap"];

export function DeviceSheet({ deviceId, onClose }: { deviceId: string; onClose: () => void }) {
	const dev = devices.value.find(d => d.id === deviceId);
	if (!dev) return null;
	const dupeName = devices.value.filter(d => (d.name ?? "") === (dev.name ?? "")).length > 1;
	const titleName = dupeName && dev.ipAddress ? `${dev.name} (${dev.ipAddress})` : dev.name;
	const props = dev.keyProperties ?? [];
	const mapTypes = props.map(p => p.valueType).filter(t => MAP_TYPES.includes(t));
	const fieldProps = props.filter(p => !MAP_TYPES.includes(p.valueType));
	const hasGridMaps = mapTypes.some(t => GRID_MAP_TYPES.includes(t));

	const fields = fieldProps.length > 0 ? (
		<>
			<div class="section-label">Device settings</div>
			<div class="fields-grid">
				{fieldProps.map(p => (
					<PropControl key={p.valueName} dev={dev} prop={p} />
				))}
			</div>
		</>
	) : null;

	return (
		<Sheet title={dev.name} hideTitle onClose={onClose}>
			{/* Device head group: info + brightness, no divider between them,
			    one divider below before the mapping content. */}
			<div class="rows" style={{ marginBottom: "16px", borderBottom: "1px solid var(--line)" }}>
				<div class="row-item no-sep">
					<div class="row-icon"><img src={deviceIcon(dev)} alt="" /></div>
					<div class="row-body">
						<div class="row-title">{titleName}</div>
						<div class="row-sub">
							{dev.deviceTag ?? dev.tag}
							{(dev.multiZoneCount ?? dev.ledCount) != null && dev.deviceTag !== "Lifx Bulb"
								? ` · ${dev.multiZoneCount ?? dev.ledCount} LEDs`
								: ""}
							{dev.ipAddress ? (
								<>
									{" · "}
									{dev.tag === "Wled" || dev.tag === "Glimmr" ? (
										<a href={`http://${dev.ipAddress}`} target="_blank" rel="noreferrer">
											{dev.ipAddress}
										</a>
									) : (
										dev.ipAddress
									)}
								</>
							) : null}
						</div>
					</div>
					<div class="row-trail">
						<button class="btn small" onClick={() => flashDevice(dev.id)} aria-label="Flash device">
							<IconFlash size={15} /> Locate
						</button>
						<Toggle
							on={dev.enable}
							onChange={v => updateDevice({ ...dev, enable: v })}
							label="Enable streaming"
						/>
					</div>
				</div>
				<div class="row-item">
					<div class="row-body" style={{ display: "flex", alignItems: "center", gap: "12px" }}>
						<span class="tiny muted" style={{ width: "70px" }}>Brightness</span>
						<Slider
							value={dev.brightness ?? 100}
							max={dev.maxBrightness ?? 100}
							onCommit={v => updateDevice({ ...dev, brightness: Math.round(v) })}
							label="Device brightness"
						/>
					</div>
				</div>
			</div>

			<DeviceMaps dev={dev} types={mapTypes} />

			{hasGridMaps ? (
				<div class="map-split">
					<div class="map-split-map">
						<GridMaps dev={dev} types={mapTypes} />
					</div>
					<div class="map-split-list">
						{mapTypes.includes("ledmap") && <LedOverlapWarning dev={dev} />}
						{fields}
					</div>
				</div>
			) : (
				fields
			)}

			{dev.tag !== "Led" && (
				<button
					class="btn danger mt"
					style={{ width: "100%" }}
					onClick={() => {
						if (confirmAction(`Remove ${dev.name} and all of its settings?`)) {
							deleteDevice(dev.id);
							onClose();
						}
					}}
				>
					<IconTrash size={16} /> Remove device
				</button>
			)}
		</Sheet>
	);
}

export { deviceIcon };
