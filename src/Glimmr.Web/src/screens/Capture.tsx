import { useEffect } from "preact/hooks";
import {
	system, setSystemProps, usbDevices, audioDevices,
	inputImage, outputImage, setPreviewsActive
} from "../lib/store";
import { SelectField, TextField, ToggleRow, CountCell } from "../components/ui";
import { LedMap, SectorMap } from "../components/maps";

export function CaptureScreen() {
	const sd = system.value;

	// Tell the backend to stream preview frames while this screen is visible.
	useEffect(() => {
		setPreviewsActive(true);
		return () => setPreviewsActive(false);
	}, []);

	if (!sd) return <div class="screen" />;

	const usb = usbDevices.value;
	const showUsb = sd.captureMode === 2 || (sd.captureMode === 1 && sd.camType === 1);

	return (
		<div class="screen">
			<div class="screen-title">Capture</div>
			<p class="screen-sub">What Glimmr watches and how it carves the screen into light.</p>

			<div class="section-label">Preview</div>
			<div class="preview-pair">
				<figure>
					<img src={inputImage.value ?? "/img/no_preview.jpg"} alt="Input preview" />
					<figcaption>Input</figcaption>
				</figure>
				<figure>
					<img src={outputImage.value ?? "/img/no_preview.jpg"} alt="Output preview" />
					<figcaption>Output</figcaption>
				</figure>
			</div>
			<SelectField
				label="Preview Overlay"
				value={[0, 1, 2].includes(sd.previewMode) ? sd.previewMode : 0}
				options={[
					{ value: 0, label: "None" },
					{ value: 1, label: "LEDs" },
					{ value: 2, label: "Sectors" }
				]}
				onChange={v => setSystemProps({ previewMode: parseInt(v) })}
			/>

			<div class="section-label">Video input</div>
			<SelectField
				label="Input Source"
				value={sd.captureMode}
				options={[
					{ value: 1, label: "Camera" },
					{ value: 2, label: "HDMI Capture" },
					{ value: 3, label: "Screen" }
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
			{showUsb && (
				<SelectField
					label="USB Device"
					value={sd.usbSelection}
					allowEmpty
					options={Object.entries(usb).map(([k, v]) => ({ value: k, label: String(v) }))}
					onChange={v => setSystemProps({ usbSelection: parseInt(v) || 0 })}
				/>
			)}
			<div class="grid-2">
				<TextField
					label="Black Level"
					hint="Minimum brightness for colors to display. Default 7."
					type="number" min={0} max={255} step={1}
					value={sd.blackLevel}
					onCommit={v => setSystemProps({ blackLevel: parseInt(v) || 0 })}
				/>
				<TextField
					label="Gamma"
					hint="Gamma correction for wired LEDs (1.0–4.0)."
					type="number" min={1} max={4} step={0.1}
					value={sd.gammaCorrection}
					onCommit={v => setSystemProps({ gammaCorrection: parseFloat(v) || 1 })}
				/>
			</div>

			<div class="section-label">Audio input</div>
			<SelectField
				label="Input Device"
				value={sd.recDev ?? ""}
				allowEmpty
				options={audioDevices.value.map(d => ({ value: d.id, label: d.id }))}
				onChange={v => setSystemProps({ recDev: v })}
			/>

			<div class="section-label">Cropping</div>
			<div class="rows">
				<ToggleRow
					label="Letterbox cropping"
					hint="Crop black bars from the top and bottom."
					on={sd.enableLetterBox}
					onChange={v => setSystemProps({ enableLetterBox: v })}
				/>
				<ToggleRow
					label="Pillarbox cropping"
					hint="Crop black bars from the left and right."
					on={sd.enablePillarBox}
					onChange={v => setSystemProps({ enablePillarBox: v })}
				/>
			</div>
			<div class="grid-2 mt">
				<TextField
					label="Cropping Delay"
					hint="Seconds to wait before cropping."
					type="number" min={1} max={60} step={1}
					value={sd.cropDelay}
					onCommit={v => setSystemProps({ cropDelay: parseInt(v) || 1 })}
				/>
				<TextField
					label="Crop Black Level"
					hint="Non-black pixels allowed per line."
					type="number" min={8} max={40} step={1}
					value={sd.cropBlackLevel}
					onCommit={v => setSystemProps({ cropBlackLevel: parseInt(v) || 8 })}
				/>
			</div>

			<div class="section-label">LED grid</div>
			<p class="field-hint" style={{ margin: "0 8px 10px" }}>
				Total LEDs around the perimeter of your screen — the master grid every device maps onto.
			</p>
			<div class="count-grid">
				<CountCell label="Top" value={sd.topCount} onCommit={v => setSystemProps({ topCount: v })} />
				<CountCell label="Bottom" value={sd.bottomCount} onCommit={v => setSystemProps({ bottomCount: v })} />
				<CountCell label="Left" value={sd.leftCount} onCommit={v => setSystemProps({ leftCount: v })} />
				<CountCell label="Right" value={sd.rightCount} onCommit={v => setSystemProps({ rightCount: v })} />
			</div>
			<LedMap caption={`${sd.ledCount} LEDs total — tap one to flash it.`} />

			<div class="section-label">Sector grid</div>
			<p class="field-hint" style={{ margin: "0 8px 10px" }}>
				Sectors for single-color devices like bulbs and panels. Re-map devices after changing.
			</p>
			<div class="count-grid">
				<CountCell label="Horiz" value={sd.hSectors} onCommit={v => setSystemProps({ hSectors: v })} />
				<CountCell label="Vert" value={sd.vSectors} onCommit={v => setSystemProps({ vSectors: v })} />
			</div>
			<div class="rows">
				<ToggleRow
					label="Include center"
					hint="Sample the middle of the screen too, not just the edges."
					on={sd.useCenter}
					onChange={v => setSystemProps({ useCenter: v })}
				/>
			</div>
			<SectorMap caption={`${sd.sectorCount} sectors — tap one to flash it.`} />
		</div>
	);
}
