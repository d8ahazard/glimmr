import type { RoomMapEmitter, Vec3 } from "../../types";

interface EmitterEditorProps {
	emitters: RoomMapEmitter[];
	selectedId?: string | null;
	onSelect: (id: string) => void;
	onUpdatePosition: (id: string, position: Vec3) => void;
	onFlash: (id: string) => void;
}

function num(v: number) {
	return Number.isFinite(v) ? v.toFixed(2) : "0.00";
}

export function EmitterEditor({
	emitters,
	selectedId,
	onSelect,
	onUpdatePosition,
	onFlash
}: EmitterEditorProps) {
	if (emitters.length === 0) {
		return <div class="row-item"><div class="row-body muted">No emitters mapped yet.</div></div>;
	}

	return (
		<div class="rows">
			{emitters.map(e => (
				<div
					key={e.id}
					class={"row-item tappable" + (e.id === selectedId ? " picking" : "")}
					onClick={() => onSelect(e.id)}
				>
					<div class="row-body">
						<div class="row-title">{e.deviceId || e.id}</div>
						<div class="row-sub">{e.providerId} · conf {Math.round((e.confidence ?? 0) * 100)}%</div>
						<div class="grid-2" style={{ marginTop: "8px" }}>
							<input
								class="input"
								type="number"
								step="0.1"
								value={num(e.position.x)}
								onClick={ev => ev.stopPropagation()}
								onChange={ev => onUpdatePosition(e.id, {
									...e.position,
									x: parseFloat((ev.currentTarget as HTMLInputElement).value) || 0
								})}
							/>
							<input
								class="input"
								type="number"
								step="0.1"
								value={num(e.position.y)}
								onClick={ev => ev.stopPropagation()}
								onChange={ev => onUpdatePosition(e.id, {
									...e.position,
									y: parseFloat((ev.currentTarget as HTMLInputElement).value) || 0
								})}
							/>
						</div>
						<input
							class="input mt"
							type="number"
							step="0.1"
							value={num(e.position.z)}
							onClick={ev => ev.stopPropagation()}
							onChange={ev => onUpdatePosition(e.id, {
								...e.position,
								z: parseFloat((ev.currentTarget as HTMLInputElement).value) || 0
							})}
						/>
					</div>
					<div class="row-trail" onClick={ev => ev.stopPropagation()}>
						<button class="btn small" onClick={() => onFlash(e.id)}>Flash</button>
					</div>
				</div>
			))}
		</div>
	);
}
