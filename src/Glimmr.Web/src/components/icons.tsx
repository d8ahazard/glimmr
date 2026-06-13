import type { JSX } from "preact";

interface IconProps {
	size?: number;
}

const base = (size = 22): JSX.SVGAttributes<SVGSVGElement> => ({
	width: size,
	height: size,
	viewBox: "0 0 24 24",
	fill: "none",
	stroke: "currentColor",
	"stroke-width": 1.9,
	"stroke-linecap": "round",
	"stroke-linejoin": "round"
});

export const IconPower = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M12 2v9" /><path d="M5.5 5.6a9 9 0 1 0 13 0" /></svg>
);

export const IconVideo = ({ size }: IconProps) => (
	<svg {...base(size)}><rect x="2" y="5" width="20" height="13" rx="2.5" /><path d="M8.5 21h7" /></svg>
);

export const IconAudio = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M9 18V5l11-2v13" /><circle cx="6" cy="18" r="3" /><circle cx="17" cy="16" r="3" /></svg>
);

export const IconAv = ({ size }: IconProps) => (
	<svg {...base(size)}><rect x="2" y="5" width="20" height="13" rx="2.5" /><path d="M10 9.5v5l4-2.5z" fill="currentColor" stroke="none" /><path d="M8.5 21h7" /></svg>
);

export const IconAmbient = ({ size }: IconProps) => (
	<svg {...base(size)}><circle cx="12" cy="12" r="4.5" /><path d="M12 2.5v2.6M12 18.9v2.6M2.5 12h2.6M18.9 12h2.6M5 5l1.9 1.9M17.1 17.1 19 19M19 5l-1.9 1.9M6.9 17.1 5 19" /></svg>
);

export const IconStream = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M5 12.5a7 7 0 0 1 14 0" /><path d="M8.2 15a3.8 3.8 0 0 1 7.6 0" /><circle cx="12" cy="18" r="1.6" fill="currentColor" stroke="none" /></svg>
);

export const IconHue = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M9 18h6" /><path d="M10 21h4" /><path d="M12 3a6.5 6.5 0 0 0-3.5 12V17h7v-2A6.5 6.5 0 0 0 12 3z" /></svg>
);

export const IconControl = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M4 8h10M18 8h2M4 16h2M10 16h10" /><circle cx="16" cy="8" r="2.2" /><circle cx="8" cy="16" r="2.2" /></svg>
);

export const IconDevices = ({ size }: IconProps) => (
	<svg {...base(size)}><rect x="2" y="4" width="13" height="10" rx="2" /><path d="M6 18h5" /><rect x="17" y="9" width="5" height="11" rx="1.6" /></svg>
);

export const IconCapture = ({ size }: IconProps) => (
	<svg {...base(size)}><rect x="3" y="3" width="18" height="18" rx="3" /><circle cx="12" cy="12" r="4" /><circle cx="12" cy="12" r="1.2" fill="currentColor" stroke="none" /></svg>
);

export const IconSettings = ({ size }: IconProps) => (
	<svg {...base(size)}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.87l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-1.87-.34 1.7 1.7 0 0 0-1 1.55V21a2 2 0 1 1-4 0v-.09a1.7 1.7 0 0 0-1-1.55 1.7 1.7 0 0 0-1.87.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.7 1.7 0 0 0 .34-1.87 1.7 1.7 0 0 0-1.55-1H3a2 2 0 1 1 0-4h.09a1.7 1.7 0 0 0 1.55-1 1.7 1.7 0 0 0-.34-1.87l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.7 1.7 0 0 0 1.87.34h.01a1.7 1.7 0 0 0 1-1.55V3a2 2 0 1 1 4 0v.09a1.7 1.7 0 0 0 1 1.55h.01a1.7 1.7 0 0 0 1.87-.34l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.7 1.7 0 0 0-.34 1.87v.01a1.7 1.7 0 0 0 1.55 1H21a2 2 0 1 1 0 4h-.09a1.7 1.7 0 0 0-1.55 1z" /></svg>
);

export const IconRefresh = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M21 12a9 9 0 1 1-2.64-6.36" /><path d="M21 3v6h-6" /></svg>
);

export const IconFlash = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M13 2 4.5 13.5H11L9.5 22 19 10h-6.5z" /></svg>
);

export const IconTrash = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M3 6h18" /><path d="M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2" /><path d="M19 6 18 21H6L5 6" /><path d="M10 11v6M14 11v6" /></svg>
);

export const IconChevron = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="m9 6 6 6-6 6" /></svg>
);

export const IconUp = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="m6 15 6-6 6 6" /></svg>
);

export const IconDown = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="m6 9 6 6 6-6" /></svg>
);

export const IconClose = ({ size }: IconProps) => (
	<svg {...base(size)}><path d="M5 5l14 14M19 5 5 19" /></svg>
);

export const MODE_ICONS: Record<string, (p: IconProps) => JSX.Element> = {
	power: IconPower,
	video: IconVideo,
	audio: IconAudio,
	av: IconAv,
	ambient: IconAmbient,
	stream: IconStream,
	hue: IconHue
};
