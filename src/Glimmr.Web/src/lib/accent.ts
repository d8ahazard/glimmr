import { effect } from "@preact/signals";
import { mode, system } from "./store";

// Mode hues drive the live UI accent when ambient color isn't in play.
const MODE_ACCENTS: Record<number, string> = {
	0: "#8a8f98", // off: neutral
	1: "#46c8ff", // video: cyan
	2: "#e25dff", // audio: magenta
	3: "#ffffff", // ambient: replaced by ambient color
	4: "#7b6cff", // a/v: violet
	5: "#5dff8f", // stream: green
	6: "#5dff8f",
	7: "#ff9e43", // hue sync: hue orange
	8: "#ffb24d"  // scenes: amber (replaced by color for the solid scene)
};

function hexToRgb(hex: string): [number, number, number] | null {
	const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
	if (!m) return null;
	const n = parseInt(m[1], 16);
	return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function luminance([r, g, b]: [number, number, number]) {
	return (0.299 * r + 0.587 * g + 0.114 * b) / 255;
}

/** Keep accents readable on black: lift very dark colors. */
function ensureVisible(rgb: [number, number, number]): [number, number, number] {
	const lum = luminance(rgb);
	if (lum >= 0.25) return rgb;
	const boost = lum < 0.02 ? 0 : 0.25 / Math.max(lum, 0.05);
	if (boost === 0) return [138, 143, 152];
	return rgb.map(c => Math.min(255, Math.round(c * boost))) as [number, number, number];
}

export function startAccentSync() {
	effect(() => {
		const m = mode.value;
		const sd = system.value;
		let accent = MODE_ACCENTS[m] ?? "#46c8ff";
		// The static-color scene (and legacy ambient) drives the accent from the chosen color.
		if ((m === 3 || (m === 8 && sd?.sceneId === "solid")) && sd?.ambientColor) {
			accent = "#" + sd.ambientColor.replace("#", "");
		}
		const rgb = hexToRgb(accent);
		if (!rgb) return;
		const [r, g, b] = ensureVisible(rgb);
		const root = document.documentElement;
		root.style.setProperty("--accent", `rgb(${r} ${g} ${b})`);
		root.style.setProperty("--accent-rgb", `${r} ${g} ${b}`);
	});

	effect(() => {
		const theme = system.value?.theme;
		document.documentElement.dataset.theme = theme === "light" ? "light" : "dark";
	});
}
