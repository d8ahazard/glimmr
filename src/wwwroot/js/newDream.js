let socketLoaded = false;
let frameInt;
let loadTimeout;
let loadCalled;
// Row for settings and device cards divs
let settingsRow;
let cardRow;
// Settings content elements
let settingsTitle;
let settingsTab;
let settingsContent;
let newColor;
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
let croppr;
let leftCount, rightCount, topCount, bottomCount, hSectors, vSectors;
let refreshTimer;
let errModal = new bootstrap.Modal(document.getElementById('errorModal'));
// We're going to create one object to store our stuff, and add listeners for when values are changed.
let data = {
    storeInternal: [],
    devicesInternal:[],
    storeListener: function(val) {},
    devicesListener: function(val) {},
    set store(val) {
        this.storeInternal = val;
        this.storeListener(val);
    },
    get store() {
        return this.storeInternal;
    },
    set devices(val) {
        this.devicesInternal = val;
        this.devicesListener(val);
    },
    get devices() {
        return this.devicesInternal;
    },
    registerStoreListener: function(listener) {
        this.storeListener = listener;        
    },
    registerDevicesListener: function(listener) {
        this.devicesListener = listener;
    }
}

data.registerStoreListener(function(val) {
    console.log("Datastore has been updated: ", val);
    if (loadTimeout === null || loadCalled) {
        loadCalled = false;
        loadUi();        
    }
});

data.registerDevicesListener(function(val) {
    loadDevices();
});

let websocket = new signalR.HubConnectionBuilder()
    .configureLogging(signalR.LogLevel.Information)
    .withUrl("/socket")
    .build();

document.addEventListener("DOMContentLoaded", function(){
    let getUrl = window.location;
    baseUrl = getUrl .protocol + "//" + getUrl.host;
    closeButton = document.getElementById("closeBtn");
    settingsRow = document.getElementById("settingsRow");    
    settingsTab = document.getElementById("settingsTab");
    settingsTitle = document.getElementById("settingsTitle");
    settingsContent = document.getElementById("settingsContent");
    cardRow = document.getElementById("cardRow");
    pickr = Pickr.create({
        el: '.pickrBtn',
        theme: 'nano', // or 'monolith', or 'nano'
        swatches: [
            'rgba(255, 0, 0, 1)',
            'rgba(255, 128, 0, 1)',
            'rgba(255, 255, 0, 1)',
            'rgb(128,255,0)',
            'rgb(0,255,128)',
            'rgb(0,255,255)',
            'rgba(0, 0, 255, 1)',
            'rgba(128, 0, 255, 1)',
            'rgba(255, 0, 255, 1)',
            'rgb(255,0,128)'
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
    pickr.on('change', (color, source, instance) => {
        let col = color.toRGBA();
        newColor = rgbToHex(col[0], col[1], col[2]);
    }).on('changestop', (source, instance) => {
        console.log('Event: "change"', newColor, source, instance);
        if (isValid(data.store["SystemData"])) {
            data.store["SystemData"]["AmbientColor"] = newColor;
            data.store["SystemData"]["AmbientShow"] = -1;
            let asSelect = document.getElementById("AmbientShow");
            asSelect.value = "-1";
            pickr.setColor("#" + newColor);
            console.log("Sending: ", data.store["SystemData"]);
            sendMessage("SystemData",data.store["SystemData"]);
        }
    }).on('swatchselect', (color, instance) => {
        let col = color.toRGBA();
        newColor = rgbToHex(col[0], col[1], col[2]);
        if (isValid(data.store["SystemData"])) {            
            data.store["SystemData"]["AmbientColor"] = newColor;
            data.store["SystemData"]["AmbientShow"] = -1;
            let asSelect = document.getElementById("AmbientShow");
            asSelect.value = "-1";
            pickr.setColor("#" + newColor);
            console.log("Sending: ", data.store["SystemData"]);
            sendMessage("SystemData",data.store["SystemData"]);
        }
    });

    croppr = new Croppr('#croppr', {
        onCropEnd: function(data) {
            console.log(data.x, data.y, data.width, data.height);
            if (!loading) {
                let sd = getStoreProperty("SystemData");
                sd["CaptureRegion"] = data.x.toString() + ", " + data.y.toString() + ", " + data.width.toString() + ", " + data.height.toString();
                setStoreProperty("SystemData",sd);
                sendMessage("SystemData", sd);
            }
        }
    });
    setSocketListeners();
    loadSocket();
    setTimeout(function() {
        new Image().src = "../img/sectoring_screen.png";
    }, 1000);
});

function rgbToHex(r, g, b) {
    return ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
}

function loadCounts() {
    let sd = data.store["SystemData"];
    leftCount = 0;
    rightCount = 0;
    topCount = 0;
    bottomCount = 0;
    hSectors = 5;
    vSectors = 3;

    if (!isValid(sd)) return;
    let capMode = sd["CaptureMode"];
    let target = sd["DsIp"];

    console.log("Loading counts...");
    let devs = data.store["Devices"];
    console.log("Devs got: ", devs);
    let devSelect = document.getElementById("targetDs");
    if (isValid(devSelect)) {
        for (let i = 0; i < devSelect.options.length; i++) {
            devSelect.options[i] = null;
        }
    }
    
    if (isValid(devs)) {
        for (let i = 0; i < devs.length; i++) {
            let dev = devs[i];
            if (dev["Tag"] === "DreamScreen" && dev["DeviceTag"].includes("DreamScreen")) {
                console.log("Adding DS option", dev);
                let opt = document.createElement("option");
                opt.value = dev["Id"];
                opt.innerText = dev["Name"] + " - " + dev["Id"];
                if (opt.value === target) opt.selected = true;
                devSelect.appendChild(opt);
            }
        }
    }
    
    
    let lSel = document.querySelector('[data-property="LeftCount"][data-object="SystemData"]');
    let rSel = document.querySelector('[data-property="RightCount"][data-object="SystemData"]');
    let tSel = document.querySelector('[data-property="TopCount"][data-object="SystemData"]');
    let bSel = document.querySelector('[data-property="BottomCount"][data-object="SystemData"]');


    lSel.disabled = capMode === 0;
    rSel.disabled = capMode === 0;
    tSel.disabled = capMode === 0;
    bSel.disabled = capMode === 0;
    // If using DS capture, set static/dev LED counts.
    if (capMode === 0) {
        
        // If a target is set, try to load the flex settings
        console.log("Target: ", target);
        if (isValid(target)) {
            let dev;
            for (let i=0; i < devs.length; i++) {                
                if (devs[i]["Id"] === target) {
                    dev = devs[i];
                }                
            }
            console.log("DEV: ", dev);
            if (isValid(dev)) {
                let flex = dev["FlexSetup"];
                console.log("FLEX: ", flex);
                if (isValid(flex)) {
                    console.log("Loading flex!: ", flex);
                    leftCount = flex[0];
                    rightCount = flex[0];
                    topCount = flex[1];
                    bottomCount = flex[1];
                }
            }
        }
    } else {
        leftCount = sd["LeftCount"];
        rightCount = sd["RightCount"];
        topCount = sd["TopCount"];
        bottomCount = sd["BottomCount"];
        hSectors = sd["HSectors"];
        vSectors = sd["VSectors"];
    }
    lSel.value = leftCount;
    rSel.value = rightCount;
    tSel.value = topCount;
    bSel.value = bottomCount;
}

// Send a message to the server, websocket or not
function sendMessage(endpoint, sData, encode=true) {
    if (encode && isValid(sData)) sData = JSON.stringify(sData);
    // Set a .5s timeout so that responses from sent messages aren't loaded
    loadTimeout = setTimeout(function(){
        loadTimeout = null;
    },500);
    if (socketLoaded) {
        if (isValid(sData)) {
            websocket.invoke(endpoint, sData).catch(function (err) {
                return console.error("Error: ", err);
            });
        } else {
            websocket.invoke(endpoint).catch(function (err) {
                return console.error("Error: ", err);
            });
        }
    } else {
        doPost(endpoint, data);
    }
}

function doPost(endpoint, payload) {
    let url = baseUrl + "/api/DreamData/" + endpoint;
    if (posting) {
        console.log("Already posting?");
        return;
    }
    let xhttp = new XMLHttpRequest();
    console.log(`Posting to ` + url, data);

    
    xhttp.open("POST", url, true);
    xhttp.onreadystatechange = function() {
        if (this.readyState === 4 && this.status === 200) {
            postResult = this.json;
            if (endpoint === "loadData") {
                let stuff = postResult.replace(/\\n/g, '');
                let parsed = JSON.parse(stuff);
                data.store = parsed;
                loadUi();
            }
        }
    };
    xhttp.setRequestHeader("Content-Type", "application/json");
    xhttp.send(JSON.stringify(payload));
    xhttp.send();    
}

function doGet(endpoint) {
    fetch(endpoint)
        .then(function(response) {
            return response.json();
        });
}

// Set various actions/responses on the websocket
function setSocketListeners() {
    websocket.on("ReceiveMessage", function (message) {
        console.log("RecMsg: " + message);
    });

    websocket.on("mode", function (mode) {
        console.log("Socket has set mode to " + mode);
        setMode(mode);
    });

    websocket.on("cpuData", function (cpuData) {
        let tempDiv = $("#tempDiv");
        let tempText = $("#temperature");
        let cpuText = $("#cpuPct");
        let overIcon = $("#overIcon");
        tempText.textContent = cpuData["tempCurrent"] + "°C";
        cpuText.textContent = cpuData["loadAvg1"] + "%";
        overIcon.textContent = "";
        tempDiv.classList.remove("text-danger");
        tempDiv.classList.add("text-success");
        overIcon.classList.remove("text-danger");
        for(let i=0; i< cpuData["throttledState"].length; i++) {
            if (cpuData["throttledState"][i] === "Currently throttled") {
                tempDiv.classList.add("text-danger");
                tempDiv.classList.remove("text-success");
            }
            if (cpuData["throttledState"][i] === "Under-voltage detected") {
                overIcon.textContent = "power_input";
                overIcon.classList.add("text-danger");
            }
        }
    });

    websocket.on("ambientMode", function (mode) {
        console.log("Socket has set ambient mode to " + mode);
    });

    websocket.on("ambientShow", function (show) {
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
        console.log("New device data " + device);
    });
    
    websocket.on("loadPreview", function(){
        // If our preview isn't visible, there's no reason to pull data
        if (!settingsShown) return;
        let inputElement = document.getElementById('inputPreview');
        let croppedElement = document.getElementById('outputPreview');
        let screen = document.getElementById("croppr");
        inputElement.src = './img/_preview_input.jpg?rand=' + Math.random();
        croppedElement.src = './img/_preview_output.jpg?rand=' + Math.random();
        croppr.source = "./img/_preview_screen.jpg?rand=" + Math.random();
    });

    websocket.on("auth", function (value1, value2) {
        console.log("Auth message: " + value1);
        let cb = document.getElementById("CircleBar");        
        switch (value1) {
            case "start":
                bar.animate(0);
                cb.classList.remove("hide");
                console.log("Auth start...");
                break;
            case "error":
                console.log("Auth error...");
                cb.classList.add("hide");
                break;
            case "stop":
                console.log("Auth stop...");
                cb.classList.add("hide");
                break;
            case "update":
                console.log("Tick is " + value2);
                bar.animate(value2/30);
                if (value2 === 30) cb.classList.add("hide");
                break;
            case "authorized":
                console.log("Auth success!");
                let led = document.querySelector(".linkImg");
                led.classList.remove("unlinked");
                led.classList.add("linked");
                cb.classList.add("hide");
                break;
            default:
                break;
        }
    });


    websocket.on('open', function() {
        console.log("Socket connected (onOpen).");
        socketLoaded = true;        
    });

    websocket.on('olo', function(stuff) {
        stuff = stuff.replace(/\\n/g, '');
        let parsed = JSON.parse(stuff);
        console.log("OLO: ", parsed);
        data.store = parsed;
        loadUi();
    });
    
    websocket.on('deleteDevice', function(id) {
       console.log("Removing device...");
        let idx = -1;
        for(let i = 0; i < data.devices.length; i++) {
            if (data.devices[i].Id === id) {
                idx = i;
            }
        }
        if (idx !== -1) data.devices.splice(idx, 1);
        let devCard = document.querySelector('.devCard[data-id="'+id+'"]');
        devCard.remove();
    });
    
    websocket.on('frames', function(stuff) {
        console.log("frame Stuff: ", stuff);       
    });

    websocket.on('device', function(dData) {
        dData = dData.replace(/\\n/g, '');
        let stuff = JSON.parse(dData);
        stuff["Id"] = stuff["id"];
        console.log("Device data retrieved: ", stuff);
        for(let i=0; i<data.devices.length; i++) {
           let dev = data.devices[i];
           if (dev["Id"] === stuff["Id"]) {
               console.log("Device updated: ",stuff);
               data.devices[i] = mergeDeviceData(dev,stuff);
           }
           
           if (isValid(deviceData) && deviceData["Id"] === stuff["Id"]) {
               console.log("Updating selected deviceData:",stuff);               
               deviceData = mergeDeviceData(deviceData,stuff);
               if (settingsShown) createDeviceSettings();
           }
           
        }
        loadDevices();
    });

    websocket.onclose(function() {
        console.log("Socket Disconnected...");
        socketLoaded = false;
        showSocketError();
        let i = 0;
        let intr = setInterval(function() {
            loadSocket();
            if (++i >= 100 || socketLoaded) clearInterval(intr);
        }, 5000);
    })
}

function mergeDeviceData(existing, newDev) {
    let clone = existing;
    for (const [key, value] of Object.entries(existing)) {
        let titleKey = key[0].toUpperCase() + key.substring(1);
        for (const [nKey, nValue] of Object.entries(newDev)) {
            if (key.toLowerCase() === nKey.toLowerCase()) {
                clone[titleKey] = nValue;        
            }
        }
    }
    return clone;
}

// Initialize our websocket
function loadSocket() {
    if (socketLoaded) return;
    console.log("Trying to connect to socket...");
    websocket.start().then(function () {
        console.log("Socket connected.");
        socketLoaded = true;        
        errModal.hide();
    }).catch(function (err) {
        console.log("Socket connection error: ", err.toString());
        showSocketError();
    });
}

function downloadDb() {
    let link = document.createElement("a");
    // If you don't know the name or want to use
    // the webserver default set name = ''
    link.setAttribute('download', name);
    link.href = "/api/DreamData/DbDownload";
    document.body.appendChild(link);
    link.click();
    link.remove();
}

function showSocketError() {
    errModal.show();
}

function TriggerRefresh() {
    let sd = data.store["SystemData"];
    let refreshIcon = document.getElementById("refreshIcon");
    if (refreshTimer == null) {
        if (!isValid(sd)) return;
        refreshIcon.classList.add("rotate");
        sendMessage("ScanDevices");
        refreshTimer = setTimeout(function() {
            refreshIcon.classList.remove("rotate");
            refreshTimer = null;
        }, sd["DiscoveryTimeout"] * 1000);
    }
    
}

// Set all of the various listeners our page may use
function setListeners() {
    listenersSet = true;
    window.addEventListener('resize', sizeContent);
    
    document.addEventListener('change', function(e) {
        let target = e.target;
        let obj = target.getAttribute("data-object");
        let property = target.getAttribute("data-property");
        let id = target.getAttribute("data-id");
        let val = target.value;
        if (target.type && target.type ==="checkbox") {
            val = target.checked;
            console.log("Checkbox value is " + val);
        }
        if (target.classList.contains("lightProperty")) {
            console.log("Updating light property.");
            let id = target.getAttribute("data-id");
            let property = target.getAttribute("data-property");
            let numVal = parseInt(val);
            if (!isNaN(numVal)) val = numVal;
            updateLightProperty(id, property, val);
            return;
        }
        
        if (property === "CaptureMode" || property === "ScreenCapMode" || property === "PreviewMode" || property === "AutoUpdateTime") {
            val = parseInt(val);            
        }
        
        let pack;
        if (isValid(obj) && isValid(property) && isValid(val)) {
            console.log("Trying to set: ", obj, property, val);
            let numVal = parseInt(val);
            if (!isNaN(numVal) && property !== "DsIp" && property !== "OpenRgbIp") val = numVal; 
            
            if (isValid(id)) {
                let strips = data.store[obj];
                for(let i=0; i < strips.length; i++) {
                    let strip = strips[i];
                    if (strip["Id"] === id) {
                        strip[property] = val;
                        strip["Id"] = id;
                        strips[i] = strip;
                        pack = strip;
                        console.log("Updating LED data, huzzah!");
                        sendMessage(obj, pack,true);
                    }
                }
                data.store[obj] = strips;
                
            } else {
                if (target.classList.contains("devSetting")) {
                    updateDevice(obj, property, val);  
                    createDeviceSettings();
                    return;
                } else {
                    if (property === "SelectedMonitors") {
                        let mons = data.store["Dev_Video"];
                        for (let m=0; m < mons.length; m++) {
                            let mon = mons[m];
                            for(let i=0; i< target.options.length; i++) {
                                if (target.options[i].value === mon["Id"])
                                    mons[m]["Enable"] = target.options[i].selected;                                
                            }    
                        }
                        data.store["Dev_Video"] = mons;
                        console.log("Updating monitor selections: ", mons);
                        sendMessage("Monitors",mons,true);
                    } else {
                        data.store[obj][property] = val;
                        pack = data.store[obj];
                        if (property === "ScreenCapMode" || property === "CaptureMode") {
                            updateCaptureUi();
                        }
                        console.log("Sending updated object: ", obj, pack);
                        sendMessage(obj, pack,true);
                        return;    
                    }                    
                }
            }
                        
            if (property === "LeftCount" || property === "RightCount" || property ==="TopCount" || property === "BottomCount") {
                let lPreview = document.getElementById("sLedPreview");
                let lImage = document.getElementById("ledImage");
                setTimeout(function(){
                    createLedMap(lPreview, lImage, pack);
                }, 500);
                return;
            }
            if (property === "Theme") {
                loadTheme(val);
                return;
            }
            if (property === "AudioMap") {
                let mapImg = document.getElementById("audioMapImg");
                mapImg.setAttribute("src","./img/MusicMode" + val + ".png");
                return;
            }
            
        }   
        obj = target.getAttribute("data-target");
        property = target.getAttribute("data-attribute");
        if (isValid(obj) && isValid(property) && isValid(val)) {
            
            updateDevice(obj, property, val);
        }
    });
    
    document.addEventListener('click',function(e){
        let target = e.target;
        handleClick(target);
    });
}

function handleClick(target) {
    switch(true) {
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
                sendMessage("SystemControl", action, false);
            } else {
                console.log(action + " canceled.");
            }
            console.log("Control button click");
            break;
        case target === closeButton:
            closeCard();
            break;
        case target.classList.contains("sector"):
            let val = target.getAttribute("data-sector");
            updateDeviceSector(val, target);
            break;
        case target.classList.contains("linkDiv"):
            if (target.getAttribute("data-linked") === "false") {
                let devId = deviceData["Id"];
                sendMessage("AuthorizeDevice", devId,false);
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
        case target.classList.contains("devSetting"):
            let devId = target.getAttribute("data-target");
            let attribute = target.getAttribute("data-attribute");
            console.log("Dev setting clicked, we are setting ", attribute, devId, target.checked);
            updateDevice(devId, attribute, target.checked);
            break;
        case target.classList.contains("removeDevice"):
            console.log("Device removal fired.");
            let deviceId = deviceData["Id"];
            let devName = deviceData["Name"];
            if (confirm('Warning! The device named ' + devName + " will have all settings removed. Do you want to continue?")) {
                let res = closeCard();
                sendMessage("DeleteDevice", deviceId, false);
                console.log('Deleting device.');
            } else {
                console.log('Device deletion canceled.');
            }
            break;
        case target.classList.contains("settingBtn"):
            if (expanded) {
                closeCard();
            } else {
                let devId = target.getAttribute("data-target");
                deviceData = findDevice(devId);
                showDeviceCard(target);
            }
            break;
        case target.classList.contains("enableBtn"):
            let dId = target.getAttribute("data-target");
            let devEnabled = target.getAttribute("data-enabled");
            let icon = target.firstChild;
            if (devEnabled === "true") {
                target.setAttribute("data-enabled","false");
                icon.innerText = "cast";
            } else {
                target.setAttribute("data-enabled","true");
                icon.innerText = "cast_connected";
            }
            deviceData = findDevice(dId);
            //data.devices[devId]["Enable"] = (devEnabled !== "true");
            updateDevice(dId,"Enable",(devEnabled !== "true"));
            break;
        case target.classList.contains("refreshBtn"):
            TriggerRefresh();
            break;
        case target.classList.contains("modeBtn"):
            let newMode = parseInt(target.getAttribute("data-mode"));
            setMode(newMode);
            sendMessage("Mode", newMode, false);
            break;
        case target.classList.contains("ledCtl"):
            let lAction = target.getAttribute("data-function");
            let id = target.getAttribute("data-id");
            ledAction(lAction, id);
            break;
        case target.classList.contains("mainSettings"):
            toggleSettingsDiv(0);
            break;
        case target.classList.contains("nav-link"):
            let cDiv = target.getAttribute("href");
            let fadePanes = document.querySelectorAll(".tab-pane");
            for (let i=0; i < fadePanes.length; i++) {
                if (fadePanes[i]) {
                    if (fadePanes[i].classList.contains("show")) {
                        fadePanes[i].classList.remove("show", "active");
                    }
                }
            }
            document.querySelector(cDiv).classList.add("show", "active");
            loadSettings();
            break;
    }
}

function ledAction(action, id) {
    let ledData = data.store["LedData"];
    if (isValid(ledData)) {
        let led = getObj(ledData, "Id", id);
        if (isValid(led)) {
            if (action === "test") {
                sendMessage("DemoLed", id.toString(), false);
            } else {
                led["Enable"] = (action === "enable");
                let t1 = document.querySelector('[data-id="'+id+'"][data-function="enable"]');
                let t2 = document.querySelector('[data-id="'+id+'"][data-function="disable"]');
                if (action === "enable") {
                    t1.classList.add("active");
                    t2.classList.remove("active");
                } else {
                    t2.classList.add("active");
                    t1.classList.remove("active");
                }
            }
            data.store["LedData"] = setObj(ledData, "Id", id, led);
            led["Id"] = led["Id"];
            sendMessage("LedData", led, true);
        } else {
            console.log("Invalid led")
        }
    } else {
        console.log("Invalid led data");
    }
}

async function toggleSettingsDiv(target) {
    let settingsIcon = document.querySelector(".mainSettings span");
    if (!settingsShown) {
        settingsIcon.textContent = "chevron_left";
        settingsRow.classList.remove("d-none");
        cardRow.classList.add("d-none");
        loadSettings();        
    } else {
        settingsIcon.textContent = "settings_applications";
        settingsRow.classList.add("d-none");
        cardRow.classList.remove("d-none");
    }
    settingsShown = !settingsShown;
    settingsTitle.textContent = "Main Settings";
}


function updateDeviceSector(sector, target) {
    console.log("Sector click: ", sector, target);
    let sectors = document.querySelectorAll(".sector");
    for (let i=0; i<sectors.length; i++) {
        sectors[i].classList.remove("checked");
    }
    target.classList.add("checked");
    let dev = deviceData;
    if (dev["Tag"] === "Nanoleaf") {
        console.log("DEV: ", dev);
        let layout = dev["Layout"];        
        let positions = layout["PositionData"];
        
        
        for(let i=0; i < positions.length; i++) {
            if (positions[i]["PanelId"] === nanoTarget) {
                positions[i]["TargetSector"] = sector;
            }    
        }
        layout["PositionData"] = positions;
        dev["Layout"] = layout;
        drawNanoShapes(dev);
        updateDevice(dev["Id"],"Layout", layout);
    }
    
    sendMessage("flashSector", parseInt(sector), false);
}

function updateLightProperty(myId, propertyName, value) {
    let lm = getLightMap(myId);
    lm[propertyName] = value;
    console.log("Updated lm: ", lm);
    setLightMap(lm);
    let fGroup = deviceData["Groups"];
    let nGroup = [];
    for (let g in fGroup) {
        if (fGroup.hasOwnProperty(g)) {
            fGroup[g]["Id"] = fGroup[g]["_id"];
            nGroup.push(fGroup[g]);
        }

    }
    console.log("Updating bridge: ", deviceData);
    updateDevice(deviceData["Id"],"Groups", nGroup);    
}

function getLightMap(id) {
    let hueLightMap = deviceData["MappedLights"];
    for (let l in hueLightMap) {
        if (hueLightMap.hasOwnProperty(l)) {
            if (hueLightMap[l]["_id"] === id) {
                return hueLightMap[l];
            }
        }
    }
    return {
        _id: id,
        TargetSector: -1,
        TargetSector2: -1,
        Brightness: 255,
        Override: false
    };
}

function setLightMap(map) {
    let hueLightMap = deviceData["MappedLights"];
    for (let l in hueLightMap) {
        if (hueLightMap.hasOwnProperty(l)) {
            if (hueLightMap[l]["_id"] === map["_id"]) {
                hueLightMap[l] = map;
                return;
            }
        }
    }
    hueLightMap.push(map);
    updateDevice(deviceData["_id"],"MappedLights", hueLightMap);
}


function setMode(newMode) {    
    //data.store["DeviceMode"][0]["value"] = newMode;
    mode = newMode;
    let target = document.querySelector("[data-mode='"+mode+"']");    
    let others = document.querySelectorAll(".modeBtn");
    for (let i=0; i< others.length; i++) {
        if (others[i]) {
            others[i].classList.remove("active");
        }
    }
    if (target != null) target.classList.add("active");
    let ambientNav = document.getElementById("ambientNav");
    if (mode === 3) {
        ambientNav.classList.add("show");
        ambientNav.classList.remove("hide");
    } else {
        ambientNav.classList.add("hide");
        ambientNav.classList.remove("show");        
    }
    sizeContent();
}

function loadUi() {
    loadCounts();
    console.log("Loading ui.");
    let mode = 0; 
    let autoDisabled = getStoreProperty("AutoDisabled");
    
    if (isValid(data.store["SystemData"])) {
        let sd = data.store["SystemData"];
        let theme = sd["Theme"];
        mode = sd["DeviceMode"];
        setMode(mode);
        if (isValid(data.store["AmbientScenes"])) {
            let scenes = data.store["AmbientScenes"];
            let ambientMode = sd["AmbientShow"];
            scenes.sort((a, b) => (a.Id > b.Id) ? 1 : -1);
            let sceneSelector = document.getElementById("AmbientShow");
            sceneSelector.innerHTML = "";
            for(let i=0; i < scenes.length; i++) {
                let opt = document.createElement("option");
                opt.value = scenes[i]["Id"];
                opt.innerText = scenes[i]["Name"];
                if (opt.value === ambientMode) opt.selected = true;
                sceneSelector.appendChild(opt);
            }
            sceneSelector.value = ambientMode;
        }
        loadTheme(theme);
        pickr.setColor("#" + sd["AmbientColor"]);
    }
    
    if (isValid(data.store["Dev_Audio"])) {
        let recList = document.getElementById("RecDev");
        for (let i = 0; i < recList.options.length; i++) {
            recList.options[i] = null;
        }
        let recDevs = data.store["Dev_Audio"];
        let recDev = getStoreProperty("RecDev");
        if (isValid(recDevs)) {
            for (let i = 0; i < recDevs.length; i++) {
                console.log("Adding dev");
                let dev = recDevs[i];
                let opt = document.createElement("option");
                opt.value = dev["Id"];
                opt.innerText = dev["Id"];
                if (opt.value === recDev) opt.selected = true;
                recList.options.add(opt);
            }
        }
    } else {
        console.log("No recording devices found.");
    }
    let sectorMap = getStoreProperty("AudioMap");
    if (isValid(sectorMap)) {
        let mapImg = document.getElementById("audioMapImg");
        mapImg.setAttribute("src","./img/MusicMode" + sectorMap + ".png");
    }
    
    if (autoDisabled) mode = 0;
    setMode(mode);
    getDevices();
    if (!listenersSet) {
        setListeners();
        let tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
        let tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl)
        });
    }
    sizeContent();
    document.getElementById("cardRow").click();
}

function loadTheme(theme) {
    console.log("Loading theme...", theme);
    let head = document.getElementsByTagName("head")[0];
    if (theme === "light") {
        let last = head.lastChild;
        console.log("LAST: ", last);
        if (isValid(last.href)) {
            if (!last.href.includes("site")) {
                console.log("Deleting?");
                last.parentNode.removeChild(last);
            }
        }
        
    } else {
        let newSS=document.createElement('link');
        newSS.rel='stylesheet';
        newSS.href='/css/' + theme + '.css';
        document.getElementsByTagName("head")[0].appendChild(newSS);    
    }
    
    
}

function loadSettings() {
    let ledData = data.store["LedData"];
    let systemData = data.store["SystemData"];
    let updateTime = systemData["AutoUpdateTime"].toString();
    let timeSelect = document.getElementById("AutoUpdateTime");
    if (isValid(timeSelect)) {
        let length = timeSelect.options.length;
        for (let i = length-1; i >= 0; i--) {
            timeSelect.options[i] = null;
        }
        
        let hourval = 0;
        let timeText = document.getElementById("updateTime");
        for (let ampm = 0; ampm < 2; ampm++) {
            for (let hour=0; hour < 12; hour++) {
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
        timeText.innerHTML = "Updates will be installed at "+updateTime.toString()+":00"+ampm+" every day when enabled.";
    }
    
    let capTab = document.getElementById("capture-tab");
    if (data.store == null) return;
    if (isValid(ledData)) {
        for(let i=0; i < 4; i++) {
            loadSettingObject(ledData[i]);
        }    
    }


    let monitors = data.store["Dev_Video"];
    if (isValid(monitors) && monitors.length) {
        let monList = document.getElementById("monitorSelect");
        let length = monList.options.length;
        for (let i = length-1; i >= 0; i--) {
            monList.options[i] = null;
        }
        
        console.log("Adding monitors: ", monitors);
        let vals = [];
        for(let i=0; i < monitors.length; i++) {
            let opt = document.createElement("option");
            opt.value = monitors[i]["Id"];
            opt.innerText = monitors[i]["DeviceString"];
            monList.appendChild(opt);
            if (monitors[i]["Enable"]) {
                console.log("This options *should* be selected.");
                vals.push(monitors[i]["Id"]);
            }            
        }
        console.log("VALS: ", vals);
        for (const option of document.querySelectorAll('#monitorSelect option')) {
            const value = option.value;
            if (vals.indexOf(value) !== -1) {
                console.log("Selecting " + value);
                option.setAttribute('selected', 'selected');
            } else {
                console.log("Clearing " + value);
                option.removeAttribute('selected');
            }
        }
    }
    
    if (isValid(systemData)) {
        loadSettingObject(systemData);
        updateCaptureUi();
        loadCounts();
        console.log("Loading System Data: ", systemData);
        let lPreview = document.getElementById("sLedPreview");
        let lImage = document.getElementById("ledImage");
        let sPreview = document.getElementById("sectorPreview");
        let sImage = document.getElementById("sectorImage");
        let rect = systemData["CaptureRegion"].split(", ");
        let sRect = systemData["MonitorRegion"].split(", ");
        let crop = document.querySelector(".croppr-image");
        let cw = 0;
        let ch = 0;
        if (isValid(crop)) {
            cw = crop.width;
            ch = crop.height;
        }
        //console.log("Rect: ", rect, crop.width, crop.height);
        let x = parseInt(rect[0]);
        let y = parseInt(rect[1]);
        let w = parseInt(rect[2]);
        let h = parseInt(rect[3]);
        let sw = parseInt(sRect[2]);
        let sh = parseInt(sRect[3]);
        let scale = cw / sw;
        let hscale = ch /sh;
        x = x * scale;
        y = y * hscale;
        w = w * scale;
        h = h * hscale;
        console.log("Scalez: ",x,y,w,h);
        loading = true;
        if (isValid(croppr) && isValid(crop) && capTab.classList.contains("active")) croppr.resizeTo(w, h)
            .moveTo(x, y);
        loading = false;
        if (capTab.classList.contains("active")) setTimeout(function(){
            createLedMap(lPreview, lImage, systemData);
            createSectorMap(sPreview, sImage);
        },500);
    } else {
        console.log("NO LED DATA");
    }   
    
}

function updateCaptureUi() {
    let systemData = data.store["SystemData"];
    if (!isValid(systemData)) return;
    let capGroups = document.querySelectorAll(".capGroup");
    let mode = systemData["CaptureMode"].toString();
    let camMode = systemData["CamType"].toString();
    let usbIdx = systemData["UsbSelection"].toString();
    let capSelect = document.getElementById("CapModeSelectRow");
    let monRow = document.getElementById("MonitorSelectRow");
    let regionRow = document.getElementById("RegionSelectRow");
    let usbRow = document.getElementById("UsbSelectRow");
    let usbSel = document.getElementById("UsbSelect");
    let target = document.getElementById("ScreenCapMode");

    for (let i=0; i < capGroups.length; i++) {
        let group = capGroups[i];
        let groupMode = group.getAttribute("data-mode");
        console.log("Checking ",groupMode, mode);
        if (groupMode === mode) {
            console.log("Showing group " + i);
            group.classList.add("show");
            group.classList.remove("hide");
            
        } else {
            console.log("Hiding group " + i);
            group.classList.add("hide");
            group.classList.remove("show");
        }
    }

    if (isValid(usbSel.options)) {
        for (let i = 0; i < usbSel.options.length; i++) {
            usbSel.options[i] = null;
        }
    }
    let usbDevs = data.store["Dev_Usb"];
    
    for (const [key, value] of Object.entries(usbDevs)) {
        console.log("Appending usb: ",key,value, usbDevs);
        let opt = document.createElement("option");
        opt.value = key.toString();
        opt.innerText = value.toString();
        if (opt.value === usbIdx) opt.selected = true;
        usbSel.appendChild(opt);
    }
    
    if (mode === "2" || (mode === "1" && camMode === "1")) {
        usbRow.classList.add("show");
        usbRow.classList.remove("hide");
    } else {
        usbRow.classList.add("hide");
        usbRow.classList.remove("show");
    }
    
    if (systemData["IsWindows"]) {
        let capSelection = systemData["ScreenCapMode"];
        
        target.value = capSelection;
        if (capSelection === 1) {
            console.log("Monitor setup...")
            monRow.classList.add("show");
            monRow.classList.remove("hide");
            regionRow.classList.add("hide");
            regionRow.classList.remove("show");
        } else {
            monRow.classList.add("hide");
            monRow.classList.remove("show");
            regionRow.classList.add("show");
            regionRow.classList.remove("hide");
        }
        capSelect.classList.add("show");
        capSelect.classList.remove("hide");
    } else {
        regionRow.classList.add("show");
        regionRow.classList.remove("hide");
        capSelect.classList.add("show");
        capSelect.classList.remove("hide");
    }
    if (mode !== "3") {
        monRow.classList.add("hide");
        monRow.classList.remove("show");
        regionRow.classList.add("hide");
        regionRow.classList.remove("show");
        capSelect.classList.add("hide");
        capSelect.classList.remove("show");
    }
}

function loadSettingObject(obj) {
    if (obj == null) {
        console.log("Object is null?");
        return;
    }
    let dataProp = obj;
    let id = obj["Id"];
    let name = "SystemData";
    console.log("Loading object: ",name, dataProp, id);
    for(let prop in dataProp) {
        if (dataProp.hasOwnProperty(prop)) {
            let value = dataProp[prop];
            let target = document.querySelector('[data-property='+prop+'][data-object="'+name+'"]');
            if (obj.hasOwnProperty("GpioNumber")) {
                target = document.querySelector('[data-property='+prop+'][data-object="'+name+'"][data-id="'+id+'"]');
                console.log("Target: ", target);
            }

            if (prop === "Enable") {
                console.log("Enableprop: ", value, id);
                if (value) {
                    target = document.querySelector('[data-id="'+id+'"][data-function="enable"]');
                    if (isValid(target))target.classList.add("active");
                } else {
                    target = document.querySelector('[data-id="'+id+'"][data-function="disable"]');
                    if (isValid(target))target.classList.add("active");
                }
            }
            
            
            
            if (isValid(target) && prop !== "SelectedMonitors" && prop !== "ScreenCapMode") {
                if (value === true) {
                    target.setAttribute('checked', "true");
                } else {
                    target.value = dataProp[prop];    
                }                                
                console.log("Setting property with magick.", prop, dataProp[prop]);
            }            
        }        
    }
}

function loadDevices() {    
    let container = $("#cardRow");
    container.innerHTML = "";
    for (let i = 0; i< data.devices.length; i++) {
        if (data.devices.hasOwnProperty(i)) {
            let device = data.devices[i];
            if (device.Tag === "DreamScreen" && device["DeviceTag"].includes("Dreamscreen")) continue;
            // Create main card
            let mainDiv = document.createElement("div");
            mainDiv.classList.add("card", "m-4", "devCard");
            mainDiv.setAttribute("data-id",device.Id);
            // Create card body
            let bodyDiv = document.createElement("div");
            bodyDiv.classList.add("card-body", "row");            
            // Create title/subtitle headers
            let title = document.createElement("h5");
            let subTitle = document.createElement("h6");
            title.classList.add("card-title");
            subTitle.classList.add("card-subtitle", "mb2", "text-muted");
            title.textContent = device.Name;
            
            if (device["Tag"] === "Wled" || device["Tag"] === "Glimmr") {
                let a = document.createElement("a");
                a.href = "http://" + device["IpAddress"];
                a.innerText = device["IpAddress"];
                a.target = "_blank";
                subTitle.appendChild(a);
                if (device["Tag"] === "Wled") {
                    let count = document.createElement("span");
                    count.innerText = " (" + device["LedCount"] + ")";
                    subTitle.appendChild(count);
                }
            } else {
                subTitle.textContent = device["IpAddress"];
            }
            // Create icon
            let titleRow = document.createElement("div");
            titleRow.classList.add("mb-3", "col-12", "titleRow");
            let titleCol = document.createElement("div");
            titleCol.classList.add("col-8", "titleCol", "exp");            
            let iconCol = document.createElement("div");
            iconCol.classList.add("iconCol", "exp", "col-4");
            let image = document.createElement("img");
            image.classList.add("deviceIcon", "img-fluid");
            image.setAttribute("data-bs-toggle", "tooltip");
            image.setAttribute("data-bs-placement", "top");
            image.setAttribute("data-title", "Flash/locate device");
            image.setAttribute("data-device", device["Id"]);
            let tag = device.Tag;
            if (isValid(tag)) {
                if (isValid(device["DeviceTag"]) && (tag === "Dreamscreen" || tag === "Lifx")) tag = device["DeviceTag"];
                image.setAttribute("src", baseUrl + "/img/" + tag.toLowerCase() + "_icon.png");
            }
            
            // Settings column
            let settingsCol = document.createElement("div");
            settingsCol.classList.add("col-12", "settingsCol", "pb-2", "text-center", "exp");
            // Create enabled checkbox
            let enableButton = document.createElement("button");
            enableButton.classList.add("btn", "btn-outline-secondary", "btn-clear", "enableBtn", "pt-2");
            enableButton.setAttribute("data-target", device["Id"]);
            enableButton.setAttribute("data-enabled", device["Enable"]);
            // And the icon
            let eIcon = document.createElement("span");
            eIcon.classList.add("material-icons");
            if (device["Enable"]) {
                eIcon.textContent = "cast_connected";                
            } else {
                eIcon.textContent = "cast";
            }
            enableButton.appendChild(eIcon);
             
            let enableCol = document.createElement("div");
            enableCol.classList.add("btn-group", "settingsGroup", "pt-4");
            enableCol.appendChild(enableButton);
            
            let settingsButton = document.createElement("button");
            settingsButton.classList.add("btn", "btn-outline-secondary", "btn-clear", "settingBtn", "pt-2");
            settingsButton.setAttribute("data-target",device["Id"]);
            let sIcon = document.createElement("span");
            sIcon.classList.add("material-icons");
            sIcon.textContent = "settings";
            settingsButton.appendChild(sIcon);
            enableCol.appendChild(settingsButton);
            settingsCol.appendChild(enableCol);
            // Create settings button
            //Brightness slider
            let brightnessRow = document.createElement("div");
            brightnessRow.classList.add("col-12", "brightRow");
            
            // Slider
            let brightnessSlide = document.createElement("input");
            brightnessSlide.setAttribute("type","range");
            brightnessSlide.setAttribute("data-target",device["Id"]);
            brightnessSlide.setAttribute("data-attribute","Brightness");
            brightnessSlide.setAttribute("min", "0");
            brightnessSlide.setAttribute("max", "100");
            brightnessSlide.value = device["Brightness"];
            brightnessSlide.classList.add("form-control", "w-100", 'custom-range');
            
            // Brightness column
            let brightnessCol = document.createElement("div");
            brightnessCol.classList.add("col-12", "pt-1");
            
            brightnessCol.appendChild(brightnessSlide);
            

            // Put it all together
            iconCol.appendChild(image);
            titleCol.appendChild(title);
            titleCol.appendChild(subTitle);
            
            bodyDiv.appendChild(iconCol);
            bodyDiv.appendChild(titleCol);
            bodyDiv.appendChild(settingsCol);
            bodyDiv.appendChild(brightnessCol);
            mainDiv.appendChild(bodyDiv);
            container.appendChild(mainDiv);
        }         
    }
}

function isValid(toCheck) {
    return (toCheck !== null && toCheck !== undefined);
}

function getObj(group, key, val) {
    if (isValid(group)) {
        for(let i=0; i < group.length; i++) {
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
        for(let i=0; i < group.length; i++) {
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
    let d = data.store["Devices"];
    if (isValid(d)) {
        d.sort((a, b) => (a.Name > b.Name) ? 1 : -1);
        data.devices = d;
    } else {
        sendMessage("ScanDevices");
    }
}

function updateDevice(id, property, value) {
    let dev;
    let isLoaded = false;
    if (isValid(deviceData) && deviceData["Id"] === id) {
        dev = deviceData;
        isLoaded = true;
    } else {
        dev = findDevice(id);
    }
    if (isValid(dev) && dev.hasOwnProperty(property)) {
        dev[property] = value;
        saveDevice(dev);
        sendMessage("updateDevice", dev, true);
    }    
    if (isLoaded) {
        deviceData = dev;
        if(expanded) {
            createDeviceSettings();
        }
        
    }
}

// Find device by id in datastore
function findDevice(id) {
    for (let i=0; i < data.devices.length; i++) {
        if (data.devices[i]) {
            if (data.devices[i]["Id"] === id) {
                return data.devices[i];
            }
        }
    }
    return null;
}

// Save device by id in datastore
function saveDevice(deviceData) {
    for(let i=0; i < data.devices.length; i++) {
        if (data.devices[i]) {
            if(data.devices[i]["Id"] === deviceData["Id"]) {
                data.devices[i] = deviceData;
                return;
            }
        }
    }
}

function loadData() {
    let getUrl = window.location;
    let baseUrl = getUrl .protocol + "//" + getUrl.host;
    console.log("URL Base: " + baseUrl);    
    sendMessage("LoadData");
}

function RefreshData() {
    if (!refreshing) {
        refreshing = true;
        console.log("Refreshing data.");
        if (socketLoaded) {
            doGet("./api/DreamData/action?action=refreshDevices");
        } else {
            doGet("./api/DreamData/action?action=refreshDevices", function (newData) {
                console.log("Loading dream data from /GET: ", newData);
                data.store = newData;                
            });
        }
    }
}

// Utility functions!
function $(elem) {
    return document.querySelector(elem);
}


function create(tag) {
    return document.createElement(tag);
}

function setCookie(cname, cvalue, exdays) {
    let d = new Date();
    d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
    let expires = "expires="+d.toUTCString();
    document.cookie = cname + "=" + cvalue + ";" + expires + ";path=/";
}

function getCookie(cname) {
    let name = cname + "=";
    let ca = document.cookie.split(';');
    for(let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) === 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

function getStoreProperty(name) {
    let store = data.store;
    if (!isValid(store)) return null;
    let sysData = store["SystemData"];
    if (isValid(sysData) && isValid(sysData[0])) {
        if (name === "SystemData") return sysData;
        if (sysData.hasOwnProperty(name)) {
            return sysData[name];
        }
    }

    if (data.store.hasOwnProperty(name)) {
        return data.store[name];
    }
    
    return null;
}

function setStoreProperty(name, value) {
    let store = data.store;
    if (!isValid(store)) return;
    if (data.store.hasOwnProperty(name)) {
        data.store[name] = value;
    }
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
            element.style.top = to.top;
            element.style.left = to.left;
            element.style.width = to.width;
            element.style.height = to.height;
            element.style.padding = to.padding;
            
        });
        setTimeout(function(){
            let bbGroup = element.querySelector(".settingsGroup");
            if (isValid(bbGroup)) {
                if (expanded) {
                    bbGroup.classList.add("float-right");
                } else {
                    bbGroup.classList.remove("float-right");
                }
            }
            element.querySelector(".card-body").querySelectorAll(".exp").forEach(function(row){
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
            
            if (row.classList.contains("settingsCol")) {
                if (expanded) {
                    row.classList.add("col-md-6");
                } else {
                    row.classList.remove("col-md-6");
                }
            }            
        });
            
        
        }, 50);
        
        setTimeout(res, duration);
    })
}

const fadeContent = (element, opacity, duration = 300) => {
    return new Promise(res => {
        [...element.children, element].forEach((child) => {
            requestAnimationFrame(() => {
                child.style.transition = `opacity ${duration}ms ease-in`;
                child.style.opacity = opacity;
            });
        })
        setTimeout(res, duration);
    })
}

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
    // hide the original card with opacity
    card.style.opacity = '0';
    // add card to the main container
    document.querySelector(".main").appendChild(cardClone);
    let cardRow = document.getElementById("mainContent");
    let oh = cardRow.offsetTop;
    // remove the display style so the original content is displayed right
    cardClone.style.display = 'block';
    // Expand that bish
    await toggleExpansion(cardClone, {top: oh + "px", left: 0, width: '100%', height: 'calc(100% - ' + oh + 'px)', padding: "1rem 3rem"}, 250);
    
    let sepDiv = document.createElement("div");
    sepDiv.classList.add("dropdown-divider");
    let settingsDiv = document.createElement("div");
    settingsDiv.classList.add("deviceSettings", "row", "text-center");
    settingsDiv.id = "deviceSettings";
    //settingsDiv.style.opacity = "0%";
    settingsDiv.style.overflow = "scrollY";
    settingsDiv.style.position = "relative";
    cardClone.appendChild(sepDiv);
    cardClone.appendChild(settingsDiv);
    cardClone.style.overflowY = "scroll";

    // Create settings for our card
    document.querySelector(".mainSettings").classList.add('d-none');
    closeButton.classList.remove('d-none');
    createDeviceSettings();
};

function createDeviceSettings() {
    let settingsDiv = document.getElementById("deviceSettings");
    settingsDiv.innerHTML = "";
    let linkCol = document.createElement("div");
    let mapCol = document.createElement("div");
    linkCol.classList.add("col-12", "row", "justify-content-center");
    mapCol.classList.add("col-12");
    linkCol.id = "linkCol";
    mapCol.id = "mapCol";
    settingsDiv.appendChild(linkCol);
    settingsDiv.appendChild(mapCol);
    console.log("Loading device data: ", deviceData);   
    let props = deviceData["KeyProperties"];
    if (isValid(props)) {
        let container = document.createElement("div");
        container.classList.add("container");
        let id = deviceData["Id"];
        let row = document.createElement("div");
        row.classList.add("row", "border", "justify-content-center", "pb-5");
        let header = document.createElement("div");
        header.classList.add("col-12", "headerCol");
        row.appendChild(header);

        for (let i =0; i < props.length; i++) {
            
            let prop = props[i];
            let propertyName = prop["ValueName"];
            let elem, se;
            let value = deviceData[propertyName];
            
            switch(prop["ValueType"]) {
                case "text":
                    elem = new SettingElement(prop["ValueLabel"], "text", id, propertyName, value);
                    elem.isDevice = true;
                    break;
                case "number":
                    elem = new SettingElement(prop["ValueLabel"], "number", id, propertyName, value);
                    elem.isDevice = true;
                    break;
                case "check":
                    elem = new SettingElement(prop["ValueLabel"], "check", id, propertyName, value);
                    elem.isDevice = true;
                    break;
                case "ledmap":
                    drawLedMap = true;
                    appendLedMap();
                    break;
                case "select":
                    elem = new SettingElement(prop["ValueLabel"], "select", id, propertyName, value);
                    elem.options = prop["Options"];
                    break;
                case "sectormap":
                    appendSectorMap();
                    break;
                case "nanoleaf":
                    appendNanoSettings();
                    break;
                case "hue":
                    appendHueSettings();
                    break;
            }
            if (isValid(elem)) {
                elem.isDevice = true;
                se = createSettingElement(elem);
                row.appendChild(se);
            }
        }
        
        container.appendChild(row);
        if (deviceData.Tag !== "Led") {
            let removeBtn = new SettingElement("Remove device", "button", id, "removeDevice", id);
            let row2 = document.createElement("div");
            row2.classList.add("row", "border", "justify-content-center", "pb-5");
            row2.appendChild(createSettingElement(removeBtn));
            container.appendChild(row2);
        }
        let ds = document.getElementById("deviceSettings");
        ds.appendChild(container);
    }
}


function createSettingElement(settingElement) {
    let group = document.createElement("div");
    group.classList.add("col-12", "col-md-6", "col-xl-3", "justify-content-center", "form-group");
    let label = document.createElement("label");   
    label.innerText = settingElement.descrption;
    let element;
    
    switch(settingElement.type) {
        case "check":
            element = document.createElement("input");
            element.type = "checkbox";
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
            label.classList.add("pt-5");
            element = document.createElement("div");
            element.classList.add("btn", "btn-danger", "removeDevice");
            let icon = document.createElement("span");
            icon.classList.add("material-icons");
            icon.textContent = "delete";
            element.appendChild(icon);
            break;
    }
    if (settingElement.type === "check") {
        label.classList.add("form-check-label");
        group.classList.add("form-check");
        if (isValid(element)) element.classList.add("form-check-input");
    } else {
        label.classList.add("form-label");
        if (isValid(element)) element.classList.add("form-control");
    }
    
    group.appendChild(label);
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
    return group;
}

function SettingElement(description, type, object, property, value, hint, minLimit, maxLimit, increment, options, id, isDevice) {
    this.descrption = description;
    this.type = type;
    this.object = object;
    this.property = property ?? {};
    this.hint = hint ?? "";
    this.minLimit = minLimit ?? 0;
    this.maxLimit = maxLimit ?? 255;
    this.increment = increment ?? 1;
    this.options = options;
    this.id = id;
    this.value = value;
    this.isDevice = isDevice ?? false;
}

function appendNanoSettings() {
    if (isValid(deviceData["Token"])) {
        drawLinkPane("nanoleaf", true);
        drawNanoShapes(deviceData);
    } else {
        drawLinkPane("nanoleaf", false);
    }
}

function appendHueSettings() {
    if (isValid(deviceData["Token"])) {
        drawLinkPane("hue", true);
        appendSectorMap();
        createHueMap();
    } else {
        drawLinkPane("hue", false);
    }
}

function drawLinkPane(type, linked) {
    let wrap = document.createElement("div");
    wrap.classList.add("row", "col-12", "justify-content-center");
    let header = document.createElement("div");
    header.classList.add("header");
    header.innerText = linked ? "Device is linked" : "Click here to link";
    let div = document.createElement("div");
    div.classList.add("col-8", "col-sm-6", "col-md-4", "col-lg-3", "col-xl-2", "linkDiv");
    div.setAttribute("data-type",type);
    div.setAttribute("data-id", deviceData["Id"]);
    div.setAttribute("data-linked",linked);
    let img = document.createElement("img");
    img.classList.add("img-fluid");
    img.src = "./img/" + type + "_icon.png";
    let linkImg = document.createElement("img");
    linkImg.classList.add("linkImg");
    linkImg.classList.add(linked ? "linked" : "unlinked");
    let circle = document.createElement("div");
    circle.classList.add("hide");
    circle.id = "CircleBar";
    div.appendChild(img);
    div.appendChild(linkImg);
    div.appendChild(circle);
    wrap.appendChild(header);
    wrap.appendChild(div);    
    document.getElementById("linkCol").appendChild(wrap);
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

function appendSectorMap() {
    let imgDiv = document.createElement("div");
    imgDiv.id = "mapDiv";
    let img = document.createElement("img");
    let exSect = document.getElementById("sectorImage");
    if (isValid(exSect)) exSect.remove();
    img.id = "sectorImage";
    img.classList.add("img-fluid", "col-xl-8", "col-lg-8", "col-md-12");
    img.src = baseUrl + "/img/sectoring_screen.png";
    img.addEventListener("load", function() {
        console.log("image has loaded");
        setTimeout(function() {createSectorMap(imgDiv, document.getElementById("sectorImage"))}, 500);
    });
    imgDiv.appendChild(img);
    let settingsDiv = document.getElementById("deviceSettings");
    settingsDiv.append(imgDiv);
}

function appendLedMap() {
    let mapDiv = document.getElementById("mapDiv");
    if (isValid(mapDiv)) mapDiv.remove();
    let imgDiv = document.createElement("div");
    imgDiv.id = "mapDiv";
    let img = document.createElement("img");
    img.id = "sectorImage";
    img.classList.add("img-fluid", "col-xl-8", "col-lg-8", "col-md-12");
    img.src = baseUrl + "/img/sectoring_screen.png";
    imgDiv.appendChild(img);
    let settingsDiv = document.getElementById("deviceSettings");
    settingsDiv.append(imgDiv);
    let systemData = data.store["SystemData"];
    setTimeout(function() {createLedMap(imgDiv, img ,systemData, deviceData)}, 500);
}

function createSectorMap(targetElement, sectorImage, regionName) {
    let img = sectorImage;
    let w = img.offsetWidth;
    let h = img.offsetHeight;
    let imgL = img.offsetLeft;
    let imgT = img.offsetTop;
    console.log("Img dims: ",w,h,imgL,imgT, img);
    let exMap = targetElement.querySelector("#sectorMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1100;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let fHeight = (h - hMargin - hMargin) / vSectors;
    let fWidth = (w - wMargin - wMargin) / hSectors;
    let map = document.createElement("div");
    map.id = "sectorMap";
    map.classList.add("sectorMap");
    map.style.top = imgT + "px";
    map.style.left = imgL + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
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

    let s2 = document.createElement("div");
    s2.classList.add("sector2");
    s2.style.position = "absolute";
    s2.style.top = hMargin + fHeight + "px";
    s2.style.height = (fHeight * 4) + "px";
    s2.style.left = wMargin + fWidth + "px";
    s2.style.width = (fWidth * 8) + "px";
    s2.style.border = "2px solid black";
    map.appendChild(s2);
    targetElement.appendChild(map);
    if (isValid(deviceData) && expanded) {
        let mappedLights;
        if (isValid(deviceData["MappedLights"])) {
            mappedLights = deviceData["MappedLights"];            
        }
        if (isValid(mappedLights)) {
            for(let i =0; i < mappedLights.length; i++) {
                let lMap = mappedLights[i];
                let target = lMap["TargetSector"];
                let id = lMap["_id"];
                let targetDiv = document.querySelector('.sector[data-sector="'+target+'"]');
                if (isValid(targetDiv)) {
                    console.log("Checking sector " + target);
                    targetDiv.classList.add("checked");
                }
            }
        }
    }
}

function createLedMap(targetElement, sectorImage) {
    let range1;
    let sd = data.store["SystemData"];
    
    if (isValid(deviceData)) {
        let count = deviceData["LedCount"];
        let offset = deviceData["Offset"];
        let mode = deviceData["StripMode"];
        let total = sd["LedCount"];
        if (isValid(mode) && mode === 2) count /=2;
        range1 = ranges(total, offset, count);
    }
    
    let img = sectorImage;
    let w = img.offsetWidth;
    let h = img.offsetHeight;
    let imgL = img.offsetLeft;
    let imgT = img.offsetTop;
    let exMap = targetElement.querySelector("#ledMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1100;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let flHeight = (h - hMargin - hMargin) / leftCount;
    let frHeight = (h - hMargin - hMargin) / rightCount;
    let ftWidth = (w - wMargin - wMargin) / topCount;
    let fbWidth = (w - wMargin - wMargin) / bottomCount;
    let dHeight = (flHeight + frHeight) / 2;
    let dWidth = (ftWidth + fbWidth) / 2;
    let map = document.createElement("div");
    map.id = "ledMap";
    map.classList.add("ledMap");
    map.style.top = imgT + "px";
    map.style.left = imgL + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
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
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        s1.setAttribute("title", ledCount.toString());
        map.appendChild(s1);
        ledCount++;
    }

    for (let i = 0; i < topCount - 1; i++) {
        l = w - wMargin - (ftWidth * (i + 1));
        r = l - ftWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = ftWidth.toString() + "px";
        s1.style.height = frHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        s1.setAttribute("title", ledCount.toString());
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
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = dWidth.toString() + "px";
        s1.style.height = flHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        s1.setAttribute("title", ledCount.toString());
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
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
        s1.setAttribute("data-sector", ledCount.toString());
        s1.style.position = "absolute";
        s1.style.top = t.toString() + "px";
        s1.style.left = l.toString() + "px";
        s1.style.width = fbWidth.toString() + "px";
        s1.style.height = dHeight.toString() + "px";
        s1.setAttribute("data-bs-toggle", "tooltip");
        s1.setAttribute("data-bs-placement", "top");
        s1.setAttribute("title", ledCount.toString());
        map.appendChild(s1);
        ledCount++;
    }
    targetElement.appendChild(map);
}

function ranges(ledCount, offset, total) {
    let range = [];
    while (range.length < offset + total) {
        for (let i = 0; i < ledCount; i++) {
            range.push(i);
        }        
    }
    console.log("Full range: ", range);
    let sliced = range.slice(offset, total + offset);
    console.log("Sliced: ", sliced);
    return sliced;
}


function createHueMap() {
    let settingsDiv = document.getElementById("deviceSettings");
    let selectedGroup = deviceData["SelectedGroup"];

    let groups = deviceData["Groups"];
    let group;
    for(let i=0; i < groups.length; i++) {
        let sg = groups[i];
        if (sg["Id"] === selectedGroup.toString()) {
            group = sg;
            console.log("Group: ",group);
        }
    }
    
    // Main container
    let hueMapRow = document.createElement("div");
    hueMapRow.classList.add("row");
    // Group select row
    let groupSelectCol = document.createElement("div");
    groupSelectCol.classList.add("col-12");
    // Group select
    let groupSelect = document.createElement("select");
    groupSelect.setAttribute("data-property", "SelectedGroup");
    groupSelect.setAttribute("data-object", deviceData["Id"]);
    groupSelect.classList.add("devSetting");
    let defaultOption = document.createElement("option");
    defaultOption.textContent = "";
    defaultOption.value = "-1";
    if (selectedGroup === -1) defaultOption.selected = true;
    
    groupSelect.appendChild(defaultOption);
    groupSelect.id = "HueGroup";
    for(let i = 0; i < groups.length; i++) {
        let opt = document.createElement("option");
        opt.value = groups[i]["Id"];
        opt.innerText = groups[i]["name"];
        if (selectedGroup.toString() === groups[i]["Id"]) opt.selected = true;
        groupSelect.appendChild(opt);
    }
    groupSelectCol.appendChild(groupSelect);
    settingsDiv.appendChild(groupSelectCol);

    if (!isValid(group)) {
        console.log("No group, returning.");
        return;        
    }

    let lights = deviceData['Lights'];
    let lightMap = deviceData['MappedLights'];
    console.log("Mapping lights: ", lights);
    // Get the main light group
    let lightGroup = document.createElement("div");
    lightGroup.classList.add("row");
    if (!group.hasOwnProperty('lights')) return false;
    const ids = group["lights"];

    // Sort our lights by name
    lights = lights.sort(function (a, b) {
        if (!a.hasOwnProperty('Name') || !b.hasOwnProperty('Name')) return false;
        return a.Name.localeCompare(b.Name);
    });
    console.log("Sorted lights: " + lights);
    // Loop through our list of all lights
    for (let l in lights) {
        if (lights.hasOwnProperty(l)) {
            let light = lights[l];
            let id = light['_id'];
            let map;
            let brightness = 255;
            let override = false;
            let selection = -1;

            for(let m in lightMap) {
                if (lightMap.hasOwnProperty(m)) {
                    if (lightMap[m]['_id'] === id) {
                        map = lightMap[m];
                        brightness = map["Brightness"];
                        override = map["Override"];
                        selection = map["TargetSector"];                                           
                    }
                }
            }

            if (ids.includes(id)) {
                const name = light['Name'];
                // Create the label for select
                const label = document.createElement('label');
                label.innerHTML = name + ":";
                label.setAttribute("for", "lightMap" + id);

                // Create a select for this light
                const newSelect = document.createElement('select');
                newSelect.className = "lightProperty form-control text-dark bg-light";
                newSelect.setAttribute('data-id', id);
                newSelect.setAttribute('data-property',"TargetSector");

                // Create the blank "unmapped" option
                let opt = document.createElement("option");
                opt.value = "-1";
                opt.innerHTML = "";

                // Set it to selected if we don't have a mapping
                if (selection === -1) {
                    opt.setAttribute('selected', 'selected');
                }
                newSelect.appendChild(opt);

                // Add the options for our regions
                for (let i = 1; i < 29; i++) {
                    opt = document.createElement("option");
                    opt.value = (i).toString();
                    opt.innerHTML = "<BR>" + (i);
                    // Mark it selected if it's mapped
                    if (selection === i) opt.setAttribute('selected', 'selected');
                    newSelect.appendChild(opt);
                }


                // Create the div to hold our select
                const selDiv = document.createElement('div');
                selDiv.className += "form-group";
                selDiv.appendChild(label);
                selDiv.appendChild(newSelect);

                // Create label for brightness
                const brightLabel = document.createElement('label');
                brightLabel.innerHTML = "Brightness: ";
                brightLabel.setAttribute('for', 'brightness' + id);

                // Create the brightness slider
                const newRange = document.createElement("input");
                newRange.className = "form-control lightProperty";
                newRange.setAttribute("type", "range");
                newRange.setAttribute('data-id', id);
                newRange.setAttribute('data-property',"Brightness");
                newRange.setAttribute('name', 'brightness' + id);
                newRange.setAttribute('min', "0");
                newRange.setAttribute('max', "100");
                newRange.setAttribute('value', brightness.toString());
                newRange.setAttribute('data-id', id);
                newRange.setAttribute('data-property',"Brightness");

                // Create container div for brightness slider
                const rangeDiv = document.createElement('div');
                rangeDiv.className += "form-group";
                rangeDiv.appendChild(brightLabel);
                rangeDiv.appendChild(newRange);

                // Create label for override check
                const checkLabel = document.createElement('label');
                checkLabel.innerHTML = "Override";
                checkLabel.className += "form-check-label";
                checkLabel.setAttribute('for', 'overrideBrightness' + id);

                // Create a checkbox
                const newCheck = document.createElement("input");
                newCheck.className += "overrideBright form-check-input lightProperty";
                newCheck.setAttribute('data-id', id);
                newCheck.setAttribute('data-property',"Override");
                newCheck.setAttribute("type", "checkbox");
                if (override) newCheck.setAttribute("checked", 'checked');

                // Create the div to hold the checkbox
                const chkDiv = document.createElement('div');
                chkDiv.className += "form-check";
                chkDiv.appendChild(newCheck);
                chkDiv.appendChild(checkLabel);

                // Create the div for the other divs
                const lightDiv = document.createElement('div');
                lightDiv.className += "delSel col-12 col-md-6 col-xl-3 justify-content-center form-group";
                lightDiv.id = id;
                lightDiv.setAttribute('data-name', name);
                lightDiv.setAttribute('data-id', id);
                lightDiv.appendChild(selDiv);
                lightDiv.appendChild(chkDiv);
                lightDiv.appendChild(rangeDiv);

                // Add it to our document
                lightGroup.appendChild(lightDiv);                
            }
        }
    }
    settingsDiv.appendChild(lightGroup);
    
    // $('.delSel').bootstrapMaterialDesign();
}


function drawNanoShapes(panel) {
    
    // Get window width
    let width = window.innerWidth;
    let height = width * .5625;

    // Get layout data from panel
    let mirrorX = panel['MirrorX'];
    let mirrorY = panel['MirrorY'];
    let layout = panel['Layout'];
    let sideLength = layout['SideLength'];

    // Create our stage
    let stage = new Konva.Stage({
        container: 'mapCol',
        width: width,
        height: height
    });

    // Shape layer
    let cLayer = new Konva.Layer();
    stage.add(cLayer);

    let positions = layout['PositionData'];
    let minX = 1000;
    let minY = 1000;
    let maxX = 0;
    let maxY = 0;

    // Calculate the min/max range for each tile
    for (let i=0; i< positions.length; i++) {
        let data = positions[i];
        if (data.X < minX) minX = data.X;
        if ((data.Y * -1) < minY) minY = (data.Y * -1);
        if (data.X > maxX) maxX = data.X;
        if ((data.Y * -1) > maxY) maxY = (data.Y * -1);
    }
    let wX = maxX - minX;
    let wY = maxY - minY;
    let scaleXY = 1;
    if (wX > width || wY > height) {
        scaleXY = .5;
        console.log("Scaling to half.");
        maxX *= scaleXY;
        maxY *= scaleXY;
        minX *= scaleXY;
        minY *= scaleXY;
    }
    let x0 = (width - maxX - minX) / 2;
    let y0 = (height - maxY - minY) / 2;
    
    console.log("WTF: ", x0, y0);
    
    // Group for the shapes
    let shapeGroup = new Konva.Group({
        rotation: 0,
        draggable: false,
        x: x0,
        y: y0,
        scale: {
            x: scaleXY,
            y: scaleXY
        }
    });

    
    
    for (let i=0; i < positions.length; i++) {
        let data = positions[i];
        let shape = data['ShapeType'];
        sideLength = data["SideLength"];
        let x = data.X;
        let y = data.Y;
        if (mirrorX) x *= -1;
        if (!mirrorY) y *= -1;
        
        let sText = new Konva.Text({
            x: x,
            y: y,
            text: data["PanelId"],
            fontSize: 30,
            listening: false,
            fontFamily: 'Calibri'
        });

        let sectorText = data['TargetSector'];

        let sText2 = new Konva.Text({
            x: x,
            y: y - 35,
            text: sectorText,
            fontSize: 30,
            listening: false,
            fontFamily: 'Calibri'
        });
        let o = data['O'];
        // Draw each individual shape
        switch (shape) {
            case 0:
            case 1:
                y = (data.y * -1) + Math.abs(minY);
                let invert = false;
                if (o === 60 || o === 180 || o === 300) {
                    invert = true;
                }

                let angle = (2*Math.PI)/3;
                // Calculate our overall height based on side length
                let h = sideLength * (Math.sqrt(3)/2);
                h *= 2;
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
                let poly = new Konva.Line({
                    points: [x0, y0, x1, y1, x2, y2],
                    fill: 'white',
                    stroke: 'black',
                    strokeWidth: 5,
                    closed: true,
                    id: data["PanelId"]
                });
                poly.on('click', function(){
                    console.log("POLY CLICK");
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
                poly.on('tap', function(){
                    console.log("POLY TAP")
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
                shapeGroup.add(poly);
                break;
            case 2:
            case 3:
            case 4:
                let tx = x - (sideLength / 2);
                let ty = y - (sideLength / 2);
                let rect1 = new Konva.Rect({
                    x: tx,
                    y: ty,
                    width: sideLength,
                    height: sideLength,
                    fill: 'white',
                    stroke: 'black',
                    strokeWidth: 4
                });
                rect1.on('click', function(){
                    console.log("RECT CLICK:", data);
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
                rect1.on('tap', function(){
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
                console.log("Adding shape: ", rect1);
                shapeGroup.add(rect1);
                break;
            case 5:
                console.log("Draw a power supply??");
                break;
        }
        sText.offsetX(sText.width() / 2);
        sText2.offsetX(sText2.width() / 2);
        
        shapeGroup.add(sText);
        shapeGroup.add(sText2);
    }
    // Add to our canvas layer and draw
    cLayer.add(shapeGroup);
    
    let container = document.getElementById('mapCol');

    // now we need to fit stage into parent
    let containerWidth = container.offsetWidth;
    // to do this we need to scale the stage
    let scale = containerWidth / width;

    stage.width(width * scale);
    stage.height(height * scale);
    stage.scale({ x: scale, y: scale });
    //shapeGroup.scale = scale;
    console.log("Drawing stage: ", stage);
    stage.draw();
    
    cLayer.draw();
    //console.log("Clayer drawn: ", cLayer);
    cLayer.zIndex(0);    
}

function setNanoMap(id, current) {
    nanoTarget = id;
    nanoSector = current;
    
    let myModal = new bootstrap.Modal(document.getElementById('nanoModal'));
    let wrap = document.getElementById("nanoPreviewWrap");
    let img = document.getElementById("nanoPreview");
    myModal.show();
    createSectorMap(wrap, img, "nano");

    let nanoRegion = document.querySelectorAll(".nanoRegion");
    for (let i=0; i < nanoRegion.length; i++) {
        let obj = nanoRegion[i];
        obj.classList.remove("checked");
    }

    if (current !== -1) {
        console.log("Looking for " + current);
        document.querySelector('.sector[data-sector="'+current+'"]').classList.add("checked");
    }

}


function sizeContent() {
    let navDiv = document.getElementById("mainNav");
    let footDiv = document.getElementById("footer");
    let cDiv = document.getElementById("mainContent");
    let colorDiv = document.getElementById("ambientNav");
    let wHeight = window.innerHeight;
    let wWidth = window.innerWidth;
    let ambientOffset = 0;
    if (mode === 3) {
        ambientOffset = 48; 
    }
    cDiv.style.position = "fixed";
    cDiv.style.top = navDiv.offsetHeight + ambientOffset + "px";
    cDiv.style.height = wHeight - navDiv.offsetHeight - footDiv.offsetHeight - ambientOffset + "px";
    cDiv.style.width = wWidth + "px";
    colorDiv.style.width = wWidth + "px";
    if (expanded) {
        createDeviceSettings();
    }
    if (settingsShown) {
        loadSettings();
    }
}

async function closeCard() {
    cardClone.style.overflowY = "none";
    deviceData = undefined;
    drawSectorMap = false;
    drawLedMap = false;    
    expanded = false;
    // shrink the card back to the original position and size    
    await toggleExpansion(cardClone, {top: `${toggleTop}px`, left: `${toggleLeft}px`, width: `${toggleWidth}px`, height: `${toggleHeight}px`, padding: '1rem 1rem'}, 300);
    // show the original card again
    document.querySelector(".mainSettings").classList.remove('d-none');
    document.getElementById("closeBtn").classList.add('d-none');

    baseCard.style.removeProperty('opacity');
    cardClone.remove();
}

