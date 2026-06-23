import type { Signal } from "@preact/signals";

interface PreviewImageProps {
	signal: Signal<string | null>;
	alt: string;
	fallbackSrc?: string;
	className?: string;
}

export function PreviewImage({
	signal,
	alt,
	fallbackSrc = "/img/no_preview.jpg",
	className
}: PreviewImageProps) {
	const src = signal.value ?? fallbackSrc;
	return <img src={src} alt={alt} class={className} />;
}
