import type { BeamSegment, DeviceData } from "../../types";
import { updateDevice } from "../../lib/store";
import { Toggle } from "../ui";
import { BeamLedMap } from "../maps";
import { IconUp, IconDown } from "../icons";

/** LIFX Beam segment ordering and per-segment settings. */
export function BeamEditor({ dev }: { dev: DeviceData }) {
	const layout = dev.beamLayout;
	if (!layout?.segments?.length) return null;
	const segments = [...layout.segments].sort((a, b) => a.position - b.position);

	const commit = (next: BeamSegment[]) => {
		const renumbered = next.map((s, i) => ({ ...s, position: i }));
		updateDevice({ ...dev, beamLayout: { ...layout, segments: renumbered } });
	};

	const move = (idx: number, dir: -1 | 1) => {
		const next = [...segments];
		const swap = idx + dir;
		if (swap < 0 || swap >= next.length) return;
		[next[idx], next[swap]] = [next[swap], next[idx]];
		commit(next);
	};

	const setProp = (idx: number, prop: keyof BeamSegment, value: number | boolean) => {
		const next = segments.map((s, i) => (i === idx ? { ...s, [prop]: value } : s));
		commit(next);
	};

	return (
		<div class="map-split">
			<div class="map-split-map">
				<BeamLedMap dev={dev} />
			</div>
			<div class="map-split-list">
				{segments.map((seg, i) => (
					<div key={i} class="beam-item">
						<div class="beam-name">
							{seg.ledCount === 1 ? "Corner" : "Beam"} {seg.position}
						</div>
						<div class="beam-move">
							<button class="btn small" disabled={i === 0} onClick={() => move(i, -1)} aria-label="Move up">
								<IconUp size={14} />
							</button>
							<button class="btn small" disabled={i === segments.length - 1} onClick={() => move(i, 1)} aria-label="Move down">
								<IconDown size={14} />
							</button>
						</div>
						<div class="beam-controls">
							<div class="mini-field">
								<span>Offset</span>
								<input
									class="input"
									type="number"
									value={seg.offset}
									onChange={e => setProp(i, "offset", parseInt((e.target as HTMLInputElement).value) || 0)}
								/>
							</div>
							<div class="mini-field">
								<span>Repeat</span>
								<Toggle on={seg.repeat} onChange={v => setProp(i, "repeat", v)} label="Repeat" />
							</div>
							<div class="mini-field">
								<span>Reverse</span>
								<Toggle on={seg.reverse} onChange={v => setProp(i, "reverse", v)} label="Reverse" />
							</div>
						</div>
					</div>
				))}
			</div>
		</div>
	);
}
