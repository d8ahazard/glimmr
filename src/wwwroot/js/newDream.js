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
// Is our settings window currently open?
let settingsShown = false;
// This is the data for the currently shown device in settings
let deviceData;

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
    settingsRow = document.getElementById("settingsRow");
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
            if (target.classList.contains("devSetting")) {
                let targetId = target.getAttribute("data-target");
                let attribute = target.getAttribute("data-attribute");                
                console.log("Dev setting clicked, we are setting ", attribute, targetId, target.checked);
                updateDevice(targetId, attribute, target.checked);
                
            }
            
            if (target.classList.contains("settingBtn")  || target.parentElement.classList.contains("settingBtn")) {
                if (target.parentElement.classList.contains("settingBtn")) target = target.parentElement;
                console.log("S parent")
                onCardClick(target);                
            }

            if (target.parentElement.classList.contains("settingBtn")) {
                target.parentElement;
                console.log("S");
                onCardClick(target);
            }

            if (target.classList.contains("refreshBtn") || target.parentElement.classList.contains("refreshBtn")) {
                if (target.parentElement.classList.contains("refreshBtn")) target = target.parentElement;
                console.log("Refresh clicked!");
                sendMessage("ScanDevices");
            }

            if (target.classList.contains("modeBtn") || target.parentElement.classList.contains("modeBtn")) {
                if (target.parentElement.classList.contains("modeBtn")) target = target.parentElement;
                let newMode = parseInt(target.getAttribute("data-mode"));
                setMode(newMode);
                sendMessage("Mode", newMode, false);
            }

            if (target.classList.contains("mainSettings") || target.parentElement.classList.contains("mainSettings")) {
                if (target.parentElement.classList.contains("refreshBtn")) target = target.parentElement;
                console.log("Refresh clicked!");                
                toggleSettingsDiv(0);
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

function toggleSettingsDiv(target) {
    let settingsIcon = document.querySelector(".mainSettings span");
    if (!settingsShown) {
        settingsIcon.textContent = "chevron_left";
        // Load main settings
        if (target === 0) {
            console.log("We should be populating main settings.");
            showSettingsMain();
        } else {
            let tDev = null;
            for (let i=0; i < data.devices.length; i++) {
                if (data.devices[i]) {
                    if (data.devices[i]["_id"] === target) {
                        tDev = data.devices[i];
                        break;
                    }
                }                
            }
            if (tDev !== null) {
                deviceData = tDev;
                switch(tDev["Tag"]) {
                    case "Wled":
                        showSettingsWled();
                        break;
                    case "Lifx":
                        showSettingsLifx();
                        break;
                    case "Dreamscreen":                    
                        showSettingsDreamscreen();
                        break;
                    case "Nanoleaf":
                        showSettingsNanoleaf();
                        break;
                    case "HueBridge":
                        showSettingsHue();
                        break;
                    default:
                        console.log("Unknown device tag: " + tDev["Tag"]);
                }
            }
        }
        cardRow.classList.add("hide");
        cardRow.classList.remove("show");
        settingsRow.classList.add("show");
        settingsRow.classList.remove("hide");
    } else {
        settingsIcon.textContent = "settings_applications";
        cardRow.classList.add("show");
        cardRow.classList.remove("hide");
        settingsRow.classList.add("hide");
        settingsRow.classList.remove("show");
        deviceData = null;
        settingsTitle.textContent = "";
        settingsTab.innerHTML = "";
        settingsContent.innerHTML = "";
    }
    settingsShown = !settingsShown;
}

function showSettingsMain() {
    settingsTitle.textContent = "Main Settings";
}

function showSettingsLifx() {
    settingsTitle.textContent = "Lifx Settings";
}

function showSettingsDreamscreen() {
    settingsTitle.textContent = "DS Settings";
}

function showSettingsHue() {
    settingsTitle.textContent = "Hue Settings";
}

function showSettingsWled() {
    settingsTitle.textContent = "Wled Settings";
}

function showSettingsNanoleaf() {
    settingsTitle.textContent = "Nanoleaf Settings";
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
            console.log("I don't know what this is: ", data.store.DeviceMode);
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
    setMode(mode);
    getDevices();
    document.getElementById("cardRow").click();
}

function loadDevices() {
    let getUrl = window.location;
    let baseUrl = getUrl .protocol + "//" + getUrl.host;
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
            bodyDiv.classList.add("card-body");            
            // Create title/subtitle headers
            let title = document.createElement("h5");
            let subTitle = document.createElement("h6");
            title.classList.add("card-title");
            subTitle.classList.add("card-subtitle", "mb2", "text-muted");
            title.textContent = device.Name;
            subTitle.textContent = device["IpAddress"];
            // Create icon
            let titleRow = document.createElement("div");
            titleRow.classList.add("row", "mb-3");
            let titleCol = document.createElement("div");
            titleCol.classList.add("col-8");            
            let iconCol = document.createElement("div");
            iconCol.classList.add("deviceIconWrap", "col-4");
            let image = document.createElement("img");
            image.classList.add("deviceIcon");
            let tag = device.Tag;
            if (tag === "Dreamscreen") tag = device["DeviceTag"];
            image.setAttribute("src", baseUrl + "/img/" + tag.toLowerCase() + "_icon.png");
            // Setting Row
            let settingRow = document.createElement("div");
            settingRow.classList.add("row");
            // Create enabled checkbox
            let checkDiv = document.createElement("div");
            checkDiv.classList.add("mt-4", "col-8");
            let enabled = document.createElement("INPUT");
            let checkId = device["_id"] + "Enabled";
            enabled.setAttribute("id", checkId);
            enabled.setAttribute("type", "checkbox");
            if (device["Enable"] === true) enabled.setAttribute("checked", "checked");
            enabled.classList.add("form-check-input", "devSetting", "mr-1");
            let eLabel = document.createElement("label");
            enabled.setAttribute("data-target",device["_id"]);
            enabled.setAttribute("data-attribute","Enable");
            if (device["Enable"]) enabled.checked = true;
            eLabel.classList.add("form-check-label");
            eLabel.setAttribute("for", checkId);
            eLabel.innerText = "Enable streaming";
            checkDiv.appendChild(enabled);
            checkDiv.appendChild(eLabel);
            settingRow.appendChild(checkDiv);
            // Create settings button
            let settingCol = document.createElement("div");
            settingCol.classList.add("col-4", "pt-2");
            let settingsButton = document.createElement("button");
            settingsButton.classList.add("btn", "btn-outline-secondary", "settingBtn", "pt-2");
            settingsButton.setAttribute("data-target",device["_id"]);
            let sIcon = document.createElement("span");
            sIcon.classList.add("material-icons", "pt-1");
            sIcon.textContent = "settings";
            settingsButton.appendChild(sIcon);
            settingCol.appendChild(settingsButton);
            settingRow.appendChild(settingCol);
            //Brightness slider
            let brightnessRow = document.createElement("div");
            brightnessRow.classList.add("row");
            let bIcon = document.createElement("span");
            bIcon.classList.add("material-icons");
            bIcon.textContent = "brightness_low";
            let brightnessSlide = document.createElement("input");
            brightnessSlide.setAttribute("type","range");
            brightnessSlide.setAttribute("data-target",device["_id"]);
            brightnessSlide.setAttribute("data-attribute","Brightness");
            brightnessSlide.value = device["Brightness"];
            brightnessSlide.classList.add("form-input", "w-100");
            let brightnessCol = document.createElement("div");
            brightnessCol.classList.add("col-10", "pt-1");
            let bLabelCol = create("div");
            bLabelCol.classList.add("col-2", "material-icons", "fs-5", "pl-2");
            let bLabel = create("span");
            bLabel.textContent = "emoji_objects";
            brightnessCol.appendChild(brightnessSlide);
            bLabelCol.appendChild(bLabel);
            brightnessRow.appendChild(bLabelCol);
            brightnessRow.appendChild(brightnessCol);

            // Put it all together
            iconCol.appendChild(image);
            titleCol.appendChild(title);
            titleCol.appendChild(subTitle);
            titleRow.appendChild(titleCol)
            titleRow.appendChild(iconCol);
            bodyDiv.appendChild(titleRow);
            bodyDiv.appendChild(brightnessRow);
            bodyDiv.appendChild(settingRow);
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
						top ${duration}ms ease-in-out
					`;
            element.style.top = to.top;
            element.style.left = to.left;
            element.style.width = to.width;
            element.style.height = to.height;
            
        });
        setTimeout(function(){
            element.querySelector(".card-body").querySelectorAll(".row").forEach(function(row){
            console.log("Adding to row? ", row);
            row.style.transition = `
						width ${duration}ms ease-in-out,
						height ${duration}ms ease-in-out,
						left ${duration}ms ease-in-out,
						top ${duration}ms ease-in-out
					`;
            row.classList.add("col-6", "d-flex", "justify-content-center");
        });
        }, 50);
        
        setTimeout(res, duration);
    })
}

const fadeContent = (element, opacity, duration = 300) => {
    return new Promise(res => {
        [...element.children].forEach((child) => {
            requestAnimationFrame(() => {
                child.style.transition = `opacity ${duration}ms linear`;
                child.style.opacity = opacity;
            });
        })
        setTimeout(res, duration);
    })
}

const onCardClick = async (e) => {
    const card = (e.parentElement.parentElement.parentElement.parentElement);
    // clone the card
    let targetId = e.getAttribute("data-target");
    console.log("Setting button clicked, we are opening ", targetId);
    //toggleSettingsDiv(targetId);
    const cardClone = card.cloneNode(true);
    cardClone.classList.remove("devCard", "m-4");
    cardClone.classList.add("container-fluid");
    // get the location of the card in the view
    const {top, left, width, height} = card.getBoundingClientRect();
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
    // create a close button to handle the undo
    const closeButton = document.createElement('button');
    // position the close button top corner
    closeButton.style = `
				position: fixed;
				z-index: 10000;
				top: 0;
				right: 40px;
				width: 35px;
				height: 35px;
			`;
    // attach click event to the close button
    closeButton.classList.add("btn", "btn-clear", "btn-lg");
    const closeSpan = document.createElement('span');
    closeSpan.classList.add("material-icons");
    closeSpan.textContent = "arrow_left";
    closeButton.appendChild(closeSpan);
    closeButton.addEventListener('click', async () => {
        // remove the button on close
        document.querySelector(".mainSettings").classList.remove('d-none');
        closeButton.remove();
        // remove the display style so the original content is displayed right
        cardClone.style.removeProperty('display');
        cardClone.style.removeProperty('padding');
        // show original card content
        [...cardClone.children].forEach(child => child.style.removeProperty('display'));
        fadeContent(cardClone, '0');        
        // shrink the card back to the original position and size
        await toggleExpansion(cardClone, {top: `${top}px`, left: `${left}px`, width: `${width}px`, height: `${height}px`}, 300)
        // show the original card again
        card.style.removeProperty('opacity');
        // remove the clone card
        cardClone.remove();
    });
    // fade the content away
    
    // expand the clone card
    let topNav = document.getElementById("mainNav");
    let oh = topNav.offsetHeight;
    console.log("Top offset is " + oh);
    // fadeContent(cardClone, '0')
    //     .then(() => {
    //         [...cardClone.children].forEach(child => child.style.display = 'none');
    //     });
    await toggleExpansion(cardClone, {top: oh + "px", left: 0, width: '100%', height: '100%'});
    //const content = getCardContent(card.textContent, card.dataset.type)
    // set the display block so the content will follow the normal flow in case the original card is not display block
    cardClone.style.display = 'block';
    cardClone.style.padding = '0';
    // append the close button after the expansion is done
    topNav.appendChild(closeButton);
    document.querySelector(".mainSettings").classList.add('d-none');
    //cardClone.insertAdjacentHTML('afterbegin', content);
};

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
}