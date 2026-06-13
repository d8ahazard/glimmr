import type { HueSyncStatus } from "../types";

const base = "/api/Glimmr";

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
