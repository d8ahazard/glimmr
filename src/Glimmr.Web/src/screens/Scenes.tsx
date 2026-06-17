import { useState, useEffect, useRef } from "preact/hooks";
import { system, scenes, devices, outputImage, setPreviewsActive, setSystemProps, showToast } from "../lib/store";
import type { SceneV2 } from "../types";
import { Sheet, TextField, ToggleRow, SelectField, confirmAction } from "../components/ui";
import { ColorWheel } from "../components/ColorWheel";
import { IconTrash } from "../components/icons";
import {
	saveScene, validateScene, selectScene, deleteScene, bakeVideo
} from "../lib/api";

const STARTER_SCRIPT = `@scene fps=30 bg=#000010 trail=0.85 size=32x48

emit fountain
  rate    = 2 per 0.05..0.12s
  origin  = x:center y:bottom
  vel     = dir:up speed:22..30
  gravity = down 18
  color   = pick(#4da6ff,#7fd2ff,#ffffff)
  fade    = 1.6s
`;

/** Suggest a render grid size from the first configured WLED matrix (curtain), else 32x48. */
function suggestSize(): { w: number; h: number } {
	const m = devices.value.find(
		d => d.stripMode === 4 && (d.matrixWidth ?? 0) > 0 && (d.matrixHeight ?? 0) > 0
	);
	if (m) return { w: Math.min(m.matrixWidth!, 64), h: Math.min(m.matrixHeight!, 64) };
	return { w: 32, h: 48 };
}

async function activate(id: string) {
	const cur = system.value;
	if (cur) system.value = { ...cur, deviceMode: 8, sceneId: id, autoDisabled: false };
	await selectScene(id);
}

export function ScenesScreen() {
	const sd = system.value;
	const [editing, setEditing] = useState<{ kind: "script" | "video"; scene?: SceneV2 } | null>(null);

	// Stream live preview frames while the Scenes tab is open.
	useEffect(() => {
		setPreviewsActive(true);
		return () => setPreviewsActive(false);
	}, []);

	if (!sd) return <div class="screen" />;

	const list = [...scenes.value].sort((a, b) => a.name.localeCompare(b.name));
	const activeId = sd.deviceMode === 8 ? sd.sceneId : undefined;
	const images = list.filter(s => s.kind === "image");
	const animations = list.filter(s => s.kind === "script");
	const videos = list.filter(s => s.kind === "video");

	const renderRow = (s: SceneV2) => (
		<div
			key={s.id}
			class={"row-item selectable" + (s.id === activeId ? " picking" : "")}
			onClick={() => activate(s.id)}
		>
			<div class="row-body">
				<div class="row-title">
					{s.name}{" "}
					<span class="muted tiny">
						{s.kind === "video" ? `${s.frameCount}f · ` : ""}{s.width}×{s.height}{s.mirror ? " · mirror" : ""}
					</span>
				</div>
				<div class="row-sub">{s.id === activeId ? "Playing now" : "Tap to play"}</div>
			</div>
			<div class="row-trail" onClick={e => e.stopPropagation()}>
				{s.kind === "script" && !s.system && (
					<button class="btn small" onClick={() => setEditing({ kind: "script", scene: s })}>Edit</button>
				)}
				{s.kind === "script" && s.system && (
					<button
						class="btn small"
						onClick={() => setEditing({ kind: "script", scene: { ...s, id: "", name: s.name + " copy", system: false } })}
					>
						Duplicate
					</button>
				)}
				{!s.system && (
					<button
						class="icon-btn"
						aria-label="Delete scene"
						onClick={async () => {
							if (!confirmAction(`Delete "${s.name}"?`)) return;
							const ok = await deleteScene(s.id);
							showToast("Scene", ok ? "Deleted." : "Delete failed.");
						}}
					>
						<IconTrash size={18} />
					</button>
				)}
			</div>
		</div>
	);

	return (
		<div class="screen">
			<div class="screen-title">Scenes</div>
			<p class="screen-sub">Scripted light animations and baked video, played across every device.</p>

			{activeId && (
				<>
					<div class="section-label">Now playing</div>
					<div class="preview-pair" style={{ gridTemplateColumns: "1fr" }}>
						<figure>
							<img src={outputImage.value ?? "/img/no_preview.jpg"} alt="Scene preview" />
							<figcaption>{list.find(s => s.id === activeId)?.name ?? "Scene"}</figcaption>
						</figure>
					</div>
				</>
			)}

			<div class="section-label">Static color</div>
			<ColorWheel
				color={sd.ambientColor}
				onCommit={hex => {
					setSystemProps({ ambientColor: hex });
					if (sd.sceneId !== "solid" || sd.deviceMode !== 8) {
						const cur = system.value;
						if (cur) system.value = { ...cur, deviceMode: 8, sceneId: "solid", autoDisabled: false };
						void selectScene("solid");
					}
				}}
			/>

			<div class="section-label">Images</div>
			{images.length === 0 && (
				<p class="field-hint" style={{ margin: "0 8px" }}>No image scenes. Build one with Image2Scene and import it as an ambient scene.</p>
			)}
			<div class="rows">{images.map(renderRow)}</div>

			<div class="section-label">Animations</div>
			<div class="scene-chips" style={{ marginBottom: "6px" }}>
				<button class="btn small primary" onClick={() => setEditing({ kind: "script" })}>New animation</button>
			</div>
			<div class="rows">{animations.map(renderRow)}</div>

			<div class="section-label">Videos</div>
			<div class="scene-chips" style={{ marginBottom: "6px" }}>
				<button class="btn small primary" onClick={() => setEditing({ kind: "video" })}>New video</button>
			</div>
			{videos.length === 0 && (
				<p class="field-hint" style={{ margin: "0 8px" }}>No video scenes yet — bake one from a clip.</p>
			)}
			<div class="rows">{videos.map(renderRow)}</div>

			{editing?.kind === "script" && (
				<ScriptEditor scene={editing.scene} onClose={() => setEditing(null)} />
			)}
			{editing?.kind === "video" && (
				<VideoEditor onClose={() => setEditing(null)} />
			)}
		</div>
	);
}

// ---------------------------------------------------------------------------
// Script editor
// ---------------------------------------------------------------------------

function ScriptEditor({ scene, onClose }: { scene?: SceneV2; onClose: () => void }) {
	const [name, setName] = useState(scene?.name ?? "New Animation");
	const [script, setScript] = useState(scene?.script || STARTER_SCRIPT);
	const [mirror, setMirror] = useState(scene?.mirror ?? false);
	const [errors, setErrors] = useState<string[]>([]);
	const [busy, setBusy] = useState(false);

	async function doValidate() {
		const r = await validateScene(script);
		setErrors(r.errors);
		if (r.ok) showToast("Script", "Looks good — ready to play.");
	}

	async function save(play: boolean) {
		setBusy(true);
		const r = await saveScene({ id: scene?.id, name, kind: "script", script, mirror });
		setBusy(false);
		if (!r.ok) {
			setErrors(r.errors);
			return;
		}
		setErrors([]);
		if (play) await activate(r.scene.id);
		showToast("Scene saved", name);
		onClose();
	}

	return (
		<Sheet title={scene?.id ? "Edit animation" : "New animation"} onClose={onClose}>
			<TextField label="Name" value={name} onCommit={setName} />
			<div class="field">
				<label class="field-label">Script</label>
				<textarea
					class="input code-area"
					spellcheck={false}
					rows={16}
					value={script}
					onInput={e => setScript((e.target as HTMLTextAreaElement).value)}
				/>
				<div class="field-hint">
					Particle shorthand. <code>@scene</code> sets size/fps/trail; each <code>emit</code> block
					defines a particle source (<code>rate</code>, <code>origin</code>, <code>vel</code>,{" "}
					<code>gravity</code>, <code>color</code>, <code>fade</code>, <code>spawn</code>).
				</div>
			</div>

			<div class="rows">
				<ToggleRow
					label="Mirror on matrices"
					hint="Reflect the left half onto the right for a symmetric display."
					on={mirror}
					onChange={setMirror}
				/>
			</div>

			{errors.length > 0 && (
				<div class="scene-errors">
					{errors.map((e, i) => (
						<div key={i} class="scene-error">{e}</div>
					))}
				</div>
			)}

			<div class="sheet-actions">
				<button class="btn small" onClick={doValidate} disabled={busy}>Validate</button>
				<button class="btn small" onClick={() => save(false)} disabled={busy}>Save</button>
				<button class="btn small primary" onClick={() => save(true)} disabled={busy}>
					Save &amp; play
				</button>
			</div>
		</Sheet>
	);
}

// ---------------------------------------------------------------------------
// Video editor (hybrid: browser preview + params, server bakes)
// ---------------------------------------------------------------------------

function VideoEditor({ onClose }: { onClose: () => void }) {
	const sug = suggestSize();
	const [file, setFile] = useState<File | null>(null);
	const [url, setUrl] = useState<string | null>(null);
	const [name, setName] = useState("Video Scene");
	const [width, setWidth] = useState(sug.w);
	const [height, setHeight] = useState(sug.h);
	const [fps, setFps] = useState(20);
	const [duration, setDuration] = useState(0);
	const [trimStart, setTrimStart] = useState(0);
	const [trimEnd, setTrimEnd] = useState(0);
	const [speed, setSpeed] = useState(1);
	const [fit, setFit] = useState<"stretch" | "crop" | "fit">("crop");
	const [loop, setLoop] = useState(true);
	const [reverse, setReverse] = useState(false);
	const [mirror, setMirror] = useState(false);
	const [busy, setBusy] = useState(false);

	const videoRef = useRef<HTMLVideoElement>(null);
	const dispRef = useRef<HTMLCanvasElement>(null);
	const offRef = useRef<HTMLCanvasElement | null>(null);
	const rafRef = useRef<number | undefined>(undefined);

	useEffect(() => () => {
		if (url) URL.revokeObjectURL(url);
		if (rafRef.current) cancelAnimationFrame(rafRef.current);
	}, [url]);

	// Continuously render the downsampled preview while the video plays/seeks.
	useEffect(() => {
		const video = videoRef.current;
		const disp = dispRef.current;
		if (!video || !disp) return;
		if (!offRef.current) offRef.current = document.createElement("canvas");
		const off = offRef.current;

		const draw = () => {
			off.width = width;
			off.height = height;
			const octx = off.getContext("2d");
			const dctx = disp.getContext("2d");
			if (octx && dctx && video.readyState >= 2) {
				const vw = video.videoWidth || width;
				const vh = video.videoHeight || height;
				octx.clearRect(0, 0, width, height);
				if (fit === "stretch") {
					octx.drawImage(video, 0, 0, width, height);
				} else {
					// crop = cover (max scale, center-crop); fit = contain (min scale, letterbox)
					const scale = fit === "crop"
						? Math.max(width / vw, height / vh)
						: Math.min(width / vw, height / vh);
					const dw = vw * scale;
					const dh = vh * scale;
					if (fit === "fit") {
						octx.fillStyle = "#000";
						octx.fillRect(0, 0, width, height);
					}
					octx.drawImage(video, (width - dw) / 2, (height - dh) / 2, dw, dh);
				}
				dctx.imageSmoothingEnabled = false;
				dctx.clearRect(0, 0, disp.width, disp.height);
				dctx.drawImage(off, 0, 0, width, height, 0, 0, disp.width, disp.height);
			}
			rafRef.current = requestAnimationFrame(draw);
		};
		rafRef.current = requestAnimationFrame(draw);
		return () => {
			if (rafRef.current) cancelAnimationFrame(rafRef.current);
		};
	}, [url, width, height, fit]);

	function onPick(e: Event) {
		const f = (e.target as HTMLInputElement).files?.[0];
		if (!f) return;
		setFile(f);
		setName(f.name.replace(/\.[^.]+$/, ""));
		if (url) URL.revokeObjectURL(url);
		setUrl(URL.createObjectURL(f));
	}

	const trimDur = Math.max(0, trimEnd - trimStart);
	const outDur = speed > 0 ? trimDur / speed : trimDur;
	const frames = Math.max(0, Math.round(outDur * fps));
	const estMb = (frames * width * height * 3) / (1024 * 1024);

	async function bake() {
		if (!file) return;
		setBusy(true);
		const r = await bakeVideo(file, {
			name, width, height, fps, trimStart, trimEnd, speed, fit, loop, reverse, mirror
		});
		setBusy(false);
		if (r.ok && r.scene) {
			showToast("Video baked", `${r.scene.frameCount} frames.`);
			await activate(r.scene.id);
			onClose();
		} else {
			showToast("Bake failed", r.error ?? "Unknown error.");
		}
	}

	return (
		<Sheet title="New video scene" onClose={onClose}>
			{!file && (
				<div class="field">
					<label class="field-label">Video file</label>
					<input class="input" type="file" accept="video/*" onChange={onPick} />
					<div class="field-hint">
						The clip is downsampled to a small grid sized for your curtain, then baked into a
						compact looping sequence.
					</div>
				</div>
			)}

			{url && (
				<>
					<TextField label="Name" value={name} onCommit={setName} />
					<div class="section-label">Preview</div>
					<div class="preview-pair">
						<figure>
							<video
								ref={videoRef}
								src={url}
								controls
								loop
								muted
								playsInline
								style={{ width: "100%", borderRadius: "10px" }}
								onLoadedMetadata={e => {
									const d = (e.target as HTMLVideoElement).duration || 0;
									setDuration(d);
									setTrimStart(0);
									setTrimEnd(Math.min(d, 600));
								}}
							/>
							<figcaption>Source</figcaption>
						</figure>
						<figure>
							<canvas
								ref={dispRef}
								width={width * 6}
								height={height * 6}
								style={{ width: "100%", imageRendering: "pixelated", borderRadius: "10px", background: "#000" }}
							/>
							<figcaption>{width}×{height} downsample</figcaption>
						</figure>
					</div>

					<div class="grid-2">
						<TextField
							label="Width" type="number" min={1} max={64} step={1} value={width}
							onCommit={v => setWidth(Math.max(1, Math.min(64, parseInt(v) || 1)))}
						/>
						<TextField
							label="Height" type="number" min={1} max={64} step={1} value={height}
							onCommit={v => setHeight(Math.max(1, Math.min(64, parseInt(v) || 1)))}
						/>
					</div>
					<div class="grid-2">
						<TextField
							label="Frame rate" type="number" min={1} max={60} step={1} value={fps}
							onCommit={v => setFps(Math.max(1, Math.min(60, parseInt(v) || 1)))}
						/>
						<TextField
							label="Speed" type="number" min={0.1} max={10} step={0.1} value={speed}
							onCommit={v => setSpeed(Math.max(0.1, Math.min(10, parseFloat(v) || 1)))}
						/>
					</div>
					<p class="field-hint" style={{ margin: "-4px 8px 8px" }}>
						1 = real time. Below 1 stretches it out (0.5 = half speed / twice as long), above 1 speeds it up.
					</p>
					<SelectField
						label="Scaling"
						hint="How the video maps onto the grid. Crop and Fit keep the aspect ratio."
						value={fit}
						options={[
							{ value: "crop", label: "Crop (fill, no distortion)" },
							{ value: "fit", label: "Fit (letterbox)" },
							{ value: "stretch", label: "Stretch (fill, distort)" }
						]}
						onChange={v => setFit(v as "stretch" | "crop" | "fit")}
					/>
					<div class="grid-2">
						<TextField
							label="Trim start (s)" type="number" min={0} max={duration} step={0.1} value={trimStart}
							onCommit={v => setTrimStart(Math.max(0, parseFloat(v) || 0))}
						/>
						<TextField
							label="Trim end (s)" type="number" min={0} max={Math.min(duration || 600, 600)} step={0.1}
							value={trimEnd}
							onCommit={v => setTrimEnd(Math.min(Math.min(duration || 600, 600), parseFloat(v) || 0))}
						/>
					</div>
					<div class="rows">
						<ToggleRow label="Loop" hint="Repeat the sequence." on={loop} onChange={setLoop} />
						<ToggleRow
							label="Ping-pong"
							hint="Play forward, then backward, when looping."
							on={reverse}
							onChange={setReverse}
						/>
						<ToggleRow
							label="Mirror on matrices"
							hint="Reflect the left half onto the right for a symmetric display."
							on={mirror}
							onChange={setMirror}
						/>
					</div>
					<p class="field-hint" style={{ margin: "8px" }}>
						~{frames} frames · plays {outDur.toFixed(1)}s · est. {estMb.toFixed(1)} MB
						{estMb > 60 && <span class="danger"> — large; consider a shorter clip or lower fps.</span>}
					</p>

					<div class="sheet-actions">
						<button class="btn small" onClick={onClose} disabled={busy}>Cancel</button>
						<button class="btn small primary" onClick={bake} disabled={busy || frames === 0}>
							{busy ? "Baking…" : "Bake & play"}
						</button>
					</div>
				</>
			)}
		</Sheet>
	);
}
