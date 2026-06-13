import { defineConfig } from "vite";
import preact from "@preact/preset-vite";

// The built app is served by the Glimmr ASP.NET host from wwwroot/app.
// During development, run the Glimmr backend and `npm run dev`; API and
// socket traffic is proxied to the local instance (override with GLIMMR_HOST).
const backend = process.env.GLIMMR_HOST ?? "http://localhost:80";

export default defineConfig({
	plugins: [preact()],
	base: "/app/",
	build: {
		outDir: "../Glimmr/wwwroot/app",
		emptyOutDir: true
	},
	server: {
		proxy: {
			"/api": { target: backend, changeOrigin: true },
			"/img": { target: backend, changeOrigin: true },
			"/socket": { target: backend, ws: true, changeOrigin: true }
		}
	}
});
