import type { HueSyncStatus, SceneV2 } from "../types";

const base = "/api/Glimmr";

// MVC controllers serialize with Newtonsoft's default (PascalCase) naming, whereas the
// SignalR "olo" store is camelCase. Normalize scene payloads from REST so callers always
// see the camelCase SceneV2 shape regardless of which path produced them.
function pick<T>(r: Record<string, unknown>, lower: string, upper: string, dflt: T): T {
	const v = r[lower] ?? r[upper];
	return (v === undefined || v === null ? dflt : v) as T;
}

function normScene(raw: unknown): SceneV2 {
	const r = (raw ?? {}) as Record<string, unknown>;
	return {
		id: pick(r, "id", "Id", ""),
		name: pick(r, "name", "Name", ""),
		kind: pick<"color" | "image" | "video" | "script">(r, "kind", "Kind", "script"),
		system: pick(r, "system", "System", false),
		width: pick(r, "width", "Width", 0),
		height: pick(r, "height", "Height", 0),
		fps: pick(r, "fps", "Fps", 30),
		loop: pick(r, "loop", "Loop", true),
		reverse: pick(r, "reverse", "Reverse", false),
		mirror: pick(r, "mirror", "Mirror", false),
		color: pick(r, "color", "Color", ""),
		script: pick(r, "script", "Script", ""),
		framesFile: pick(r, "framesFile", "FramesFile", ""),
		frameCount: pick(r, "frameCount", "FrameCount", 0)
	};
}

export async function getScenes(): Promise<SceneV2[]> {
	try {
		const res = await fetch(`${base}/scenes`);
		if (!res.ok) return [];
		const arr = (await res.json()) as unknown[];
		return Array.isArray(arr) ? arr.map(normScene) : [];
	} catch {
		return [];
	}
}

export type SaveSceneResult =
	| { ok: true; scene: SceneV2 }
	| { ok: false; errors: string[] };

export async function saveScene(scene: Partial<SceneV2>): Promise<SaveSceneResult> {
	try {
		const res = await fetch(`${base}/scene`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(scene)
		});
		if (res.ok) {
			return { ok: true, scene: normScene(await res.json()) };
		}
		// 400 carries { ok:false, errors } for script compile failures.
		try {
			const body = await res.json();
			if (body && Array.isArray(body.errors)) return { ok: false, errors: body.errors };
		} catch {
			// fall through
		}
		return { ok: false, errors: ["Save failed."] };
	} catch {
		return { ok: false, errors: ["Network error."] };
	}
}

export async function validateScene(script: string): Promise<{ ok: boolean; errors: string[] }> {
	try {
		const res = await fetch(`${base}/scene/validate`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(script)
		});
		const body = await res.json();
		return { ok: !!body.ok, errors: Array.isArray(body.errors) ? body.errors : [] };
	} catch {
		return { ok: false, errors: ["Network error."] };
	}
}

export async function selectScene(id: string): Promise<boolean> {
	try {
		const res = await fetch(`${base}/scene/select`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(id)
		});
		return res.ok;
	} catch {
		return false;
	}
}

export async function deleteScene(id: string): Promise<boolean> {
	try {
		const res = await fetch(`${base}/scene?id=${encodeURIComponent(id)}`, { method: "DELETE" });
		return res.ok;
	} catch {
		return false;
	}
}

export interface BakeVideoOptions {
	name: string;
	width: number;
	height: number;
	fps: number;
	trimStart: number;
	trimEnd: number;
	speed: number;
	fit: "stretch" | "crop" | "fit";
	loop: boolean;
	reverse: boolean;
	mirror: boolean;
}

export async function bakeVideo(file: File, opts: BakeVideoOptions): Promise<{ ok: boolean; scene?: SceneV2; error?: string }> {
	const form = new FormData();
	form.append("file", file);
	form.append("name", opts.name);
	form.append("width", String(opts.width));
	form.append("height", String(opts.height));
	form.append("fps", String(opts.fps));
	form.append("trimStart", String(opts.trimStart));
	form.append("trimEnd", String(opts.trimEnd));
	form.append("speed", String(opts.speed));
	form.append("fit", opts.fit);
	form.append("loop", String(opts.loop));
	form.append("reverse", String(opts.reverse));
	form.append("mirror", String(opts.mirror));
	try {
		const res = await fetch(`${base}/scene/bakeVideo`, { method: "POST", body: form });
		if (res.ok) return { ok: true, scene: normScene(await res.json()) };
		return { ok: false, error: await res.text() };
	} catch (e) {
		return { ok: false, error: String(e) };
	}
}

export async function getHueSyncStatus(): Promise<HueSyncStatus | null> {
	try {
		const res = await fetch(`${base}/hueSyncStatus`);
		return (await res.json()) as HueSyncStatus;
	} catch {
		return null;
	}
}

export async function armHueSyncLink(): Promise<boolean> {
	try {
		const res = await fetch(`${base}/hueSyncLink`);
		return res.ok;
	} catch {
		return false;
	}
}

export async function unpairHueSync(): Promise<boolean> {
	try {
		const res = await fetch(`${base}/hueSyncUnpair`);
		return res.ok;
	} catch {
		return false;
	}
}

export function downloadDatabase() {
	triggerDownload(`${base}/database`);
}

export function downloadLogs() {
	triggerDownload(`${base}/logs`);
}

function triggerDownload(url: string) {
	const link = document.createElement("a");
	link.setAttribute("download", "");
	link.href = url;
	document.body.appendChild(link);
	link.click();
	link.remove();
}

export async function uploadFile(endpoint: "database" | "importAmbientScene" | "importAudioScene", file: File): Promise<boolean> {
	const form = new FormData();
	form.append("files", file);
	try {
		const res = await fetch(`${base}/${endpoint}`, { method: "POST", body: form });
		return res.ok;
	} catch {
		return false;
	}
}
