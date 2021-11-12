let socketLoaded = false;
let loadTimeout;
// Row for settings and device cards divs
let settingsRow;
let cardRow;
// Settings content elements
let settingsTab;
let settingsContent;
let newColor;
let mode;
// Is a device card expanded?
let expanded = false;
// Do we have a LED map?
let drawLedMap = false;
// Do we have a Sector map?
let drawSectorMap = false;
// Is our settings window currently open?
let settingsShown = false;
// This is the data for the currently shown device in settings
let deviceData;
let listenersSet;
let cardClone;
let baseCard;
let closeButton;
let toggleWidth = 0;
let toggleHeight = 0;
let toggleLeft = 0;
let toggleTop = 0;
let posting = false;
let loading = false;
let baseUrl;
let pickr;
let bar;
let leftCount, rightCount, topCount, bottomCount, hSectors, vSectors;
let useCenter;
let refreshTimer;
let fpsCounter;
let nanoTarget, nanoSector, nanoModal;
let demoLoaded = false;
let reload = false;
let toLoad = null;
let devTimeout = null;
let ledData = '{"AutoBrightnessLevel":true,"FixGamma":true,"AblMaxMilliamps":5000,"GpioNumber":18,"LedCount":150,"MilliampsPerLed":25,"Offset":50,"StartupAnimation":0,"StripType":0,"Name":"Demo LED Strip","Id":"-1","Tag":"Led","IpAddress":"","Brightness":100,"Enable":false,"LastSeen":"08/05/2021 13:28:53","KeyProperties":[{"Options":{},"ValueLabel":"","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"ledmap","ValueStep":"1","ValueType":"ledmap"},{"Options":{},"ValueLabel":"Led Count","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"LedCount","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"Led Offset","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"Offset","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"LED Multiplier","ValueHint":"Positive values to multiply (skip), negative values to divide (duplicate).","ValueMax":"5","ValueMin":"-5","ValueName":"LedMultiplier","ValueStep":"1","ValueType":"number"},{"Options":{},"ValueLabel":"Reverse Strip","ValueHint":"Reverse the order of the leds to clockwise (facing screen).","ValueMax":"100","ValueMin":"0","ValueName":"ReverseStrip","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Fix Gamma","ValueHint":"Automatically correct Gamma (recommended)","ValueMax":"100","ValueMin":"0","ValueName":"FixGamma","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Enable Auto Brightness","ValueHint":"Automatically adjust brightness to avoid dropouts.","ValueMax":"100","ValueMin":"0","ValueName":"AutoBrightnessLevel","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Milliamps Per LED","ValueHint":"\'Conservative\' = 25, \'Normal\' = 55","ValueMax":"100","ValueMin":"0","ValueName":"MilliampsPerLed","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"Power Supply Voltage","ValueHint":"Total PSU voltage in Milliamps","ValueMax":"100","ValueMin":"0","ValueName":"AblMaxMilliamps","ValueStep":"1","ValueType":"text"}]}';
let logEl = document.getElementById("logModal");
let errModal = new bootstrap.Modal(document.getElementById('errorModal'));
let logModal = new bootstrap.Modal(logEl);
let log1 = true;
let log2 = true;
let log3 = true;
let log4 = true;
let logOpen = false;
let winWidth, winHeight;

// We're going to create one object to store our stuff, and add listeners for when values are changed.
let data = {
    devicesInternal: [],
    systemInternal: [],
    audioScenes: [],
    ambientScenes: [],
    audioDevices: [],
    usbDevices: [],
    version: "",
    statsInternal: {},
    load: function (val) {
        if (val.hasOwnProperty("systemData")) {
            this.systemInternal = val["systemData"];
            this.version = val["systemData"]["version"];
            this.systemListener(this.systemInternal);
        }
        if (val.hasOwnProperty("devices")) {
            this.devicesInternal = val["devices"];
            this.devicesInternal.sort((a, b) => (a["name"] > b["name"]) ? 1 : -1);
            this.devicesListener(this.devicesInternal);
        }
        if (val.hasOwnProperty("devUsb")) {
            this.usbDevices = val["devUsb"];
        }
        if (val.hasOwnProperty("devAudio")) {
            this.audioDevices = val["devAudio"];
        }
        if (val.hasOwnProperty("ambientScenes")) {
            this.ambientScenes = val["ambientScenes"];
        }
        if (val.hasOwnProperty("audioScenes")) {
            this.audioScenes = val["audioScenes"];
        }
        if (val.hasOwnProperty("stats")) {
            this.statsInternal = val["stats"];
            this.statListener(val["stats"]);
        }
    },
    statListener: function (val) {
    },
    set Stats(val) {
        this.statsInternal = val;
        this.statListener(val);
    },
    get Stats() {
        return this.statsInternal;
    },
    systemListener: function (val) {
    },
    set SystemData(val) {
        this.systemInternal = val;
        this.systemListener(val);
    },
    get SystemData() {
        return this.systemInternal;
    },
    devicesListener: function (val) {
    },
    set Devices(val) {
        this.devicesInternal = val;
        this.devicesInternal.sort((a, b) => (a["name"] > b["name"]) ? 1 : -1);
        this.devicesListener(val);
    },
    get Devices() {
        return this.devicesInternal;
    },
    deleteDevice(id) {
        console.log("Removing device: ", id);
        let devs = this.devicesInternal;
        let newDevs = [];
        for (let i = 0; i < devs.length; i++) {
            if (devs[i].id !== id) {
                newDevs.push(devs[i]);
            }
        }
        this.devicesInternal = newDevs;
        this.devicesInternal.sort((a, b) => (a["name"] > b["name"]) ? 1 : -1);
        this.devicesListener(this.devicesInternal);
    },
    getDevice(id) {
        for (let i = 0; i < this.devicesInternal.length; i++) {
            if (id === this.devicesInternal[i]["id"]) {
                return this.devicesInternal[i];
            }
        }
        return null;
    },
    setDevice(deviceData) {
        let exists = false;
        for (let i = 0; i < this.devicesInternal.length; i++) {
            if (deviceData["id"] === this.devicesInternal[i]["id"]) {
                this.devicesInternal[i] = deviceData;
                exists = true;
            }
        }
        if (!exists) this.devicesInternal.push(deviceData);
        this.devicesInternal.sort((a, b) => (a["name"] > b["name"]) ? 1 : -1);
        this.devicesListener(this.devicesInternal);
    },
    getProp(string) {
        if (this.systemInternal.hasOwnProperty(string)) return this.systemInternal[string];
        return null;
    },
    setProp(string, value) {
        if (this.systemInternal.hasOwnProperty(string)) {
            let check = this.systemInternal[string];
            // If types match, set it
            if (typeof (check) === typeof (value)) {
                this.systemInternal[string] = value;
                return;
            }
            // If not, try a conversion
            switch (typeof (check)) {
                case "number":
                    let num = parseInt(value);
                    if (typeof (num) === typeof (check)) {
                        this.systemInternal[string] = num;
                    } else {
                        console.log("FAIL: " + typeof (num));
                    }
                    break;
                case "string":
                    let str = value.toString();
                    if (isValid(str)) {
                        this.systemInternal[string] = str;
                    }
                    break;
                case "boolean":
                    if (value === 0) this.systemInternal[string] = false;
                    if (value === 1) this.systemInternal[string] = true;
                    if (value === "false" || value === "false") this.systemInternal[string] = true;
                    if (value === "true" || value === "True") this.systemInternal[string] = true;
                    break;
                case "object":
                    if (value === null) this.systemInternal[string] = value;
                    if (typeof (value) === "string") {
                        try {
                            let obj = JSON.parse(value);
                            if (typeof (obj) === "object" && isValid(obj)) {
                                this.systemInternal[string] = obj;
                            }
                        } catch {
                            console.log("Error...");
                        }
                    }
                    break;
                case "undefined":
                    console.log("Can't set undefined prop: ", string, value);
                    break;
            }
            this.systemListener();
        } else {
            console.log("Property doesn't exist, stupid: " + string);
        }
    },
    registerSystemListener: function (listener) {
        this.systemListener = listener;
    },
    registerDevicesListener: function (listener) {
        this.devicesListener = listener;
    },
    registerStatsListener: function (listener) {
        this.statListener = listener;
    }
};

data.registerSystemListener(function () {
    if (loadTimeout !== null) {
        clearTimeout(loadTimeout);
        loadTimeout = null;
    }
    loadTimeout = setTimeout(function () {
        loadUi();
        clearTimeout(loadTimeout);
        loadTimeout = null;
    }, 250);
});

data.registerStatsListener(function (data) {
    loadStats(data);
});

data.registerDevicesListener(function () {
    if (devTimeout !== null) {
        clearTimeout(devTimeout);
        devTimeout = null;
    }
    devTimeout = setTimeout(function () {
        loadDevices();
    }, 500);
});

let websocket = new signalR.HubConnectionBuilder()
    .configureLogging(signalR.LogLevel.Information)
    .withUrl("/socket")
    .build();

document.addEventListener("DOMContentLoaded", function () {
    document.addEventListener("backbutton", onBackKeyDown, false);
    document.addEventListener('keydown', function (e) {
        if (e.key === "Escape") {
            onBackKeyDown();
        }
    });

    let getUrl = window.location;
    baseUrl = getUrl.protocol + "//" + getUrl.host;
    fpsCounter = document.getElementById("fps");
    closeButton = document.querySelectorAll(".closeBtn");
    settingsRow = document.getElementById("settingsRow");
    settingsTab = document.getElementById("settingsTab");
    settingsContent = document.getElementById("settingsContent");
    cardRow = document.getElementById("cardRow");
    setSocketListeners();
    loadSocket();
    setTimeout(function () {
        new Image().src = "../img/sectoring_screen.png";
    }, 1000);
});

function onBackKeyDown() {
    if (expanded) closeCard().then();
    if (settingsShown) toggleSettingsDiv().then();
}


function loadPickr() {
    pickr = Pickr.create({
        el: '.pickrBtn',
        theme: 'nano',
        default: "#" + data.getProp("ambientColor"),
        swatches: [
            'rgb(255, 0, 0)',
            'rgb(255, 128, 0)',
            'rgb(128, 255, 0)',
            'rgb(0, 255, 0)',
            'rgb(0, 255, 128)',
            'rgb(0,128,128)',
            'rgb(0,128,255)',
            'rgb(0,0,255)',
            'rgb(128, 0, 255)',
            'rgb(255, 0, 255)',
            'rgb(255, 0, 128)'
        ],

        components: {
            // Main components
            preview: true,
            opacity: false,
            hue: true,

            // Input / output Options
            interaction: {
                hex: true
            }
        }
    });
    pickr.on('change', (color) => {
        let col = color.toHEXA();
        newColor = col[0] + col[1] + col[2];
    }).on('changestop', () => {
        data.setProp("ambientColor", newColor);
        data.setProp("ambientScene", -1);
        let asSelect = document.getElementById("AmbientScene");
        asSelect.value = "-1";
        pickr.setColor("#" + newColor);
        sendMessage("systemData", data.SystemData);

    }).on('swatchselect', (color) => {
        let col = color.toHEXA();
        newColor = col[0] + col[1] + col[2];
        data.setProp("ambientColor", newColor);
        data.setProp("ambientScene", -1);
        let asSelect = document.getElementById("AmbientScene");
        asSelect.value = "-1";
        pickr.setColor("#" + newColor);
        sendMessage("systemData", data.SystemData);
    });
}

function loadCounts() {
    let sd = data.SystemData;
    leftCount = 0;
    rightCount = 0;
    topCount = 0;
    bottomCount = 0;
    hSectors = 5;
    vSectors = 3;

    if (!isValid(sd)) return;
    let target = sd["dsIp"];
    let devs = data.Devices;
    let devSelect = document.getElementById("targetDs");
    if (isValid(devSelect)) {
        for (let i = 0; i < devSelect.options.length; i++) {
            devSelect.options[i] = null;
        }
    }

    if (isValid(devs)) {
        let opt = document.createElement("option");
        opt.value = "";
        opt.innerText = "";
        devSelect.appendChild(opt);
        for (let i = 0; i < devs.length; i++) {
            let dev = devs[i];
            if (dev["tag"] === "DreamScreen" && dev["deviceTag"].includes("DreamScreen")) {
                let opt = document.createElement("option");
                opt.value = dev["id"];
                opt.innerText = dev["name"] + " - " + dev["id"];
                if (isValid(target) && opt.value === target) opt.selected = true;
                devSelect.appendChild(opt);
            }
        }
    }

    let lSel = document.querySelector('[data-property="leftCount"][data-object="SystemData"]');
    let rSel = document.querySelector('[data-property="rightCount"][data-object="SystemData"]');
    let tSel = document.querySelector('[data-property="topCount"][data-object="SystemData"]');
    let bSel = document.querySelector('[data-property="bottomCount"][data-object="SystemData"]');

    // If using DS capture, set static/dev LED counts.    
    leftCount = sd["leftCount"];
    rightCount = sd["rightCount"];
    topCount = sd["topCount"];
    bottomCount = sd["bottomCount"];
    hSectors = sd["hSectors"];
    vSectors = sd["vSectors"];
    useCenter = sd["useCenter"];

    lSel.value = leftCount;
    rSel.value = rightCount;
    tSel.value = topCount;
    bSel.value = bottomCount;
}

// Send a message to the server, websocket or not
function sendMessage(endpoint, sData, encode = true) {
    let input = sData;
    if (encode && isValid(sData)) sData = JSON.stringify(sData);
    if (socketLoaded) {
        if (isValid(sData)) {
            console.log("Sending message: " + endpoint, input);
            websocket.invoke(endpoint, sData).catch(function (err) {
                return console.error("Error: ", err);
            });
        } else {
            websocket.invoke(endpoint).catch(function (err) {
                return console.error("Error: ", err);
            });
        }
    } else {
        console.log("Posting: " + endpoint, input);
        doPost(endpoint, data);
    }
}

function doPost(endpoint, payload) {
    let url = baseUrl + "/api/Glimmr/" + endpoint;
    if (posting) {
        return;
    }
    let xhttp = new XMLHttpRequest();

    xhttp.open("POST", url, true);
    xhttp.onreadystatechange = function () {
        if (this.readyState === 4 && this.status === 200) {
            let postResult = this.json;
            if (endpoint === "loadData") {
                let stuff = postResult.replace(/\\n/g, '');
                data.load(JSON.parse(stuff));
            }
        }
    };
    xhttp.setRequestHeader("Content-Type", "application/json");
    xhttp.send(JSON.stringify(payload));
    xhttp.send();
}

function doGet(endpoint) {
    fetch(endpoint)
        .then(function (response) {
            return response.json();
        });
}

// Set various actions/responses on the websocket
function setSocketListeners() {
    websocket.on("ReceiveMessage", function (message) {
        console.log("Message received: " + message);
    });

    websocket.on("mode", function (mode) {
        console.log("Mode updated via web socket:", mode);
        setMode(mode);
        data.setProp("deviceMode", mode);
    });

    websocket.on("stats", function (cpuData) {
        data.Stats = cpuData;
    });

    websocket.on("ambientMode", function (mode) {
        console.log("Socket has set ambient mode to " + mode);
    });

    websocket.on("ambientScene", function (show) {
        console.log("Socket has set ambient show to " + show);
    });

    websocket.on("ambientColor", function (color) {
        console.log("Socket has set ambient color to " + color);
    });

    websocket.on("brightness", function (brightness) {
        console.log("Socket has set ambient mode to " + brightness);
    });

    websocket.on("saturation", function (sat) {
        console.log("Socket has set saturation to " + sat);
    });

    websocket.on("updateDevice", function (device) {
        console.log("Updating device data " + device);
    });

    let logDiv = document.getElementById("logWrap");
    websocket.on("log", function (msg) {
        let message = JSON.parse(msg);
        let logLine = document.createElement("div");
        let lvl = "";
        let show = true;
        switch (message["Level"]) {
            case 1:
                lvl = "DBG";
                show = log1;
                break;
            case 2:
                lvl = "INF";
                show = log2;
                break;
            case 3:
                lvl = "WRN";
                show = log3;
                break;
            case 4:
                lvl = "ERR";
                show = log4;
                break;
        }
        logLine.classList.add("logLine");
        if (!show) logLine.classList.add("hideLine");
        logLine.classList.add("log-" + message["Level"].toString());
        let ts = message["Timestamp"].split("T")[1].split(".")[0];
        logLine.innerHTML = "[" + ts + " " + lvl + "]" + message["Properties"]["Caller"]["Value"] + " " + message["MessageTemplate"]["Text"];
        logDiv.prepend(logLine);
        for (let i = 0; i < logDiv.children.length; i++) {
            if (i > 500) {
                logDiv.children[i].remove();
            }
        }
    });


    websocket.on("auth", function (value1, value2) {
        let cb = document.getElementById("CircleBar");
        switch (value1) {
            case "start":
                bar.animate(0);
                cb.classList.remove("hide");
                break;
            case "error":
                cb.classList.add("hide");
                break;
            case "stop":
                cb.classList.add("hide");
                break;
            case "update":
                bar.animate(value2 / 30);
                if (value2 === 30) cb.classList.add("hide");
                break;
            case "authorized":
                let led = document.querySelector(".linkImg");
                led.classList.remove("unlinked");
                led.classList.add("linked");
                cb.classList.add("hide");
                break;
            default:
                break;
        }
    });


    websocket.on('open', function () {
        console.log("Socket connected.");
        socketLoaded = true;
    });

    websocket.on('olo', function (parsed) {
        if (isValid(parsed)) {
            console.log("Loading data: ", parsed);
            data.load(parsed);
        }
    });

    websocket.on('inputImage', function (data) {
        document.getElementById("inputPreview").src = "data:image/png;base64," + data;
    });

    websocket.on('outputImage', function (data) {
        document.getElementById("outputPreview").src = "data:image/png;base64," + data;
    });

    websocket.on('deleteDevice', function (id) {
        data.deleteDevice(id);
        if (isValid(deviceData) && deviceData["id"] === id) {
            if (expanded) {
                closeCard().then(function () {
                    let devCard = document.querySelector('.devCard[data-id="' + id + '"]');
                    if (isValid(devCard)) {
                        devCard.classList.add('min');
                        setTimeout(function () {
                            devCard.remove();
                        }, 750);
                    }
                });
            }
        } else {
            let devCard = document.querySelector('.devCard[data-id="' + id + '"]');
            if (isValid(devCard)) {
                devCard.classList.add('min');
                setTimeout(function () {
                    devCard.remove();
                }, 500);
            }
        }
    });

    websocket.on('device', function (dData) {
        let parsed = JSON.parse(dData);
        if (parsed && typeof(parsed) === "object" && parsed.hasOwnProperty("Id") && parsed["Id"] !== undefined && parsed["Id"] !== "") {
            console.log("Received device data from server: ", parsed);
        } else {
            console.log("Device data is invalid:", parsed);
            return;
        }

        let existing = data.getDevice(parsed["id"]);
        data.setDevice(parsed);
        if (isValid(existing) && existing !== null) {
            // Check if dev settings are shown and refresh them
            if (isValid(deviceData) && deviceData["id"] === parsed["id"]) {
                deviceData = parsed;
                if (expanded) {
                    createDeviceSettings();
                } else {
                    console.log("No settings shown?!");
                }
            }
        }
    });

    websocket.onclose(function () {
        console.log("Socket Disconnected...");
        socketLoaded = false;
        showSocketError();
        let i = 0;
        let intr = setInterval(function () {
            loadSocket();
            if (++i >= 100 || socketLoaded) clearInterval(intr);
        }, 5000);
    })
}

// Initialize our websocket
function loadSocket() {
    if (socketLoaded) return;
    console.log("Trying to connect to socket...");
    websocket.start().then(function () {
        console.log("Socket connected.");
        socketLoaded = true;
        errModal.hide();
        sendMessage("settingsShown", settingsShown, false);
    }).catch(function (err) {
        console.log("Socket connection error: ", err.toString());
        showSocketError();
    });
}

function loadStats(cpuData) {
    let tempDiv = $("#tempDiv");
    let tempText = $("#temperature");
    let cpuText = $("#cpuPct");
    let memText = $("#memPct");
    let overIcon = $("#overIcon");
    let sd = data.SystemData;
    let utDiv = document.getElementById("utDiv");
    data.stats = cpuData;
    let tempUnit = "°F";

    if (isValid(sd)) {
        tempUnit = (sd["units"] === 0) ? "°F" : "°C";
    }
    tempText.textContent = cpuData["cpuTemp"] + tempUnit;
    cpuText.textContent = cpuData["cpuUsage"] + "%";
    memText.textContent = cpuData["memoryUsage"] + "%";
    utDiv.innerHTML = cpuData["uptime"];
    overIcon.textContent = "";
    tempDiv.classList.remove("text-danger");
    tempDiv.classList.add("text-success");
    overIcon.classList.remove("text-danger");
    fpsCounter.innerText = cpuData["fps"]["source"] + "FPS";
    for (let i = 0; i < cpuData["throttledState"].length; i++) {
        if (cpuData["throttledState"][i] === "Currently throttled") {
            tempDiv.classList.add("text-danger");
            tempDiv.classList.remove("text-success");
        }
        if (cpuData["throttledState"][i] === "Under-voltage detected") {
            overIcon.textContent = "power_input";
            overIcon.classList.add("text-danger");
        }
    }
}

function downloadDb() {
    let link = document.createElement("a");
    // If you don't know the name or want to use
    // the webserver default set name = ''
    link.setAttribute('download', name);
    link.href = "/api/Glimmr/database";
    document.body.appendChild(link);
    link.click();
    link.remove();
}

function fetchLog() {
    let link = document.createElement("a");
    // If you don't know the name or want to use
    // the webserver default set name = ''
    link.setAttribute('download', name);
    link.href = "/api/Glimmr/logs";
    document.body.appendChild(link);
    link.click();
    link.remove();
}

function showSocketError() {
    errModal.show();
}

function showLog() {
    logModal.show();
    logOpen = true;
}

function hideLog() {
    logModal.hide();
    logOpen = false;
}

function clearLog() {
    let logDiv = document.getElementById("logWrap");
    logDiv.innerHTML = "";
}

function toggleLog(event, level) {
    let highlightedItems = document.querySelectorAll(".log-" + level.toString());
    console.log("E: ", event);
    switch (level) {
        case 1:
            log1 = !log1;
            break;
        case 2:
            log2 = !log2;
            break;
        case 3:
            log3 = !log3;
            break;
        case 4:
            log4 = !log4;
            break;
    }
    event.classList.toggle("active");
    highlightedItems.forEach(function (userItem) {
        userItem.classList.toggle("hideLine");
    });
}

function TriggerRefresh() {
    let sd = data.SystemData;
    let refreshIcon = document.getElementById("refreshIcon");
    if (refreshTimer == null) {
        if (!isValid(sd)) return;
        refreshIcon.classList.add("rotate");
        sendMessage("scanDevices");
        refreshTimer = setTimeout(function () {
            refreshIcon.classList.remove("rotate");
            refreshTimer = null;
        }, sd["discoveryTimeout"] * 1000);
    }

}

// Set all of the various listeners our page may use
function setListeners() {
    listenersSet = true;
    window.addEventListener('resize', sizeContent);

    document.addEventListener('keydown', function (event) {
        if (event.key === 'l') {
            if (logOpen) {
                hideLog();
            } else {
                showLog();
            }
        }
    });


    document.addEventListener('change', function (e) {
        let target = e.target;
        let obj = target.getAttribute("data-object");
        let property = target.getAttribute("data-property");
        let val = target.value;
        if (target.type && target.type === "checkbox") {
            val = target.checked;
        }


        if (target.classList.contains("beam-control")) {
            let pos = parseInt(target.getAttribute("data-position"));
            let prop = target.getAttribute("data-beamproperty");
            let val;
            if (prop === "Orientation" || prop === "Offset") {
                val = target.value;
                if (prop === "Orientation") val = parseInt(val);
            } else {
                val = target.checked;
            }
            updateBeamProperty(pos, prop, val);

        }
        if (target.classList.contains("lightProperty")) {
            let id = target.getAttribute("data-id");
            let property = target.getAttribute("data-property");
            let numVal = parseInt(val);
            if (!isNaN(numVal)) val = numVal;
            updateLightProperty(id, property, val);
            return;
        }

        if (isValid(obj) && isValid(property) && isValid(val) && obj === "SystemData") {
            data.setProp(property, val);
            if (property === "screenCapMode" || property === "captureMode" || property === "streamMode") {
                updateCaptureUi();
            }

            console.log("Sending updated object: ", obj, data.SystemData);
            sendMessage(obj, data.SystemData, true);

            if (property === "leftCount" || property === "rightCount" || property === "topCount" || property === "bottomCount") {
                let lPreview = document.getElementById("sLedWrap");
                createLedMap(lPreview);
            }

            if (property === "yseCenter" || property === "hSectors" || property === "vSectors") {
                if (property === "useCenter") useCenter = val;
                if (property === "hSectors") hSectors = val;
                if (property === "vSectors") vSectors = val;
                let sPreview = document.getElementById("sectorWrap");
                createSectorMap(sPreview);
            }

            if (property === "theme") {
                loadTheme(val);
            }
            return;
        }

        // If we're still here, check for a device setting
        if (target.classList.contains("devSetting")) {
            updateDevice(obj, property, val);
            return;
        }

        obj = target.getAttribute("data-target");
        property = target.getAttribute("data-attribute");
        if (isValid(obj) && isValid(property) && isValid(val)) {
            updateDevice(obj, property, val);
        }
    });

    document.addEventListener('click', function (e) {
        let target = e.target;
        handleClick(target);
    });
}

function handleClick(target) {
    switch (true) {
        case target.id === "showIntro":
            showIntro();
            break;
        case target.id === "fetchLog":
            fetchLog();
            break;
        case target.id === "capture-tab":
            sizeContent();
            break;
        case target.classList.contains("controlBtn"):
            let action = target.getAttribute("data-action");
            let message = "Warning: ";
            switch (action) {
                case "shutdown":
                    message += "This will shut down the device. You will need to manually turn it back on.";
                    break;
                case "reboot":
                    message += "This will restart the device.";
                    break;
                case "restart":
                    message += "This will restart the Glimmr TV Service.";
                    break;
                case "update":
                    message += "This will update and restart Glimmr TV.";
                    break;
            }
            message += " Would you like to continue?";
            if (confirm(message)) {
                sendMessage("systemControl", action, false);
            } else {
                console.log(action + " canceled.");
            }
            break;
        case target.classList.contains("closeBtn"):
            closeCard().then();
            break;
        case target.classList.contains("sector"):
            let val = parseInt(target.getAttribute("data-sector"));
            if (target.classList.contains("flashSectorRegion")) {
                console.log("Flashing sector...");
                sendMessage("flashSector", val, false);
                return;
            }
            if (target.classList.contains("nanoRegion")) {
                if (nanoModal != null) {
                    nanoModal.hide();
                    nanoModal = null;
                }
                updateDeviceSector(val, target);
                return;
            }
            if (target.classList.contains("lifxSectorRegion") || target.classList.contains("wledSectorRegion") || target.classList.contains("dreamSectorRegion")) {
                updateDevice(deviceData["id"], "targetSector", val);
                return;
            }
            break;
        case target.classList.contains("linkDiv"):
            if (target.getAttribute("data-linked") === "false") {
                let devId = deviceData["id"];
                if (!isValid(bar)) {
                    bar = new ProgressBar.Circle("#CircleBar", {
                        strokeWidth: 15,
                        easing: 'easeInOut',
                        duration: 0,
                        color: '#0000FF',
                        trailColor: '#eee',
                        trailWidth: 0,
                        svgStyle: null,
                        value: 1
                    });
                }
                sendMessage("authorizeDevice", devId, false);
            }
            break;
        case target.classList.contains("led"):
            let sector = target.getAttribute("data-sector");
            console.log("Flashing LED " + sector);
            sendMessage("flashLed", parseInt(sector), false);
            break;
        case target.classList.contains("deviceIcon"):
            let targetId = target.getAttribute("data-device");
            sendMessage("flashDevice", targetId, false);
            break;
        case target.classList.contains("removeDevice"):
            let deviceId = deviceData["id"];
            let devName = deviceData["name"];
            if (confirm('Warning! The device named ' + devName + " will have all settings removed. Do you want to continue?")) {
                closeCard().then(function () {
                    data.deleteDevice(deviceId);
                    let devCard = document.querySelector('.devCard[data-id="' + deviceId + '"]');
                    if (isValid(devCard)) {
                        if (isValid(devCard)) {
                            setTimeout(function () {
                                devCard.classList.add('min');
                                setTimeout(function () {
                                    devCard.remove();
                                }, 750);
                            }, 1500);
                        }
                    }
                });

                sendMessage("deleteDevice", deviceId, false);
                console.log('Deleting device.');
            } else {
                console.log('Device deletion canceled.');
            }
            break;
        case target.classList.contains("settingBtn"):
            if (expanded) {
                closeCard().then();
            } else {
                let devId = target.getAttribute("data-target");
                deviceData = data.getDevice(devId);
                if (devId === "-1") deviceData = JSON.parse(ledData);
                showDeviceCard(target).then();
            }
            break;
        case target.classList.contains("enableBtn"):
            let dId = target.getAttribute("data-target");
            let devEnabled = target.getAttribute("data-enabled");
            let icon = target.firstChild;
            if (devEnabled === "true") {
                target.setAttribute("data-enabled", "false");
                icon.innerText = "cast";
            } else {
                target.setAttribute("data-enabled", "true");
                icon.innerText = "cast_connected";
            }
            deviceData = data.getDevice(dId);
            updateDevice(dId, "enable", (devEnabled !== "true"));
            break;
        case target.classList.contains("refreshBtn"):
            TriggerRefresh();
            break;
        case target.classList.contains("modeBtn"):
            let newMode = parseInt(target.getAttribute("data-mode"));
            data.setProp("deviceMode", mode);
            setMode(newMode);
            sendMessage("mode", newMode, false);
            break;
        case target.classList.contains("mainSettings"):
            toggleSettingsDiv().then();
            break;
        case target.classList.contains("nav-link"):
            let cDiv = target.getAttribute("href");
            let fadePanes = document.querySelectorAll(".tab-pane");
            for (let i = 0; i < fadePanes.length; i++) {
                if (fadePanes[i]) {
                    if (fadePanes[i].classList.contains("show")) {
                        fadePanes[i].classList.remove("show", "active");
                    }
                }
            }
            document.querySelector(cDiv).classList.add("show", "active");
            break;
    }
}


async function toggleSettingsDiv() {
    let x = window.matchMedia("(max-width: 576px)");
    let settingsIcon = document.querySelector(".mainSettings.mainSettings-lg span");
    if (x.matches) settingsIcon = document.querySelector(".mainSettings.mainSettings-sm span");
    if (!settingsShown) {
        settingsIcon.textContent = "chevron_left";
        showSettingsCard().then(function () {
            cardRow.classList.add("d-none");
            loadSettings();
        });
    } else {
        settingsIcon.textContent = "settings_applications";
        hideSettingsCard().then(function () {
            cardRow.classList.remove("d-none");
        });
    }
    sendMessage("settingsShown", settingsShown, false);
}


function updateDeviceSector(sector, target) {
    console.log("Sector click: ", sector, target);
    let dev = deviceData;
    if (dev["tag"] === "Nanoleaf") {
        let layout = dev["layout"];
        let positions = layout["positionData"];
        for (let i = 0; i < positions.length; i++) {
            if (positions[i]["panelId"] === nanoTarget) {
                positions[i]["targetSector"] = sector;
            }
        }
        layout["PositionData"] = positions;
        dev["Layout"] = layout;
        updateDevice(dev["id"], "layout", layout);
    } else {
        updateDevice(dev["id"], "targetSector", sector);
    }
    sendMessage("flashSector", sector, false);
}

function updateLightProperty(myId, propertyName, value) {
    let lm = getLightMap(myId);
    lm[propertyName] = value;
    setLightMap(lm);
    let fGroup = deviceData["groups"];
    let nGroup = [];
    for (let g in fGroup) {
        if (fGroup.hasOwnProperty(g)) {
            fGroup[g]["id"] = fGroup[g]["id"];
            nGroup.push(fGroup[g]);
        }

    }
    updateDevice(deviceData["id"], "groups", nGroup);
}

function updateBeamProperty(beamPos, propertyName, value) {
    let id = deviceData["id"];
    if (propertyName === "offset") value = parseInt(value);
    let beamLayout = deviceData["beamLayout"];
    let beams = beamLayout["segments"];
    for (let i = 0; i < beams.length; i++) {
        let beam = beams[i];
        if (beam["position"] === beamPos) {
            beam[propertyName] = value;
            beams[i] = beam;
        }
    }

    beamLayout["segments"] = beams;
    console.log("Updating beam " + id, propertyName, value);
    appendBeamLedMap();
    updateDevice(id, "beamLayout", beamLayout);
}

function getLightMap(id) {
    let hueLightMap = deviceData["mappedLights"];
    for (let l in hueLightMap) {
        if (hueLightMap.hasOwnProperty(l)) {
            if (hueLightMap[l]["id"] === id) {
                return hueLightMap[l];
            }
        }
    }
    return {
        id: id,
        TargetSector: -1,
        TargetSector2: -1,
        Brightness: 255,
        Override: false
    };
}

function setLightMap(map) {
    let hueLightMap = deviceData["mappedLights"];
    for (let l in hueLightMap) {
        if (hueLightMap.hasOwnProperty(l)) {
            if (hueLightMap[l]["id"] === map["id"]) {
                hueLightMap[l] = map;
                return;
            }
        }
    }
    hueLightMap.push(map);
    updateDevice(deviceData["id"], "mappedLights", hueLightMap);
}


function setMode(newMode) {
    if (newMode == null) return;
    mode = newMode;
    console.log("Updating mode: " + mode);
    let target = document.querySelector("[data-mode='" + mode + "']");
    let others = document.querySelectorAll(".modeBtn");
    for (let i = 0; i < others.length; i++) {
        if (others[i]) {
            others[i].classList.remove("active");
        }
    }
    if (target != null) target.classList.add("active");
    let ambientNav = document.getElementById("ambientNav");
    let audioNav = document.getElementById("audioNav");
    ambientNav.classList.add("hide");
    ambientNav.classList.remove("show");
    audioNav.classList.add("hide");
    audioNav.classList.remove("show");
    switch (mode) {
        case 3:
            ambientNav.classList.add("show");
            ambientNav.classList.remove("hide");
            break;
        case 4:
        case 2:
            audioNav.classList.add("show");
            audioNav.classList.remove("hide");
    }

    sizeContent();
}

function setModeButtons() {
    let sd = data.SystemData;
    let capMode = sd["captureMode"];
    let streamMode = sd["streamMode"];
    let videoBtns = document.querySelectorAll(".videoBtn");
    let streamBtns = document.querySelectorAll(".streamBtn");
    let vString = "tv";
    if (capMode === 1) vString = "videocam";
    if (capMode === 2) vString = "settings_input_hdmi";
    if (capMode === 3) vString = "tv";

    for (let i = 0; i < videoBtns.length; ++i) {
        videoBtns[i].firstElementChild.innerHTML = vString;
    }

    for (let i = 0; i < streamBtns.length; ++i) {
        let streamBtn = streamBtns[i];
        streamBtn.firstElementChild.innerHTML = "";
        streamBtn.firstElementChild.classList.remove("material-icons");
        streamBtn.firstElementChild.classList.remove("appz-glimmr");
        streamBtn.firstElementChild.classList.remove("appz-dreamscreen");
        switch (streamMode) {
            case 0:
                streamBtn.firstElementChild.classList.add("appz-dreamscreen");
                break;
            case 1:
                streamBtn.firstElementChild.classList.add("appz-glimmr");
                break;
            case 2:
                streamBtn.firstElementChild.innerHTML = "sensors";
                streamBtn.firstElementChild.classList.add("material-icons");
                break;
        }
    }
}


function loadUi() {
    loadCounts();
    setModeButtons();
    getDevices();

    if (isValid(data.audioDevices)) {
        let recList = document.getElementById("RecDev");
        let options = document.querySelectorAll('#recDev option');
        options.forEach(o => o.remove());
        let recDevs = data.audioDevices;
        let recDev = data.getProp("recDev");
        if (isValid(recDevs)) {
            for (let i = 0; i < recDevs.length; i++) {
                let dev = recDevs[i];
                let opt = document.createElement("option");
                opt.value = dev["id"];
                opt.innerText = dev["id"];
                if (opt.value === recDev) opt.selected = true;
                recList.options.add(opt);
            }
        }
    } else {
        console.log("No recording devices found.");
    }

    let mode = data.getProp("deviceMode");
    let autoDisabled = data.getProp("autoDisabled");
    if (autoDisabled) mode = 0;
    setMode(mode);

    if (!listenersSet) {
        setListeners();
        let tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl)
        });
    }

    let sd = data.SystemData;

    if (isValid(sd)) {
        let version = sd.version;
        let vDiv = document.getElementById("versionDiv");
        let hDiv = document.getElementById("hostDiv");
        let utDiv = document.getElementById("utDiv");
        let ipDiv = document.getElementById("ipDiv");
        vDiv.innerHTML = version.toString();
        hDiv.innerHTML = sd["deviceName"];
        ipDiv.innerHTML = sd["ipAddress"];
        utDiv.innerHTML = data.stats["uptime"];

        if (!isValid(pickr)) loadPickr();
        let theme = sd["theme"];
        loadTheme(theme);
        if (isValid(data.ambientScenes)) {
            let scenes = data.ambientScenes;
            let ambientScene = sd["ambientScene"];
            scenes.sort((a, b) => (a["name"] > b["name"]) ? 1 : -1);
            let sceneSelector = document.getElementById("AmbientScene");
            sceneSelector.innerHTML = "";
            for (let i = 0; i < Object.entries(scenes).length; i++) {
                let opt = document.createElement("option");
                opt.value = scenes[i]["id"].toString();
                opt.innerText = scenes[i]["name"];
                if (opt.key === ambientScene) opt.selected = true;
                sceneSelector.appendChild(opt);
            }
            sceneSelector.value = ambientScene;
        }
        if (isValid(data.audioScenes)) {
            let aScenes = data.audioScenes;
            let audioMode = sd["audioMap"];
            aScenes.sort((a, b) => (a["id"] > b["id"]) ? 1 : -1);
            let sceneSelector = document.getElementById("AudioMap");
            sceneSelector.innerHTML = "";
            for (let i = 0; i < aScenes.length; i++) {
                let opt = document.createElement("option");
                opt.value = aScenes[i]["id"];
                opt.innerText = aScenes[i]["name"];
                if (opt.value === audioMode) opt.selected = true;
                sceneSelector.appendChild(opt);
            }
            sceneSelector.value = audioMode;
        }
        let color = "#" + sd["ambientColor"];
        pickr.setColor(color);
    }

    sizeContent();
    document.getElementById("cardRow").click();
}

function showIntro() {
    let x = window.matchMedia("(max-width: 576px)");
    let myTour = new Tour(
        {
            backdropPadding: 5,
            backdrop: true,
            orphan: true,
            storage: false,
            onStart: function () {
                let ledObj = JSON.parse(ledData);
                let newCard = createDeviceCard(ledObj, true);
                document.getElementById("cardRow").prepend(newCard);
                if (settingsShown) toggleSettingsDiv().then();
            },
            onEnd: function () {
                let devCard = document.querySelector('.devCard[data-id="-1"]');
                if (expanded) {
                    closeCard().then(function () {
                        devCard.remove();
                    });
                } else {
                    devCard.remove();
                }
                let sd = data.SystemData;
                sd.skipTour = true;
                sendMessage("SystemData", sd);
            },
            steps: [
                {
                    element: '',
                    title: 'Welcome to Glimmr!',
                    content: 'Hello, and thanks for trying out Glimmr. This short tour will help you get familiar with the UI.'
                },
                {
                    element: ".modeGroup",
                    title: 'Mode Selection',
                    placement: 'bottom',
                    content: 'Use these buttons to select the lighting mode for enabled devices. You can hover over each one to see what mode it enables.'
                },
                {
                    element: "#statDiv",
                    title: 'System Stats',
                    placement: 'top',
                    content: 'Here you can see the current frame rate, CPU temperature, CPU usage, and if any throttling is occurring.'
                },
                {
                    element: "#refreshBtn",
                    title: 'Device Refresh',
                    placement: 'left',
                    smartPlacement: false,
                    content: 'Click here to re-scan/refresh devices.'
                },
                {
                    element: x.matches ? ".settingBtn-sm" : ".settingBtn-lg",
                    title: 'Glimmr Settings',
                    placement: 'left',
                    smartPlacement: false,
                    content: 'You can access system settings by clicking this button. Let\'s take a look!',
                    reflex: true,
                    onNext: function () {
                        if (!settingsShown) toggleSettingsDiv().then();
                        document.getElementById("system-tab").click();
                    }
                },
                {
                    element: "#settingsMainControl",
                    title: 'System Control',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    content: 'Shutdown or reboot your computer, restart Glimmr, or manually trigger an update.',
                    onNext: function () {
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    }
                },
                {
                    element: "#settingsMainUpdates",
                    title: 'Automatic Updates',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    placement: 'bottom',
                    content: 'Select if and when to automatically install Glimmr updates.',
                    onNext: function () {
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#settingsMainOpenRGB",
                    title: 'OpenRGB Integration',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    placement: 'top',
                    content: 'With OpenRGB, you can control a massive array of RGB PC Peripherals. Enter the IP and port here to enable.',
                    onNext: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#settingsCaptureSource",
                    title: 'Capture Settings',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    placement: 'bottom',
                    content: 'Here, you can select the capture source/mode, as well as other settings related to each mode.',
                    onNext: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#settingsCaptureStream",
                    title: 'Streaming Settings',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    placement: 'bottom',
                    content: 'Similarly, you can select the stream source/mode here, as well as other settings related to each mode, if applicable.',
                    onNext: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#settingsCaptureLed",
                    title: 'LED Dimensions',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    content: 'The values set here determine the overall size of the "master grid" used to divide up the screen edges for LED strips.',
                    onNext: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("capturem-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#settingsCaptureSector",
                    title: 'Sector Dimensions',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    content: 'The values set here determine the overall size of the "master grid" used to divide up the screen edges for single-color devices, like Hue bulbs or a single Nanoleaf panel.',
                    onNext: function () {
                        scrollElement(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#devCard",
                    title: 'This is a device',
                    content: 'Here you can enable and configure various settings for each device discovered by Glimmr.',
                    onPrev: function () {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    },
                    onNext: function () {
                        scrollElement(myTour.getStep(myTour.getCurrentStep() + 1));
                    }
                },
                {
                    element: "#demoIcon",
                    title: 'Click me!',
                    content: 'Click any device icon in order to locate the device.'
                },
                {
                    element: "#devEnableBtn",
                    title: 'Enable/Disable Device Streaming',
                    content: 'Click here to enable or disable streaming to this device.'
                },
                {
                    element: "#devPrefBtn",
                    title: 'Device Settings',
                    content: 'Each device has a unique group of settings depending on what it does. Clicking here will open the device settings.',
                    onNext: function () {
                        deviceData = JSON.parse(ledData);
                        if (!expanded) showDeviceCard(document.getElementById("devPrefBtn")).then();
                    }

                },
                {
                    element: "#mapWrap",
                    title: 'Element mapping',
                    content: 'Every device in Glimmr has a mapping section where you can preview the light data in relation to the screen.',
                    onNext: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function () {
                        closeCard().then();
                    }
                },
                {
                    element: '.devSetting[data-property="LedCount"]',
                    title: 'LED Count',
                    content: 'This is the total number of leds in your strip. It can be less than the total number in the grid.',
                    onNext: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1))
                    }
                },
                {
                    element: '.devSetting[data-property="Offset"]',
                    title: 'LED Offset',
                    content: 'The offset controls how many leds to skip from the start of the strip, allowing you to segment strips as need.',
                    onNext: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1))
                    }
                },
                {
                    element: '.devSetting[data-property="LedMultiplier"]',
                    title: 'LED Multiplier',
                    content: 'The LED Multiplier can be used to adjust for strips or configurations where the number of LEDs' +
                        'doesn\'t correspond to the number of leds in the grid. By setting this value to a positive number,' +
                        'the strip will use every N colors from the main color array.' +
                        '' +
                        'If set to a negative value, then each color from the main array will be repeated that many times.',
                    onNext: function () {
                        closeCard().then();
                    },
                    onPrev: function () {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: '',
                    title: 'Tour Complete',
                    content: 'This completes the tour. Other devices have other settings, but if I were to try covering' +
                        'everything, you would be sitting here all day. Feel free to play around and look at everything,' +
                        'and head on over to <a href="https://github.com/d8ahazard/glimmr" target="_blank">the project page</a> to submit' +
                        'an issue or feature request.',
                    onPrev: function () {
                        deviceData = JSON.parse(ledData);
                        if (!expanded) showDeviceCard(document.getElementById("devPrefBtn")).then(function () {
                            scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1));
                        });
                    }
                }
            ]
        });
    myTour.init();

    // Start the tour
    myTour.start();
}

function scrollSetting(step) {
    if (!settingsShown) toggleSettingsDiv().then();
    let elem = document.querySelector(step.element);
    let parent = document.getElementById("mainContent");
    parent.scrollTop = elem.offsetTop;
}

function scrollElement(step) {
    if (settingsShown) toggleSettingsDiv().then();
    let elem = document.querySelector(step.element);
    let parent = document.getElementById("mainContent");
    parent.scrollTop = elem.offsetTop;
}

function scrollDevPref(step) {
    let elem = document.querySelector(step.element);
    let parent = document.querySelector("#devCard");
    parent.scrollTop = elem.offsetTop;
}


function loadTheme(theme) {
    let head = document.getElementsByTagName("head")[0];
    if (theme === "light") {
        let last = head.lastChild;
        if (isValid(last.href)) {
            if (last.href.includes("dark.css")) {
                last.parentNode.removeChild(last);
            }
        }
    } else {
        let newSS = document.createElement('link');
        newSS.rel = 'stylesheet';
        newSS.href = '/css/' + theme + '.css';
        document.getElementsByTagName("head")[0].appendChild(newSS);
    }


}

function loadSettings() {
    let systemData = data.SystemData;
    let updateTime = systemData["autoUpdateTime"].toString();
    let timeSelect = document.getElementById("AutoUpdateTime");
    if (isValid(timeSelect)) {
        let length = timeSelect.options.length;
        for (let i = length - 1; i >= 0; i--) {
            timeSelect.options[i] = null;
        }

        let hourval = 0;
        let timeText = document.getElementById("updateTime");
        for (let ampm = 0; ampm < 2; ampm++) {
            for (let hour = 0; hour < 12; hour++) {
                let string = (ampm === 0) ? "AM" : "PM";
                let opt = document.createElement("option");
                opt.value = hourval.toString();
                let hourText = hour === 0 ? 12 : hour;
                opt.innerText = hourText + " " + string;
                if (hourval.toString === updateTime) {
                    opt.selected = true;
                }
                timeSelect.options.add(opt);
                hourval++;
            }
        }
        let ampm = "AM";
        if (updateTime > 12) {
            updateTime -= 12;
            ampm = "PM";
        }
        timeText.innerHTML = "Updates will be installed at " + updateTime.toString() + ":00" + ampm + " every day when enabled.";
    }
    if (isValid(systemData)) {
        loadSettingObject(systemData);
        updateCaptureUi();
        setModeButtons();
        loadCounts();
        let lPreview = document.getElementById("sLedWrap");
        let sPreview = document.getElementById("sectorWrap");
        setTimeout(function () {
            createLedMap(lPreview);
            createSectorMap(sPreview, "FlashSector");
        }, 500);
    }
}

function updateCaptureUi() {
    let systemData = data.SystemData;
    if (!isValid(systemData)) return;
    let capGroups = document.querySelectorAll(".capGroup");
    let mode = systemData["captureMode"].toString();
    let camMode = systemData["camType"].toString();
    let usbIdx = systemData["usbSelection"].toString();
    let usbRow = document.getElementById("UsbSelectRow");
    let usbSel = document.getElementById("UsbSelect");
    let streamMode = systemData["streamMode"].toString();
    let streamGroups = document.querySelectorAll(".streamGroup");
    for (let i = 0; i < streamGroups.length; i++) {
        let group = streamGroups[i];
        if (group.getAttribute("data-stream") === streamMode) {
            group.classList.remove("d-none");
        } else {
            group.classList.add("d-none");
        }
    }

    for (let i = 0; i < capGroups.length; i++) {
        let group = capGroups[i];
        let groupMode = group.getAttribute("data-mode");
        if (groupMode === mode) {
            group.classList.remove("d-none");
        } else {
            group.classList.add("d-none");
        }
    }

    let options = document.querySelectorAll('#UsbSelect option');
    options.forEach(o => o.remove());
    let usbDevs = data.usbDevices;

    let opt = document.createElement("option");
    opt.value = "";
    opt.innerText = "";
    if (opt.value === usbIdx) opt.selected = true;
    usbSel.appendChild(opt);

    for (const [key, value] of Object.entries(usbDevs)) {
        if (!isValid(key) || !isValid(value)) continue;
        let opt = document.createElement("option");
        opt.value = key.toString();
        opt.innerText = value.toString();
        if (opt.value === usbIdx) opt.selected = true;
        usbSel.appendChild(opt);
    }

    if (mode === "2" || (mode === "1" && camMode === "1")) {
        usbRow.classList.remove("d-none");
    } else {
        usbRow.classList.add("d-none");
    }
}

function loadSettingObject(obj) {
    if (obj == null) {
        return;
    }
    let dataProp = obj;
    let id = obj["id"];
    let name = "SystemData";
    for (let prop in dataProp) {
        if (dataProp.hasOwnProperty(prop)) {
            let value = dataProp[prop];
            let target = document.querySelector('[data-property=' + prop + '][data-object="' + name + '"]');
            if (obj.hasOwnProperty("GpioNumber")) {
                target = document.querySelector('[data-property=' + prop + '][data-object="' + name + '"][data-id="' + id + '"]');
            }

            if (prop === "Enable") {
                if (value) {
                    target = document.querySelector('[data-id="' + id + '"][data-function="enable"]');
                    if (isValid(target)) target.classList.add("active");
                } else {
                    target = document.querySelector('[data-id="' + id + '"][data-function="disable"]');
                    if (isValid(target)) target.classList.add("active");
                }
            }

            if (isValid(target) && prop !== "selectedMonitors" && prop !== "screenCapMode") {
                if (value === true) {
                    target.setAttribute('checked', "true");
                } else {
                    target.value = dataProp[prop];
                }
            }
        }
    }
}

function expandCards() {
    let devCards = document.querySelectorAll(".card.devCard.min");
    if (devCards.length >= 1) {
        devCards[0].classList.remove("min");
        setTimeout(expandCards, 50);
    }
}

function loadDevices() {
    if (demoLoaded) return;
    let blankCard = $("#blankCard");
    if (isValid(blankCard)) {
        blankCard.remove();
    }
    let devs = data.Devices;
    let container = $("#cardRow");
    let cards = container.querySelectorAll(".card.devCard");
    let empty = (cards.length === 0);
    setTimeout(expandCards, 250);
    for (let i = 0; i < devs.length; i++) {
        let pos = i - 1;
        let card = null;
        let expand = false;
        if (devs.hasOwnProperty(i)) {
            let devData = devs[i];
            card = createDeviceCard(devData, false);
            if (isValid(card) && card !== null) {
                if (empty) {
                    container.append(card);
                    expand = true;
                } else {
                    let exCard = document.querySelector(".devCard[data-id='" + devData.id + "']");
                    if (isObject(exCard)) {
                        card.classList.remove("min");
                        container.replaceChild(card, exCard);
                        if (expanded && devData["id"] === deviceData["id"]) {
                            let sub = document.querySelector(".card.container-fluid > .card-body > .titleRow > .titleCol > .card-subtitle");
                            if (isValid(sub)) sub.innerHTML = getSubtitle(devData).innerHTML;
                        }
                    } else if (pos !== -1 && container.children.length >= i + 1) {
                        container.insertBefore(card, container.children[i]);
                        expand = true;
                    } else {
                        if (i === 0) {
                            container.prepend(card);
                            expand = true;
                        } else {
                            container.append(card);
                            expand = true;
                        }
                    }
                }
            }
        }
    }
}

function getSubtitle(device) {
    let subTitle = document.createElement("div");
    if (device["tag"] === "Wled" || device["tag"] === "Glimmr") {
        let a = document.createElement("a");
        a.href = "http://" + device["ipAddress"];
        a.innerText = device["ipAddress"];
        a.target = "_blank";
        subTitle.appendChild(a);
    } else {
        subTitle.textContent = device["ipAddress"];
    }

    if ((device.hasOwnProperty("multiZoneCount") || device.hasOwnProperty("ledCount")) && device["deviceTag"] !== "Lifx Bulb") {
        let val = (device.hasOwnProperty("multiZoneCount")) ? device["multiZoneCount"] : device["ledCount"];
        let count = document.createElement("span");
        count.innerText = " (" + val + ")";
        subTitle.appendChild(count);
    }
    return subTitle;
}

function createDeviceCard(device, addDemoText) {
    if (device["tag"] === "DreamScreen" && device["deviceTag"].includes("DreamScreen")) return;
    // Create main card
    let card = document.createElement("div");
    if (addDemoText) {
        card.id = "devCard";
    }
    card.classList.add("card", "m-4", "devCard");
    card.setAttribute("data-id", device["id"]);
    // Create card body
    let cardBody = document.createElement("div");
    cardBody.classList.add("card-body");
    // Create title/subtitle headers
    let title = document.createElement("h5");
    let subTitle = document.createElement("h6");
    title.classList.add("card-title");
    subTitle.classList.add("card-subtitle", "mb2", "text-muted", "ledCount");
    title.textContent = device["name"];
    subTitle.innerHTML = getSubtitle(device).innerHTML;
    // Create title row
    let titleRow = document.createElement("div");
    titleRow.classList.add("row", "titleRow");
    let titleCol = document.createElement("div");
    titleCol.classList.add("col-8", "titleCol", "exp");
    let iconCol = document.createElement("div");
    iconCol.classList.add("iconCol", "exp", "col-4");
    let image = document.createElement("img");
    image.classList.add("deviceIcon", "img-fluid");
    if (addDemoText) {
        image.id = "demoIcon";
    }
    image.setAttribute("data-bs-toggle", "tooltip");
    image.setAttribute("data-bs-placement", "top");
    image.setAttribute("data-title", "Flash/locate device");
    image.setAttribute("data-device", device["id"]);
    let tag = device["tag"];
    if (isValid(tag)) {
        if (isValid(device["deviceTag"]) && (tag === "Dreamscreen" || tag === "Lifx")) tag = device["deviceTag"];
        image.setAttribute("src", baseUrl + "/img/" + tag.toLowerCase().replace(" ", "") + "_icon.png");
    }

    // Settings column
    let settingsRow = document.createElement("div");
    settingsRow.classList.add("row", "align-items-end", "settingsCol", "pb-2", "justify-content-center", "exp");
    // Create enabled checkbox

    let enableButton = document.createElement("div");
    enableButton.classList.add("btn", "btn-outline-secondary", "btn-clear", "enableBtn", "pt-2");
    enableButton.setAttribute("data-target", device["id"]);
    enableButton.setAttribute("data-enabled", device["enable"]);
    if (addDemoText) {
        enableButton.id = "devEnableBtn";
    }
    // And the icon
    let eIcon = document.createElement("span");
    eIcon.classList.add("material-icons");
    if (device["enable"]) {
        enableButton.classList.add("active");
        eIcon.textContent = "cast_connected";
    } else {
        eIcon.textContent = "cast";
    }
    enableButton.appendChild(eIcon);

    let enableCol = document.createElement("div");
    enableCol.classList.add("btn-group", "settingsGroup", "pt-3");
    enableCol.appendChild(enableButton);

    let settingsButton = document.createElement("div");
    settingsButton.classList.add("btn", "btn-outline-secondary", "btn-clear", "settingBtn", "pt-2");
    settingsButton.setAttribute("data-target", device["id"]);
    if (addDemoText) {
        settingsButton.id = "devPrefBtn";
    }
    let sIcon = document.createElement("span");
    sIcon.classList.add("material-icons");
    sIcon.textContent = "settings";
    settingsButton.appendChild(sIcon);
    enableCol.appendChild(settingsButton);
    settingsRow.appendChild(enableCol);
    // Create settings button
    //Brightness slider

    // Slider
    let brightnessSlide = document.createElement("input");
    brightnessSlide.setAttribute("type", "range");
    brightnessSlide.setAttribute("data-target", device["id"]);
    brightnessSlide.setAttribute("data-attribute", "brightness");
    brightnessSlide.setAttribute("data-toggle", "tooltip");
    brightnessSlide.setAttribute("data-placement", "top");
    brightnessSlide.setAttribute("title", "Device brightness");
    let max = "100";
    if (isValid(device["maxBrightness"])) max = device["maxBrightness"].toString;
    brightnessSlide.setAttribute("min", "0");
    brightnessSlide.setAttribute("max", max);
    brightnessSlide.value = device["brightness"];
    brightnessSlide.classList.add("form-control", "w-100", 'custom-range');

    // Brightness column
    let brightnessRow = document.createElement("div");
    brightnessRow.classList.add("row", "pt-3", "brightRow", "brightSlider", "justify-content-center");
    if (addDemoText) brightnessRow.id = "devPrefBrightness";
    let brightCol = document.createElement("div");
    brightCol.classList.add("col-12", "brightCol");
    brightCol.appendChild(brightnessSlide);
    brightnessRow.appendChild(brightCol);

    // Put it all together
    iconCol.appendChild(image);
    titleCol.appendChild(title);
    titleCol.appendChild(subTitle);

    titleRow.appendChild(iconCol);
    titleRow.appendChild(titleCol);

    cardBody.appendChild(titleRow);
    cardBody.appendChild(settingsRow);
    cardBody.appendChild(brightnessRow);
    card.appendChild(cardBody);
    card.classList.add("min");
    return card;
}

function isValid(toCheck) {
    if (toCheck == null) return false;
    return toCheck !== "";
}

const isObject = (value) => typeof value === "object" && value !== null;

function getObj(group, key, val) {
    if (isValid(group)) {
        for (let i = 0; i < group.length; i++) {
            let obj = group[i];
            if (obj.hasOwnProperty(key)) {
                if (obj[key] === val) {
                    return obj;
                }
            }
        }
    }
    return null;
}

function setObj(group, key, val, obj) {
    if (isValid(group)) {
        for (let i = 0; i < group.length; i++) {
            let ex = group[i];
            if (ex.hasOwnProperty(key)) {
                if (ex[key] === val) {
                    group[i] = obj;
                }
            }
        }
    }
    return group;
}

function getDevices() {
    if (!isValid(data.Devices)) {
        sendMessage("scanDevices");
    }
}

function updateDevice(id, property, value) {
    let dev;
    let isLoaded = false;
    if (property === "stripMode") value = parseInt(value);
    if (isValid(deviceData) && deviceData["id"] === id) {
        dev = deviceData;
        isLoaded = true;
    } else {
        dev = data.getDevice(id);
    }
    if (property.includes("segmentOffset")) {
        let segmentId = parseInt(property.replace("segmentOffset", ""));
        let segments = dev["segments"];
        segments[segmentId]["offset"] = parseInt(value);
        value = segments;
        property = "segments";
    }
    if (isValid(dev) && dev.hasOwnProperty(property)) {
        dev[property] = value;
        data.setDevice(dev);
        sendMessage("updateDevice", dev, true);
    }
    if (isLoaded) {
        deviceData = dev;
        let ledProps = ["offset", "ledCount", "stripMode", "ledMultiplier"];
        if (ledProps.includes(property)) {
            appendLedMap();
        }
    }

}

function loadData() {
    sendMessage("store");
}


// Utility functions!
function $(elem) {
    return document.querySelector(elem);
}


function create(tag) {
    return document.createElement(tag);
}


const toggleExpansion = (element, to, duration = 350) => {
    return new Promise((res) => {
        requestAnimationFrame(() => {
            element.style.transition = `
						width ${duration}ms ease-in-out,
						height ${duration}ms ease-in-out,
						left ${duration}ms ease-in-out,
						top ${duration}ms ease-in-out,
						padding ${duration}ms ease-in-out,						
						margin ${duration}ms ease-in-out
					`;
            if (to.opacity !== null) {
                element.style.transition += `, opacity  ${duration}ms ease-in-out`;
                element.style.opacity = to.opacity;
            }
            element.style.top = to.top;
            element.style.left = to.left;
            element.style.width = to.width;
            element.style.height = to.height;
            element.style.padding = to.padding;


        });
        setTimeout(function () {
            let body = element.querySelector(".card-body");
            if (isValid(body)) {

                let exps = body.querySelectorAll(".exp");
                exps.forEach(function (row) {
                    row.style.transition = `
                width ${duration}ms ease-in-out,
                height ${duration}ms ease-in-out,
                left ${duration}ms ease-in-out,
                top ${duration}ms ease-in-out,
                padding ${duration}ms ease-in-out,
                margin ${duration}ms ease-in-out,
                order ${duration}ms ease-in-out
            `;

                    if (row.classList.contains("iconCol")) {
                        if (expanded) {
                            row.classList.add("col-md-2", "col-lg-1")
                        } else {
                            row.classList.remove("col-md-2", "col-lg-1")
                        }
                    }

                    if (row.classList.contains("titleCol")) {
                        if (expanded) {
                            row.classList.add("col-md-4", "col-lg-5")
                        } else {
                            row.classList.remove("col-md-4", "col-lg-5")
                        }
                    }
                });
            }
        }, duration / 2);

        setTimeout(res, duration);
    })
};


const showSettingsCard = async () => {
    settingsShown = true;
    const card = document.getElementById("mainSettingsCard");
    let mainContent = document.getElementById("mainContent");
    let oh = mainContent.offsetTop;
    let btn = document.querySelector(".mainSettings.mainSettings-lg span");
    let x = window.matchMedia("(max-width: 576px)");
    if (x.matches) btn = document.querySelector(".mainSettings.mainSettings-sm span");
    let fh = document.getElementById("footer").offsetHeight;
    // This is the proper height for the dev card
    fh += oh;
    let rect = btn.getBoundingClientRect();
    let left = rect.left;
    let width = rect.width;
    let h = 'calc(100% - ' + fh + 'px)';
    card.style.position = 'fixed';
    card.style.top = oh + 'px';
    card.style.left = left + 'px';
    card.style.width = width + 'px';
    card.style.height = h;
    card.style.opacity = "0";
    await toggleExpansion(card, {top: oh + 'px', left: 0, width: '100%', height: h, opacity: "100%"}, 250);
};

const hideSettingsCard = async () => {
    settingsShown = false;
    const card = document.getElementById("mainSettingsCard");
    let mainContent = document.getElementById("mainContent");
    let oh = mainContent.offsetTop;
    let btn = document.querySelector(".mainSettings.mainSettings-lg span");
    let x = window.matchMedia("(max-width: 576px)");
    if (x.matches) btn = document.querySelector(".mainSettings.mainSettings-sm span");
    let fh = document.getElementById("footer").offsetHeight;
    fh += oh;
    let rect = btn.getBoundingClientRect();
    let left = rect.left;
    let width = rect.width;
    let h = 'calc(100% - ' + fh + 'px)';
    card.style.display = 'block';
    await toggleExpansion(card, {
        top: oh + "px",
        left: left + "px",
        width: width + "px",
        height: h + 'px',
        opacity: "0"
    }, 250);
};

const showDeviceCard = async (e) => {
    expanded = true;
    const card = (e.parentElement.parentElement.parentElement.parentElement);
    baseCard = card;
    // clone the card
    cardClone = card.cloneNode(true);
    cardClone.classList.remove("devCard", "m-4");
    cardClone.classList.add("container-fluid");
    // get the location of the card in the view
    const {top, left, width, height} = card.getBoundingClientRect();
    toggleWidth = width;
    toggleHeight = height;
    toggleTop = top;
    toggleLeft = left;
    // position the clone on top of the original
    cardClone.style.position = 'fixed';
    cardClone.style.top = top + 'px';
    cardClone.style.left = left + 'px';
    cardClone.style.width = width + 'px';
    cardClone.style.height = height + 'px';
    cardClone.style.display = 'block';
    cardClone.style.overflowY = "scroll";

    // hide the original card with opacity
    //card.style.opacity = '0';
    // add card to the main container
    document.querySelector(".main").appendChild(cardClone);
    let body = document.querySelector("div.card.container-fluid > div");
    let cardRow = document.getElementById("mainContent");
    let oh = cardRow.offsetTop;
    let fh = document.getElementById("footer").offsetHeight;
    // This is the proper height for the dev card
    fh += oh;
    // remove the display style so the original content is displayed right
    let sepDiv = document.createElement("div");
    sepDiv.classList.add("dropdown-divider", "row");

    body.appendChild(sepDiv);
    // Create settings for our card
    let settingsButtons = document.querySelectorAll(".mainSettings");
    for (let i = 0; i < settingsButtons.length; ++i) {
        settingsButtons[i].classList.add('d-none');
    }
    for (let i = 0; i < closeButton.length; ++i) {
        closeButton[i].classList.remove('d-none');
    }
    // Expand that bish
    await toggleExpansion(cardClone, {
        top: oh + "px",
        left: 0,
        width: '100%',
        height: 'calc(100% - ' + fh + 'px)'
    }, 250).then(function () {
        let bCol = cardClone.querySelector(".brightCol");
        bCol.classList.add("col-md-8", "col-lg-6");
    });
    createDeviceSettings();


};

function createDeviceSettings() {
    let card = document.querySelector("div.card.container-fluid");
    let st = card.scrollTop;
    let container = document.querySelector("div.card.container-fluid > div");
    if (deviceData === undefined) deviceData = JSON.parse(ledData);
    console.log("Loading device data: ", deviceData);
    document.querySelectorAll(".delSetting").forEach(e => e.remove());
    let props = deviceData["keyProperties"];
    if (isValid(props)) {
        let id = deviceData["id"];
        let keys = [];
        for (let i = 0; i < props.length; i++) {
            keys.push(props[i]["valueType"]);
        }
        let mapProps = ["ledmap", "beamMap", "nanoleaf", "hue", "sectormap", "sectorLedMap"];
        let addLink = false;

        if (keys.includes("nanoleaf") || keys.includes("hue")) {
            addLink = true;
        }

        if (addLink) {
            let linkRow = document.createElement("div");
            linkRow.classList.add("row", "justify-content-center", "delSetting", "pb-3", "pt-3");
            linkRow.id = "linkCol";
            container.appendChild(linkRow);
        }

        let addMap = false;
        for (let i = 0; i < mapProps.length; i++) {
            if (keys.includes(mapProps[i])) addMap = true;
        }

        if (addMap) {
            let mapRow = document.createElement("div");
            let mapWrap = document.createElement("div");
            mapRow.classList.add("row", "justify-content-center", "delSetting", "pb-4", "pt-1");
            mapWrap.id = "mapWrap";
            mapWrap.classList.add("col-12", "col-md-8", "col-lg-6");
            mapRow.id = "mapCol";
            mapRow.appendChild(mapWrap);
            container.appendChild(mapRow);
        }

        let devPrefRow = document.createElement("div");
        devPrefRow.classList.add("row", "justify-content-center", "pb-3", "pt-3", "delSetting", "devPrefs");

        let devPrefCol = document.createElement("div");
        devPrefCol.classList.add("col-12", "col-md-8", "col-lg-6", "border");

        let addRow = false;
        for (let i = 0; i < props.length; i++) {
            let prop = props[i];
            let propertyName = prop["valueName"];
            propertyName = propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
            let elem, se;
            let value = deviceData[propertyName];
            if (propertyName.includes("segmentOffset")) {
                let segments = deviceData["segments"];
                let segmentId = parseInt(propertyName.replace("segmentOffset", ""));
                value = segments[segmentId]["offset"];
            }
            let dirCol = document.createElement("div");
            dirCol.classList.add("row", "justify-content-center", "delSetting");
            switch (prop["valueType"]) {
                case "text":
                case "check":
                case "ledMultiplier":
                    elem = new SettingElement(prop["valueLabel"], prop["valueType"], id, propertyName, value, prop["valueHint"]);
                    elem.isDevice = true;
                    break;
                case "number":
                    elem = new SettingElement(prop["valueLabel"], "number", id, propertyName, value, prop["valueHint"], prop["valueMin"], prop["valueMax"], prop["valueStep"]);
                    elem.isDevice = true;
                    break;
                case "ledmap":
                    drawLedMap = true;
                    appendLedMap();
                    break;
                case "beamMap":
                    drawLedMap = true;
                    appendBeamLedMap();
                    appendBeamMap();
                    break;
                case "sectorLedMap":
                    drawLedMap = true;
                    appendSectorLedMap();
                    break;
                case "select":
                    elem = new SettingElement(prop["valueLabel"], "select", id, propertyName, value, prop["valueHint"]);
                    elem.options = prop["options"];
                    break;
                case "sectormap":
                    let region = "flashSector";
                    let dirString = "Click a sector above to assign it to your " + deviceData["tag"] + " device.";

                    if (deviceData["tag"] === "Wled" && deviceData["stripMode"] === 3) {
                        region = "wledSector";
                    }
                    if (deviceData["tag"] === "Lifx" && !deviceData["hasMultiZone"]) {
                        region = "lifxSector";
                    }
                    if (deviceData["tag"] === "dreamScreen") {
                        region = "dreamSector";
                    }
                    if (region === "flashSector") {
                        dirString = "Click a tile to select it's target sector.";
                    }
                    let mapDiv = document.getElementById("mapWrap");
                    createSectorMap(mapDiv, region);
                    dirCol.innerHTML = dirString;
                    container.appendChild(dirCol);
                    break;
                case "nanoleaf":
                    if (isValid(deviceData["token"]) && isValid(deviceData["layout"]["positionData"])) {
                        let linkPane = createLinkPane("nanoleaf", true);
                        let mapCol = document.getElementById("mapWrap");
                        createSectorMap(mapCol, "flashSector");
                        document.getElementById("linkCol").append(linkPane);
                        let stageRow = document.createElement("div");
                        stageRow.id = "stageRow";
                        stageRow.classList.add("row", "justify-content-center", "delSetting");
                        let stageCol = document.createElement("div");
                        stageCol.id = "stageCol";
                        stageCol.classList.add("col-12", "col-md-8", "col-lg-6");
                        stageRow.appendChild(stageCol);
                        dirCol.innerHTML = "Click a tile to select a target sector.";
                        container.appendChild(dirCol);
                        container.appendChild(stageRow);
                        drawNanoShapes(deviceData);
                    } else {
                        let linkPane = createLinkPane("nanoleaf", false);
                        document.getElementById("linkCol").append(linkPane);
                    }
                    break;
                case "hue":
                    if (isValid(deviceData["token"])) {
                        let linkPane = createLinkPane("hue", true);
                        let mapCol = document.getElementById("mapWrap");
                        let hueMap = createHueMap();
                        createSectorMap(mapCol, "flashSector");
                        dirCol.innerHTML = "Select the target sector for each light below.";
                        container.appendChild(dirCol);

                        document.getElementById("linkCol").append(linkPane);
                        container.append(hueMap);
                    } else {
                        let linkPane = createLinkPane("hue", false);
                        document.getElementById("linkCol").append(linkPane);
                    }
                    //addRow = true;
                    break;
            }

            if (isValid(elem)) {
                elem.isDevice = true;
                se = createSettingElement(elem);
                if (demoLoaded) se.id = propertyName;
                addRow = true;
                devPrefCol.appendChild(se);
                devPrefRow.appendChild(devPrefCol);
            }
        }

        if (deviceData["tag"] !== "Led") {
            let removeBtn = new SettingElement("Remove device", "button", id, "removeDevice", id);
            devPrefCol.appendChild(createSettingElement(removeBtn));
            devPrefRow.appendChild(devPrefCol);
            addRow = true;
        }
        if (addRow) container.appendChild(devPrefRow);
        card = document.querySelector("div.card.container-fluid");
        if (st !== card.scrollTop) card.scrollTop = st;
    }
}


function createSettingElement(settingElement) {
    let group = document.createElement("div");
    group.classList.add("col-12", "align-self-top", "form-group", "custom-control");
    let label = document.createElement("label");
    label.innerText = settingElement.descrption;
    let element;
    switch (settingElement.type) {
        case "check":
            element = document.createElement("input");
            label.classList.add("custom-control-label");
            group.classList.add("custom-control");
            group.classList.add("custom-switch");
            group.classList.add("form-check-dev");
            element.classList.add("custom-control-input");
            element.type = "checkbox";
            element.id = "customSwitch" + deviceData.id;
            element.checked = settingElement.value;
            break;
        case "select":
            element = document.createElement("select");
            if (isValid(settingElement.options)) {
                for (const [key, value] of Object.entries(settingElement.options)) {
                    let option = document.createElement("option");
                    option.value = key.toString();
                    option.innerText = value.toString();
                    if (key.toString() === settingElement.value.toString()) option.selected = true;
                    element.appendChild(option);
                }
            }
            element.value = settingElement.value;
            break;
        case "number":
            element = document.createElement("input");
            element.type = "number";
            element.min = settingElement.minValue;
            element.max = settingElement.maxValue;
            element.step = settingElement.increment;
            element.value = settingElement.value;
            break;
        case "text":
            element = document.createElement("input");
            element.type = "text";
            element.value = settingElement.value;
            break;
        case "button":
            element = document.createElement("div");
            element.classList.add("btn", "btn-clear", "btn-danger", "removeDevice");
            let icon = document.createElement("span");
            icon.classList.add("material-icons");
            icon.textContent = "delete";
            element.appendChild(icon);
            break;
        case "ledMultiplier":
            label.innerHTML = "LED Scale";
            element = document.createElement("input");
            element.classList.add("multiSlider");
            element.type = "number";
            element.min = "0.125";
            element.max = "5";
            element.step = "0.025";
            element.value = settingElement.value;
            settingElement.hint = "Multiply or divide the number of LEDs that are pulled from the grid.";
    }
    if (settingElement.type !== "check") {
        label.classList.add("form-label");
        if (isValid(element)) {
            element.classList.add("form-control");
        }
        group.appendChild(label);
    }


    if (isValid(element)) {
        if (settingElement.isDevice) {
            element.classList.add("devSetting");
        }
        element.setAttribute("data-property", settingElement.property);
        element.setAttribute("data-object", settingElement.object);
        if (isValid(settingElement.id)) {
            element.setAttribute("data-id", settingElement.id);
        }
        group.append(element);
    }
    if (settingElement.type === "check") {
        group.appendChild(label);
    }

    // Append hint
    let hint = document.createElement("div");
    hint.classList.add("form-text");

    if (settingElement.hint !== "") {
        hint.innerText = settingElement.hint;
    }
    group.append(hint);
    return group;
}

function SettingElement(description, type, object, property, value, hint, minLimit, maxLimit, increment, options, id, isDevice) {
    property = property.charAt(0).toLowerCase() + property.slice(1);
    this.descrption = description;
    this.type = type;
    this.object = object;
    this.property = property ?? {};
    this.hint = hint ?? "";
    this.increment = increment ?? 1;
    this.options = options;
    this.id = id;
    this.value = value;
    this.isDevice = isDevice ?? false;
}


function createLinkPane(type, linked) {
    let linkRow = document.createElement("div");
    linkRow.classList.add("col-12", "row", "justify-content-center", "delSetting");
    let statusText = document.createElement("div");
    statusText.classList.add("header", "text-light");
    statusText.innerText = linked ? "Device is linked" : "Click here to link";
    let linkCol = document.createElement("div");
    linkCol.classList.add("col-8", "col-sm-6", "col-md-4", "col-lg-3", "col-xl-2", "linkDiv");
    linkCol.setAttribute("data-type", type);
    linkCol.setAttribute("data-id", deviceData["id"]);
    linkCol.setAttribute("data-linked", linked);
    let deviceIcon = document.createElement("img");
    deviceIcon.classList.add("img-fluid");
    deviceIcon.src = "./img/" + type + "_icon.png";
    let statusImage = document.createElement("img");
    statusImage.classList.add("linkImg", linked ? "linked" : "unlinked");
    let timer = document.createElement("div");
    timer.classList.add("hide");
    timer.id = "CircleBar";
    linkCol.appendChild(deviceIcon);
    linkCol.appendChild(statusImage);
    linkCol.appendChild(timer);
    linkRow.appendChild(statusText);
    linkRow.appendChild(linkCol);
    return linkRow;
}

function updateBeamLayout(items) {
    let beamLayout = deviceData["beamLayout"];
    if (isValid(beamLayout)) {
        let existing = beamLayout["segments"];
        let sorted = [];
        for (let i = 0; i < items.length; i++) {
            let pos = parseInt(items[i].getAttribute("data-position"));
            for (let ex = 0; ex < existing.length; ex++) {
                if (existing[ex]["position"] === pos) {
                    sorted.push(existing[ex]);
                }
            }
        }
        beamLayout["segments"] = [];
        for (let i = 0; i < sorted.length; i++) {
            let seg = sorted[i];
            seg["position"] = i;
            beamLayout["segments"].push(seg);
        }
        updateDevice(deviceData["id"], "beamLayout", beamLayout);
    }
}

function appendBeamMap() {
    let settingsDiv = document.querySelector(".card.container-fluid > .card-body");

    if (deviceData.hasOwnProperty("beamLayout")) {
        let beamLayout = deviceData["beamLayout"];
        if (isValid(beamLayout)) {
            let items = beamLayout["segments"];
            if (items.length > 0) {
                let beamRow = document.createElement("div", "delSetting");
                beamRow.id = "BeamDiv";
                beamRow.classList.add("row", "justify-content-center", "delSetting", "pt-2");

                let beamCol = document.createElement("div");
                beamCol.classList.add("sortable", "col-12", "col-md-8", "col-lg-6");

                items.sort((a, b) => (a["position"] > b["position"]) ? 1 : -1);
                for (let i = 0; i < items.length; i++) {
                    let item = items[i];
                    let position = item["position"];
                    let offset = item["offset"];
                    let repeat = item["repeat"];
                    let reverse = item["reverse"];
                    let count = item["ledCount"];
                    let itemDiv = document.createElement("div");
                    itemDiv.classList.add("beamItem", "form-inline");
                    itemDiv.setAttribute("data-position", position);
                    if (count === 1) {
                        itemDiv.setAttribute("data-type", "corner");
                    } else {
                        itemDiv.setAttribute("data-type", "beam");
                    }

                    // drag handle
                    let dragHandle = document.createElement("span");
                    dragHandle.classList.add("material-icons", "dragHandle");
                    dragHandle.innerText = "drag_handle";
                    itemDiv.appendChild(dragHandle);

                    let nameLabel = document.createElement("div");
                    nameLabel.classList.add("col-12", "headerCol", "text-center");
                    nameLabel.innerText = (count === 1 ? "Corner" : "Beam") + " " + position;
                    itemDiv.appendChild(nameLabel);

                    let oGroup = document.createElement("div");
                    oGroup.classList.add("form-group");

                    // Offset
                    let label2 = document.createElement("label");
                    label2.innerText = "Offset";
                    label2.classList.add("my-1", "mr-2");
                    let offsetText = document.createElement("input");
                    offsetText.type = "number";
                    offsetText.value = offset;
                    offsetText.classList.add("custom-select", "beam-control");
                    offsetText.setAttribute("data-position", position);
                    offsetText.setAttribute("data-beamProperty", "Offset");

                    oGroup.appendChild(label2);
                    oGroup.appendChild(offsetText);
                    itemDiv.appendChild(oGroup);

                    // Repeat
                    let rGroup = document.createElement("div");
                    rGroup.classList.add("form-group", "custom-control", "custom-switch");

                    let checkDiv1 = document.createElement("div");
                    checkDiv1.classList.add("form-check");
                    let label3 = document.createElement("label");
                    label3.innerText = "Repeat";
                    label3.classList.add("custom-control-label");
                    let rCheck = document.createElement("input");
                    rCheck.type = "checkbox";
                    if (repeat) rCheck.checked = true;
                    rCheck.classList.add("custom-control-input", "beam-control");
                    rCheck.setAttribute("data-position", position);
                    rCheck.setAttribute("data-beamProperty", "Repeat");
                    checkDiv1.appendChild(rCheck);
                    checkDiv1.appendChild(label3);

                    rGroup.appendChild(checkDiv1);
                    itemDiv.appendChild(rGroup);

                    // Reverse
                    let rGroup2 = document.createElement("div");
                    rGroup2.classList.add("form-group", "custom-control", "custom-switch");
                    let checkDiv2 = document.createElement("div");
                    checkDiv2.classList.add("form-check");
                    let label4 = document.createElement("label");
                    label4.innerText = "Reverse";
                    label4.classList.add("custom-control-label");
                    let rCheck2 = document.createElement("input");
                    rCheck2.classList.add("custom-control-input", "beam-control");
                    rCheck2.type = "checkbox";
                    if (reverse) rCheck2.checked = true;
                    rCheck2.setAttribute("data-position", position);
                    rCheck2.setAttribute("data-beamProperty", "Reverse");
                    checkDiv2.appendChild(rCheck2);
                    checkDiv2.appendChild(label4);

                    rGroup2.appendChild(checkDiv2);
                    itemDiv.appendChild(rGroup2);
                    beamCol.appendChild(itemDiv);
                    beamRow.appendChild(beamCol);
                }
                settingsDiv.appendChild(beamRow);

                sortable(".sortable");
                sortable('.sortable')[0].addEventListener('sortupdate', function (e) {
                    let items = e.detail.destination.items;
                    updateBeamLayout(items);
                });
            }

        }
    }
}

function appendLedMap() {
    let mapDiv = document.getElementById("mapWrap");
    createLedMap(mapDiv);
}

function appendSectorLedMap() {
    let targetElement = document.getElementById("mapWrap");
    let sd = data.SystemData;
    let colorClasses = [
        "ledRed",
        "ledOrange",
        "ledYellow",
        "ledGreen",
        "ledBlue",
        "ledIndigo",
        "ledViolet",
        "ledRed",
        "ledOrange",
        "ledYellow",
        "ledGreen",
        "LedBlue",
        "ledIndigo",
        "ledViolet"
    ];

    if (!isValid(deviceData)) {
        console.log("Invalid device data...");
        return;
    }

    let segments = deviceData["segments"];
    let total = sd["ledCount"];
    let rangeList = [];
    for (let s = 0; s < segments.length; s++) {
        let offset = segments[s]["offset"];
        let len = segments[s]["len"];
        rangeList.push(ranges(total, offset, len));
    }
    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;
    let h = (w / 16) * 9;
    let imgT = tgt.offsetTop;
    let exMap = targetElement.querySelector("#ledMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1080;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let flHeight = (h - hMargin - hMargin) / leftCount;
    let frHeight = (h - hMargin - hMargin) / rightCount;
    let ftWidth = (w - wMargin - wMargin) / topCount;
    let fbWidth = (w - wMargin - wMargin) / bottomCount;
    let dHeight = (flHeight + frHeight) / 2;
    let dWidth = ((ftWidth + fbWidth) / 2);
    let map = document.createElement("div");
    map.id = "ledMap";
    map.classList.add("ledMap", "delSetting");
    map.style.top = imgT + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    map.style.position = "relative";
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    let ledCount = 0;
    for (let i = 0; i < rightCount; i++) {
        t = h - hMargin - ((i + 1) * frHeight);
        b = t + frHeight;
        l = w - wMargin - dWidth;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", sd["ledCount"].toString() + "/" + (ledCount).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    for (let i = 0; i < topCount - 1; i++) {
        l = w - wMargin - (ftWidth * (i + 1));
        r = l - ftWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = ftWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    // Left, top-bottom
    for (let i = 0; i < leftCount - 1; i++) {
        t = hMargin + (i * flHeight);
        b = t + flHeight;
        l = wMargin;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = dWidth.toString() + "px";
        s1.style.height = flHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    // This one, stupid
    for (let i = 0; i < bottomCount; i++) {
        t = h - hMargin - dHeight;
        b = t + dHeight;
        l = wMargin + (fbWidth * (i));
        r = l + fbWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = dHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }
    targetElement.appendChild(map);
}

function appendBeamLedMap() {
    let cardBody = document.querySelector("div.card.container-fluid > .card-body");
    let dirCol = document.createElement("div");
    dirCol.classList.add("row", "justify-content-center", "delSetting");
    dirCol.innerHTML = "Note: Lifx beams are spaced so that 10 LEDs/beam equal 20 LEDs within Glimmr.";
    cardBody.appendChild(dirCol);
    createBeamLedMap();
}

function createSectorCenter(targetElement, regionName) {
    let exMap = targetElement.querySelector("#sectorMap");
    if (isValid(exMap)) exMap.remove();
    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;
    let h = (w / 16) * 9;
    let imgL = tgt.offsetLeft;
    let imgT = tgt.offsetTop;
    let selected = -1;
    if (isValid(deviceData)) selected = deviceData["targetSector"];
    if (!isValid(selected)) selected = -1;
    let wFactor = w / 1920;
    let hFactor = h / 1080;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let fHeight = (h - hMargin - hMargin) / vSectors;
    let fWidth = (w - wMargin - wMargin) / hSectors;
    let map = document.createElement("div");
    map.id = "sectorMap";
    map.classList.add("sectorMap", "delSetting");
    map.style.top = imgT + "px";
    map.style.left = imgL + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    // Bottom-right, rtl, bottom to top
    let t = hFactor - hMargin - fHeight + h;
    let sector = 1;
    for (let v = vSectors; v > 0; v--) {
        let l = wFactor - wMargin - fWidth + w;
        for (let h = hSectors; h > 0; h--) {
            let s1 = document.createElement("div");
            s1.classList.add("sector");
            if (isValid(regionName)) s1.classList.add(regionName + "Region");
            if (sector.toString() === selected.toString()) s1.classList.add("checked");
            s1.setAttribute("data-sector", sector.toString());
            s1.style.position = "absolute";
            s1.style.top = t.toString() + "px";
            s1.style.left = l.toString() + "px";
            s1.style.width = fWidth.toString() + "px";
            s1.style.height = fHeight.toString() + "px";
            s1.innerText = sector.toString();
            map.appendChild(s1);
            sector++;
            l -= fWidth;
        }
        t -= fHeight;
    }
    targetElement.appendChild(map);
    if (isValid(deviceData) && expanded) {
        let mappedLights;
        if (isValid(deviceData["mappedLights"])) {
            mappedLights = deviceData["mappedLights"];
        }
        if (isValid(mappedLights)) {
            for (let i = 0; i < mappedLights.length; i++) {
                let lMap = mappedLights[i];
                let target = lMap["targetSector"];
                let targetDiv = document.querySelector('.sector[data-sector="' + target + '"]');
                if (isValid(targetDiv)) {
                    targetDiv.classList.add("checked");
                }
            }
        }
    }
}

function createSectorMap(targetElement, regionName) {
    if (useCenter) {
        createSectorCenter(targetElement, regionName);
        return;
    }
    let exMap = targetElement.querySelector("#sectorMap");
    if (isValid(exMap)) exMap.remove();

    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;
    let h = (w / 16) * 9;
    let selected = -1;
    if (isValid(deviceData)) selected = deviceData["targetSector"];
    if (!isValid(selected)) selected = -1;
    let wFactor = w / 1920;
    let hFactor = h / 1080;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let fHeight = (h - hMargin - hMargin) / vSectors;
    let fWidth = (w - wMargin - wMargin) / hSectors;
    let map = document.createElement("div");
    map.id = "sectorMap";
    map.classList.add("sectorMap", "delSetting");
    map.style.width = w + "px";
    map.style.height = h + "px";
    map.style.position = "relative";
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    let sector = 1;
    for (let i = 0; i < vSectors; i++) {
        t = h - hMargin - ((i + 1) * fHeight);
        b = t + fHeight;
        l = w - wMargin - fWidth;
        r = l + fWidth;
        let s1 = document.createElement("div");
        s1.classList.add("sector");
        if (isValid(regionName)) s1.classList.add(regionName + "Region");
        if (sector.toString() === selected.toString()) s1.classList.add("checked");
        s1.setAttribute("data-sector", sector.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fWidth.toString() + "px";
        s1.style.height = fHeight.toString() + "px";
        s1.innerText = sector.toString();
        map.appendChild(s1);
        sector++;
    }

    for (let i = 1; i < hSectors - 1; i++) {
        l = w - wMargin - (fWidth * (i + 1));
        r = l - fWidth;
        let s1 = document.createElement("div");
        s1.classList.add("sector");
        if (sector.toString() === selected.toString()) s1.classList.add("checked");
        if (isValid(regionName)) s1.classList.add(regionName + "Region");
        s1.setAttribute("data-sector", sector.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fWidth.toString() + "px";
        s1.style.height = fHeight.toString() + "px";
        s1.innerText = sector.toString();
        map.appendChild(s1);
        sector++;
    }

    // Left, top-bottom
    for (let i = 0; i < vSectors - 1; i++) {
        t = hMargin + (i * fHeight);
        b = t + fHeight;
        l = wMargin;
        r = l + fWidth;
        let s1 = document.createElement("div");
        s1.classList.add("sector");
        if (sector.toString() === selected.toString()) s1.classList.add("checked");
        if (isValid(regionName)) s1.classList.add(regionName + "Region");
        s1.setAttribute("data-sector", sector.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fWidth.toString() + "px";
        s1.style.height = fHeight.toString() + "px";
        s1.innerText = sector.toString();
        map.appendChild(s1);
        sector++;
    }

    // This one, stupid
    for (let i = 0; i < hSectors - 1; i++) {
        t = h - hMargin - fHeight;
        b = t + fHeight;
        l = wMargin + (fWidth * (i));
        r = l + fWidth;
        let s1 = document.createElement("div");
        s1.classList.add("sector");
        if (isValid(regionName)) s1.classList.add(regionName + "Region");
        if (sector.toString() === selected.toString()) s1.classList.add("checked");
        s1.setAttribute("data-sector", sector.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fWidth.toString() + "px";
        s1.style.height = fHeight.toString() + "px";
        s1.innerText = sector.toString();
        map.appendChild(s1);
        sector++;
    }

    targetElement.appendChild(map);
    if (isValid(deviceData) && expanded) {
        let mappedLights;
        if (isValid(deviceData["mappedLights"])) {
            mappedLights = deviceData["mappedLights"];
        }
        if (isValid(mappedLights)) {
            for (let i = 0; i < mappedLights.length; i++) {
                let lMap = mappedLights[i];
                let target = lMap["targetSector"];
                let targetDiv = document.querySelector('.sector[data-sector="' + target + '"]');
                if (isValid(targetDiv)) {
                    targetDiv.classList.add("checked");
                }
            }
        }
        let tileLayout = deviceData.Layout;
        if (isValid(tileLayout)) {
            let positionData = tileLayout.PositionData;
            if (isValid(positionData)) {
                for (let i = 0; i < positionData.length; i++) {
                    let target = positionData[i]["targetSector"];
                    let targetDiv = document.querySelector('.sector[data-sector="' + target + '"]');
                    if (isValid(targetDiv)) {
                        targetDiv.classList.add("checked");
                    }
                }
            }
        }

    }
}

function createBeamLedMap() {
    let targetElement = document.getElementById("mapWrap");
    let sd = data.SystemData;
    let colorClasses = [
        "ledRed",
        "ledOrange",
        "ledYellow",
        "ledGreen",
        "ledBlue",
        "ledIndigo",
        "ledViolet",
        "ledRed",
        "ledOrange",
        "ledYellow",
        "ledGreen",
        "LedBlue",
        "ledIndigo",
        "ledViolet"
    ];

    if (!isValid(deviceData)) {
        console.log("Invalid device data...");
        return;
    }

    let beamLayout = deviceData["beamLayout"];
    let segments = beamLayout["segments"];
    let total = sd["ledCount"];
    let rangeList = [];
    for (let s = 0; s < segments.length; s++) {
        let offset = segments[s]["offset"];
        let len = segments[s]["ledCount"];
        if (segments[s]["repeat"]) len = 1;
        len *= 2;
        rangeList.push(ranges(total, offset, len));
    }
    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;
    let h = (w / 16) * 9;
    let imgT = tgt.offsetTop;
    let exMap = targetElement.querySelector("#ledMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1080;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let flHeight = (h - hMargin - hMargin) / leftCount;
    let frHeight = (h - hMargin - hMargin) / rightCount;
    let ftWidth = (w - wMargin - wMargin) / topCount;
    let fbWidth = (w - wMargin - wMargin) / bottomCount;
    let dHeight = (flHeight + frHeight) / 2;
    let dWidth = ((ftWidth + fbWidth) / 2);
    let map = document.createElement("div");
    map.id = "ledMap";
    map.classList.add("ledMap", "delSetting");
    map.style.top = imgT + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    map.style.position = "relative";
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    let ledCount = 0;
    for (let i = 0; i < rightCount; i++) {
        t = h - hMargin - ((i + 1) * frHeight);
        b = t + frHeight;
        l = w - wMargin - dWidth;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", sd["ledCount"].toString() + "/" + (ledCount).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    for (let i = 0; i < topCount - 1; i++) {
        l = w - wMargin - (ftWidth * (i + 1));
        r = l - ftWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = ftWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    // Left, top-bottom
    for (let i = 0; i < leftCount - 1; i++) {
        t = hMargin + (i * flHeight);
        b = t + flHeight;
        l = wMargin;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = dWidth.toString() + "px";
        s1.style.height = flHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    // This one, stupid
    for (let i = 0; i < bottomCount; i++) {
        t = h - hMargin - dHeight;
        b = t + dHeight;
        l = wMargin + (fbWidth * (i));
        r = l + fbWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        for (let r = 0; r < rangeList.length; r++) {
            let colClass = colorClasses[r];
            let range = rangeList[r];
            if (range.includes(ledCount)) s1.classList.add(colClass);
        }
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = dHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }
    targetElement.appendChild(map);
}

function createLedMap(targetElement) {
    let range;
    let sd = data.SystemData;
    let count = 0;
    if (isValid(deviceData)) {
        count = deviceData["ledCount"];
        let offset = deviceData["offset"];
        let mode = deviceData["stripMode"];
        let total = sd["ledCount"];
        if (isValid(mode) && mode === 2) count /= 2;
        if (isValid(deviceData["ledMultiplier"])) {
            let mult = deviceData["ledMultiplier"];
            if (mult === 0) mult = 1;
            count *= mult;
        }
        range = ranges(total, offset, count);
        console.log("Range created: ", range);
    }

    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;

    let h = (w / 16) * 9;
    let imgT = tgt.offsetTop;
    let exMap = targetElement.querySelector("#ledMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1080;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let flHeight = (h - hMargin - hMargin) / leftCount;
    let frHeight = (h - hMargin - hMargin) / rightCount;
    let ftWidth = (w - wMargin - wMargin) / topCount;
    let fbWidth = (w - wMargin - wMargin) / bottomCount;
    let dHeight = (flHeight + frHeight) / 2;
    let dWidth = ((ftWidth + fbWidth) / 2);
    let map = document.createElement("div");
    map.id = "ledMap";
    map.classList.add("ledMap", "delSetting");
    if (targetElement.id !== "sLedWrap") map.style.top = imgT + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    map.style.position = "relative";
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    let index = 0;
    let reverse = isValid(deviceData && deviceData["reverseStrip"]) ? deviceData["reverseStrip"] : false;
    console.log("Reversed? ", reverse);
    for (let i = 0; i < rightCount; i++) {
        t = h - hMargin - ((i + 1) * frHeight);
        b = t + frHeight;
        l = w - wMargin - dWidth;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        if (isValid(range) && range.includes(index)) {
            s1.classList.add("highLed");
        }

        s1.setAttribute("data-sector", index.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", sd["ledCount"].toString() + "/" + (index).toString());
        } else {
            s1.setAttribute("title", index.toString());
        }
        map.appendChild(s1);
        index++;
    }

    for (let i = 0; i < topCount - 1; i++) {
        l = w - wMargin - (ftWidth * (i + 1));
        r = l - ftWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        if (isValid(range) && range.includes(index)) {
            s1.classList.add("highLed");
        }
        s1.setAttribute("data-sector", index.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = ftWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", index.toString() + "/" + (index + 1).toString());
            index++;
            let clone = s1.cloneNode();
            clone.setAttribute("data-sector", index);
            map.appendChild(clone);
        } else {
            s1.setAttribute("title", index.toString());
        }
        map.appendChild(s1);
        index++;
    }

    // Left, top-bottom
    for (let i = 0; i < leftCount - 1; i++) {
        t = hMargin + (i * flHeight);
        b = t + flHeight;
        l = wMargin;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        if (isValid(range) && range.includes(index)) {
            s1.classList.add("highLed");
        }
        s1.setAttribute("data-sector", index.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = dWidth.toString() + "px";
        s1.style.height = flHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", index.toString() + "/" + (index + 1).toString());
            index++;
            let clone = s1.cloneNode();
            clone.setAttribute("data-sector", index);
            map.appendChild(clone);
        } else {
            s1.setAttribute("title", index.toString());
        }
        map.appendChild(s1);
        index++;
    }

    // This one, stupid
    for (let i = 0; i < bottomCount - 1; i++) {
        t = h - hMargin - dHeight;
        b = t + dHeight;
        l = wMargin + (fbWidth * (i));
        r = l + fbWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        if (isValid(range) && range.includes(index)) {
            s1.classList.add("highLed");
        }
        s1.setAttribute("data-sector", index.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = dHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        if (i === 0) {
            s1.setAttribute("title", index.toString() + "/" + (index + 1).toString());
            index++;
            let clone = s1.cloneNode();
            clone.setAttribute("data-sector", index);
            map.appendChild(clone);
        } else {
            s1.setAttribute("title", index.toString());
        }
        map.appendChild(s1);
        index++;
    }
    targetElement.appendChild(map);
    if (isValid(range) && range.length) {
        let target = range[0];
        if (reverse) {
            target = range[range.length - 1];
        }
        let tLed = document.querySelector('.led[data-sector="' + target + '"]');
        if (isValid(tLed)) {
            tLed.classList.add("firstLed");
        }
    }
}

function createHueMap() {
    let selectedGroup = deviceData["selectedGroup"];
    let groups = deviceData["groups"];
    let group = deviceData["selectedGroup"];
    let devBrightness = deviceData["brightness"];
    for (let i = 0; i < groups.length; i++) {
        let sg = groups[i];
        if (sg["id"] === selectedGroup) {
            group = sg;
        }
    }

    // Main container
    let hueMapRow = document.createElement("div");
    hueMapRow.classList.add("row", "justify-content-center");
    // Group select row
    let groupSelectCol = document.createElement("div");
    groupSelectCol.classList.add("col-12", "col-md-8", "col-lg-6", "delSetting", "pb-4");
    // Group select
    let gLabel = document.createElement("label");
    gLabel.classList.add("form-label");
    gLabel.innerHTML = "Entertainment Group";
    let groupSelect = document.createElement("select");
    groupSelect.setAttribute("data-property", "selectedGroup");
    groupSelect.setAttribute("data-object", deviceData["id"]);
    groupSelect.classList.add("devSetting", "form-control");
    let defaultOption = document.createElement("option");
    defaultOption.textContent = "";
    defaultOption.value = "-1";
    if (selectedGroup === -1) defaultOption.selected = true;

    groupSelect.appendChild(defaultOption);
    groupSelect.id = "HueGroup";
    for (let i = 0; i < groups.length; i++) {
        if (groups[i]["type"] !== 4) continue;
        let opt = document.createElement("option");
        opt.value = groups[i]["id"];
        opt.innerText = groups[i]["name"];
        console.log("Checking:", selectedGroup, groups[i]["id"]);
        if (selectedGroup === groups[i]["id"]) opt.selected = true;
        groupSelect.appendChild(opt);
    }
    groupSelectCol.appendChild(gLabel);
    groupSelectCol.appendChild(groupSelect);
    hueMapRow.appendChild(groupSelectCol);

    if (!isValid(group) || !isValid(group["lights"])) {
        console.log("No group, returning: ", group);
        return hueMapRow;
    }

    let lights = deviceData["lights"];
    let lightMap = deviceData["mappedLights"];
    if (!isValid(lights) || !isValid(lightMap)) {
        return hueMapRow;
    }
    // Get the main light group
    let lightRow = document.createElement("div");
    lightRow.classList.add("row", "justify-content-center", "col-12", "delSetting");
    const ids = group["lights"];

    // Sort our lights by name
    lights = lights.sort(function (a, b) {
        if (!a.hasOwnProperty('name') || !b.hasOwnProperty('name')) return false;
        return a["name"].localeCompare(b["name"]);
    });
    // Loop through our list of all lights
    for (let l in lights) {
        if (lights.hasOwnProperty(l)) {
            let light = lights[l];
            let id = light["id"];
            let map;
            let brightness = 255;
            let override = false;
            let selection = -1;

            for (let m in lightMap) {
                if (lightMap.hasOwnProperty(m)) {
                    if (lightMap[m]["id"] === id) {
                        map = lightMap[m];
                        brightness = map["brightness"];
                        override = map["override"];
                        if (!override) brightness = devBrightness;
                        selection = map["targetSector"];
                    }
                }
            }
            if (ids.includes(id)) {
                // Create the div for the other divs
                let name = light["name"];
                const lightCol = document.createElement('div');
                lightCol.className += "delSel col-12 col-md-6 col-lg-3 justify-content-center form-group";
                lightCol.id = id;
                lightCol.setAttribute('data-name', name);
                lightCol.setAttribute('data-id', id);

                // Light name

                let lightLabel = document.createElement("div");
                lightLabel.classList.add("headerCol");
                lightLabel.innerHTML = name;

                // Create the div to hold our select
                let selDiv = document.createElement('div');
                selDiv.className = "form-group";

                // Create the label for select                
                let targetLabel = document.createElement('label');
                targetLabel.classList.add("mr-2");
                targetLabel.innerHTML = "Target Sector";
                targetLabel.setAttribute("for", "lightMap" + id);

                // Create a select for this light
                let targetSelect = document.createElement('select');
                targetSelect.className = "lightProperty form-control text-dark bg-light";
                targetSelect.setAttribute('data-id', id);
                targetSelect.setAttribute('data-property', "TargetSector");

                // Create the blank "unmapped" option
                let opt = document.createElement("option");
                opt.value = "-1";
                opt.innerHTML = "";

                // Set it to selected if we don't have a mapping
                if (selection === -1) {
                    opt.setAttribute('selected', 'selected');
                }
                targetSelect.appendChild(opt);

                // Add the options for our regions
                let sectorCount = data.getProp("sectorCount");
                for (let i = 1; i < sectorCount; i++) {
                    opt = document.createElement("option");
                    opt.value = (i).toString();
                    opt.innerHTML = "<BR>" + (i);
                    // Mark it selected if it's mapped
                    if (selection === i) opt.setAttribute('selected', 'selected');
                    targetSelect.appendChild(opt);
                }

                selDiv.appendChild(targetLabel);
                selDiv.appendChild(targetSelect);

                // Create label for brightness
                const brightLabel = document.createElement('label');
                brightLabel.innerHTML = "Brightness";
                brightLabel.setAttribute('for', 'brightness' + id);

                // Create the brightness slider
                const newRange = document.createElement("input");
                newRange.className = "form-control lightProperty";
                newRange.setAttribute("type", "range");
                newRange.setAttribute('data-id', id);
                newRange.setAttribute('data-property', "Brightness");
                newRange.setAttribute('name', 'brightness' + id);
                newRange.setAttribute('min', "0");
                newRange.setAttribute('max', "100");
                newRange.setAttribute('value', brightness.toString());
                newRange.setAttribute('data-id', id);
                newRange.setAttribute('data-property', "Brightness");
                if (!override) newRange.disabled = true;

                // Create container div for brightness slider
                const rangeDiv = document.createElement('div');
                rangeDiv.className += "form-group";
                rangeDiv.appendChild(brightLabel);
                rangeDiv.appendChild(newRange);

                // Create label for override check
                const checkLabel = document.createElement('label');
                checkLabel.innerHTML = "Override";
                checkLabel.className += "custom-control-label";
                checkLabel.setAttribute('for', 'overrideBrightness' + id);

                // Create a checkbox
                const newCheck = document.createElement("input");
                newCheck.className += "overrideBright custom-control-input lightProperty";
                newCheck.setAttribute('data-id', id);
                newCheck.setAttribute('data-property', "Override");
                newCheck.setAttribute("type", "checkbox");
                if (override) newCheck.setAttribute("checked", 'checked');

                // Create the div to hold the checkbox
                const chkDiv = document.createElement('div');
                chkDiv.className += "form-check custom-control custom-switch hue-switch";
                chkDiv.appendChild(newCheck);
                chkDiv.appendChild(checkLabel);

                // Put it all together
                lightCol.appendChild(lightLabel);
                lightCol.appendChild(selDiv);
                lightCol.appendChild(rangeDiv);
                lightCol.appendChild(chkDiv);
                lightRow.appendChild(lightCol);
            }
        }
    }
    hueMapRow.appendChild(lightRow);
    return hueMapRow;
}

function drawNanoShapes(panel) {
    // Get window width
    let width = document.getElementById("stageCol").offsetWidth;
    let height = width * .5625;
    if (height > 800) height = 800;
    let rotation = panel["rotation"];
    if (!isValid(rotation)) rotation = 0;
    // Get layout data from panel
    let mirrorX = panel["mirrorX"];
    let mirrorY = panel["mirrorY"];
    let layout = panel["layout"];
    if (!isValid(layout)) return;

    let sideLength = layout["sideLength"];
    let positions = layout["positionData"];
    let minX = 1000;
    let minY = 1000;
    let maxX = 0;
    let maxY = 0;

    // Calculate the min/max range for each tile
    for (let i = 0; i < positions.length; i++) {
        let data = positions[i];
        if (data["x"] < minX) minX = data["x"];
        if ((data["y"] * -1) < minY) minY = (data["y"] * -1);
        if (data["x"] > maxX) maxX = data["x"];
        if ((data["y"] * -1) > maxY) maxY = (data["y"] * -1);
    }
    let wX = maxX - minX;
    let wY = maxY - minY;
    let scaleXY = 1;
    if (wX + 150 >= width) {
        scaleXY = width / (wX + 150);
        maxX *= scaleXY;
        maxY *= scaleXY;
        minX *= scaleXY;
        minY *= scaleXY;
    }
    height = wY + 75;

    // Create our stage
    let stage = new Konva.Stage({
        container: 'stageCol',
        width: width,
        height: height
    });

    // Shape layer
    let cLayer = new Konva.Layer();
    stage.add(cLayer);

    let x0 = (width - maxX - minX) / 2;
    let y0 = ((height - maxY - minY) / 2) + 30;

    // Group for the shapes
    let shapeGroup = new Konva.Group({
        rotation: rotation,
        draggable: false,
        x: x0,
        y: y0,
        scale: {
            x: scaleXY,
            y: scaleXY
        }
    });

    for (let i = 0; i < positions.length; i++) {
        let shapeDrawing;
        let data = positions[i];
        let shape = data["shapeType"];
        sideLength = data["sideLength"];
        let x = data["x"];
        let y = data["y"];
        if (mirrorX) x *= -1;
        if (!mirrorY) y *= -1;
        if (shape === 12) continue;

        let sText = new Konva.Text({
            x: x,
            y: y,
            text: data["panelId"],
            fontSize: 30,
            listening: false,
            fontFamily: 'Calibri'
        });

        let sectorText = data["targetSector"];

        let sText2 = new Konva.Text({
            x: x,
            y: y - 35,
            text: sectorText,
            fontSize: 30,
            listening: false,
            fontFamily: 'Calibri'
        });
        let o = data["o"];
        // Draw each individual shape
        switch (shape) {
            // Hexagon
            case 7:
                const hexA = 2 * Math.PI / 6;
                let r = sideLength;
                let points = [];
                for (let i = 0; i < 6; i++) {
                    let px = x + r * Math.cos(hexA * i);
                    let py = y + r * Math.sin(hexA * i);
                    points.push(px);
                    points.push(py);
                }
                shapeDrawing = new Konva.Line({
                    points: points,
                    fill: 'white',
                    stroke: 'black',
                    strokeWidth: 5,
                    closed: true,
                    id: data["panelId"]
                });
                break;
            // Triangles
            case 0:
            case 1:
            case 8:
            case 9:
                //y = (data.y * -1) + Math.abs(minY);
                let invert = false;
                if (o === 60 || o === 180 || o === 300 || o === 540) {
                    invert = true;
                }

                let angle = (2 * Math.PI) / 3;
                // Calculate our overall height based on side length
                let h = sideLength;
                //h *= 2;
                let halfHeight = h / 2;
                let ha = angle / 4;
                let a0 = ha;
                let a1 = angle + ha;
                let a2 = (angle * 2) + ha;
                let x0 = halfHeight * Math.cos(a0) + x;
                let x1 = halfHeight * Math.cos(a1) + x;
                let x2 = halfHeight * Math.cos(a2) + x;
                let y0 = (halfHeight * Math.sin(a0) + y) - halfHeight;
                let y1 = halfHeight * Math.sin(a1) + y - halfHeight;
                let y2 = halfHeight * Math.sin(a2) + y + h;
                if (!invert) {
                    y0 = halfHeight * Math.sin(a0) + y;
                    y1 = halfHeight * Math.sin(a1) + y;
                    y2 = halfHeight * Math.sin(a2) + y;
                }
                shapeDrawing = new Konva.Line({
                    points: [x0, y0, x1, y1, x2, y2],
                    fill: 'white',
                    stroke: 'black',
                    strokeWidth: 5,
                    closed: true,
                    rotation: rotation,
                    id: data["panelId"]
                });
                break;
            // Squares
            case 2:
            case 3:
            case 4:
                let tx = x - (sideLength / 2);
                let ty = y - (sideLength / 2);
                shapeDrawing = new Konva.Rect({
                    x: tx,
                    y: ty,
                    width: sideLength,
                    height: sideLength,
                    fill: 'white',
                    stroke: 'black',
                    strokeWidth: 4
                });

                break;
            case 5:
                break;
        }
        if (isValid(shapeDrawing)) {
            shapeDrawing.on('click', function () {
                setNanoMap(data["panelId"], data["targetSector"]);
            });
            shapeDrawing.on('tap', function () {
                setNanoMap(data["panelId"], data["targetSector"]);
            });
            shapeGroup.add(shapeDrawing);
        }
        sText.offsetX(sText.width() / 2);
        sText2.offsetX(sText2.width() / 2);
        sText.rotation(360 - rotation);
        sText2.rotation(360 - rotation);

        shapeGroup.add(sText);
        shapeGroup.add(sText2);
    }
    // Add to our canvas layer and draw
    let tr = new Konva.Transformer({
        nodes: [shapeGroup],
        resizeEnabled: false,
        rotateEnabled: true,
        rotateAnchorOffset: 20
    });

    shapeGroup.on('transformend', function () {
        updateDevice(deviceData["id"], "rotation", shapeGroup.rotation());
    });
    cLayer.add(tr);
    tr.nodes([shapeGroup]);
    cLayer.add(shapeGroup);

    let container = document.getElementById('stageCol');

    // now we need to fit stage into parent
    let containerWidth = container.offsetWidth;
    // to do this we need to scale the stage
    let scale = containerWidth / width;

    stage.width(width * scale);
    stage.height(height * scale);
    stage.scale({x: scale, y: scale});
    //shapeGroup.scale = scale;
    stage.draw();

    cLayer.draw();
    cLayer.zIndex(0);
}

function setNanoMap(id, current) {
    nanoTarget = id;
    nanoSector = current;
    nanoModal = new bootstrap.Modal(document.getElementById('nanoModal'));
    let wrap = document.getElementById("nanoPreviewWrap");
    nanoModal.show();
    createSectorMap(wrap, "nano");

    let nanoRegion = document.querySelectorAll(".nanoRegion");
    for (let i = 0; i < nanoRegion.length; i++) {
        let obj = nanoRegion[i];
        obj.classList.remove("checked");
    }

    if (current !== -1) {
        console.log("Trying to check sector " + current);
        document.querySelector('.sector[data-sector="' + current + '"]').classList.add("checked");
    }

}

function ranges(ledCount, offset, total) {
    let range = [];
    while (range.length < offset + total) {
        for (let i = 0; i < ledCount; i++) {
            range.push(i);
        }
    }
    return range.slice(offset, total + offset);
}

function sizeContent() {
    let navDiv = document.getElementById("mainNav");
    let footDiv = document.getElementById("footer");
    let cDiv = document.getElementById("mainContent");
    let colorDiv = document.getElementById("ambientNav");
    let audioDiv = document.getElementById("audioNav");
    let cardRow = document.getElementById("cardRow");
    let devSettings = document.querySelector(".card.container-fluid");
    let mainSettings = document.getElementById("mainSettingsCard");
    let wHeight = window.innerHeight;
    let wWidth = window.innerWidth;
    winWidth = wWidth;
    winHeight = wHeight;

    let ambientOffset = 0;
    if (mode === 3) {
        ambientOffset = colorDiv.offsetHeight;
    }
    if (mode === 2 || mode === 4) {
        ambientOffset = audioDiv.offsetHeight;
    }
    let top = navDiv.offsetHeight + ambientOffset + "px";
    let height = wHeight - navDiv.offsetHeight - footDiv.offsetHeight - ambientOffset + "px";
    cDiv.style.position = "fixed";
    cardRow.style.position = "fixed";
    cDiv.style.top = top;
    cardRow.style.top = top;
    cDiv.style.height = height;
    cardRow.style.height = height;
    cDiv.style.width = wWidth + "px";
    cardRow.style.width = wWidth + "px";
    colorDiv.style.width = wWidth + "px";
    audioDiv.style.width = wWidth + "px";
    if (expanded) {
        devSettings.style.top = top;
        devSettings.style.height = height;
        createDeviceSettings();
    }
    if (settingsShown) {
        mainSettings.style.top = top;
        mainSettings.style.height = height;
        loadSettings();
    }
}

async function closeCard() {
    if (!expanded) {
        return;
    }
    deviceData = undefined;
    drawSectorMap = false;
    drawLedMap = false;
    expanded = false;
    cardClone.style.overflow = "hidden";
    await toggleExpansion(cardClone, {
        top: `${toggleTop}px`,
        left: `${toggleLeft}px`,
        width: `${toggleWidth}px`,
        height: `${toggleHeight}px`,
        padding: '1rem 1rem'
    }, 300).then(function () {
        cardClone.classList.add("devCard", "m-4");
        cardClone.classList.remove("container-fluid");
        cardClone.style.scrollY = "hidden";
        baseCard.style.removeProperty('opacity');
        // shrink the card back to the original position and size
        document.querySelectorAll(".delSetting").forEach(e => e.remove());
        cardClone.remove();

        let settingsButtons = document.querySelectorAll(".mainSettings");
        for (let i = 0; i < settingsButtons.length; ++i) {
            settingsButtons[i].classList.remove('d-none');
        }
        let closeButtons = document.querySelectorAll(".closeBtn");
        for (let i = 0; i < closeButtons.length; ++i) {
            closeButtons[i].classList.add('d-none');
        }
    });
}

