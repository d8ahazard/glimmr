import type {
	CalibrationSession,
	EmitterGraph,
	HueSyncStatus,
	RoomMapEmitter,
	SceneParam,
	SceneV2,
	Vec3
} from "../types";

const base = "/api/Glimmr";
const roomMapBase = "/api/RoomMap";
const calibrationBase = "/api/CalibrationSession";

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
		kind: pick<"color" | "image" | "video" | "script" | "builtin">(r, "kind", "Kind", "script"),
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
		frameCount: pick(r, "frameCount", "FrameCount", 0),
		thumbnailFile: pick(r, "thumbnailFile", "ThumbnailFile", ""),
		provider: pick(r, "provider", "Provider", ""),
		params: normParams(pick<unknown[]>(r, "params", "Params", []))
	};
}

function normParams(raw: unknown): SceneParam[] {
	if (!Array.isArray(raw)) return [];
	return raw.map(p => {
		const o = (p ?? {}) as Record<string, unknown>;
		return {
			key: pick(o, "key", "Key", ""),
			label: pick(o, "label", "Label", ""),
			min: pick(o, "min", "Min", 0),
			max: pick(o, "max", "Max", 1),
			step: pick(o, "step", "Step", 1),
			default: pick(o, "default", "Default", 0),
			hint: pick(o, "hint", "Hint", "")
		};
	});
}

function normVec(raw: unknown): Vec3 {
	const r = (raw ?? {}) as Record<string, unknown>;
	return {
		x: pick(r, "x", "X", 0),
		y: pick(r, "y", "Y", 0),
		z: pick(r, "z", "Z", 0)
	};
}

function normEmitter(raw: unknown): RoomMapEmitter {
	const r = (raw ?? {}) as Record<string, unknown>;
	return {
		id: pick(r, "id", "Id", ""),
		providerId: pick(r, "providerId", "ProviderId", ""),
		deviceId: pick(r, "deviceId", "DeviceId", ""),
		physicalType: pick(r, "physicalType", "PhysicalType", ""),
		addressableCount: pick(r, "addressableCount", "AddressableCount", 0),
		position: normVec(pick(r, "position", "Position", {})),
		orientation: normVec(pick(r, "orientation", "Orientation", {})),
		normal: normVec(pick(r, "normal", "Normal", {})),
		confidence: pick(r, "confidence", "Confidence", 0)
	};
}

function normGraph(raw: unknown): EmitterGraph {
	const r = (raw ?? {}) as Record<string, unknown>;
	const emits = pick<unknown[]>(r, "emitters", "Emitters", []);
	return {
		id: pick(r, "id", "Id", ""),
		name: pick(r, "name", "Name", ""),
		version: pick(r, "version", "Version", "1"),
		emitters: Array.isArray(emits) ? emits.map(normEmitter) : []
	};
}

function normCalibrationSession(raw: unknown): CalibrationSession {
	const r = (raw ?? {}) as Record<string, unknown>;
	return {
		sessionId: pick(r, "sessionId", "SessionId", ""),
		createdAtUnixMs: pick(r, "createdAtUnixMs", "CreatedAtUnixMs", 0),
		expiresAtUnixMs: pick(r, "expiresAtUnixMs", "ExpiresAtUnixMs", 0),
		status: pick(r, "status", "Status", "created") as CalibrationSession["status"],
		sceneId: pick(r, "sceneId", "SceneId", ""),
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

export function sceneThumbnailUrl(sceneId: string, stamp?: string): string {
	const v = encodeURIComponent(stamp ?? "");
	return `${base}/scene/thumbnail?id=${encodeURIComponent(sceneId)}${v ? `&v=${v}` : ""}`;
}

export async function saveSceneThumbnail(sceneId: string, dataUrl: string): Promise<boolean> {
	try {
		const res = await fetch(`${base}/scene/thumbnail`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ sceneId, dataUrl })
		});
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

export async function getRoomMap(): Promise<EmitterGraph | null> {
	try {
		const res = await fetch(roomMapBase);
		if (!res.ok) return null;
		return normGraph(await res.json());
	} catch {
		return null;
	}
}

export async function importRoomMapProvider(providerId: string): Promise<boolean> {
	try {
		const res = await fetch(`${roomMapBase}/import/${encodeURIComponent(providerId)}`, { method: "POST" });
		return res.ok;
	} catch {
		return false;
	}
}

export async function updateRoomEmitter(id: string, position?: Vec3, orientation?: Vec3): Promise<boolean> {
	try {
		const res = await fetch(`${roomMapBase}/emitters/${encodeURIComponent(id)}`, {
			method: "PUT",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ position, orientation })
		});
		return res.ok;
	} catch {
		return false;
	}
}

export async function addRoomEmitter(emitter: Record<string, unknown>): Promise<boolean> {
	try {
		const res = await fetch(`${roomMapBase}/emitters`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(emitter)
		});
		return res.ok;
	} catch {
		return false;
	}
}

export async function deleteRoomEmitter(id: string): Promise<boolean> {
	try {
		const res = await fetch(`${roomMapBase}/emitters/${encodeURIComponent(id)}`, { method: "DELETE" });
		return res.ok;
	} catch {
		return false;
	}
}

export async function flashRoomEmitter(id: string, color = { r: 255, g: 255, b: 255, w: 0 }): Promise<boolean> {
	try {
		const res = await fetch(`${roomMapBase}/calibrate/flash/${encodeURIComponent(id)}`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(color)
		});
		return res.ok;
	} catch {
		return false;
	}
}

export async function createCalibrationSession(sceneId: string): Promise<CalibrationSession | null> {
	try {
		const res = await fetch(`${calibrationBase}/session`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ sceneId })
		});
		if (!res.ok) return null;
		return normCalibrationSession(await res.json());
	} catch {
		return null;
	}
}

export async function getCalibrationSession(sessionId: string): Promise<CalibrationSession | null> {
	try {
		const res = await fetch(`${calibrationBase}/session/${encodeURIComponent(sessionId)}`);
		if (!res.ok) return null;
		return normCalibrationSession(await res.json());
	} catch {
		return null;
	}
}

export async function submitCalibrationFrame(sessionId: string, dataUrl: string): Promise<boolean> {
	try {
		const res = await fetch(`${calibrationBase}/session/${encodeURIComponent(sessionId)}/frame`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ dataUrl })
		});
		return res.ok;
	} catch {
		return false;
	}
}

export async function completeCalibrationSession(sessionId: string): Promise<boolean> {
	try {
		const res = await fetch(`${calibrationBase}/session/${encodeURIComponent(sessionId)}/complete`, {
			method: "POST"
		});
		return res.ok;
	} catch {
		return false;
	}
}
