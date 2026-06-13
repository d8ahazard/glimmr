// Geometry helpers shared by the LED / sector map components.
// All maps render into a 16:9 viewBox (1920x1080) with margins matching the
// legacy UI so positions feel familiar.

export const VIEW_W = 1920;
export const VIEW_H = 1080;
export const MARGIN_X = 62;
export const MARGIN_Y = 52;

export interface Cell {
	index: number;
	x: number;
	y: number;
	w: number;
	h: number;
}

/**
 * Compute the LED range a device occupies on the master grid, with wrap-around.
 * Mirrors the legacy `ranges()` implementation.
 */
export function ledRange(systemTotal: number, offset: number, deviceTotal: number): Set<number> {
	const range = new Set<number>();
	if (systemTotal <= 0) return range;
	let fromEnd = 0;
	let startOffset = offset;
	if (offset < 0) {
		fromEnd = systemTotal + offset;
		deviceTotal += offset;
		offset = fromEnd;
		startOffset = 0;
	} else if (offset + deviceTotal > systemTotal) {
		while (offset > systemTotal) offset -= systemTotal;
		fromEnd = systemTotal - offset;
		deviceTotal -= fromEnd;
		startOffset = 0;
	}
	if (fromEnd !== 0) {
		for (let i = offset; i < systemTotal; i++) range.add(i);
	}
	if (deviceTotal > 0) {
		for (let i = startOffset; i < startOffset + deviceTotal; i++) range.add(i);
	}
	return range;
}

/**
 * Perimeter LED cells, indexed the same way Glimmr walks the strip:
 * bottom-right corner, up the right side, across the top (right to left),
 * down the left side, then across the bottom (left to right).
 */
export function ledCells(left: number, right: number, top: number, bottom: number): Cell[] {
	const cells: Cell[] = [];
	const w = VIEW_W;
	const h = VIEW_H;
	const innerW = w - MARGIN_X * 2;
	const innerH = h - MARGIN_Y * 2;
	const rH = innerH / Math.max(right, 1);
	const lH = innerH / Math.max(left, 1);
	const tW = innerW / Math.max(top, 1);
	const bW = innerW / Math.max(bottom, 1);
	const depthY = (lH + rH) / 2;
	const depthX = (tW + bW) / 2;
	let i = 0;
	// Right side, bottom to top
	for (let n = 0; n < right; n++) {
		cells.push({ index: i++, x: w - MARGIN_X - depthX, y: h - MARGIN_Y - (n + 1) * rH, w: depthX, h: rH });
	}
	// Top, right to left
	for (let n = 0; n < top; n++) {
		cells.push({ index: i++, x: w - MARGIN_X - (n + 1) * tW, y: MARGIN_Y, w: tW, h: depthY });
	}
	// Left side, top to bottom
	for (let n = 0; n < left; n++) {
		cells.push({ index: i++, x: MARGIN_X, y: MARGIN_Y + n * lH, w: depthX, h: lH });
	}
	// Bottom, left to right
	for (let n = 0; n < bottom; n++) {
		cells.push({ index: i++, x: MARGIN_X + n * bW, y: h - MARGIN_Y - depthY, w: bW, h: depthY });
	}
	return cells;
}

/**
 * Sector cells. Without center mode this is the perimeter ring, numbered from
 * 1 starting at the bottom-right going counter-clockwise (matches backend
 * sector numbering). With center mode it's the full grid, numbered
 * bottom-right to top-left, row by row.
 */
export function sectorCells(hSectors: number, vSectors: number, useCenter: boolean): Cell[] {
	const cells: Cell[] = [];
	const w = VIEW_W;
	const h = VIEW_H;
	const fW = (w - MARGIN_X * 2) / Math.max(hSectors, 1);
	const fH = (h - MARGIN_Y * 2) / Math.max(vSectors, 1);
	let sector = 1;

	if (useCenter) {
		for (let v = 0; v < vSectors; v++) {
			const y = h - MARGIN_Y - (v + 1) * fH;
			for (let x = 0; x < hSectors; x++) {
				cells.push({ index: sector++, x: w - MARGIN_X - (x + 1) * fW, y, w: fW, h: fH });
			}
		}
		return cells;
	}

	// Right column, bottom to top
	for (let v = 0; v < vSectors; v++) {
		cells.push({ index: sector++, x: w - MARGIN_X - fW, y: h - MARGIN_Y - (v + 1) * fH, w: fW, h: fH });
	}
	// Top row, right to left (excluding corners)
	for (let i = 1; i < hSectors - 1; i++) {
		cells.push({ index: sector++, x: w - MARGIN_X - (i + 1) * fW, y: MARGIN_Y, w: fW, h: fH });
	}
	// Left column, top to bottom
	for (let v = 0; v < vSectors - 1; v++) {
		cells.push({ index: sector++, x: MARGIN_X, y: MARGIN_Y + v * fH, w: fW, h: fH });
	}
	// Bottom row, left to right (excluding left corner)
	for (let i = 0; i < hSectors - 1; i++) {
		cells.push({ index: sector++, x: MARGIN_X + i * fW, y: h - MARGIN_Y - fH, w: fW, h: fH });
	}
	return cells;
}

export const SEGMENT_COLORS = [
	"#ff5470", "#ff9e43", "#ffe347", "#5dff8f",
	"#46c8ff", "#7b6cff", "#e25dff", "#ff5470",
	"#ff9e43", "#ffe347", "#5dff8f", "#46c8ff"
];
