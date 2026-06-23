import { useEffect, useRef } from "preact/hooks";
import * as THREE from "three";
import type { RoomMapEmitter } from "../../types";

interface RoomScene3DProps {
	emitters: RoomMapEmitter[];
	selectedEmitterId?: string | null;
	onSelect?: (id: string) => void;
}

export function RoomScene3D({ emitters, selectedEmitterId, onSelect }: RoomScene3DProps) {
	const hostRef = useRef<HTMLDivElement>(null);
	const onSelectRef = useRef<typeof onSelect>();
	const selectedIdRef = useRef<string | null>(selectedEmitterId ?? null);
	const sceneRef = useRef<THREE.Scene | null>(null);
	const normalMaterialRef = useRef<THREE.MeshBasicMaterial | null>(null);
	const highlightMaterialRef = useRef<THREE.MeshBasicMaterial | null>(null);
	const nodesRef = useRef<Map<string, THREE.Mesh>>(new Map());
	const rafRef = useRef(0);

	onSelectRef.current = onSelect;
	selectedIdRef.current = selectedEmitterId ?? null;

	useEffect(() => {
		const host = hostRef.current;
		if (!host) return;

		const scene = new THREE.Scene();
		scene.background = new THREE.Color(0x09090c);

		const width = host.clientWidth || 640;
		const height = host.clientHeight || 360;
		const camera = new THREE.PerspectiveCamera(55, width / height, 0.1, 200);
		camera.position.set(0, 1.4, 4.2);

		const renderer = new THREE.WebGLRenderer({ antialias: true });
		renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
		renderer.setSize(width, height);
		host.appendChild(renderer.domElement);

		const room = new THREE.Mesh(
			new THREE.BoxGeometry(6, 3, 6),
			new THREE.MeshBasicMaterial({ color: 0x22242c, wireframe: true })
		);
		room.position.set(0, 1.5, 0);
		scene.add(room);

		const tv = new THREE.Mesh(
			new THREE.PlaneGeometry(2.4, 1.35),
			new THREE.MeshBasicMaterial({ color: 0x10131f, side: THREE.DoubleSide })
		);
		tv.position.set(0, 1.3, -2.2);
		scene.add(tv);

		const highlightMaterial = new THREE.MeshBasicMaterial({ color: 0x39d4ff });
		const normalMaterial = new THREE.MeshBasicMaterial({ color: 0x8b94aa });
		normalMaterialRef.current = normalMaterial;
		highlightMaterialRef.current = highlightMaterial;
		const nodes = nodesRef.current;
		nodes.clear();

		const light = new THREE.DirectionalLight(0xffffff, 0.7);
		light.position.set(2, 4, 3);
		scene.add(light);
		scene.add(new THREE.AmbientLight(0xffffff, 0.3));

		const raycaster = new THREE.Raycaster();
		const pointer = new THREE.Vector2();
		const onClick = (ev: MouseEvent) => {
			if (!onSelect) return;
			const rect = renderer.domElement.getBoundingClientRect();
			pointer.x = ((ev.clientX - rect.left) / rect.width) * 2 - 1;
			pointer.y = -((ev.clientY - rect.top) / rect.height) * 2 + 1;
			raycaster.setFromCamera(pointer, camera);
			const meshes = [...nodes.values()];
			const hits = raycaster.intersectObjects(meshes);
			if (hits.length === 0) return;
			for (const [id, mesh] of nodes) {
				if (mesh === hits[0].object) {
					onSelectRef.current?.(id);
					return;
				}
			}
		};
		renderer.domElement.addEventListener("click", onClick);

		const renderLoop = () => {
			rafRef.current = requestAnimationFrame(renderLoop);
			if (document.hidden) {
				return;
			}

			room.rotation.y += 0.0015;
			renderer.render(scene, camera);
		};
		renderLoop();

		const onResize = () => {
			const w = host.clientWidth || 640;
			const h = host.clientHeight || 360;
			camera.aspect = w / h;
			camera.updateProjectionMatrix();
			renderer.setSize(w, h);
		};
		window.addEventListener("resize", onResize);
		sceneRef.current = scene;

		return () => {
			window.removeEventListener("resize", onResize);
			renderer.domElement.removeEventListener("click", onClick);
			cancelAnimationFrame(rafRef.current);
			for (const mesh of nodesRef.current.values()) {
				scene.remove(mesh);
				mesh.geometry.dispose();
			}
			nodesRef.current.clear();
			highlightMaterial.dispose();
			normalMaterial.dispose();
			renderer.dispose();
			sceneRef.current = null;
			highlightMaterialRef.current = null;
			normalMaterialRef.current = null;
			host.innerHTML = "";
		};
	}, []);

	useEffect(() => {
		const scene = sceneRef.current;
		const normalMaterial = normalMaterialRef.current;
		const highlightMaterial = highlightMaterialRef.current;
		if (!scene || !normalMaterial || !highlightMaterial) return;

		const nodes = nodesRef.current;
		const nextIds = new Set(emitters.map(e => e.id));

		for (const [id, mesh] of nodes) {
			if (nextIds.has(id)) continue;
			scene.remove(mesh);
			mesh.geometry.dispose();
			nodes.delete(id);
		}

		for (const emitter of emitters) {
			let mesh = nodes.get(emitter.id);
			if (!mesh) {
				mesh = new THREE.Mesh(new THREE.SphereGeometry(0.07, 12, 12), normalMaterial);
				scene.add(mesh);
				nodes.set(emitter.id, mesh);
			}

			mesh.position.set(emitter.position.x, emitter.position.y, emitter.position.z);
			mesh.material = emitter.id === selectedIdRef.current ? highlightMaterial : normalMaterial;
		}
	}, [emitters]);

	useEffect(() => {
		const normalMaterial = normalMaterialRef.current;
		const highlightMaterial = highlightMaterialRef.current;
		if (!normalMaterial || !highlightMaterial) return;

		for (const [id, mesh] of nodesRef.current) {
			mesh.material = id === selectedEmitterId ? highlightMaterial : normalMaterial;
		}
	}, [selectedEmitterId]);

	return <div class="room-preview-canvas" ref={hostRef} />;
}
