import { useState } from "preact/hooks";
import {
	system, setSystemProps, sortedDevices, scanDevices, scanning, updateDevice
} from "../lib/store";
import { TextField, SelectField, Toggle, ToggleRow, CountCell } from "../components/ui";
import { LedMap, SectorMap } from "../components/maps";
import { LinkPane } from "../components/device/LinkPane";
import { deviceIcon } from "../components/device/DeviceSheet";

const STEPS = ["welcome", "name", "source", "leds", "sectors", "devices", "link", "done"] as const;

export function SetupWizard({ onDone }: { onDone: () => void }) {
	const [step, setStep] = useState(0);
	const sd = system.value;
	if (!sd) return null;

	const devs = sortedDevices.value;
	const linkable = devs.filter(
		d => (d.tag === "Hue" || d.tag === "HueV2" || d.tag === "Nanoleaf") && !d.token
	);

	const finish = () => {
		setSystemProps({ skipTour: true });
		onDone();
	};

	const next = () => {
		// Skip the link step entirely when nothing needs pairing.
		if (STEPS[step + 1] === "link" && linkable.length === 0) setStep(step + 2);
		else setStep(step + 1);
	};

	const back = () => {
		if (STEPS[step - 1] === "link" && linkable.length === 0) setStep(step - 2);
		else setStep(step - 1);
	};

	const id = STEPS[step];

	return (
		<div class="wizard">
			<div class="wizard-inner">
				<div class="wizard-step-label">Setup · {step + 1}/{STEPS.length}</div>

				{id === "welcome" && (
					<>
						<h1>Welcome to Glimmr</h1>
						<p class="lead">
							Let's get your room glowing. This quick setup walks through naming your
							system, picking a video source, sizing your LED grid and finding your devices.
							You can change everything later.
						</p>
					</>
				)}

				{id === "name" && (
					<>
						<h1>Name your system</h1>
						<p class="lead">This is how Glimmr shows up on your network and in the app.</p>
						<TextField
							label="Device Name"
							value={sd.deviceName}
							onCommit={v => setSystemProps({ deviceName: v })}
						/>
					</>
				)}

				{id === "source" && (
					<>
						<h1>What should Glimmr watch?</h1>
						<p class="lead">Pick where the colors come from when you're in Video mode.</p>
						<SelectField
							label="Input Source"
							value={sd.captureMode}
							options={[
								{ value: 1, label: "Camera (pointed at the screen)" },
								{ value: 2, label: "HDMI capture device" },
								{ value: 3, label: "Screen capture (this machine)" }
							]}
							onChange={v => setSystemProps({ captureMode: parseInt(v) })}
						/>
						{sd.captureMode === 1 && (
							<SelectField
								label="Camera Type"
								value={sd.camType}
								options={[
									{ value: 0, label: "Raspberry Pi Camera" },
									{ value: 1, label: "USB Web Camera" }
								]}
								onChange={v => setSystemProps({ camType: parseInt(v) })}
							/>
						)}
					</>
				)}

				{id === "leds" && (
					<>
						<h1>Size your LED grid</h1>
						<p class="lead">
							How many LEDs run along each edge of your screen? This master grid is what
							every device maps onto.
						</p>
						<div class="count-grid">
							<CountCell label="Top" value={sd.topCount} onCommit={v => setSystemProps({ topCount: v })} />
							<CountCell label="Bottom" value={sd.bottomCount} onCommit={v => setSystemProps({ bottomCount: v })} />
							<CountCell label="Left" value={sd.leftCount} onCommit={v => setSystemProps({ leftCount: v })} />
							<CountCell label="Right" value={sd.rightCount} onCommit={v => setSystemProps({ rightCount: v })} />
						</div>
						<LedMap caption={`${sd.ledCount} LEDs total.`} />
					</>
				)}

				{id === "sectors" && (
					<>
						<h1>Slice up the screen</h1>
						<p class="lead">
							Sectors are the zones single-color devices (bulbs, panels) can mirror.
						</p>
						<div class="count-grid">
							<CountCell label="Horiz" value={sd.hSectors} onCommit={v => setSystemProps({ hSectors: v })} />
							<CountCell label="Vert" value={sd.vSectors} onCommit={v => setSystemProps({ vSectors: v })} />
						</div>
						<div class="rows">
							<ToggleRow
								label="Include center"
								hint="Sample the middle of the screen too."
								on={sd.useCenter}
								onChange={v => setSystemProps({ useCenter: v })}
							/>
						</div>
						<SectorMap caption={`${sd.sectorCount} sectors.`} />
					</>
				)}

				{id === "devices" && (
					<>
						<h1>Find your lights</h1>
						<p class="lead">
							Scan the network, then flip on the devices you want Glimmr to drive.
						</p>
						<button class="btn primary" disabled={scanning.value} onClick={scanDevices}>
							{scanning.value ? "Scanning…" : devs.length ? "Scan again" : "Scan network"}
						</button>
						<div class="rows mt">
							{devs.length === 0 && (
								<div class="row-item">
									<div class="row-body muted">
										{scanning.value ? "Looking for devices…" : "No devices found yet."}
									</div>
								</div>
							)}
							{devs.map(dev => (
								<div key={dev.id} class="row-item">
									<div class="row-icon"><img src={deviceIcon(dev)} alt="" /></div>
									<div class="row-body">
										<div class="row-title">{dev.name}</div>
										<div class="row-sub">{dev.ipAddress}</div>
									</div>
									<div class="row-trail">
										<Toggle
											on={dev.enable}
											onChange={v => updateDevice({ ...dev, enable: v })}
											label={`Enable ${dev.name}`}
										/>
									</div>
								</div>
							))}
						</div>
					</>
				)}

				{id === "link" && (
					<>
						<h1>Pair your bridges</h1>
						<p class="lead">
							These devices need a one-time pairing handshake before Glimmr can stream to them.
						</p>
						{linkable.map(dev => (
							<div key={dev.id}>
								<div class="section-label">{dev.name}</div>
								<LinkPane
									deviceId={dev.id}
									type={dev.tag === "Nanoleaf" ? "nanoleaf" : "hue"}
									linked={!!dev.token}
								/>
							</div>
						))}
						{linkable.length === 0 && <p class="lead">All linked — nothing to do here.</p>}
					</>
				)}

				{id === "done" && (
					<>
						<h1>You're glowing</h1>
						<p class="lead">
							Setup is complete. Pick a mode on the Control tab and enjoy the show.
							Fine-tune device mappings any time from the Devices tab.
						</p>
					</>
				)}

				<div class="wizard-dots">
					{STEPS.map((_, i) => <i key={i} class={i === step ? "on" : ""} />)}
				</div>
			</div>

			<div class="wizard-nav">
				{step > 0 && step < STEPS.length - 1 && (
					<button class="btn" onClick={back}>Back</button>
				)}
				{step === 0 && (
					<button class="btn" onClick={finish}>Skip</button>
				)}
				{step < STEPS.length - 1 ? (
					<button class="btn primary" onClick={next}>
						{step === 0 ? "Get started" : "Next"}
					</button>
				) : (
					<button class="btn primary" onClick={finish}>Done</button>
				)}
			</div>
		</div>
	);
}
