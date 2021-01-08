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
// Is our settings window currently open?
let settingsShown = false;
// This is the data for the currently shown device in settings
let deviceData;
let cardClone;
let baseCard;
let closeButton;
let toggleWidth = 0;
let toggleHeight = 0;
let toggleLeft = 0;
let toggleTop = 0;
let posting = false;
let baseUrl;

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
    settingsRow.style.position = 'fixed';
    settingsRow.style.top = "0";
    settingsRow.style.left = window.innerWidth.toString() + "px";
    settingsRow.style.width = 0 + 'px';
    settingsRow.style.height = 0 + 'px';

    settingsTab = document.getElementById("settingsTab");
    settingsTitle = document.getElementById("settingsTitle");
    settingsContent = document.getElementById("settingsContent");
    cardRow = document.getElementById("cardRow");
    setSocketListeners();
    loadSocket();
    loadUi();
    setListeners();
    sizeContent();
});


// Send a message to the server, websocket or not
function sendMessage(endpoint, sData, encode=true) {
    if (encode && sData !== null && sData !== undefined) sData = JSON.stringify(sData);
    // Set a .5s timeout so that responses from sent messages aren't loaded
    loadTimeout = setTimeout(function(){
        loadTimeout = null;
    },500);
    if (socketLoaded) {
        if (sData !== null && sData !== undefined) {
            websocket.invoke(endpoint, sData).catch(function (err) {
                return console.error("Fuck: ", err);
            });
        } else {
            websocket.invoke(endpoint).catch(function (err) {
                return console.error("Fuck: ", err);
            });
        }
    } else {
        postData(endpoint, data);
    }
}

// Fallback sending method if socket is disabled
function postData(endpoint, payload) {
    if (posting) {
        console.log("Already posting?");
        return;
    }
    $.ajax({
        url: "./api/DreamData/" + endpoint,
        dataType: "json",
        contentType: "application/json;",
        data: JSON.stringify(payload),
        success: function(data) {
            console.log(`Posting to ${endpoint}`, endpoint, data);
            postResult = data;
        },
        type: 'POST'
    });
}

function doPost(endpoint, payload) {
    if (posting) {
        console.log("Already posting?");
        return;
    }
    let xhttp = new XMLHttpRequest();
    console.log(`Posting to ${endpoint}`, endpoint, data);

    xhttp.onreadystatechange = function() {
            if (this.readyState === 4 && this.status === 200) {
                postResult = this.json;
            }
    };
    xhttp.open("POST", endpoint, true);
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
        loadData();
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
        loadData();
    }).catch(function (err) {
        console.error("Socket connection error: ", err.toString());
        loadData();
    });
}

// Set all of the various listeners our page may use
function setListeners() {
    window.addEventListener('resize', sizeContent);
    
    document.addEventListener('click',function(e){
        let target = e.target;
        if (target) {
            if (target === closeButton || target.parentElement === closeButton) {
                console.log("Close button");
                closeCard();
                return;
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
        await toggleExpansion(settingsRow, {top: "3rem", left: "0px", width: '100%', height: '100%', padding: "1rem 3rem"});
    } else {
        settingsIcon.textContent = "settings_applications";
        await toggleExpansion(settingsRow, {top: "0", left: window.innerWidth.toString() + "px", width: '0%', height: '0%', padding: "1rem 3rem"});
    }
    settingsShown = !settingsShown;
    settingsTitle.textContent = "Main Settings";
}


function setMode(newMode) {
    console.log("Changing mode to ", newMode);
    //data.store["DeviceMode"][0]["value"] = newMode;
    mode = newMode;
    let target;
    switch(mode) {
        case 0:
            target = document.querySelector("[data-mode='0']");
            break;
        case 1:
            target = document.querySelector("[data-mode='1']");
            break;
        case 2:
            target = document.querySelector("[data-mode='2']");
            break;
        case 3:
            target = document.querySelector("[data-mode='3']");
            break;
        default:
            console.log("I don't know what this is: ", mode);
            break;
    }

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
    if (autoDisabled) mode = 0;
    setMode(mode);
    getDevices();
    document.getElementById("cardRow").click();
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
                enableButton.classList.add("btn-dark");
                enableButton.classList.remove("btn-outline-secondary");
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
    if (socketLoaded) {
        loadCalled = true;
        doGet(baseUrl + "/api/DreamData/action?action=loadData");
    } else {
        doGet(baseUrl + "/api/DreamData/action?action=loadData", function (newData) {
            console.log("Loading dream data from /GET: ", newData);
            data.store = newData;
        });
    }
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
    if (data.store.hasOwnProperty(name)) {
        return data.store[name][0]["value"];
    } else {
        console.log("Prop not found: ", name);
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
            if (expanded) {
                bbGroup.classList.add("float-right");
            } else {
                bbGroup.classList.remove("float-right");
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
        let createSectors = false;
        let createLed = false;
        let createHue = false;
        let createNano = false;
        
        switch(deviceData["Tag"]) {
            case "Dreamscreen":      
                createSectors = (deviceData["Tag"] === "Connect" || deviceData["Tag"] === "Sidekick");
                break;
            case "HueBridge":
                if (deviceData["Key"] !== null && deviceData["User"] !== null) {
                    createHue = true;
                    createSectors = true;    
                } else {
                    
                }
                
                break;
            case "Lifx":
            case "Yeelight":
            case "Wled":
                appendImageMap();
                break;
            case "Nanoleaf":
                if (deviceData["Token"] !== null) {
                    createSectors = true;
                    createNano = true;    
                }
                
                break;
            default:
                console.log("Unknown device tag.");
                return;
        }
        
        if (createSectors) {
            
        }
        
        
    }   
}

function appendImageMap() {
    let sepDiv = document.createElement("div");
    sepDiv.classList.add("dropdown-divider");
    let settingsDiv = document.createElement("div");
    settingsDiv.classList.add("deviceSettings", "row", "text-center");
    let imgDiv = document.createElement("div");
    imgDiv.id = "mapDiv";
    let img = document.createElement("img");
    img.id = "sectorImage";
    img.classList.add("img-fluid", "col-xl-8", "col-lg-8", "col-md-12");
    img.src = baseUrl + "/img/sectoring_screen.png";
    imgDiv.appendChild(img);
    settingsDiv.append(imgDiv);
    settingsDiv.style.opacity = "0%";
    settingsDiv.style.overflow = "scrollY";
    settingsDiv.style.position = "relative";
    cardClone.appendChild(sepDiv);
    cardClone.appendChild(settingsDiv);
    setTimeout(function() {createSectorMap(imgDiv)}, 200);
    fadeContent(settingsDiv,100, 500);
}

function createSectorMap(targetElement) {
    let img = document.getElementById("sectorImage");
    let w = img.offsetWidth;
    let h = img.offsetHeight;
    let imgL = img.offsetLeft;
    let imgT = img.offsetTop;
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
        secMap.remove();
        createSectorMap(imgDiv);
    }
}

async function closeCard() {
    cardClone.style.overflowY = "none";
    deviceData = undefined;
    expanded = false;
    // shrink the card back to the original position and size    
    await toggleExpansion(cardClone, {top: `${toggleTop}px`, left: `${toggleLeft}px`, width: `${toggleWidth}px`, height: `${toggleHeight}px`, padding: '1rem 1rem'}, 300);
    // show the original card again
    document.querySelector(".mainSettings").classList.remove('d-none');
    document.getElementById("closeBtn").classList.add('d-none');

    baseCard.style.removeProperty('opacity');
    cardClone.remove();
}

