// Shapes mirror the camelCase JSON pushed by the Glimmr SignalR hub ("olo" store).

export interface KeyProperty {
	valueName: string;
	valueLabel: string;
	valueHint: string;
	valueType: string;
	valueMin: string;
	valueMax: string;
	valueStep: string;
	options: Record<string, string>;
}

export interface BeamSegment {
	position: number;
	offset: number;
	repeat: boolean;
	reverse: boolean;
	ledCount: number;
}

export interface WledSegment {
	offset: number;
	len: number;
	multiplier?: number;
	[key: string]: unknown;
}

export interface NanoPosition {
	panelId: number;
	x: number;
	y: number;
	o: number;
	shapeType: number;
	sideLength: number;
	targetSector: number;
}

export interface NanoLayout {
	sideLength: number;
	positionData: NanoPosition[];
}

export interface HueLightService {
	svcId: string;
	channel: number;
	name: string;
	type: string;
	targetSector: number;
	brightness: number;
	override: boolean;
	[key: string]: unknown;
}

export interface HueGroup {
	id: string;
	name: string;
	/** Hue V2 (entertainment API) light services. Absent on legacy V1 bridges. */
	services?: HueLightService[];
	/** Hue V1 group membership: light IDs belonging to this group. */
	lights?: string[];
	[key: string]: unknown;
}

/** Hue V1 light info (DeviceData.lights). */
export interface HueLightData {
	id: string;
	name: string;
	type?: string;
	hasStreaming?: boolean;
	[key: string]: unknown;
}

/** Hue V1 light-to-sector mapping (DeviceData.mappedLights). Brightness is 0-255. */
export interface HueLightMap {
	id: string;
	targetSector: number;
	brightness: number;
	override: boolean;
}

export interface DeviceData {
	id: string;
	name: string;
	tag: string;
	deviceTag?: string;
	ipAddress: string;
	enable: boolean;
	brightness: number;
	maxBrightness?: number;
	lastSeen: string;
	keyProperties?: KeyProperty[];
	// Mapping-related optional payloads
	ledCount?: number;
	multiZoneCount?: number;
	offset?: number;
	ledMultiplier?: number;
	stripMode?: number;
	reverseStrip?: boolean;
	targetSector?: number;
	// WLED 2D matrix (StripMode.Matrix). Width/height/wiring auto-detected from WLED.
	matrixWidth?: number;
	matrixHeight?: number;
	// 0 = Blend, 1 = Column, 2 = Mirror.
	matrixMode?: number;
	// 0 = Left, 1 = Right, 2 = Top, 3 = Bottom (screen edge the panel sits next to).
	matrixSide?: number;
	extendDepth?: number;
	extendFade?: number;
	flipHorizontal?: boolean;
	flipVertical?: boolean;
	serpentine?: boolean;
	vertical?: boolean;
	startCorner?: number;
	token?: string;
	appKey?: string;
	segments?: WledSegment[];
	beamLayout?: { segments: BeamSegment[] };
	layout?: NanoLayout;
	mirrorX?: boolean;
	mirrorY?: boolean;
	groups?: HueGroup[];
	lights?: HueLightData[];
	mappedLights?: HueLightMap[];
	selectedGroup?: string | number;
	hasMultiZone?: boolean;
	[key: string]: unknown;
}

export interface SystemData {
	deviceName: string;
	ipAddress: string;
	version: string;
	theme: string;
	units: number;
	timeZone: string;
	deviceMode: number;
	autoDisabled: boolean;
	captureMode: number;
	camType: number;
	usbSelection: number;
	recDev: string;
	enableLetterBox: boolean;
	enablePillarBox: boolean;
	cropBlackLevel: number;
	cropDelay: number;
	useCenter: boolean;
	leftCount: number;
	rightCount: number;
	topCount: number;
	bottomCount: number;
	hSectors: number;
	vSectors: number;
	matrixSampleWidth?: number;
	matrixSampleHeight?: number;
	ledCount: number;
	sectorCount: number;
	gammaCorrection: number;
	blackLevel: number;
	enableAutoBrightness: boolean;
	ablAmps: number;
	ablVolts: number;
	baudRate: number;
	audioGain: number;
	audioCutoff?: number;
	audioScene: number;
	audioRotationLower: number;
	audioRotationUpper: number;
	audioRotationSpeed: number;
	audioRotationTrigger: number;
	ambientScene: number;
	ambientColor: string;
	sceneId?: string;
	streamMode: number;
	dsIp: string;
	autoDiscoveryFrequency: number;
	discoveryTimeout: number;
	autoRemoveDevices: boolean;
	autoRemoveDevicesAfter: number;
	autoUpdate: boolean;
	autoUpdateTime: number;
	enableAutoDisable: boolean;
	autoDisableDelay: number;
	openRgbIp: string;
	openRgbPort: number;
	previewMode: number;
	logLevel: number;
	defaultSet: boolean;
	skipTour: boolean;
	skipDemo: boolean;
	hueSyncBridgeId?: string;
	hueSyncBridgeMac?: string;
	[key: string]: unknown;
}

export interface Scene {
	id: number;
	name: string;
	[key: string]: unknown;
}

/** A "Scenes" tab scene: a scripted particle animation or a baked video sequence. */
export interface SceneV2 {
	id: string;
	name: string;
	kind: "color" | "image" | "video" | "script";
	system: boolean;
	width: number;
	height: number;
	fps: number;
	loop: boolean;
	reverse: boolean;
	mirror: boolean;
	color?: string;
	script: string;
	framesFile: string;
	frameCount: number;
}

export interface StatData {
	cpuTemp: number;
	cpuUsage: number;
	memoryUsage: number;
	uptime: string;
	throttledState: string[];
	fps: Record<string, number>;
	[key: string]: unknown;
}

export interface StoreData {
	systemData?: SystemData;
	devices?: DeviceData[];
	devUsb?: Record<string, string>;
	devAudio?: { id: string;[key: string]: unknown }[];
	ambientScenes?: Scene[];
	audioScenes?: Scene[];
	scenes?: SceneV2[];
	stats?: StatData;
}

export interface HueSyncStatus {
	bridgeId: string;
	bridgeMac: string;
	armed: boolean;
	paired: boolean;
	streaming: boolean;
}

export interface LogLine {
	level: number;
	ts: string;
	caller: string;
	text: string;
}

export interface ToastMsg {
	id: number;
	title: string;
	body: string;
}

export interface AuthState {
	active: boolean;
	progress: number; // 0..1
	authorized: boolean;
	error: boolean;
}

export const MODES = [
	{ id: 0, name: "Off", icon: "power" },
	{ id: 1, name: "Video", icon: "video" },
	{ id: 2, name: "Audio", icon: "audio" },
	{ id: 4, name: "A/V", icon: "av" },
	{ id: 8, name: "Scenes", icon: "scenes" },
	{ id: 5, name: "Stream", icon: "stream" },
	{ id: 7, name: "Hue Sync", icon: "hue" }
] as const;
