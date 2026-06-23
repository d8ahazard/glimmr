import { useEffect, useRef, useState } from "preact/hooks";
import { completeCalibrationSession, getCalibrationSession, submitCalibrationFrame } from "../lib/api";
import type { CalibrationSession } from "../types";

export function CalibratePhoneScreen({ sessionId }: { sessionId: string }) {
	const [session, setSession] = useState<CalibrationSession | null>(null);
	const [running, setRunning] = useState(false);
	const [error, setError] = useState("");
	const videoRef = useRef<HTMLVideoElement>(null);
	const canvasRef = useRef<HTMLCanvasElement>(null);

	useEffect(() => {
		void getCalibrationSession(sessionId).then(setSession);
	}, [sessionId]);

	useEffect(() => {
		if (!running) return;
		let timer: number | undefined;
		let stream: MediaStream | null = null;
		const run = async () => {
			try {
				stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" }, audio: false });
				const video = videoRef.current;
				if (!video) return;
				video.srcObject = stream;
				await video.play();
				timer = window.setInterval(() => {
					const c = canvasRef.current;
					if (!c || !video.videoWidth || !video.videoHeight) return;
					c.width = 640;
					c.height = 360;
					const ctx = c.getContext("2d");
					if (!ctx) return;
					ctx.drawImage(video, 0, 0, c.width, c.height);
					const dataUrl = c.toDataURL("image/jpeg", 0.65);
					void submitCalibrationFrame(sessionId, dataUrl);
				}, 750);
			} catch (e) {
				setError(`Camera error: ${String(e)}`);
				setRunning(false);
			}
		};
		void run();
		return () => {
			if (timer) clearInterval(timer);
			if (stream) stream.getTracks().forEach(t => t.stop());
		};
	}, [running, sessionId]);

	return (
		<div class="screen">
			<div class="screen-title">Phone Calibration</div>
			<p class="screen-sub">
				Session: <code>{sessionId}</code>
			</p>
			<div class="rows">
				<div class="row-item">
					<div class="row-body">
						<div class="row-title">Step 1</div>
						<div class="row-sub">Turn off ambient room lights and keep all fixtures visible in frame.</div>
					</div>
				</div>
				<div class="row-item">
					<div class="row-body">
						<div class="row-title">Step 2</div>
						<div class="row-sub">Point the camera at your TV + lights. Glimmr will flash a tracking pattern.</div>
					</div>
				</div>
				<div class="row-item">
					<div class="row-body">
						<div class="row-title">Step 3</div>
						<div class="row-sub">Keep steady for ~20-30s while frames upload for solving.</div>
					</div>
				</div>
			</div>
			<div class="preview-pair" style={{ gridTemplateColumns: "1fr" }}>
				<figure>
					<video ref={videoRef} playsInline muted style={{ width: "100%", borderRadius: "10px", background: "#000" }} />
					<figcaption>Live camera</figcaption>
				</figure>
			</div>
			<canvas ref={canvasRef} style={{ display: "none" }} />
			<div style={{ display: "flex", gap: "10px", margin: "0 4px", flexWrap: "wrap" }}>
				<button class="btn primary" onClick={() => setRunning(v => !v)}>
					{running ? "Stop streaming" : "Start streaming"}
				</button>
				<button class="btn" onClick={async () => {
					await completeCalibrationSession(sessionId);
					void getCalibrationSession(sessionId).then(setSession);
				}}>
					Finish calibration
				</button>
			</div>
			{session && (
				<p class="field-hint" style={{ margin: "10px 8px 0" }}>
					Status: <b>{session.status}</b> · Frames uploaded: <b>{session.frameCount}</b>
				</p>
			)}
			{error && <p class="field-hint danger" style={{ margin: "8px" }}>{error}</p>}
		</div>
	);
}
