import { useRef, useState, useEffect } from "preact/hooks";

const SWATCHES = [
	"#ff0000", "#ff8000", "#ffe347", "#80ff00", "#00ff00", "#00ff80",
	"#008080", "#0080ff", "#0000ff", "#8000ff", "#ff00ff", "#ff0080", "#ffffff"
];

function hsToHex(h: number, s: number): string {
	// v fixed at 1 — the wheel picks hue/saturation, brightness lives on devices.
	const c = s;
	const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
	const m = 1 - c;
	let r = 0, g = 0, b = 0;
	if (h < 60) [r, g, b] = [c, x, 0];
	else if (h < 120) [r, g, b] = [x, c, 0];
	else if (h < 180) [r, g, b] = [0, c, x];
	else if (h < 240) [r, g, b] = [0, x, c];
	else if (h < 300) [r, g, b] = [x, 0, c];
	else [r, g, b] = [c, 0, x];
	const to = (v: number) => Math.round((v + m) * 255).toString(16).padStart(2, "0");
	return to(r) + to(g) + to(b);
}

function hexToHs(hex: string): { h: number; s: number } | null {
	const m = /^#?([0-9a-f]{6})$/i.exec(hex ?? "");
	if (!m) return null;
	const n = parseInt(m[1], 16);
	const r = ((n >> 16) & 255) / 255;
	const g = ((n >> 8) & 255) / 255;
	const b = (n & 255) / 255;
	const max = Math.max(r, g, b), min = Math.min(r, g, b);
	const d = max - min;
	let h = 0;
	if (d !== 0) {
		if (max === r) h = 60 * (((g - b) / d) % 6);
		else if (max === g) h = 60 * ((b - r) / d + 2);
		else h = 60 * ((r - g) / d + 4);
	}
	if (h < 0) h += 360;
	const s = max === 0 ? 0 : d / max;
	return { h, s };
}

/** Hue/saturation wheel with swatches. Color is a hex string without '#'. */
export function ColorWheel({ color, onCommit }: { color: string; onCommit: (hex: string) => void }) {
	const wheelRef = useRef<HTMLDivElement>(null);
	const [pos, setPos] = useState<{ x: number; y: number }>({ x: 50, y: 50 });
	const [preview, setPreview] = useState<string>(color);
	const draggingRef = useRef(false);

	useEffect(() => {
		if (draggingRef.current) return;
		setPreview(color);
		const hs = hexToHs("#" + color);
		if (hs) {
			const rad = (hs.h * Math.PI) / 180;
			const r = hs.s * 50;
			setPos({ x: 50 + Math.cos(rad) * r, y: 50 + Math.sin(rad) * r });
		}
	}, [color]);

	const pick = (clientX: number, clientY: number, commit: boolean) => {
		const el = wheelRef.current;
		if (!el) return;
		const rect = el.getBoundingClientRect();
		const cx = rect.left + rect.width / 2;
		const cy = rect.top + rect.height / 2;
		let dx = (clientX - cx) / (rect.width / 2);
		let dy = (clientY - cy) / (rect.height / 2);
		const dist = Math.min(Math.hypot(dx, dy), 1);
		const angle = Math.atan2(dy, dx);
		dx = Math.cos(angle) * dist;
		dy = Math.sin(angle) * dist;
		let h = (angle * 180) / Math.PI;
		if (h < 0) h += 360;
		const hex = hsToHex(h, dist);
		setPos({ x: 50 + dx * 50, y: 50 + dy * 50 });
		setPreview(hex);
		if (commit) onCommit(hex);
	};

	return (
		<div class="wheel-wrap">
			<div
				ref={wheelRef}
				class="wheel"
				onPointerDown={e => {
					draggingRef.current = true;
					(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
					pick(e.clientX, e.clientY, false);
				}}
				onPointerMove={e => {
					if (draggingRef.current) pick(e.clientX, e.clientY, false);
				}}
				onPointerUp={e => {
					draggingRef.current = false;
					pick(e.clientX, e.clientY, true);
				}}
			>
				<div
					class="wheel-knob"
					style={{ left: `${pos.x}%`, top: `${pos.y}%`, background: "#" + preview }}
				/>
			</div>
			<div class="swatches">
				{SWATCHES.map(s => (
					<button
						key={s}
						class="swatch"
						style={{ background: s }}
						aria-label={s}
						onClick={() => onCommit(s.slice(1))}
					/>
				))}
			</div>
		</div>
	);
}
