import { useState } from "preact/hooks";
import { sortedDevices, scanDevices, scanning } from "../lib/store";
import { DeviceSheet, deviceIcon } from "../components/device/DeviceSheet";
import { Toggle, Slider } from "../components/ui";
import { updateDevice } from "../lib/store";
import { IconRefresh, IconChevron } from "../components/icons";
import type { DeviceData } from "../types";

function subtitle(dev: DeviceData): string {
	const parts: string[] = [];
	if (dev.ipAddress) parts.push(dev.ipAddress);
	const count = dev.multiZoneCount ?? dev.ledCount;
	if (count != null && dev.deviceTag !== "Lifx Bulb") parts.push(`${count} LEDs`);
	return parts.join(" · ");
}

function displayName(dev: DeviceData, devs: DeviceData[]): string {
	const name = dev.name ?? "";
	const dupes = devs.filter(d => (d.name ?? "") === name).length > 1;
	return dupes && dev.ipAddress ? `${name} (${dev.ipAddress})` : name;
}

export function DevicesScreen() {
	const [openDevice, setOpenDevice] = useState<string | null>(null);
	const devs = sortedDevices.value;
	const busy = scanning.value;

	const groups = new Map<string, DeviceData[]>();
	for (const d of devs) {
		const key = d.deviceTag && d.tag === "Lifx" ? d.deviceTag : d.tag;
		const list = groups.get(key) ?? [];
		list.push(d);
		groups.set(key, list);
	}

	return (
		<div class="screen">
			<div class="screen-title">
				Devices
				<button class="btn small" disabled={busy} onClick={scanDevices}>
					<span class={busy ? "spin" : ""} style={{ display: "inline-flex" }}>
						<IconRefresh size={15} />
					</span>
					{busy ? "Scanning…" : "Scan"}
				</button>
			</div>
			<p class="screen-sub">
				{devs.length
					? `${devs.length} device${devs.length === 1 ? "" : "s"} on your network.`
					: "Scan your network to find lights, strips, bridges and panels."}
			</p>

			{devs.length === 0 && (
				<div class="rows">
					<div class="row-item">
						<div class="row-body muted">
							{busy ? "Looking for devices…" : "Nothing here yet. Tap Scan to discover devices."}
						</div>
					</div>
				</div>
			)}

			{[...groups.entries()].map(([tag, list]) => (
				<div key={tag}>
					<div class="section-label">{tag}</div>
					<div class="rows">
						{list.map(dev => (
							<div key={dev.id} class="row-item tappable" onClick={() => setOpenDevice(dev.id)}>
								<div class="row-icon"><img src={deviceIcon(dev)} alt="" loading="lazy" /></div>
								<div class="row-body">
									<div class="row-title">{displayName(dev, devs)}</div>
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
						))}
					</div>
				</div>
			))}

			{openDevice && <DeviceSheet deviceId={openDevice} onClose={() => setOpenDevice(null)} />}
		</div>
	);
}
