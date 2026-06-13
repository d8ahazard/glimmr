import { useState } from "preact/hooks";
import type { DeviceData, HueGroup, HueLightMap } from "../../types";
import { updateDevice, flashSector } from "../../lib/store";
import { SelectField, Toggle, Slider } from "../ui";
import { SectorMap } from "../maps";

/** Normalized view of one mappable light, independent of bridge API version. */
interface LightRowModel {
	key: string;
	name: string;
	targetSector: number;
	brightness: number;
	override: boolean;
	/** V1 light overrides are 0-255; V2 are 0-100. */
	maxBrightness: number;
	defaultBrightness: number;
}

/**
 * Hue entertainment group selection + per-light sector / brightness mapping.
 * Split layout: the sector map stays pinned while the light list scrolls.
 * Tap a light to select it, then tap a sector on the map to assign it.
 * Supports both V2 bridges (group.services) and legacy V1 bridges
 * (group light ids + device.mappedLights).
 */
export function HueLights({ dev }: { dev: DeviceData }) {
	const [picking, setPicking] = useState<string | null>(null);
	const groups = dev.groups ?? [];
	const selected = dev.selectedGroup;
	const group = groups.find(g => String(g.id) === String(selected));

	const setGroup = (id: string) => {
		updateDevice({ ...dev, selectedGroup: id });
	};

	const isV2 = !!group?.services?.length;

	// ---- Build normalized rows + a property setter per API version ----------

	let rows: LightRowModel[] = [];
	let setLightProp: (key: string, prop: string, value: unknown) => void = () => {};

	if (isV2) {
		rows = group!.services!.map(s => ({
			key: `${s.svcId}-${s.channel}`,
			name: s.type?.includes("strip") ? `${s.name} (${s.channel})` : s.name,
			targetSector: s.targetSector ?? -1,
			brightness: s.brightness ?? 100,
			override: !!s.override,
			maxBrightness: 100,
			defaultBrightness: dev.brightness ?? 100
		}));
		setLightProp = (key, prop, value) => {
			const nextGroups: HueGroup[] = groups.map(g => {
				if (String(g.id) !== String(selected)) return g;
				return {
					...g,
					services: (g.services ?? []).map(s =>
						`${s.svcId}-${s.channel}` === key ? { ...s, [prop]: value } : s
					)
				};
			});
			updateDevice({ ...dev, groups: nextGroups });
		};
	} else if (group?.lights) {
		rows = group.lights.map(id => {
			const info = dev.lights?.find(l => String(l.id) === String(id));
			const map = dev.mappedLights?.find(m => String(m.id) === String(id));
			return {
				key: String(id),
				name: info?.name ?? `Light ${id}`,
				targetSector: map?.targetSector ?? -1,
				brightness: map?.brightness ?? 255,
				override: map?.override ?? false,
				maxBrightness: 255,
				defaultBrightness: 255
			};
		});
		setLightProp = (key, prop, value) => {
			const existing = dev.mappedLights ?? [];
			const found = existing.some(m => String(m.id) === String(key));
			const mappedLights = found
				? existing.map(m => (String(m.id) === String(key) ? { ...m, [prop]: value } : m))
				: [...existing, { id: key, targetSector: -1, brightness: 255, override: false, [prop]: value } as HueLightMap];
			updateDevice({ ...dev, mappedLights });
		};
	}

	if (groups.length === 0) {
		return (
			<div class="map-warning" role="alert">
				No entertainment groups detected. Create an Entertainment Area in the Philips Hue
				app (Settings &gt; Entertainment areas), then re-scan devices and reopen this panel.
			</div>
		);
	}

	const pickingRow = picking != null ? rows.find(r => r.key === picking) : undefined;

	// When picking, highlight only that light's current sector so the target is obvious;
	// otherwise show everything that's mapped.
	const checked = new Set<number>();
	if (pickingRow) {
		if (pickingRow.targetSector > 0) checked.add(pickingRow.targetSector);
	} else {
		rows.forEach(r => {
			if (r.targetSector > 0) checked.add(r.targetSector);
		});
	}

	const onMapSelect = (sector: number) => {
		flashSector(sector);
		if (pickingRow) {
			setLightProp(pickingRow.key, "targetSector", sector);
			setPicking(null);
		}
	};

	return (
		<>
			<SelectField
				label="Entertainment Group"
				value={selected != null ? String(selected) : "-1"}
				allowEmpty
				options={groups.map(g => ({ value: String(g.id), label: g.name }))}
				onChange={setGroup}
			/>
			{rows.length > 0 && (
				<>
					<div class="map-split">
						<div class="map-split-map">
							<SectorMap
								checked={checked}
								onSelect={onMapSelect}
								caption={pickingRow
									? `Tap the sector ${pickingRow.name} should mirror.`
									: "Select a light below, then tap its sector. Tapping flashes the sector."}
							/>
						</div>
						<div class="map-split-list rows">
							{rows.map(row => (
								<div
									key={row.key}
									class={"row-item selectable" + (picking === row.key ? " picking" : "")}
									style={{ flexWrap: "wrap" }}
									onClick={() => setPicking(picking === row.key ? null : row.key)}
								>
									<div class="row-body" style={{ flexBasis: "100%", display: "flex", alignItems: "center", gap: "10px" }}>
										<div style={{ flex: 1, minWidth: 0 }}>
											<div class="row-title">{row.name}</div>
											<div class="row-sub">
												{row.targetSector > 0 ? `Sector ${row.targetSector}` : "Unmapped — stays off"}
												{picking === row.key ? " — tap a sector on the map" : ""}
											</div>
										</div>
										{row.targetSector > 0 && (
											<button
												class="btn small"
												title="Unmap this light so it stays off"
												onClick={e => {
													e.stopPropagation();
													setLightProp(row.key, "targetSector", -1);
													if (picking === row.key) setPicking(null);
												}}
											>
												Clear
											</button>
										)}
									</div>
									<div
										style={{ flex: 1, minWidth: "150px", display: "flex", alignItems: "center", gap: "10px" }}
										onClick={e => e.stopPropagation()}
									>
										<Slider
											value={row.override ? row.brightness : row.defaultBrightness}
											max={row.maxBrightness}
											onCommit={v => setLightProp(row.key, "brightness", Math.round(v))}
											label="Light brightness"
										/>
										<Toggle
											on={row.override}
											onChange={v => setLightProp(row.key, "override", v)}
											label="Override brightness"
										/>
									</div>
								</div>
							))}
						</div>
					</div>
					<div class="field-hint" style={{ margin: "4px 6px 14px" }}>
						{isV2
							? "Toggle override to give a light its own brightness instead of the device default."
							: "Legacy (V1) Hue bridge: mappings are stored per light. Toggle override to give a light its own brightness."}
					</div>
				</>
			)}
		</>
	);
}
