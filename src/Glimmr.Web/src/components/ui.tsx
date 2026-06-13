import { useEffect, useRef, useState } from "preact/hooks";
import type { ComponentChildren, JSX } from "preact";
import { IconClose } from "./icons";

// ---------------------------------------------------------------------------
// Toggle
// ---------------------------------------------------------------------------

export function Toggle({ on, onChange, label }: { on: boolean; onChange: (v: boolean) => void; label?: string }) {
	return (
		<button
			class={"toggle" + (on ? " on" : "")}
			role="switch"
			aria-checked={on}
			aria-label={label}
			onClick={e => {
				e.stopPropagation();
				onChange(!on);
			}}
		/>
	);
}

// ---------------------------------------------------------------------------
// Slider — live track fill, commits value on release
// ---------------------------------------------------------------------------

export function Slider({
	value, min = 0, max = 100, step = 1, onCommit, onInput, label
}: {
	value: number;
	min?: number;
	max?: number;
	step?: number;
	onCommit: (v: number) => void;
	onInput?: (v: number) => void;
	label?: string;
}) {
	const [local, setLocal] = useState(value);
	const dragging = useRef(false);
	useEffect(() => {
		if (!dragging.current) setLocal(value);
	}, [value]);
	const fill = max > min ? ((local - min) / (max - min)) * 100 : 0;
	return (
		<input
			type="range"
			class="slider"
			aria-label={label}
			min={min}
			max={max}
			step={step}
			value={local}
			style={{ "--fill": `${fill}%` } as JSX.CSSProperties}
			onInput={e => {
				dragging.current = true;
				const v = parseFloat((e.target as HTMLInputElement).value);
				setLocal(v);
				onInput?.(v);
			}}
			onChange={e => {
				dragging.current = false;
				onCommit(parseFloat((e.target as HTMLInputElement).value));
			}}
		/>
	);
}

// ---------------------------------------------------------------------------
// Form fields
// ---------------------------------------------------------------------------

export function Field({ label, hint, children }: { label?: string; hint?: string; children: ComponentChildren }) {
	return (
		<div class="field">
			{label && <label class="field-label">{label}</label>}
			{children}
			{hint && <div class="field-hint">{hint}</div>}
		</div>
	);
}

export function SelectField({
	label, hint, value, options, onChange, allowEmpty
}: {
	label?: string;
	hint?: string;
	value: string | number;
	options: { value: string | number; label: string }[];
	onChange: (v: string) => void;
	allowEmpty?: boolean;
}) {
	return (
		<Field label={label} hint={hint}>
			<select
				class="select"
				value={String(value)}
				onChange={e => onChange((e.target as HTMLSelectElement).value)}
			>
				{allowEmpty && <option value="">—</option>}
				{options.map(o => (
					<option key={o.value} value={String(o.value)}>{o.label}</option>
				))}
			</select>
		</Field>
	);
}

export function TextField({
	label, hint, value, onCommit, type = "text", min, max, step, placeholder
}: {
	label?: string;
	hint?: string;
	value: string | number;
	onCommit: (v: string) => void;
	type?: string;
	min?: string | number;
	max?: string | number;
	step?: string | number;
	placeholder?: string;
}) {
	const [local, setLocal] = useState(String(value ?? ""));
	const focused = useRef(false);
	useEffect(() => {
		if (!focused.current) setLocal(String(value ?? ""));
	}, [value]);
	return (
		<Field label={label} hint={hint}>
			<input
				class="input"
				type={type}
				min={min}
				max={max}
				step={step}
				placeholder={placeholder}
				value={local}
				onFocus={() => { focused.current = true; }}
				onInput={e => setLocal((e.target as HTMLInputElement).value)}
				onBlur={() => {
					focused.current = false;
					if (local !== String(value ?? "")) onCommit(local);
				}}
				onKeyDown={e => {
					if (e.key === "Enter") (e.target as HTMLInputElement).blur();
				}}
			/>
		</Field>
	);
}

/**
 * Numeric grid-count input. Edits are local while typing; commits are debounced
 * (spinner clicks batch up) and flushed immediately on blur/Enter so a full
 * number like "150" is sent as one update instead of one per keystroke.
 */
export function CountCell({ label, value, onCommit }: { label: string; value: number; onCommit: (v: number) => void }) {
	const [local, setLocal] = useState(String(value));
	const focused = useRef(false);
	const timer = useRef<number | undefined>(undefined);
	const latest = useRef(onCommit);
	latest.current = onCommit;

	useEffect(() => {
		if (!focused.current) setLocal(String(value));
	}, [value]);
	useEffect(() => () => window.clearTimeout(timer.current), []);

	const commit = (raw: string) => {
		const v = parseInt(raw);
		if (!Number.isNaN(v) && v >= 0 && v !== value) latest.current(v);
	};

	return (
		<div class="count-cell">
			<label>{label}</label>
			<input
				class="input"
				type="number"
				min="0"
				value={local}
				onFocus={() => { focused.current = true; }}
				onInput={e => {
					const raw = (e.target as HTMLInputElement).value;
					setLocal(raw);
					window.clearTimeout(timer.current);
					timer.current = window.setTimeout(() => commit(raw), 600);
				}}
				onBlur={e => {
					focused.current = false;
					window.clearTimeout(timer.current);
					commit((e.target as HTMLInputElement).value);
				}}
				onKeyDown={e => {
					if (e.key === "Enter") (e.target as HTMLInputElement).blur();
				}}
			/>
		</div>
	);
}

export function ToggleRow({
	label, hint, on, onChange
}: { label: string; hint?: string; on: boolean; onChange: (v: boolean) => void }) {
	return (
		<div class="row-item">
			<div class="row-body">
				<div class="row-title">{label}</div>
				{hint && <div class="row-sub" style={{ whiteSpace: "normal" }}>{hint}</div>}
			</div>
			<div class="row-trail">
				<Toggle on={on} onChange={onChange} label={label} />
			</div>
		</div>
	);
}

// ---------------------------------------------------------------------------
// Sheet (bottom sheet / side panel)
// ---------------------------------------------------------------------------

export function Sheet({
	title, hideTitle, onClose, children, head
}: {
	/** Accessible name for the dialog (always required, even when visually hidden). */
	title: string;
	/** Render the header as a bare close bar without the title text. */
	hideTitle?: boolean;
	onClose: () => void;
	children: ComponentChildren;
	head?: ComponentChildren;
}) {
	useEffect(() => {
		const onKey = (e: KeyboardEvent) => {
			if (e.key === "Escape") onClose();
		};
		document.addEventListener("keydown", onKey);
		document.body.style.overflow = "hidden";
		return () => {
			document.removeEventListener("keydown", onKey);
			document.body.style.overflow = "";
		};
	}, [onClose]);

	// NOTE: the sheet relies on position:fixed centering against the VIEWPORT.
	// No ancestor of a Sheet may retain a transform/filter/backdrop-filter
	// (they'd become the containing block) - see the .screen animation rule.
	return (
		<>
			<div class="sheet-backdrop" onClick={onClose} />
			<div class="sheet" role="dialog" aria-label={title}>
				<div class="sheet-grab" />
				<div class="sheet-head">
					{hideTitle
						? <div style={{ flex: 1 }} />
						: <div class="sheet-title">{title}</div>}
					{head}
					<button class="sheet-close" onClick={onClose} aria-label="Close">
						<IconClose size={16} />
					</button>
				</div>
				<div class="sheet-body">{children}</div>
			</div>
		</>
	);
}

// ---------------------------------------------------------------------------
// Confirm helper (native confirm keeps it simple and accessible)
// ---------------------------------------------------------------------------

export function confirmAction(message: string): boolean {
	return window.confirm(message);
}
