import { useState, useEffect } from "preact/hooks";
import { system } from "./lib/store";
import { TabBar, StatStrip, Toaster, OfflineBanner, type Tab } from "./components/chrome";
import { ControlScreen } from "./screens/Control";
import { DevicesScreen } from "./screens/Devices";
import { CaptureScreen } from "./screens/Capture";
import { SettingsScreen } from "./screens/Settings";
import { SetupWizard } from "./screens/Wizard";

export function App() {
	const [tab, setTab] = useState<Tab>("control");
	const [wizardOpen, setWizardOpen] = useState<boolean | null>(null);
	const sd = system.value;

	// First run: open the wizard once the store hydrates and skipTour is unset.
	useEffect(() => {
		if (wizardOpen === null && sd) {
			setWizardOpen(!sd.skipTour);
		}
	}, [sd, wizardOpen]);

	if (!sd) {
		return (
			<div class="shell" style={{ alignItems: "center", justifyContent: "center" }}>
				<div class="hero" style={{ textAlign: "center" }}>
					<div class="hero-glow" />
					<div class="hero-mode">Glimmr</div>
					<div class="muted tiny mt">Connecting…</div>
				</div>
			</div>
		);
	}

	if (wizardOpen) {
		return (
			<>
				<SetupWizard onDone={() => setWizardOpen(false)} />
				<Toaster />
				<OfflineBanner />
			</>
		);
	}

	return (
		<div class="shell">
			<TabBar tab={tab} onChange={setTab} />
			<main class="shell-main">
				{tab === "control" && <ControlScreen />}
				{tab === "devices" && <DevicesScreen />}
				{tab === "capture" && <CaptureScreen />}
				{tab === "settings" && <SettingsScreen onRunWizard={() => setWizardOpen(true)} />}
			</main>
			<StatStrip />
			<Toaster />
			<OfflineBanner />
		</div>
	);
}
