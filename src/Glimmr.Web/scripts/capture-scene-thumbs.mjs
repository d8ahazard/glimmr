import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

const host = process.argv[2] || "http://192.168.1.54";
const base = `${host.replace(/\/$/, "")}/api/Glimmr`;
const socketUrl = `${host.replace(/\/$/, "")}/socket`;

let latestOutput = null;
let outputSeq = 0;

const hub = new HubConnectionBuilder()
	.withUrl(socketUrl)
	.withAutomaticReconnect()
	.configureLogging(LogLevel.Warning)
	.build();

hub.on("outputImage", b64 => {
	latestOutput = `data:image/png;base64,${b64}`;
	outputSeq++;
});

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

async function postJson(url, body) {
	const res = await fetch(url, {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify(body)
	});
	if (!res.ok) {
		throw new Error(`${res.status} ${res.statusText}`);
	}
	return res;
}

async function getJson(url) {
	const res = await fetch(url);
	if (!res.ok) {
		throw new Error(`${res.status} ${res.statusText}`);
	}
	return res.json();
}

async function restoreMode(initialMode, initialSceneId) {
	try {
		if (initialMode === 8 && initialSceneId) {
			await postJson(`${base}/scene/select`, initialSceneId);
			return;
		}
		await postJson(`${base}/mode`, initialMode);
	} catch (e) {
		console.warn(`Failed to restore initial mode (${initialMode}): ${e.message}`);
	}
}

async function main() {
	await hub.start();
	await hub.invoke("SettingsShown", true);
	const store = await getJson(`${base}/store`);
	const initialMode = Number(store?.systemData?.deviceMode ?? 0);
	const initialSceneId = store?.systemData?.sceneId ?? null;
	const scenes = await getJson(`${base}/scenes`);
	const ok = [];
	const fail = [];

	try {
		for (const scene of scenes) {
			if (!scene?.id) continue;
			const id = scene.id;
			process.stdout.write(`Scene ${id} ... `);
			try {
				await postJson(`${base}/scene/select`, id);
				const before = outputSeq;
				for (let i = 0; i < 30; i++) {
					if (latestOutput && outputSeq > before) break;
					await sleep(250);
				}
				if (!latestOutput || outputSeq <= before) throw new Error("no preview frame");
				await postJson(`${base}/scene/thumbnail`, { sceneId: id, dataUrl: latestOutput });
				ok.push(id);
				process.stdout.write("captured\n");
			} catch (e) {
				fail.push(id);
				process.stdout.write(`failed (${e.message})\n`);
			}
			await sleep(400);
		}
	} finally {
		await restoreMode(initialMode, initialSceneId);
	}

	try {
		await hub.invoke("SettingsShown", false);
	} catch {
		// ignore
	}
	await hub.stop();

	console.log(`OK ${ok.length}`);
	console.log(`FAIL ${fail.length}`);
	if (fail.length > 0) {
		console.log(`FAIL_IDS ${fail.join(",")}`);
		process.exitCode = 1;
	}
}

main().catch(async err => {
	console.error(err);
	try {
		await hub.stop();
	} catch {
		// ignore
	}
	process.exit(1);
});
