import { ledCells, sectorCells, ledRange, VIEW_W, VIEW_H, MARGIN_X, MARGIN_Y, SEGMENT_COLORS } from "../lib/maps";
import { system, flashLed, flashSector } from "../lib/store";
import type { DeviceData } from "../types";

function ScreenOutline() {
	return (
		<rect
			class="map-screen"
			x={MARGIN_X + (VIEW_W - MARGIN_X * 2) * 0.16}
			y={MARGIN_Y + (VIEW_H - MARGIN_Y * 2) * 0.18}
			width={(VIEW_W - MARGIN_X * 2) * 0.68}
			height={(VIEW_H - MARGIN_Y * 2) * 0.64}
			rx={14}
		/>
	);
}

/**
 * Direction chevrons drawn in the middle of each side, showing the order
 * Glimmr walks the master grid (counter-clockwise from the bottom-right).
 * When `reversed` is set (a strip with "Reverse Strip" on) the arrows flip.
 */
function DirectionArrows({ reversed }: { reversed?: boolean }) {
	const midX = VIEW_W / 2;
	const midY = VIEW_H / 2;
	// Walk order: right side goes up, top goes right-to-left, left goes down,
	// bottom goes left-to-right. Rotation is in degrees, 0 = pointing right.
	const arrows = [
		{ x: VIEW_W - MARGIN_X * 0.55, y: midY, rot: -90 }, // right side: up
		{ x: midX, y: MARGIN_Y * 0.55, rot: 180 }, // top: leftwards
		{ x: MARGIN_X * 0.55, y: midY, rot: 90 }, // left side: down
		{ x: midX, y: VIEW_H - MARGIN_Y * 0.55, rot: 0 } // bottom: rightwards
	];
	return (
		<g class="map-arrows" aria-hidden="true">
			{arrows.map((a, i) => (
				<path
					key={i}
					d="M -14 -12 L 6 0 L -14 12"
					transform={`translate(${a.x} ${a.y}) rotate(${a.rot + (reversed ? 180 : 0)})`}
					fill="none"
					stroke="currentColor"
					stroke-width={6}
					stroke-linecap="round"
					stroke-linejoin="round"
					opacity={0.55}
				/>
			))}
		</g>
	);
}

/**
 * Perimeter LED map. Highlights the LEDs a device occupies on the master grid
 * (or per-segment colors when `segments` is provided). Tapping an LED flashes
 * it, or selects it when `onSelectLed` is provided (e.g. to set a strip offset).
 */
export function LedMap({
	highlight,
	segments,
	firstLed,
	caption,
	reversed,
	onSelectLed,
	showArrows = true
}: {
	highlight?: Set<number>;
	segments?: { range: Set<number>; color: string }[];
	firstLed?: number;
	caption?: string;
	reversed?: boolean;
	onSelectLed?: (index: number) => void;
	showArrows?: boolean;
}) {
	const sd = system.value;
	if (!sd) return null;
	const cells = ledCells(sd.leftCount, sd.rightCount, sd.topCount, sd.bottomCount);
	// Corner cells overlap (right column vs bottom row, etc.) and SVG paints in
	// document order - render the first-LED marker last so it's never covered.
	const ordered = firstLed !== undefined
		? [...cells.filter(c => c.index !== firstLed), ...cells.filter(c => c.index === firstLed)]
		: cells;
	return (
		<div class="mapwrap">
			<svg viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}>
				<ScreenOutline />
				{showArrows && <DirectionArrows reversed={reversed} />}
				{ordered.map(c => {
					let fill: string | undefined;
					if (segments) {
						for (let s = 0; s < segments.length; s++) {
							if (segments[s].range.has(c.index)) {
								fill = segments[s].color;
								break;
							}
						}
					}
					const lit = highlight?.has(c.index) ?? false;
					const first = firstLed !== undefined && c.index === firstLed;
					return (
						<rect
							key={c.index}
							class={"map-cell" + (lit ? " lit" : "") + (first ? " first" : "")}
							style={fill ? { fill } : undefined}
							x={c.x}
							y={c.y}
							width={c.w}
							height={c.h}
							onClick={() => (onSelectLed ? onSelectLed(c.index) : flashLed(c.index))}
						>
							<title>{`LED ${c.index}`}</title>
						</rect>
					);
				})}
			</svg>
			<div class="map-caption">
				{caption ?? (onSelectLed
					? "Tap an LED to set where this strip starts."
					: "Tap an LED to flash it on your devices.")}
			</div>
			<div class="map-legend">
				LEDs count from 0 at the bottom-right corner, following the arrows
				{firstLed !== undefined ? " — the green cell is this strip's first LED." : "."}
			</div>
		</div>
	);
}

/** Compute the highlight range for a strip-style device on the master grid. */
export function deviceLedRange(dev: DeviceData): Set<number> {
	const sd = system.value;
	if (!sd) return new Set();
	let count = dev.ledCount ?? 0;
	if (dev.stripMode === 2) count /= 2;
	const mult = dev.ledMultiplier && dev.ledMultiplier !== 0 ? dev.ledMultiplier : 1;
	if (mult > 0) count *= mult;
	else if (mult < 0) count /= Math.abs(mult);
	return ledRange(sd.ledCount, dev.offset ?? 0, Math.round(count));
}

/**
 * Sector map. Tapping a sector either selects it (when `onSelect` is given)
 * or flashes it for identification.
 */
export function SectorMap({
	checked,
	onSelect,
	caption
}: {
	checked?: Set<number>;
	onSelect?: (sector: number) => void;
	caption?: string;
}) {
	const sd = system.value;
	if (!sd) return null;
	const cells = sectorCells(sd.hSectors, sd.vSectors, sd.useCenter);
	return (
		<div class="mapwrap">
			<svg viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}>
				{!sd.useCenter && <ScreenOutline />}
				{cells.map(c => {
					const isChecked = checked?.has(c.index) ?? false;
					return (
						<g key={c.index}>
							<rect
								class={"map-cell" + (isChecked ? " checked" : "")}
								x={c.x}
								y={c.y}
								width={c.w}
								height={c.h}
								onClick={() => (onSelect ? onSelect(c.index) : flashSector(c.index))}
							>
								<title>{`Sector ${c.index}`}</title>
							</rect>
							<text
								class={"map-num" + (isChecked ? " lit" : "")}
								x={c.x + c.w / 2}
								y={c.y + c.h / 2}
							>
								{c.index}
							</text>
						</g>
					);
				})}
			</svg>
			<div class="map-caption">
				{caption ?? (onSelect ? "Tap a sector to assign it." : "Tap a sector to flash it.")}
			</div>
			<div class="map-legend">
				Sectors count from 1 at the bottom-right corner, going counter-clockwise
				(unlike LEDs, which count from 0).
			</div>
		</div>
	);
}

/** WLED sectored-strip map: one color per segment, derived from device.segments. */
export function SegmentLedMap({ dev }: { dev: DeviceData }) {
	const sd = system.value;
	if (!sd || !dev.segments) return null;
	const segs = dev.segments.map((s, i) => ({
		range: ledRange(sd.ledCount, s.offset, s.len),
		color: SEGMENT_COLORS[i % SEGMENT_COLORS.length]
	}));
	return <LedMap segments={segs} caption="Each color shows one segment's LEDs on the master grid." />;
}

/** LIFX beam map: each beam/corner segment colored along the master grid. */
export function BeamLedMap({ dev }: { dev: DeviceData }) {
	const sd = system.value;
	const segments = dev.beamLayout?.segments;
	if (!sd || !segments) return null;
	const segs = segments.map((s, i) => {
		let len = s.ledCount;
		if (s.repeat) len = 1;
		len *= 2;
		return {
			range: ledRange(sd.ledCount, s.offset, len),
			color: SEGMENT_COLORS[i % SEGMENT_COLORS.length]
		};
	});
	return (
		<LedMap
			segments={segs}
			caption="Beams are spaced so 10 LEDs/beam equal 20 LEDs within Glimmr."
		/>
	);
}
