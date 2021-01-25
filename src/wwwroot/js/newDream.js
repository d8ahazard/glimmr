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
let baseUrl;
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
    console.log("Devices have been updated: ", val);
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
    setSocketListeners();
    loadSocket();
    
});


// Send a message to the server, websocket or not
function sendMessage(endpoint, sData, encode=true) {
    if (encode && isValid(sData)) sData = JSON.stringify(sData);
    // Set a .5s timeout so that responses from sent messages aren't loaded
    loadTimeout = setTimeout(function(){
        loadTimeout = null;
    },500);
    console.log("Sending message: " + endpoint);
    if (socketLoaded) {
        if (isValid(sData)) {
            websocket.invoke(endpoint, sData).catch(function (err) {
                return console.error("Fuck: ", err);
            });
        } else {
            websocket.invoke(endpoint).catch(function (err) {
                return console.error("Fuck: ", err);
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
                console.log("OLO: ", parsed);
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
        inputElement.src = './img/_preview_input.jpg?rand=' + Math.random();
        croppedElement.src = './img/_preview_output.jpg?rand=' + Math.random(); 
    });

    websocket.on("hueAuth", function (value) {
        console.log("Hue Auth message: " + value);
        
        switch (value) {
            case "start":
                break;
            case "stop":
            case "authorized":
                break;
            default:
                break;
        }
    });

    websocket.on("authorizeNano", function (value) {
        console.log("Nano Auth message: " + value);
        switch (value) {
            case "start":
                break;
            case "stop":
                break;
            case "authorized":
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

function showSocketError() {
    errModal.show();
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
        
        if (property === "PreviewMode") {
            val = parseInt(val);
        }
        let pack;
        if (isValid(obj) && isValid(property) && isValid(val)) {
            console.log("Trying to set: ", obj, property, val);
            if (isValid(id)) {
                let strips = data.store[obj];
                for(let i=0; i < strips.length; i++) {
                    let strip = strips[i];
                    if (strip["_id"] === id) {
                        strip[property] = val;
                        strip["Id"] = id;
                        strips[i] = strip;
                        pack = strip;
                    }
                }
                data.store[obj] = strips;
            } else {
                data.store[obj][0][property] = val;
                pack = data.store[obj][0];
            }
            
            
            if (property === "LeftCount" || property === "RightCount" || property ==="TopCount" || property === "BottomCount") {
                let lPreview = document.getElementById("sLedPreview");
                let lImage = document.getElementById("ledImage");
                createLedMap(lPreview, lImage, pack);
            }
            if (property === "Theme") {
                loadTheme(val);
            }
            if (property === "AudioMap") {
                let mapImg = document.getElementById("audioMapImg");
                mapImg.setAttribute("src","./img/MusicMode" + val + ".png");
            }
            delete pack["_id"];
            console.log("Sending updated object: ", pack);
            sendMessage(obj, pack,true);
        }        
    });
    
    document.addEventListener('click',function(e){
        let target = e.target;
        if (target) {
            if (target.classList.contains("controlBtn")) {
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
            }
            if (target === closeButton || target.parentElement === closeButton) {
                console.log("Close button");
                closeCard();
                return;
            }
            
            if (target.classList.contains("sector") || target.parentElement.classList.contains("sector")) {
                if (target.parentElement.classList.contains("sector")) {
                    target = target.parentElement;
                }
                let sector = target.getAttribute("data-sector");
                updateDeviceSector(sector, target);
            }

            if (target.classList.contains("linkDiv") || target.parentElement.classList.contains("linkDiv")) {
                if (target.parentElement.classList.contains("linkDiv")) {
                    target = target.parentElement;
                }
                let linked = target.getAttribute("data-linked");
                if (linked === "false") {
                    let devId = deviceData["_id"];
                    console.log("We should try linking device: " + devId);
                    let type = target.getAttribute("data-type");    
                    if (type === "NanoLeaf") {
                        sendMessage("AuthorizeNano",devId,false);
                    } else {
                        sendMessage("AuthorizeHue",devId,false);
                    }
                } 
                
                
                
            }

            if (target.classList.contains("led") || target.parentElement.classList.contains("led")) {
                if (target.parentElement.classList.contains("led")) {
                    target = target.parentElement;
                }
                let sector = target.getAttribute("data-sector");
                console.log("Flashing LED " + sector);
                sendMessage("flashLed", parseInt(sector), false);
            }
            
            if (target.classList.contains("deviceIcon")) {
                let targetId = target.getAttribute("data-device");
                console.log("Device icon clicked: " + targetId);
                sendMessage("flashDevice", targetId, false);
            }
            
            if (target.classList.contains("devSetting")) {
                let targetId = target.getAttribute("data-target");
                let attribute = target.getAttribute("data-attribute");                
                console.log("Dev setting clicked, we are setting ", attribute, targetId, target.checked);
                updateDevice(targetId, attribute, target.checked);
                return;
            }
            
            if (target.classList.contains("settingBtn")  || target.parentElement.classList.contains("settingBtn")) {
                if (target.parentElement.classList.contains("settingBtn")) target = target.parentElement;
                if (expanded) {
                    closeCard();
                    return;
                } else {
                    let devId = target.getAttribute("data-target");
                    for (let i=0; i < data.devices.length; i++) {
                        if (data.devices[i]) {
                            if (data.devices[i]["_id"] === devId) {
                                deviceData = data.devices[i];
                                console.log("Setting device data: ", deviceData);
                                break;
                            }
                        }
                    }
                    onCardClick(target);
                    return;
                }               
            }

            if (target.classList.contains("enableBtn")  || target.parentElement.classList.contains("enableBtn")) {
                if (target.parentElement.classList.contains("enableBtn")) target = target.parentElement;
                let devId = target.getAttribute("data-target");
                let devEnabled = target.getAttribute("data-enabled");
                let icon = target.firstChild;
                if (devEnabled === "true") {
                    target.setAttribute("data-enabled","false");
                    icon.innerText = "cast";
                } else {
                    target.setAttribute("data-enabled","true");
                    icon.innerText = "cast-connected";
                }
                //data.devices[devId]["Enable"] = (devEnabled !== "true");
                updateDevice(devId,"Enable",(devEnabled !== "true"));
            }

            

            if (target.classList.contains("refreshBtn") || target.parentElement.classList.contains("refreshBtn")) {
                if (target.parentElement.classList.contains("refreshBtn")) target = target.parentElement;
                console.log("Refresh clicked!");
                sendMessage("ScanDevices");
                return;
            }

            if (target.classList.contains("modeBtn") || target.parentElement.classList.contains("modeBtn")) {
                if (target.parentElement.classList.contains("modeBtn")) target = target.parentElement;
                let newMode = parseInt(target.getAttribute("data-mode"));
                setMode(newMode);
                sendMessage("Mode", newMode, false);
                return;
            }

            if (target.classList.contains("ledCtl") || target.parentElement.classList.contains("ledCtl")) {
                console.log("LED CTRL CLICK");
                if (target.parentElement.classList.contains("ledCtl")) target = target.parentElement;
                let action = target.getAttribute("data-function");
                let id = target.getAttribute("data-id");                
                let ledData = data.store["LedData"];
                if (isValid(ledData)) {
                    let led = getObj(ledData, "_id", id); 
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
                        data.store["LedData"] = setObj(ledData, "_id", id, led);
                        led["Id"] = led["_id"];
                        sendMessage("LedData", led, true);
                    } else {
                        console.log("Invalid led")
                    }                    
                } else {
                    console.log("Invalid led data");
                }
                
                return;
            }

            if (target.classList.contains("mainSettings") || target.parentElement.classList.contains("mainSettings")) {
                if (target.parentElement.classList.contains("refreshBtn")) target = target.parentElement;
                console.log("Refresh clicked!");                
                toggleSettingsDiv(0);
                return;
            }
            
            if (target.classList.contains("nav-link") || target.parentElement.classList.contains("nav-item")) {
                if (target.classList.contains("nav-item")) target = target.firstChild;
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
            }
        }
        
    });
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
        updateDevice(dev["_id"],"Layout", layout);
    }
    
    sendMessage("flashSector", parseInt(sector), false);
}


function setMode(newMode) {    
    console.log("Changing mode to ", newMode);
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
}

function loadUi() {
    console.log("Loading ui.");
    let mode = getStoreProperty("DeviceMode"); 
    let autoDisabled = getStoreProperty("AutoDisabled");
    if (isValid(data.store["SystemData"])) {
        let theme = data.store["SystemData"][0]["Theme"];
        loadTheme(theme);
    }
    
    if (isValid(data.store["Dev_Audio"])) {
        let recList = document.getElementById("RecDev");
        for (let i = 0; i < recList.options.length; i++) {
            recList.options[i] = null;
        }
        let recDevs = data.store["Dev_Audio"];
        console.log("Rec Devs: ", recDevs);
        let recDev = getStoreProperty("RecDev");
        if (isValid(recDevs)) {
            for (let i = 0; i < recDevs.length; i++) {
                console.log("Adding dev");
                let dev = recDevs[i];
                let opt = document.createElement("option");
                opt.value = dev["_id"];
                opt.innerText = dev["_id"];
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
    if (data.store == null) return;
    let ledData = data.store["LedData"];
    let systemData = data.store["SystemData"][0];
    if (isValid(ledData)) {
        for(let i=0; i < 4; i++) {
            loadSettingObject(ledData[i]);
        }    
    }
    
    if (isValid(systemData)) {
        loadSettingObject(systemData);
        console.log("Loading System Data: ", systemData);
        let lPreview = document.getElementById("sLedPreview");
        let lImage = document.getElementById("ledImage");
        createLedMap(lPreview, lImage, systemData);
    } else {
        console.log("NO LED DATA");
    }    
}

function loadSettingObject(obj) {
    if (obj == null) {
        console.log("Object is null?");
        return;
    }
    let dataProp = obj;
    let id = obj["_id"];
    let name = "";
    if (obj.hasOwnProperty("GpioNumber")) {
        name = "LedData";
    } else {
        name = "SystemData";
    }
    console.log("Loading object: ",name, dataProp, id);
    for(let prop in dataProp) {
        if (dataProp.hasOwnProperty(prop)) {
            let target = document.querySelector('[data-property='+prop+'][data-object="'+name+'"]');
            if (obj.hasOwnProperty("GpioNumber")) {
                target = document.querySelector('[data-property='+prop+'][data-object="'+name+'"][data-id="'+id+'"]');
                console.log("Target: ", target);
            }

            if (prop === "Enable") {
                let value = dataProp[prop];
                console.log("Enableprop: ", value, id);
                if (value) {
                    target = document.querySelector('[data-id="'+id+'"][data-function="enable"]');
                    target.classList.add("active");
                } else {
                    target = document.querySelector('[data-id="'+id+'"][data-function="disable"]');
                    target.classList.add("active");
                }
            }
            
            if (isValid(target)) {
                let value = dataProp[prop];
                if (value === true) {
                    target.setAttribute('checked',"true");
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
            // Create main card
            let mainDiv = document.createElement("div");
            mainDiv.classList.add("card", "m-4", "devCard");
            // Create card body
            let bodyDiv = document.createElement("div");
            bodyDiv.classList.add("card-body", "row");            
            // Create title/subtitle headers
            let title = document.createElement("h5");
            let subTitle = document.createElement("h6");
            title.classList.add("card-title");
            subTitle.classList.add("card-subtitle", "mb2", "text-muted");
            title.textContent = device.Name;
            subTitle.textContent = device["IpAddress"];
            // Create icon
            let titleRow = document.createElement("div");
            titleRow.classList.add("mb-3", "col-12", "titleRow");
            let titleCol = document.createElement("div");
            titleCol.classList.add("col-8", "titleCol", "exp");            
            let iconCol = document.createElement("div");
            iconCol.classList.add("iconCol", "exp", "col-4");
            let image = document.createElement("img");
            image.classList.add("deviceIcon", "img-fluid");
            image.setAttribute("data-device", device["_id"]);
            let tag = device.Tag;
            if (tag === "Dreamscreen") tag = device["DeviceTag"];
            image.setAttribute("src", baseUrl + "/img/" + tag.toLowerCase() + "_icon.png");
            
            // Settings column
            let settingsCol = document.createElement("div");
            settingsCol.classList.add("col-12", "settingsCol", "pb-2", "text-center", "exp");
            // Create enabled checkbox
            let enableButton = document.createElement("button");
            enableButton.classList.add("btn", "btn-outline-secondary", "enableBtn", "pt-2");
            enableButton.setAttribute("data-target", device["_id"]);
            enableButton.setAttribute("data-enabled", device["Enable"]);
            // And the icon
            let eIcon = document.createElement("span");
            eIcon.classList.add("material-icons", "pt-1");
            if (device["Enable"]) {
                eIcon.textContent = "cast_connected";                
            } else {
                eIcon.textContent = "cast";
            }
            enableButton.appendChild(eIcon);
             
            let enableCol = document.createElement("div");
            enableCol.classList.add("btn-group", "settingsGroup");
            enableCol.appendChild(enableButton);
            
            let settingsButton = document.createElement("button");
            settingsButton.classList.add("btn", "btn-outline-secondary", "settingBtn", "pt-2");
            settingsButton.setAttribute("data-target",device["_id"]);
            let sIcon = document.createElement("span");
            sIcon.classList.add("material-icons", "pt-1");
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
            brightnessSlide.setAttribute("data-target",device["_id"]);
            brightnessSlide.setAttribute("data-attribute","Brightness");
            brightnessSlide.value = device["Brightness"];
            brightnessSlide.classList.add("form-input", "w-100", 'custom-range');
            
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
    let d = [];
    for (const [key, value] of Object.entries(data.store)) {
        if (key.includes("Dev_") && key !== "Dev_Audio") {
            for (let i = 0; i < value.length; i++) {
                if (value.hasOwnProperty(i)) {
                    d.push(value[i]);
                }
            }
        }
    }
    d.sort((a, b) => (a.Name > b.Name) ? 1 : -1);
    console.log("Devices: ", d);
    data.devices = d;
}

function updateDevice(id, property, value) {
    for(let i = 0; i< data.devices.length; i++) {
        let device = data.devices[i];
        if (device["_id"] === id) {
            if (device.hasOwnProperty(property)) {
                console.log("Device property " + property + "exists, setting to " + value);
                device[property] = value;
                sendMessage("updateDevice", device);
                data.devices[i] = device;
            } else {
                console.log("Device has no property for " + property);
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
        if (name === "SystemData") return sysData[0];
        if (sysData[0].hasOwnProperty(name)) {
            return sysData[0][name];
        }
    }

    if (data.store.hasOwnProperty(name)) {
        return data.store[name][0]["value"];
    }
    
    return null;
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
            if (bbGroup != undefined) {
                if (expanded) {
                    bbGroup.classList.add("float-right");
                } else {
                    bbGroup.classList.remove("float-right");
                }
            }
            element.querySelector(".card-body").querySelectorAll(".exp").forEach(function(row){
            console.log("Adding to row? ", row);
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

const onCardClick = async (e) => {
    expanded = true;
    const card = (e.parentElement.parentElement.parentElement.parentElement);
    baseCard = card;
    // clone the card
    let targetId = e.getAttribute("data-target");
    console.log("Setting button clicked, we are opening ", targetId);
    //toggleSettingsDiv(targetId);
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
    console.log("Top offset is " + oh);
    // remove the display style so the original content is displayed right
    cardClone.style.display = 'block';
    // Expand that bish
    await toggleExpansion(cardClone, {top: oh + "px", left: 0, width: '100%', height: 'calc(100% - ' + oh + 'px)', padding: "1rem 3rem"}, 250);
    addCardSettings();

    cardClone.style.overflowY = "scroll";

    // Create settings for our card
    document.querySelector(".mainSettings").classList.add('d-none');
    closeButton.classList.remove('d-none');
};

function addCardSettings() {
    if (deviceData === undefined) {
        console.log("NO DEVICE DATA");
    } else {
        console.log("Appending card settings.");
        let sepDiv = document.createElement("div");
        sepDiv.classList.add("dropdown-divider");
        
        let settingsDiv = document.createElement("div");
        settingsDiv.classList.add("deviceSettings", "row", "text-center");
        settingsDiv.id = "deviceSettings";
        //settingsDiv.style.opacity = "0%";
        settingsDiv.style.overflow = "scrollY";
        settingsDiv.style.position = "relative";
        let linkCol = document.createElement("div");
        let mapCol = document.createElement("div");
        linkCol.classList.add("col-12", "row", "justify-content-center");
        mapCol.classList.add("col-12");
        linkCol.id = "linkCol";
        mapCol.id = "mapCol";
        settingsDiv.appendChild(linkCol);
        settingsDiv.appendChild(mapCol);

        cardClone.appendChild(sepDiv);
        cardClone.appendChild(settingsDiv);
        
        //fadeContent(settingsDiv,100, 500);
        switch(deviceData["Tag"]) {
            case "Dreamscreen":      
                drawSectorMap = (deviceData["Tag"] === "Connect" || deviceData["Tag"] === "Sidekick");
                break;
            case "HueBridge":
                if (isValid(deviceData["Key"]) && isValid(deviceData["User"])) {
                    createHueMap();
                    drawSectorMap = true;    
                } 
                break;
            case "Lifx":
            case "Yeelight":
                appendImageMap();
                break;
            case "Wled":
                appendWledSettings();
                break;
            case "Nanoleaf":
                appendNanoSettings();
                break;
            default:
                console.log("Unknown device tag.");
                return;
        }
        
        if (drawSectorMap) {
            
        }
        
        
    }   
}

function appendWledSettings() {
    appendImageMap();
    
}

function appendNanoSettings() {
    if (isValid(deviceData["Token"])) {
        drawLinkPane("nanoleaf", true);
        drawNanoShapes(deviceData);
    } else {
        drawLinkPane("nanoleaf", false);
    }
}

function drawLinkPane(type, linked) {
    let div = document.createElement("div");
    div.classList.add("col-8", "col-sm-6", "col-md-4", "col-lg-3", "col-xl-2", "linkDiv");
    div.setAttribute("data-type",type);
    div.setAttribute("data-id", deviceData["_id"]);
    div.setAttribute("data-linked",linked);
    let img = document.createElement("img");
    img.classList.add("img-fluid");
    img.src = "./img/" + type + "_icon.png";
    let linkImg = document.createElement("img");
    linkImg.classList.add("linkImg");
    linkImg.classList.add(linked ? "linked" : "unlinked");
    div.appendChild(img);
    div.appendChild(linkImg);
    console.log("No, really, appending: ", div);
    document.getElementById("linkCol").appendChild(div);
}

function appendImageMap() {
    let imgDiv = document.createElement("div");
    imgDiv.id = "mapDiv";
    let img = document.createElement("img");
    img.id = "sectorImage";
    img.classList.add("img-fluid", "col-xl-8", "col-lg-8", "col-md-12");
    img.src = baseUrl + "/img/sectoring_screen.png";
    imgDiv.appendChild(img);
    let settingsDiv = document.getElementById("mapCol");    
    settingsDiv.append(imgDiv);
    setTimeout(function() {createSectorMap(imgDiv, document.getElementById("sectorImage"))}, 200);

}

function createSectorMap(targetElement, sectorImage) {
    let img = sectorImage;
    let w = img.offsetWidth;
    let h = img.offsetHeight;
    let imgL = img.offsetLeft;
    let imgT = img.offsetTop;
    let exMap = targetElement.querySelector("#sectorMap");
    if (isValid(exMap)) exMap.remove();
    let wFactor = w / 1920;
    let hFactor = h / 1100;
    let wMargin = 62 * wFactor;
    let hMargin = 52 * hFactor;
    let fHeight = (h - hMargin - hMargin) / 6;
    let fWidth = (w - wMargin - wMargin) / 10;
    let map = document.createElement("div");
    map.id = "sectorMap";
    map.classList.add("sectorMap");
    map.style.top = imgT + "px";
    map.style.left = imgL + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    console.log("W and h are ", w, h);
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    for (let i = 0; i < 6; i++) {
        t = h - hMargin - ((i + 1) * fHeight);
        b = t + fHeight;
        l = w - wMargin - fWidth;
        r = l + fWidth;
        let sector = i + 1;
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
    }
    
    for (let i = 0; i < 9; i++) {
        l = w - wMargin - (fWidth * (i + 1));
        r = l - fWidth;        
        let sector = i + 6;
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
    }

    // Left, top-bottom
    for (let i = 0; i < 5; i++) {
        t = hMargin + (i * fHeight);
        b = t + fHeight;
        l = wMargin;
        r = l + fWidth;
        let sector = i + 6 + 9;
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
    }

    // This one, stupid
    for (let i = 0; i < 9; i++) {
        t = h - hMargin - fHeight;
        b = t + fHeight;
        l = wMargin + (fWidth * (i));
        r = l + fWidth;
        let sector = i + 6 + 9 + 5;
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
}

function createLedMap(targetElement, sectorImage, ledData) {
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
    let flHeight = (h - hMargin - hMargin) / ledData["LeftCount"];
    let frHeight = (h - hMargin - hMargin) / ledData["RightCount"];
    let ftWidth = (w - wMargin - wMargin) / ledData["TopCount"];
    let fbWidth = (w - wMargin - wMargin) / ledData["BottomCount"];
    let dHeight = (flHeight + frHeight) / 2;
    let dWidth = (ftWidth + fbWidth) / 2;
    let map = document.createElement("div");
    map.id = "ledMap";
    map.classList.add("ledMap");
    map.style.top = imgT + "px";
    map.style.left = imgL + "px";
    map.style.width = w + "px";
    map.style.height = h + "px";
    console.log("W and h are ", w, h);
    // Bottom-right, up to top-right
    let t = 0;
    let b = 0;
    let l = 0;
    let r = 0;
    let ledCount = 0;
    for (let i = 0; i < ledData["RightCount"]; i++) {
        t = h - hMargin - ((i + 1) * frHeight);
        b = t + frHeight;
        l = w - wMargin - dWidth;
        r = l + dWidth;        
        let s1 = document.createElement("div");
        s1.classList.add("led");
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

    for (let i = 0; i < ledData["TopCount"] - 1; i++) {
        l = w - wMargin - (ftWidth * (i + 1));
        r = l - ftWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
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
    for (let i = 0; i < ledData["LeftCount"] - 1; i++) {
        t = hMargin + (i * flHeight);
        b = t + flHeight;
        l = wMargin;
        r = l + dWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
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
    for (let i = 0; i < ledData["BottomCount"]; i++) {
        t = h - hMargin - dHeight;
        b = t + dHeight;
        l = wMargin + (fbWidth * (i));
        r = l + fbWidth;
        let s1 = document.createElement("div");
        s1.classList.add("led");
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

function createHueMap() {
    let lights = deviceData["Lights"];
    let lightMap =deviceData["MappedLights"];
    let groups = deviceData["Groups"];
    let selectedGroup = deviceData["SelectedGroup"];
    // Main container
    let hueMapRow = document.createElement("div");
    hueMapRow.classList.add("row");
    // Group select row
    let groupSelectCol = document.createElement("div");
    groupSelectCol.classList.add("col-12");
    // Group select
    let groupSelect = document.createElement("select");
    let defaultOption = document.createElement("option");
    defaultOption.textContent = "";
    defaultOption.value = "-1";
    if (selectedGroup === -1) defaultOption.selected = true;
    groupSelect.appendChild(defaultOption);
    for(let i = 0; i < groups.length; i++) {
        let opt = document.createElement("option");
        opt.value = groups[i]["_id"];
        opt.textContent = groups[i]["_id"];
        if (selectedGroup === groups[i]["_id"]) opt.selected = true;
        groupSelect.appendChild(opt);
    }
    groupSelectCol.appendChild(groupSelect);
    cardClone.appendChild(groupSelectCol);
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
    //x0 /= scaleXY;
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

    cLayer.add(shapeGroup);
    cLayer.draw();

    
    for (let i=0; i < positions.length; i++) {
        let data = positions[i];
        let shape = data['ShapeType'];
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
                    id: data["panelId"]
                });
                poly.on('click', function(){
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
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
                rect1.on('tap', function(){
                    setNanoMap(data['PanelId'], data['TargetSector']);
                });
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
    let nanoRegion = document.querySelectorAll(".nanoRegion");
    for (let i=0; i < nanoRegion.length; i++) {
        let obj = nanoRegion[i];
        obj.classList.remove("checked");
    }
    
    if (current !== -1) {
        document.querySelector('.nanoRegion[data-region="'+current+'"]').classList.add("checked");
    }

    let myModal = new bootstrap.Modal(document.getElementById('nanoModal'));
    let wrap = document.getElementById("nanoPreviewWrap");
    let img = document.getElementById("nanoPreview");    
    myModal.show();
    createSectorMap(wrap, img);
}


function sizeContent() {
    let navDiv = document.getElementById("mainNav");
    let footDiv = document.getElementById("footer");
    let cDiv = document.getElementById("mainContent");
    let wHeight = window.innerHeight;
    let wWidth = window.innerWidth;
    cDiv.style.position = "fixed";
    cDiv.style.top = navDiv.offsetHeight + "px";
    cDiv.style.height = wHeight - navDiv.offsetHeight - footDiv.offsetHeight + "px";
    cDiv.style.width = wWidth + "px";
    console.log("Setting: ", navDiv.offsetHeight, footDiv.offsetHeight);
    if (expanded) {
        let oh = document.getElementById("mainContent").offsetTop;
        toggleExpansion(cardClone, {top: oh + "px", left: 0, width: '100%', height: 'calc(100% - ' + oh + 'px)', padding: "1rem 3rem"}, 250);
        let imgDiv = document.getElementById("mapDiv");
        let secMap = document.getElementById("sectorMap");
        if (isValid(secMap)) secMap.remove();
        if (drawSectorMap) createSectorMap(imgDiv, document.getElementById("sectorImage"));
        if (deviceData["Tag"] === "Nanoleaf" && isValid(deviceData["Token"])) {
            console.log("Redrawing nanoshapes.");
            document.querySelector(".konvajs-content").remove();
            drawNanoShapes(deviceData);
        }
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

