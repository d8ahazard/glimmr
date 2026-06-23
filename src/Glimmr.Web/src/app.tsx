import { useState, useEffect } from "preact/hooks";
import { system } from "./lib/store";
import { TabBar, StatStrip, Toaster, OfflineBanner, type Tab } from "./components/chrome";
import { ControlScreen } from "./screens/Control";
import { ScenesScreen } from "./screens/Scenes";
import { SettingsScreen } from "./screens/Settings";
import { SetupWizard } from "./screens/Wizard";
import { PreviewStudio } from "./screens/PreviewStudio";
import { CalibratePhoneScreen } from "./screens/CalibratePhone";

export function App() {
	const [tab, setTab] = useState<Tab>("control");
	const [wizardOpen, setWizardOpen] = useState<boolean | null>(null);
	const [previewOpen, setPreviewOpen] = useState(false);
	const sd = system.value;
	const phoneCalibrationSession =
		typeof window !== "undefined"
			? new URLSearchParams(window.location.search).get("calibrate")
			: null;

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

	if (phoneCalibrationSession) {
		return (
			<>
				<CalibratePhoneScreen sessionId={phoneCalibrationSession} />
				<Toaster />
				<OfflineBanner />
			</>
		);
	}

	if (previewOpen) {
		return (
			<>
				<PreviewStudio onClose={() => setPreviewOpen(false)} />
				<Toaster />
				<OfflineBanner />
			</>
		);
	}

	return (
		<div class="shell">
			<TabBar tab={tab} onChange={setTab} />
			<main class="shell-main">
				{tab === "control" && <ControlScreen onOpenPreviewStudio={() => setPreviewOpen(true)} />}
				{tab === "scenes" && <ScenesScreen />}
				{tab === "settings" && <SettingsScreen onRunWizard={() => setWizardOpen(true)} />}
			</main>
			<StatStrip />
			<Toaster />
			<OfflineBanner />
		</div>
	);
}
