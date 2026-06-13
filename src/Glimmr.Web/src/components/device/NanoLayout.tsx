import { useState } from "preact/hooks";
import type { DeviceData, NanoPosition } from "../../types";
import { updateDevice, flashSector } from "../../lib/store";
import { SectorMap } from "../maps";

function polygonPoints(cx: number, cy: number, sides: number, radius: number, rotationDeg: number): string {
	const pts: string[] = [];
	const rot = (rotationDeg * Math.PI) / 180;
	for (let i = 0; i < sides; i++) {
		const a = rot + (i * 2 * Math.PI) / sides - Math.PI / 2;
		pts.push(`${cx + radius * Math.cos(a)},${cy + radius * Math.sin(a)}`);
	}
	return pts.join(" ");
}

interface ShapeGeo {
	pos: NanoPosition;
	x: number;
	y: number;
	sides: number;
	radius: number;
	rotation: number;
}

/**
 * Nanoleaf panel mapping. Split layout: screen sector map pinned on the left,
 * panel layout on the right. Tap a panel, then tap the sector it should mirror.
 */
export function NanoLayout({ dev }: { dev: DeviceData }) {
	const [picking, setPicking] = useState<NanoPosition | null>(null);
	const layout = dev.layout;
	if (!layout?.positionData?.length) return null;

	const shapes: ShapeGeo[] = [];
	for (const pos of layout.positionData) {
		if (pos.shapeType === 12) continue; // power supply / controller
		let x = pos.x;
		let y = pos.y;
		if (dev.mirrorX) x *= -1;
		if (!dev.mirrorY) y *= -1;
		let sides = 0;
		let radius = pos.sideLength;
		let rotation = pos.o;
		switch (pos.shapeType) {
			case 7: // hexagon
				sides = 6;
				rotation += 30;
				break;
			case 0:
			case 1:
			case 8:
			case 9: // triangles
				sides = 3;
				radius = pos.sideLength / Math.sqrt(3);
				break;
			case 2:
			case 3:
			case 4: // squares
				sides = 4;
				rotation += 45;
				radius *= 0.7;
				break;
		}
		if (sides === 0) continue;
		shapes.push({ pos, x, y, sides, radius, rotation });
	}
	if (!shapes.length) return null;

	const minX = Math.min(...shapes.map(s => s.x - s.radius));
	const maxX = Math.max(...shapes.map(s => s.x + s.radius));
	const minY = Math.min(...shapes.map(s => s.y - s.radius));
	const maxY = Math.max(...shapes.map(s => s.y + s.radius));
	const pad = 40;
	const vb = `${minX - pad} ${minY - pad} ${maxX - minX + pad * 2} ${maxY - minY + pad * 2}`;

	const assign = (sector: number) => {
		flashSector(sector);
		if (!picking) return;
		const nextLayout = {
			...layout,
			positionData: layout.positionData.map(p =>
				p.panelId === picking.panelId ? { ...p, targetSector: sector } : p
			)
		};
		updateDevice({ ...dev, layout: nextLayout });
		setPicking(null);
	};

	// When a panel is selected, highlight only its current sector; otherwise
	// highlight every sector that has a panel mapped to it.
	const checked = new Set<number>();
	if (picking) {
		if (picking.targetSector > 0) checked.add(picking.targetSector);
	} else {
		shapes.forEach(s => {
			if (s.pos.targetSector > 0) checked.add(s.pos.targetSector);
		});
	}

	const unmapped = shapes.filter(s => !(s.pos.targetSector > 0)).length;

	return (
		<div class="map-split">
			<div class="map-split-map">
				<SectorMap
					checked={checked}
					onSelect={assign}
					caption={picking
						? `Tap the sector panel ${picking.panelId} should mirror.`
						: "Select a panel on the right, then tap its sector. Tapping flashes the sector."}
				/>
			</div>
			<div class="map-split-list">
				<div class="mapwrap">
					<svg viewBox={vb}>
						{shapes.map(s => (
							<g
								key={s.pos.panelId}
								onClick={() => setPicking(picking?.panelId === s.pos.panelId ? null : s.pos)}
							>
								<polygon
									class={"nano-shape" +
										(s.pos.targetSector > 0 ? " mapped" : "") +
										(picking?.panelId === s.pos.panelId ? " picking" : "")}
									points={polygonPoints(s.x, s.y, s.sides, s.radius, s.rotation)}
								/>
								<text class="nano-label" x={s.x} y={s.y + 10}>{s.pos.panelId}</text>
								{s.pos.targetSector > 0 && (
									<text class="nano-sector" x={s.x} y={s.y - 16}>{s.pos.targetSector}</text>
								)}
							</g>
						))}
					</svg>
					<div class="map-caption">
						{picking ? `Panel ${picking.panelId} selected.` : "Tap a panel to select it."}
					</div>
					{unmapped > 0 && (
						<div class="map-legend">
							{unmapped} of {shapes.length} panels are unmapped and will stay dark.
						</div>
					)}
				</div>
			</div>
		</div>
	);
}
