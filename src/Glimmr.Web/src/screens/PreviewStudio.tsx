import { useEffect, useMemo, useRef, useState } from "preact/hooks";
import { outputImage, setPreviewsActive, showToast, system } from "../lib/store";
import {
	createCalibrationSession,
	flashRoomEmitter,
	getRoomMap,
	importRoomMapProvider,
	updateRoomEmitter
} from "../lib/api";
import type { RoomMapEmitter, Vec3 } from "../types";
import { RoomScene3D } from "../components/preview/RoomScene3D";
import { EmitterEditor } from "../components/preview/EmitterEditor";
import { PreviewImage } from "../components/PreviewImage";

export function PreviewStudio({ onClose }: { onClose: () => void }) {
	const [emitters, setEmitters] = useState<RoomMapEmitter[]>([]);
	const [selectedId, setSelectedId] = useState<string | null>(null);
	const [busy, setBusy] = useState(false);
	const [sessionId, setSessionId] = useState<string | null>(null);
	const pendingUpdates = useRef<Record<string, number>>({});
	const sd = system.value;

	const selected = useMemo(
		() => emitters.find(e => e.id === selectedId) ?? null,
		[emitters, selectedId]
	);

	const refresh = async () => {
		const graph = await getRoomMap();
		if (!graph) return;
		setEmitters(graph.emitters ?? []);
		if (!selectedId && graph.emitters.length > 0) setSelectedId(graph.emitters[0].id);
	};

	useEffect(() => {
		void refresh();
		setPreviewsActive(true);
		return () => {
			setPreviewsActive(false);
			for (const id of Object.keys(pendingUpdates.current)) {
				clearTimeout(pendingUpdates.current[id]);
			}
		};
	}, []);

	const importProvider = async (provider: string) => {
		setBusy(true);
		const ok = await importRoomMapProvider(provider);
		setBusy(false);
		showToast("Room map", ok ? `Imported ${provider} emitters.` : "Import failed.");
		await refresh();
	};

	const updatePos = async (id: string, position: Vec3) => {
		setEmitters(prev => prev.map(e => (e.id === id ? { ...e, position } : e)));
		if (pendingUpdates.current[id]) {
			clearTimeout(pendingUpdates.current[id]);
		}

		pendingUpdates.current[id] = window.setTimeout(async () => {
			await updateRoomEmitter(id, position);
			delete pendingUpdates.current[id];
		}, 350);
	};

	const startPhoneCalibration = async () => {
		const s = await createCalibrationSession(selected?.id ?? "");
		if (!s) {
			showToast("Calibration", "Unable to start session.");
			return;
		}
		setSessionId(s.sessionId);
	};

	const phoneLink = sessionId ? `${window.location.origin}/app/?calibrate=${encodeURIComponent(sessionId)}` : "";

	return (
		<div class="screen">
			<div class="screen-title">
				<span>Preview Studio</span>
				<button class="btn" onClick={onClose}>Back to Control</button>
			</div>
			<p class="screen-sub">
				Virtual room preview with manual emitter mapping and phone calibration handoff.
			</p>

			<div class="section-label">Current output</div>
			<div class="preview-pair" style={{ gridTemplateColumns: "1fr" }}>
				<figure>
					<PreviewImage signal={outputImage} alt="Current output" />
					<figcaption>Live output</figcaption>
				</figure>
			</div>

			<div class="section-label split">
				<span>Virtual room</span>
				<div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
					<button class="btn small" disabled={busy} onClick={() => importProvider("Wled")}>Import WLED</button>
					<button class="btn small" disabled={busy} onClick={() => importProvider("Hue")}>Import Hue</button>
					<button class="btn small" disabled={busy} onClick={() => importProvider("Lifx")}>Import Lifx</button>
					<button class="btn small" disabled={busy} onClick={refresh}>Refresh</button>
				</div>
			</div>
			<RoomScene3D emitters={emitters} selectedEmitterId={selectedId} onSelect={setSelectedId} />

			<div class="section-label">Emitters</div>
			<EmitterEditor
				emitters={emitters}
				selectedId={selectedId}
				onSelect={setSelectedId}
				onUpdatePosition={updatePos}
				onFlash={async id => {
					const ok = await flashRoomEmitter(id);
					showToast("Emitter flash", ok ? "Emitter flashed." : "Flash failed.");
				}}
			/>

			{(sd?.enablePhoneCalibration ?? true) && (
				<>
					<div class="section-label">Phone calibration</div>
					<p class="field-hint" style={{ margin: "0 8px 10px" }}>
						Start a live calibration session, then open the link on your phone to stream camera frames.
					</p>
					<div style={{ display: "flex", gap: "10px", margin: "0 4px" }}>
						<button class="btn primary" onClick={startPhoneCalibration}>Start session</button>
					</div>
					{sessionId && (
						<div class="field">
							<label class="field-label">Phone link</label>
							<input class="input" readOnly value={phoneLink} onFocus={e => e.currentTarget.select()} />
						</div>
					)}
				</>
			)}
		</div>
	);
}
