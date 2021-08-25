let socketLoaded = false;
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
let nanoTarget, nanoSector;
let demoLoaded = false;
let myTour;
let ledData = '{"AutoBrightnessLevel":true,"FixGamma":true,"AblMaxMilliamps":5000,"GpioNumber":18,"LedCount":150,"MilliampsPerLed":25,"Offset":50,"StartupAnimation":0,"StripType":0,"Name":"Demo LED Strip","Id":"-1","Tag":"Led","IpAddress":"","Brightness":100,"Enable":false,"LastSeen":"08/05/2021 13:28:53","KeyProperties":[{"Options":{},"ValueLabel":"","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"ledmap","ValueStep":"1","ValueType":"ledmap"},{"Options":{},"ValueLabel":"Led Count","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"LedCount","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"Led Offset","ValueHint":"","ValueMax":"100","ValueMin":"0","ValueName":"Offset","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"LED Multiplier","ValueHint":"Positive values to multiply (skip), negative values to divide (duplicate).","ValueMax":"5","ValueMin":"-5","ValueName":"LedMultiplier","ValueStep":"1","ValueType":"number"},{"Options":{},"ValueLabel":"Reverse Strip","ValueHint":"Reverse the order of the leds to clockwise (facing screen).","ValueMax":"100","ValueMin":"0","ValueName":"ReverseStrip","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Fix Gamma","ValueHint":"Automatically correct Gamma (recommended)","ValueMax":"100","ValueMin":"0","ValueName":"FixGamma","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Enable Auto Brightness","ValueHint":"Automatically adjust brightness to avoid dropouts.","ValueMax":"100","ValueMin":"0","ValueName":"AutoBrightnessLevel","ValueStep":"1","ValueType":"check"},{"Options":{},"ValueLabel":"Milliamps Per LED","ValueHint":"\'Conservative\' = 25, \'Normal\' = 55","ValueMax":"100","ValueMin":"0","ValueName":"MilliampsPerLed","ValueStep":"1","ValueType":"text"},{"Options":{},"ValueLabel":"Power Supply Voltage","ValueHint":"Total PSU voltage in Milliamps","ValueMax":"100","ValueMin":"0","ValueName":"AblMaxMilliamps","ValueStep":"1","ValueType":"text"}]}';
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
};

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
    let popover = new bootstrap.Popover(document.getElementById("fps"), {
        container: 'body'
    })
    let getUrl = window.location;
    baseUrl = getUrl .protocol + "//" + getUrl.host;
    fpsCounter = document.getElementById("fps");
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
        let col = color.toHEXA();
        console.log("COL: ", col);
        newColor = col[0] + col[1] + col[2];
    }).on('changestop', (source, instance) => {
        let sd = getStoreProperty("SystemData");
        sd["AmbientColor"] = newColor;
        sd["AmbientShow"] = -1;
        let asSelect = document.getElementById("AmbientShow");
        asSelect.value = "-1";
        pickr.setColor("#" + newColor);
        setStoreProperty("SystemData",sd);
        sendMessage("SystemData",sd);
        
    }).on('swatchselect', (color, instance) => {
        let col = color.toHEXA();
        newColor = col[0] + col[1] + col[2];
        let sd = getStoreProperty("SystemData");
        sd["AmbientColor"] = newColor;
        sd["AmbientShow"] = -1;
        let asSelect = document.getElementById("AmbientShow");
        asSelect.value = "-1";
        pickr.setColor("#" + newColor);
        setStoreProperty("SystemData",sd);
        sendMessage("SystemData",sd);        
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
    let devs = data.store["Devices"];
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
            if (dev["Tag"] === "DreamScreen" && dev["DeviceTag"].includes("DreamScreen")) {
                let opt = document.createElement("option");
                opt.value = dev["Id"];
                opt.innerText = dev["Name"] + " - " + dev["Id"];
                if (isValid(target) && opt.value === target) opt.selected = true;
                devSelect.appendChild(opt);
            }
        }
    } else {
        console.log("Devs invalid?");
    }
    
    
    let lSel = document.querySelector('[data-property="LeftCount"][data-object="SystemData"]');
    let rSel = document.querySelector('[data-property="RightCount"][data-object="SystemData"]');
    let tSel = document.querySelector('[data-property="TopCount"][data-object="SystemData"]');
    let bSel = document.querySelector('[data-property="BottomCount"][data-object="SystemData"]');


    
    // If using DS capture, set static/dev LED counts.
    
    leftCount = sd["LeftCount"];
    rightCount = sd["RightCount"];
    topCount = sd["TopCount"];
    bottomCount = sd["BottomCount"];
    hSectors = sd["HSectors"];
    vSectors = sd["VSectors"];
    useCenter = sd["UseCenter"];
    
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
            console.log("Sending to " + endpoint, sData);
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
        return;
    }
    let xhttp = new XMLHttpRequest();
        
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
        let sd = data.store["SystemData"];
        let tempUnit = "°F";

        if (isValid(sd)) {
            tempUnit = (sd["Units"] === "0") ? "°F" : "°C";
        }
        tempText.textContent = cpuData["tempCurrent"] + tempUnit;
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
        console.log("Data loaded: ", parsed);
        data.store = parsed;
        loadUi();
        if (isValid(parsed["SystemData"]) && !parsed["SystemData"]["SkipIntro"]) {
            //Show intro here
            if (!demoLoaded) {
                demoLoaded = true;
                showIntro();
            }
        }
    });
    
    websocket.on('inputImage',function(data) {
        document.getElementById("inputPreview").src = "data:image/png;base64," + data;
    });

    websocket.on('outputImage',function(data) {
        document.getElementById("outputPreview").src = "data:image/png;base64," + data;
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
        console.log("frame counts: ", stuff); 
        fpsCounter.innerText = stuff['source'] + "FPS"; 
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
               deviceData["LastSeen"] = stuff["LastSeen"];
               if (Object.toJSON(deviceData) === Object.toJSON(stuff)) {
                   console.log("DD exactly matches stuff, no need to reload?");
               } else {
                   deviceData = mergeDeviceData(deviceData,stuff);
                   if (settingsShown) createDeviceSettings();    
               }               
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
        
        let intProps = [
            "CaptureMode", "ScreenCapMode", "PreviewMode", "AutoUpdateTime",
        ];
        if (intProps.includes(property)) {
            val = parseInt(val);            
        }
        
        let pack;
        if (isValid(obj) && isValid(property) && isValid(val)) {
            console.log("Trying to set: ", obj, property, val);
            let numVal = parseInt(val);
            let skipProps = ["DsIp", "OpenRgbIp","GammaCorrection"];
            if (!isNaN(numVal) && !skipProps.includes(property)) val = numVal; 
            
            if (isValid(id)) {
                let strips = data.store[obj];
                for(let i=0; i < strips.length; i++) {
                    let strip = strips[i];
                    if (strip["Id"] === id) {
                        strip[property] = val;
                        strip["Id"] = id;
                        strips[i] = strip;
                        pack = strip;
                        sendMessage(obj, pack,true);
                    }
                }
                data.store[obj] = strips;
                
            } else {
                if (target.classList.contains("devSetting")) {
                    updateDevice(obj, property, val);  
                    //createDeviceSettings();
                    return;
                } else {
                        data.store[obj][property] = val;
                        pack = data.store[obj];
                        if (property === "ScreenCapMode" || property === "CaptureMode" || property === "StreamMode") {
                            updateCaptureUi();
                        }
                        if (property === "UseCenter" || property === "HSectors" || property === "VSectors") {
                            if (property === "UseCenter") useCenter = val;
                            if (property === "HSectors") hSectors = val;
                            if (property === "VSectors") vSectors = val;
                            let sPreview = document.getElementById("sectorWrap");
                            createSectorMap(sPreview);
                        }
                        
                        console.log("Sending updated object: ", obj, pack);
                        sendMessage(obj, pack,true);
                        return;    
                                        
                }
            }
                        
            if (property === "LeftCount" || property === "RightCount" || property ==="TopCount" || property === "BottomCount") {
                let lPreview = document.getElementById("sLedWrap");
                createLedMap(lPreview);
                
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
            break;
        case target === closeButton:
            closeCard();
            break;
        case target.classList.contains("sector"):
            let val = target.getAttribute("data-sector");            
            updateDeviceSector(val, target);
            if (target.classList.contains("nanoRegion")) {
                console.log("Hiding nano modal.");
                let myModalEl = document.getElementById('nanoModal');
                let modal = bootstrap.Modal.getInstance(myModalEl);
                modal.hide();    
            }            
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
            console.log("Dev setting clicked, setting: ", attribute, devId, target.checked);
            updateDevice(devId, attribute, target.checked);
            break;
        case target.classList.contains("removeDevice"):
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
                console.log("Device id: ", devId);
                deviceData = findDevice(devId);
                if (devId === "-1") deviceData = JSON.parse(ledData);
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
        case target.classList.contains("mainSettings"):
            toggleSettingsDiv();
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


async function toggleSettingsDiv() {
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
    } else {
        updateDevice(dev["Id"],"TargetSector",sector);
    }
    
    sendMessage("flashSector", parseInt(sector), false);
}

function updateLightProperty(myId, propertyName, value) {
    let lm = getLightMap(myId);
    lm[propertyName] = value;
    setLightMap(lm);
    let fGroup = deviceData["Groups"];
    let nGroup = [];
    for (let g in fGroup) {
        if (fGroup.hasOwnProperty(g)) {
            fGroup[g]["Id"] = fGroup[g]["_id"];
            nGroup.push(fGroup[g]);
        }

    }
    updateDevice(deviceData["Id"],"Groups", nGroup);    
}

function updateBeamProperty(beamPos, propertyName, value) {
    let id = deviceData["Id"];
    if (propertyName === "Offset") value = parseInt(value);
    let beamLayout = deviceData["BeamLayout"];
    let beams = beamLayout["Segments"];
    for(let i=0; i < beams.length; i++) {
        let beam = beams[i];
        if (beam["Position"] === beamPos) {
            beam[propertyName] = value;
            beams[i] = beam;
        }
    }

    beamLayout["Segments"] = beams;
    console.log("Updating beam " + id, propertyName, value);
    appendBeamLedMap();
    updateDevice(id,"BeamLayout", beamLayout);
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
    mode = newMode;
    console.log("Updating mode: " + mode);
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

function setModeButtons() {
    let sd = data.store["SystemData"];
    let capMode = sd["CaptureMode"];
    let streamMode = sd["StreamMode"];
    let videoBtn = document.getElementById("videoBtn");
    let streamBtn = document.getElementById("streamBtn");
    if (capMode === 1) {
        videoBtn.firstElementChild.innerHTML = "videocam";
    }
    if (capMode === 2) {
        videoBtn.firstElementChild.innerHTML = "settings_input_hdmi";
    }
    if (capMode === 3) {
        videoBtn.firstElementChild.innerHTML = "tv";
    }

    if (streamMode === 0) {
        streamBtn.firstElementChild.innerHTML = "";
        streamBtn.firstElementChild.classList.remove("material-icons");
        streamBtn.firstElementChild.classList.remove("appz-glimmr");
        streamBtn.firstElementChild.classList.add("appz-dreamscreen");
    }
    if (streamMode === 1) {
        streamBtn.firstElementChild.innerHTML = "";
        streamBtn.firstElementChild.classList.remove("material-icons");
        streamBtn.firstElementChild.classList.remove("appz-dreamscreen");
        streamBtn.firstElementChild.classList.add("appz-glimmr");
    }
    if (streamMode === 2) {
        streamBtn.firstElementChild.innerHTML = "sensors";
        streamBtn.firstElementChild.classList.add("material-icons");
        streamBtn.firstElementChild.classList.remove("appz-dreamscreen");
        streamBtn.firstElementChild.classList.remove("appz-glimmr");
    }
}

function loadUi() {
    loadCounts();
    setModeButtons();
    let mode = getStoreProperty("DeviceMode"); 
    let autoDisabled = getStoreProperty("AutoDisabled");
    let version = getStoreProperty("Version");
    let vDiv = document.getElementById("versionDiv");
    vDiv.innerHTML="Glimmr Version: " + version.toString();
    let sd;
    if (isValid(data.store["SystemData"])) {
        sd = data.store["SystemData"];
        let theme = sd["Theme"];
        mode = sd["DeviceMode"];
        autoDisabled = sd["AutoDisabled"];
        if (autoDisabled) mode = 0;
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
        let tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        let tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl)
        });        
    }
    sizeContent();
    document.getElementById("cardRow").click();
}

function showIntro() {
    let myTour = new Tour(
        {
            storage: false,
            backdropPadding: 5,
            backdrop: true,
            orphan: true,
            onStart: function(){
                console.log("Creating demo device card.");
                let ledObj = JSON.parse(ledData);
                loadDevice(ledObj,true);                
            },
            onEnd: function(){
                console.log("Removing demo device card.");
                if (expanded) closeCard();
                let devCard = document.querySelector('.devCard[data-id="-1"]');
                devCard.remove();
                devCard = document.querySelector('.devCard[data-id="-2"]');
                devCard.remove();
                devCard = document.querySelector('.devCard[data-id="-3"]');
                devCard.remove();
            },
            steps: [
                {
                    element: '',
                    title: 'Welcome to Glimmr!',
                    content: 'Hello, and thanks for trying out Glimmr. This short tour will help you get familiar with the UI.'
                },
                {
                    element: "#modeBtns",
                    title: 'Mode Selection',
                    content: 'Use these buttons to select the lighting mode for enabled devices. You can hover over each one to see what mode it enables.'
                },
                {
                    element: "#statDiv",
                    title: 'System Stats',
                    content: 'Here you can see the current frame rate, CPU temperature and total usage.'
                },
                {
                    element: "#refreshBtn",
                    title: 'Device Refresh',
                    placement: 'left',
                    smartPlacement: false,
                    content: 'Click here to re-scan/refresh devices.'
                },
                {
                    element: "#settingBtn",
                    title: 'Glimmr Settings',
                    placement: 'left',
                    smartPlacement: false,
                    content: 'You can access system settings by clicking this button. Let\'s take a look!',
                    reflex: true,
                    onNext: function() {
                        if (!settingsShown) toggleSettingsDiv();
                        document.getElementById("system-tab").click();
                    }
                },
                {
                    element: "#settingsTab",
                    title: 'Setting Selection',
                    content: 'Select the various settings groups here.',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    onNext: function(){
                        if (!settingsShown) toggleSettingsDiv();
                    },
                    onPrev: function(){
                        if (settingsShown) toggleSettingsDiv();
                        document.getElementById("system-tab").click();
                    }
                },
                {
                    element: "#settingsMainControl",
                    title: 'System Control',
                    container: '#mainContent',
                    backdropContainer: '#mainContent',
                    content: 'Shutdown or reboot your computer, restart Glimmr, or manually trigger an update.',
                    onNext: function(){
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
                    onNext: function(){
                        document.getElementById("system-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
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
                    onNext: function(){
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
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
                    onNext: function(){
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
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
                    onNext: function(){
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
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
                    onNext: function(){
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
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
                    onNext: function(){
                        scrollElement(myTour.getStep(myTour.getCurrentStep() + 1));
                    },
                    onPrev: function() {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element: "#devCard",
                    title: 'This is a device',
                    content: 'Here you can enable and configure various settings for each device discovered by Glimmr.',
                    onPrev: function() {
                        document.getElementById("capture-tab").click();
                        scrollSetting(myTour.getStep(myTour.getCurrentStep() - 1));
                    },
                    onNext: function() {
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
                    onNext: function(){
                        deviceData = JSON.parse(ledData);
                        if (!expanded) showDeviceCard(document.getElementById("devPrefBtn"));
                    }
                
                },
                {
                    element: "#mapWrap",
                    title: 'Element mapping',
                    content: 'Every device in Glimmr has a mapping section where you can preview the light data in relation to the screen.',
                    onNext: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function() {
                        closeCard();
                    }
                },
                {
                    element: '#LedCount',
                    title: 'LED Count',
                    content: 'This is the total number of leds in your strip. It can be less than the total number in the grid.',
                    onNext: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1))
                    }
                },
                {
                    element: '#Offset',
                    title: 'LED Offset',
                    content: 'The offset controls how many leds to skip from the start of the strip, allowing you to segment strips as need.',
                    onNext: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() + 1))
                    },
                    onPrev: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1))
                    }
                },
                {
                    element: '#LedMultiplier',
                    title: 'LED Multiplier',
                    content: 'The LED Multiplier can be used to adjust for strips or configurations where the number of LEDs' +
                        'doesn\'t correspond to the number of leds in the grid. By setting this value to a positive number,' +
                        'the strip will use every N colors from the main color array.' +
                        '' +
                        'If set to a negative value, then each color from the main array will be repeated that many times.',
                    onNext: function() {
                        closeCard();
                    },
                    onPrev: function() {
                        scrollDevPref(myTour.getStep(myTour.getCurrentStep() - 1));
                    }
                },
                {
                    element:'',
                    title: 'Tour Complete',
                    content: 'This completes the tour. Other devices have other settings, but if I were to try covering' +
                        'everything, you would be sitting here all day. Feel free to play around and look at everything,' +
                        'and head on over to <a href="https://github.com/d8ahazard/glimmr" target="_blank">the project page</a> to submit' +
                        'an issue or feature request.',
                    onPrev: function(){
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

function scrollSetting(step){
    if (!settingsShown) toggleSettingsDiv();    
    let elem = document.querySelector(step.element);
    let parent = document.getElementById("mainContent");
    let topPos = elem.offsetTop;
    console.log("ELEMN", elem);
    console.log("PARENT: ", parent);
    console.log("TOP: ", topPos);
    parent.scrollTop = topPos;
}

function scrollElement(step) {
    if (settingsShown) toggleSettingsDiv();
    let elem = document.querySelector(step.element);
    let parent = document.getElementById("mainContent");
    let topPos = elem.offsetTop;
    console.log("ELEMN", elem);
    console.log("PARENT: ", parent);
    console.log("TOP: ", topPos);
    parent.scrollTop = topPos;
}

function scrollDevPref(step) {
    let elem = document.querySelector(step.element);
    let parent = document.querySelector("#devCard.container-fluid");
    let topPos = elem.offsetTop;
    console.log("ELEMN", elem);
    console.log("PARENT: ", parent);
    console.log("TOP: ", topPos);
    parent.scrollTop = topPos;
}


function loadTheme(theme) {
    let head = document.getElementsByTagName("head")[0];
    if (theme === "light") {
        let last = head.lastChild;
        if (isValid(last.href)) {
            if (!last.href.includes("site")) {
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
    
    if (data.store == null) return;
    
    if (isValid(systemData)) {
        loadSettingObject(systemData);
        updateCaptureUi();
        setModeButtons();
        loadCounts();
        console.log("Loading System Data: ", systemData);
        let lPreview = document.getElementById("sLedWrap");
        let sPreview = document.getElementById("sectorWrap");
        setTimeout(function(){
            createLedMap(lPreview);
            createSectorMap(sPreview);
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
    let usbRow = document.getElementById("UsbSelectRow");
    let usbSel = document.getElementById("UsbSelect");
    let streamMode = systemData["StreamMode"].toString();
    let streamGroups = document.querySelectorAll(".streamGroup");
    for (let i=0; i < streamGroups.length; i++) {
        let group = streamGroups[i];
        if (group.getAttribute("data-stream") === streamMode) {
            group.classList.add("show");
            group.classList.remove("hide");
        } else {
            group.classList.add("hide");
            group.classList.remove("show");
        }
    }
    
    for (let i=0; i < capGroups.length; i++) {
        let group = capGroups[i];
        let groupMode = group.getAttribute("data-mode");
        if (groupMode === mode) {
            group.classList.add("show");
            group.classList.remove("hide");
            
        } else {
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

    let opt = document.createElement("option");
    opt.value = "";
    opt.innerText = "";
    if (opt.value === usbIdx) opt.selected = true;
    usbSel.appendChild(opt);
    
    for (const [key, value] of Object.entries(usbDevs)) {
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
}

function loadSettingObject(obj) {
    if (obj == null) {
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
            }

            if (prop === "Enable") {
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
            }            
        }        
    }
}

function loadDevices() {    
    if (demoLoaded) return;
    let container = $("#cardRow");
    container.innerHTML = "";
    for (let i = 0; i< data.devices.length; i++) {
        if (data.devices.hasOwnProperty(i)) {
            let device = data.devices[i];
            loadDevice(device, false);
        }         
    }
    //if (demoLoaded) introJs().refresh();
}

function loadDevice(device, addDemoText) {
    let container = $("#cardRow");
    if (device.Tag === "DreamScreen" && device["DeviceTag"].includes("DreamScreen")) return;
    // Create main card
    let mainDiv = document.createElement("div");
    if (addDemoText) {
        mainDiv.id = "devCard";        
    }
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
    } else {
        subTitle.textContent = device["IpAddress"];
    }

    if ((device.hasOwnProperty("MultiZoneCount") || device.hasOwnProperty("LedCount")) && device.DeviceTag !== "Lifx Bulb") {
        let val = (device.hasOwnProperty("MultiZoneCount")) ? device["MultiZoneCount"] : device["LedCount"];
        let count = document.createElement("span");
        count.innerText = " (" + val + ")";
        subTitle.appendChild(count);
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
    if (addDemoText) {
        image.id = "demoIcon";
    }
    image.setAttribute("data-bs-toggle", "tooltip");
    image.setAttribute("data-bs-placement", "top");
    image.setAttribute("data-title", "Flash/locate device");
    image.setAttribute("data-device", device["Id"]);
    let tag = device.Tag;
    if (isValid(tag)) {
        if (isValid(device["DeviceTag"]) && (tag === "Dreamscreen" || tag === "Lifx")) tag = device["DeviceTag"];
        image.setAttribute("src", baseUrl + "/img/" + tag.toLowerCase().replace(" ","") + "_icon.png");
    }

    // Settings column
    let settingsCol = document.createElement("div");
    settingsCol.classList.add("col-12", "settingsCol", "pb-2", "text-center", "exp");
    // Create enabled checkbox
    let enableButton = document.createElement("button");
    enableButton.classList.add("btn", "btn-outline-secondary", "btn-clear", "enableBtn", "pt-2");
    enableButton.setAttribute("data-target", device["Id"]);
    enableButton.setAttribute("data-enabled", device["Enable"]);
    if (addDemoText) {
        enableButton.id = "devEnableBtn";
    }
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
    if (addDemoText) {
        settingsButton.id = "devPrefBtn";        
    }
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
    let max = "100";
    if (isValid(device["MaxBrightness"])) max = device["MaxBrightness"].toString;
    brightnessSlide.setAttribute("min", "0");
    brightnessSlide.setAttribute("max", max);
    brightnessSlide.value = device["Brightness"];
    brightnessSlide.classList.add("form-control", "w-100", 'custom-range');

    // Brightness column
    let brightnessCol = document.createElement("div");
    brightnessCol.classList.add("col-12", "pt-1");
    if (addDemoText) brightnessCol.id = "devPrefBrightness";
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
    if (addDemoText) {
        container.prepend(mainDiv);
    } else {
        container.appendChild(mainDiv);
    }
}

function isValid(toCheck) {
    return !(toCheck === null || toCheck === undefined || toCheck === "");
    
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
        let ledProps = ["Offset", "LedCount", "StripMode", "LedMultiplier"];
        if (ledProps.includes(property)) {
            appendLedMap();
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
    sendMessage("LoadData");
}

function RefreshData() {
    if (!refreshing) {
        refreshing = true;
        if (socketLoaded) {
            doGet("./api/DreamData/action?action=refreshDevices");
        } else {
            doGet("./api/DreamData/action?action=refreshDevices", function (newData) {
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
};

const fadeContent = (element, opacity, duration = 300) => {
    return new Promise(res => {
        [...element.children, element].forEach((child) => {
            requestAnimationFrame(() => {
                child.style.transition = `opacity ${duration}ms ease-in`;
                child.style.opacity = opacity;
            });
        });
        setTimeout(res, duration);
    })
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
    settingsDiv.id = "devicePrefs";
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
    let settingsDiv = document.getElementById("devicePrefs");
    settingsDiv.innerHTML = "";
    let linkCol = document.createElement("div");
    let mapCol = document.createElement("div");
    let mapWrap = document.createElement("div");
    linkCol.classList.add("col-12", "row", "justify-content-center");
    mapCol.classList.add("col-12");
    mapWrap.id = "mapWrap";
    linkCol.id = "linkCol";
    mapCol.id = "mapCol";
    mapCol.appendChild(mapWrap);
    settingsDiv.appendChild(linkCol);
    settingsDiv.appendChild(mapCol);
    if (deviceData === undefined) deviceData = JSON.parse(ledData);
    console.log("Loading device data: ", deviceData);   
    let props = deviceData["KeyProperties"];
    if (isValid(props)) {
        let container = document.createElement("div");
        container.classList.add("container");
        let id = deviceData["Id"];
        let row = document.createElement("div");
        row.classList.add("row", "border", "justify-content-center", "pb-5");
        if (demoLoaded) row.classList.add("devPrefs");
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
                    elem = new SettingElement(prop["ValueLabel"], "text", id, propertyName, value, prop["ValueHint"]);
                    elem.isDevice = true;
                    break;
                case "number":
                    elem = new SettingElement(prop["ValueLabel"], "number", id, propertyName, value,prop["ValueHint"],prop["ValueMin"], prop["ValueMax"],prop["ValueStep"]);
                    elem.isDevice = true;
                    break;
                case "check":
                    elem = new SettingElement(prop["ValueLabel"], "check", id, propertyName, value, prop["ValueHint"]);
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
                case "select":
                    elem = new SettingElement(prop["ValueLabel"], "select", id, propertyName, value, prop["ValueHint"]);
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
                if (demoLoaded) se.id = propertyName;

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
        let ds = document.getElementById("devicePrefs");
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
            label.classList.add("custom-control-label");
            group.classList.add("custom-control");
            group.classList.add("custom-switch");
            group.classList.add("form-check-dev");
            element.classList.add("custom-control-input");
            element.type = "checkbox";
            element.id = "customSwitch" + deviceData.Id;
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
            element.classList.add("btn", "btn-clear", "btn-danger", "removeDevice");
            let icon = document.createElement("span");
            icon.classList.add("material-icons");
            icon.textContent = "delete";
            element.appendChild(icon);
            break;
    }
    if (settingElement.type !== "check") {
        label.classList.add("form-label");
        //if (isValid(element)) element.classList.add("form-control");
        group.appendChild(label);
    }
    
    
    if (isValid(element)) {       
        if (settingElement.isDevice) { 
            element.classList.add("devSetting");
            element.classList.add("form-control");
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

    // Append hint if it exists
    if (settingElement.hint !== "") {
        let hint = document.createElement("div");
        hint.classList.add("form-text");
        hint.innerText = settingElement.hint;
        group.append(hint);
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
    if (isValid(deviceData["Token"]) && isValid(deviceData["Layout"]["PositionData"])) {
        drawLinkPane("nanoleaf", true);
        drawNanoShapes(deviceData);
    } else {
        drawLinkPane("nanoleaf", false);
    }
}

function appendHueSettings() {
    if (isValid(deviceData["Token"])) {
        drawLinkPane("hue", true);
        let mapCol = document.getElementById("mapWrap");
        createSectorMap(mapCol);
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

function updateBeamLayout(items) {
    let beamLayout = deviceData["BeamLayout"];
    if (isValid(beamLayout)) {
        console.log("Appending beamLayout: ", beamLayout);
        let existing = beamLayout["Segments"];
        let sorted = [];
        for (let i=0; i < items.length; i++) {
            let pos = parseInt(items[i].getAttribute("data-position"));
            for(let ex = 0; ex < existing.length; ex++) {
                if (existing[ex]["Position"] === pos) {
                    console.log("Adding: ", existing[ex]);
                    sorted.push(existing[ex]);
                }
            }
        }
        beamLayout["Segments"] = [];
        for (let i = 0; i < sorted.length; i++) {
            let seg = sorted[i];
            seg["Position"] = i;
            beamLayout["Segments"].push(seg);
        }        
        updateDevice(deviceData["Id"], "BeamLayout", beamLayout);
    }
}

function appendBeamMap() {
    let settingsDiv = document.getElementById("devicePrefs");
    let beamDiv = document.getElementById("beamDiv");
    if (isValid(beamDiv)) beamDiv.remove();
    
    if (deviceData.hasOwnProperty("BeamLayout")) {
        let beamLayout = deviceData["BeamLayout"];
        if (isValid(beamLayout)) {
            console.log("Appending beamLayout: " , beamLayout);
            let items = beamLayout["Segments"];
            if (items.length > 0) {
                let beamDiv = document.createElement("div");
                beamDiv.id = "BeamDiv";
                beamDiv.classList.add("sortable");
                items.sort((a, b) => (a["Position"] > b["Position"]) ? 1 : -1);
                console.log("ITEMS: ", items);
                for (let i = 0; i < items.length; i++) {
                    let item = items[i];
                    let position = item["Position"];
                    let offset = item["Offset"];
                    let repeat = item["Repeat"];
                    let reverse = item["Reverse"];
                    let count = item["LedCount"];
                    let itemDiv = document.createElement("div");
                    itemDiv.classList.add("beamItem");
                    itemDiv.setAttribute("data-position",position);
                    if (count === 1) {
                        itemDiv.setAttribute("data-type", "corner");
                    } else {
                        itemDiv.setAttribute("data-type", "beam");
                    }                    
                    itemDiv.classList.add("col-12", "row", "justify-content-center", "form-group", "mb-5");
                    
                    // drag handle
                    let dragHandle = document.createElement("span");
                    dragHandle.classList.add("material-icons", "dragHandle");
                    dragHandle.innerText = "drag_handle";
                    itemDiv.appendChild(dragHandle);
                    
                    let nameLabel = document.createElement("div");
                    nameLabel.classList.add("col-12", "headerCol");
                    nameLabel.innerText = (count === 1 ? "Corner" : "Beam") + " " + position;
                    itemDiv.appendChild(nameLabel);
                    
                    let oGroup = document.createElement("div");
                    oGroup.classList.add("form-group","col-12", "col-md-6", "col-lg-3");
                    
                    // Offset
                    let label2 = document.createElement("label");
                    label2.innerText = "Offset";
                    label2.classList.add("form-label");
                    let offsetText = document.createElement("input");
                    offsetText.type = "number";
                    offsetText.value = offset;
                    offsetText.classList.add("form-control", "beam-control");
                    offsetText.setAttribute("data-position",position);
                    offsetText.setAttribute("data-beamProperty","Offset");

                    oGroup.appendChild(offsetText);
                    oGroup.appendChild(label2);
                    itemDiv.appendChild(oGroup);

                    // Repeat
                    let rGroup = document.createElement("div");
                    rGroup.classList.add("form-group","col-12", "col-md-6", "col-lg-3");
                    
                    let checkDiv1 = document.createElement("div");
                    checkDiv1.classList.add("form-check");
                    let label3 = document.createElement("label");
                    label3.innerText = "Repeat";
                    label3.classList.add("custom-control-label");
                    let rCheck = document.createElement("input");
                    rCheck.type = "checkbox";
                    if (repeat) rCheck.checked = true;
                    rCheck.classList.add("form-check", "custom-control-input", "beam-control");
                    rCheck.setAttribute("data-position",position);
                    rCheck.setAttribute("data-beamProperty","Repeat");
                    checkDiv1.appendChild(rCheck);
                    checkDiv1.appendChild(label3);
                    
                    rGroup.appendChild(checkDiv1);
                    itemDiv.appendChild(rGroup);

                    // Reverse
                    let rGroup2 = document.createElement("div");
                    rGroup2.classList.add("form-group","col-12", "col-md-6", "col-lg-3");
                    
                    let checkDiv2 = document.createElement("div");
                    checkDiv2.classList.add("form-check");
                    let label4 = document.createElement("label");
                    label4.innerText = "Reverse Direction";
                    label4.classList.add("custom-control-label");
                    let rCheck2 = document.createElement("input");
                    rCheck2.classList.add("form-check", "custom-control-input", "beam-control");
                    rCheck2.type = "checkbox";
                    if (reverse) rCheck2.checked = true;
                    rCheck2.setAttribute("data-position",position);
                    rCheck2.setAttribute("data-beamProperty","Reverse");
                    checkDiv2.appendChild(rCheck2);
                    checkDiv2.appendChild(label4);

                    rGroup2.appendChild(checkDiv2);
                    itemDiv.appendChild(rGroup2);
                    beamDiv.appendChild(itemDiv);
                }
                settingsDiv.appendChild(beamDiv);

                sortable(".sortable");
                sortable('.sortable')[0].addEventListener('sortupdate', function(e) {
                    let items = e.detail.destination.items;
                    updateBeamLayout(items);
                });
            }
            
        }
    }
}

function appendSectorMap() {
    let mapDiv = document.getElementById("mapWrap");
    createSectorMap(mapDiv);    
}

function appendLedMap() {
    let mapDiv = document.getElementById("mapWrap");
    createLedMap(mapDiv);    
}

function appendBeamLedMap() {
    let mapDiv = document.getElementById("mapCol");
    let noteDiv = document.getElementById("noteDiv");
    if (isValid(noteDiv)) noteDiv.remove();
    noteDiv = document.createElement("div");
    noteDiv.classList.add("col-12", "subtitle", "noteDiv");
    noteDiv.id = "noteDiv";
    noteDiv.innerHTML = "Note: Lifx beams are spaced so that 10 LEDs/beam equal 20 LEDs within Glimmr.";
    
    mapDiv.appendChild(noteDiv);
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
    if (isValid(deviceData)) selected = deviceData["TargetSector"];
    if (!isValid(selected)) selected = -1;
    let wFactor = w / 1920;
    let hFactor = h / 1080;
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
    let imgL = tgt.offsetLeft;
    let imgT = tgt.offsetTop;
    let selected = -1;
    if (isValid(deviceData)) selected = deviceData["TargetSector"];
    if (!isValid(selected)) selected = -1;
    let wFactor = w / 1920;
    let hFactor = h / 1080;
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

    // let s2 = document.createElement("div");
    // s2.classList.add("sector2");
    // s2.style.position = "absolute";
    // s2.style.top = hMargin + fHeight + "px";
    // s2.style.height = (fHeight * 4) + "px";
    // s2.style.left = wMargin + fWidth + "px";
    // s2.style.width = (fWidth * 8) + "px";
    // s2.style.border = "2px solid black";
    // map.appendChild(s2);
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
                    targetDiv.classList.add("checked");
                }
            }
        }
    }
}

function createBeamLedMap() {
    let targetElement = document.getElementById("mapWrap");

    let sd = data.store["SystemData"];
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
    }
    
    let beamLayout = deviceData["BeamLayout"];
    let segments = beamLayout["Segments"];
    let total = sd["LedCount"];
    let rangeList = [];
    for (let s = 0; s < segments.length; s++) {
        let offset = segments[s]["Offset"];
        let len = segments[s]["LedCount"];
        if (segments[s]["Repeat"]) len = 1;
        len *= 2;
        rangeList.push(ranges(total, offset, len));
    }
    console.log("Range list: ", rangeList);
    
    let w = targetElement.offsetWidth;
    let h = (w / 16) * 9;
    let imgL = targetElement.offsetLeft;
    let imgT = targetElement.offsetTop;
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
        for(let r=0; r < rangeList.length; r++) {
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
            s1.setAttribute("title", sd["LedCount"].toString() + "/" + (ledCount).toString());
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
        for(let r=0; r < rangeList.length; r++) {
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
        for(let r=0; r < rangeList.length; r++) {
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
        for(let r=0; r < rangeList.length; r++) {
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
    let range1;
    let sd = data.store["SystemData"];
    let count = 0;
    if (isValid(deviceData)) {
        count = deviceData["LedCount"];
        let offset = deviceData["Offset"];
        let mode = deviceData["StripMode"];
        let total = sd["LedCount"];
        if (isValid(mode) && mode === 2) count /=2;
        if (isValid(deviceData["LedMultiplier"])) {
            let mult = deviceData["LedMultiplier"];
            if (mult === 0) mult = 1;
            if (mult > 0) count /= mult;
            if (mult < 0) count *= Math.abs(mult);
        }
        range1 = ranges(total, offset, count);
    }
    
    let tgt = targetElement;
    let cs = getComputedStyle(tgt);
    let paddingX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
    let borderX = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
    let w = tgt.offsetWidth - paddingX - borderX;
    
    let h = (w / 16) * 9;
    let imgL = tgt.offsetLeft;
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
        if (i === 0) {
            s1.setAttribute("title", sd["LedCount"].toString() + "/" + (ledCount).toString());
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
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
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
            ledCount++;
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
        if (isValid(range1) && range1.includes(ledCount)) s1.classList.add("highLed");
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
            ledCount++;
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
        map.appendChild(s1);
        ledCount++;
    }

    // This one, stupid
    for (let i = 0; i < bottomCount - 1; i++) {
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
        if (i === 0) {
            s1.setAttribute("title", ledCount.toString() + "/" + (ledCount + 1).toString());
            ledCount++;
        } else {
            s1.setAttribute("title", ledCount.toString());
        }
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
    return range.slice(offset, total + offset);
}

function createHueMap() {
    let settingsDiv = document.getElementById("devicePrefs");
    let selectedGroup = deviceData["SelectedGroup"];

    let groups = deviceData["Groups"];
    let group;
    for(let i=0; i < groups.length; i++) {
        let sg = groups[i];
        if (sg["Id"] === selectedGroup.toString()) {
            group = sg;
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
    groupSelect.classList.add("form-control");
    let defaultOption = document.createElement("option");
    defaultOption.textContent = "";
    defaultOption.value = "-1";
    if (selectedGroup === -1) defaultOption.selected = true;
    
    groupSelect.appendChild(defaultOption);
    groupSelect.id = "HueGroup";
    for(let i = 0; i < groups.length; i++) {
        if (groups[i]['type'] !== "Entertainment") continue;
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
    // Get the main light group
    let lightGroup = document.createElement("div");
    lightGroup.classList.add("row");
    lightGroup.classList.add("justify-content-center");
    lightGroup.classList.add("col-12");
    if (!group.hasOwnProperty('lights')) return false;
    const ids = group["lights"];

    // Sort our lights by name
    lights = lights.sort(function (a, b) {
        if (!a.hasOwnProperty('Name') || !b.hasOwnProperty('Name')) return false;
        return a.Name.localeCompare(b.Name);
    });
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
                let sectorCount = data.store["SystemData"]["SectorCount"];
                for (let i = 1; i < sectorCount; i++) {
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
                checkLabel.className += "custom-control-label";
                checkLabel.setAttribute('for', 'overrideBrightness' + id);

                // Create a checkbox
                const newCheck = document.createElement("input");
                newCheck.className += "overrideBright custom-control-input lightProperty";
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
                lightDiv.className += "delSel col-12 col-md-6 justify-content-center form-group";
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
    if (height > 800) height = 800;
    let rotation = panel['Rotation'];
    if (!isValid(rotation)) rotation = 0;
    // Get layout data from panel
    let mirrorX = panel['MirrorX'];
    let mirrorY = panel['MirrorY'];
    let layout = panel['Layout'];
    if (!isValid(layout)) return;
    let sideLength = layout['SideLength'];

   

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
    if (wX + 50 >= width) {
        scaleXY = .5;
        maxX *= scaleXY;
        maxY *= scaleXY;
        minX *= scaleXY;
        minY *= scaleXY;
    }
    height = wY + 200;

    // Create our stage
    let stage = new Konva.Stage({
        container: 'mapCol',
        width: width,
        height: height
    });

    // Shape layer
    let cLayer = new Konva.Layer();
    stage.add(cLayer);
    
    let x0 = (width - maxX - minX) / 2;
    let y0 = (height - maxY - minY) / 2;
    
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
    
    for (let i=0; i < positions.length; i++) {
        let shapeDrawing;
        let data = positions[i];
        let shape = data['ShapeType'];
        sideLength = data["SideLength"];
        let x = data.X;
        let y = data.Y;
        if (mirrorX) x *= -1;
        if (!mirrorY) y *= -1;
        if (shape === 12) continue;
        
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
        console.log("Shape type is " + shape);
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
                    id: data["PanelId"]
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

                let angle = (2*Math.PI)/3;
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
                    id: data["PanelId"]
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
                setNanoMap(data['PanelId'], data['TargetSector']);
            });
            shapeDrawing.on('tap', function () {
                setNanoMap(data['PanelId'], data['TargetSector']);
            });
            console.log("Adding shape: ", shapeDrawing);

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
        updateDevice(deviceData["Id"], "Rotation", shapeGroup.rotation());
    });
    cLayer.add(tr);
    tr.nodes([shapeGroup]);
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
    stage.draw();
    
    cLayer.draw();
    cLayer.zIndex(0);    
}

function setNanoMap(id, current) {
    nanoTarget = id;
    nanoSector = current;
    let myModal = new bootstrap.Modal(document.getElementById('nanoModal'));
    let wrap = document.getElementById("nanoPreviewWrap");
    myModal.show();
    createSectorMap(wrap, "nano");

    let nanoRegion = document.querySelectorAll(".nanoRegion");
    for (let i=0; i < nanoRegion.length; i++) {
        let obj = nanoRegion[i];
        obj.classList.remove("checked");
    }

    if (current !== -1) {
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

