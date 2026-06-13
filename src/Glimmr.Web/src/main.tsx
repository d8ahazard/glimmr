import { render } from "preact";
import { App } from "./app";
import { connect } from "./lib/store";
import { startAccentSync } from "./lib/accent";
import "./styles.css";

startAccentSync();
connect();

render(<App />, document.getElementById("app")!);
