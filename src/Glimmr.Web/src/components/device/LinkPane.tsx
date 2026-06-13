import { authState, authorizeDevice } from "../../lib/store";

const HINTS: Record<string, string> = {
	hue: "Press the link button on your Hue Bridge, then tap Link.",
	nanoleaf: "Hold the power button on your panels for ~5 seconds until the LED flashes, then tap Link."
};

/** Device pairing pane with live progress ring driven by hub "auth" events. */
export function LinkPane({ deviceId, type, linked }: { deviceId: string; type: "hue" | "nanoleaf"; linked: boolean }) {
	const auth = authState.value;
	const r = 19;
	const circ = 2 * Math.PI * r;

	return (
		<div class={"linkpane" + (linked ? " linked" : "")}>
			<div class="link-ring">
				<svg width="46" height="46" viewBox="0 0 46 46">
					<circle class="track" cx="23" cy="23" r={r} />
					{auth.active && (
						// Rotate just the arc so progress starts at 12 o'clock without
						// tipping over anything else drawn in this SVG (the checkmark).
						<circle
							class="prog"
							cx="23"
							cy="23"
							r={r}
							transform="rotate(-90 23 23)"
							stroke-dasharray={circ}
							stroke-dashoffset={circ * (1 - auth.progress)}
						/>
					)}
					{linked && (
						<path d="M14 23.5l6 6 12-12" fill="none" stroke="var(--ok)" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
					)}
				</svg>
			</div>
			<div class="row-body">
				<div class="row-title">{linked ? "Linked" : "Not linked"}</div>
				<div class="row-sub" style={{ whiteSpace: "normal" }}>
					{linked
						? "This device is paired and ready to stream."
						: auth.active
							? "Waiting for the device… complete the pairing step now."
							: auth.error
								? "Pairing failed — try again."
								: HINTS[type]}
				</div>
			</div>
			{!linked && (
				<button class="btn primary small" disabled={auth.active} onClick={() => authorizeDevice(deviceId)}>
					{auth.active ? "Linking…" : "Link"}
				</button>
			)}
		</div>
	);
}
