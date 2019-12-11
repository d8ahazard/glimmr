let dsIp;
let emulationType = "SideKick";
let deviceData = null;
let linking = false;
let bridges = [];
// Set this from config load, wham, bam, thank you maaam...
let bridge = null;
// Set all of these based on bridge, or remove them
let hueAuth = false;
let bridgeInt = 0;
let hueGroups;
let hueLights;
let lightMap;
let hueGroup;
let hueIp = "";

// Not actually used yet
let webSocketProtocol = location.protocol === "https:" ? "wss:" : "ws:";
let webSocketURI = webSocketProtocol + "//" + location.host + "/ws";
let socket = new WebSocket(webSocketURI);


$(function () {

    // Fetch data
    fetchJson();

    // List our devices
    listDreamDevices();

    // Initialize BMD
    $('body').bootstrapMaterialDesign();

    // Post our data
    $('#settingsForm').submit(function (e) {
        e.preventDefault();
        bridge["selectedGroup"] = hueGroup;
        bridges[bridgeInt] = bridge;
        postData("bridges", bridges);
        postData("dsIp", dsIp);
        if (deviceData.tag === "SideKick") {
            postData("dsSidekick", deviceData);    
        } else {
            postData("dsConnect", deviceData);
        }
        
    });

    // Do the linking
    $('#linkBtn').on('click', function () {
        if (!hueAuth && !linking) {
            linkHue();
        }
    });
    
    // Emulator type change
    $(document).on('change', '.emuType', function() {
        deviceData.name = deviceData.name.replace(deviceData.tag, $(this).val());
        deviceData.tag = $(this).val();        
        loadDeviceData();
    });

    // On light map change
    $(document).on('change', '.mapSelect', function() {
        let myId = $(this).attr('id').replace("lightMap", "");
        let newVal = $(this).val().toString();
        updateLightProperty(myId, "targetSector", newVal);
    });

    // On brightness slider change
    $(document).on('change', '.mapBrightness', function() {
        let myId = $(this).attr('id').replace("brightness", "");
        let newVal = $(this).val();
        updateLightProperty(myId, "brightness", newVal);
    });
    
    // On Override click
    $(document).on('click', '.overrideBright', function() {
        let myId = $(this).attr('id').replace("overrideBrightness", "");
        let newVal = ($(this).val() === "on");
        console.log("Change func for override!", myId, newVal);
        updateLightProperty(myId, "overrideBrightness", newVal);
    });
    
    // Cycle bridges
    $('.arrowBtn').click(function() {
        let cycleInt = 1;
        let curInt = bridgeInt;
        if ($(this).id === "bridgePrev") cycleInt = -1;
        curInt += cycleInt;
        if (curInt >= bridges.length) curInt = 0;
        if (curInt < 0) curInt = (bridges.length - 1);
        if (bridgeInt !== curInt) {
            bridgeInt = curInt;
            // #todo: Make an animation here
            loadBridge(bridgeInt);
        }
    });
        
    // On device mode change 
    $('.modeBtn').click(function () {
        if (hueAuth) {
            $(".modeBtn").removeClass("active");
            $(this).addClass('active');
            const mode = $(this).data('mode');
            postData("mode", mode);
        }
    });
    
    // On group selection change
    $('.dsGroup').change(function () {
        const id = $(this).val();
        hueGroup = id;
        bridges[bridgeInt]["selectedGroup"] = id;
        mapLights();        
    });
    
    // On selection map change
    $(document).on('focusin', '.mapSelect', function () {
        $(this).data('val', $(this).val());
    }).on('change', '.mapSelect', function () {
        const prev = $(this).data('val');
        const current = $(this).val();
        if (current !== -1) {
            const target = $('#sector' + current);
            if (!target.hasClass('checked')) target.addClass('checked');
            $('#sector' + prev).removeClass('checked');
            $(this).data('val', $(this).val());
        }
        hueGroup = current.toString();        
    });   
        
    // Socket fun
    socket.onopen = function () {
        console.log("Connected.");
    };
    
    // Socket fun, ctd.
    socket.onclose = function (event) {
        if (event.wasClean) {
            console.log('Disconnected.');
        } else {
            console.log('Connection lost.'); // for example if server processes is killed
        }
        console.log('Code: ' + event.code + '. Reason: ' + event.reason);
    };
    socket.onmessage = function (event) {
        console.log("Data received: " + event.data);
    };
    socket.onerror = function (error) {
        console.log("Error: " + error.message);
    };

});

// This gets called in loop by hue auth to see if we've linked our bridge.
function checkHueAuth() {
    $.get("./api/DreamData/action?action=authorizeHue&value=" + hueIp, function (data) {
        if (data === "Success: Bridge Linked." || data === "Success: Bridge Already Linked.") {
            console.log("LINKED");
            hueAuth = true;
            if (hueAuth) {
                fetchJson();
            }
        }
    });
}

// Post settings data in chunks for deserialization
function postData(endpoint, payload) {
    $.ajax({
        url: "./api/DreamData/" + endpoint,
        type: "POST",
        contentType: "application/json;",
        dataType: "json",
        data: JSON.stringify(payload),
        success: function (data) {
            console.log(`Posted to ${endpoint}`, endpoint, data);
        }
    });
}

// Update our stored setting data for various light values
function updateLightProperty(myId, propertyName, value) {
    console.log("Updating light " + propertyName + " for " + myId, value);
    for(let k in hueLights) {
        if (hueLights.hasOwnProperty(k)) {
            if (hueLights[k]["id"] === myId) {
                hueLights[k][propertyName] = value;
            }
        }
    }    
    console.log("Updated light data: ", hueLights);
    bridges[bridgeInt]["lights"] = hueLights;
}

// Retrieve the bulk of our setting data
function fetchJson() {
    $.get('./api/DreamData/json', function (config) {
        console.log("We have some config", config);
        // Load emulator settings
        if (config.hasOwnProperty("myDevice")) {
            deviceData = config.myDevice;
            loadDeviceData();
        }
        // Load target DS IP
        if (config.hasOwnProperty("dsIp")) {
            dsIp = config.dsIp;
        }        
        // Load DS devices? 
        if (config.hasOwnProperty("devices")) {
            buildDevList(config.devices);
        }
        // Load bridge data
        if (config.hasOwnProperty("bridges")) {
            bridges = config.bridges;
            loadBridges(bridges);
        }
    });
}

// Update our pretty table so we can map the lights
function mapLights() {
    let group = findGroup(hueGroup);
    let lights = hueLights;
    console.log("Mapping: ", group, lights);
    // Get the main light group
    const lightGroup = document.getElementById("mapSel");
    // Clear it
    $('div').remove('.delSel');
    // Clear the light region checked status
    $('.lightRegion').removeClass("checked");
    // Get the list of lights for the selected group
    if (!group.hasOwnProperty('lights')) return false;
    const ids = group["lights"];
    
    // Sort our lights by name
    lights = lights.sort(function (a, b) {
        if (!a.hasOwnProperty('Value') || !b.hasOwnProperty('Value')) return false;
        return a.Value.localeCompare(b.Value);
    });
    console.log("IDS: " + ids);
    // Loop through our list of all lights
    for (let l in lights) {
        if (lights.hasOwnProperty(l)) {
            let light = lights[l];
            console.log("LIGHT: ", light);
            let id = light['id'];
            if ($.inArray(id, ids) !== -1) {
                const name = light['name'];
                let brightness = light["brightness"];
                let override = light["overrideBrightness"];
                let selection = light["targetSector"];

                // Create the label for select
                const label = document.createElement('label');
                label.innerHTML = name + ":";
                label.setAttribute("for", "lightMap" + id);

                // Create a select for this light
                const newSelect = document.createElement('select');
                newSelect.className = "mapSelect form-control text-dark bg-light";
                newSelect.setAttribute('id', 'lightMap' + id);
                newSelect.setAttribute('name', 'lightMap' + id);
                
                // Create the blank "unmapped" option
                let opt = document.createElement("option");
                opt.value = "-1";
                opt.innerHTML = "";

                // Set it to selected if we don't have a mapping
                if (selection === -1) {
                    opt.setAttribute('selected', 'selected');
                } else {
                    // Check our box on the sector square
                    const checkDiv = $('#sector' + selection);
                    if (!checkDiv.hasClass('checked')) checkDiv.addClass('checked');
                }
                newSelect.appendChild(opt);

                // Add the options for our regions
                for (let i = 0; i < 12; i++) {
                    opt = document.createElement("option");
                    opt.value = i.toString();
                    opt.innerHTML = "<BR>" + (i + 1);
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
                newRange.className = "mapBrightness form-control";
                newRange.setAttribute("type", "range");
                newRange.setAttribute('id', 'brightness' + id);
                newRange.setAttribute('name', 'brightness' + id);
                newRange.setAttribute('min', "0");
                newRange.setAttribute('max', "100");
                newRange.setAttribute('value', brightness.toString());

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
                newCheck.className += "overrideBright form-check-input";
                newCheck.setAttribute('id', 'overrideBrightness' + id);
                newCheck.setAttribute('name', 'overrideBrightness' + id);
                newCheck.setAttribute("type", "checkbox");
                if (override) newCheck.setAttribute("checked", 'checked');
                
                // Create the div to hold the checkbox
                const chkDiv = document.createElement('div');
                chkDiv.className += "form-check";
                chkDiv.appendChild(newCheck);
                chkDiv.appendChild(checkLabel);

                // Create the div for the other divs
                const lightDiv = document.createElement('div');
                lightDiv.className += "delSel col-xs-4 col-sm-4 col-md-3";
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
    $('.delSel').bootstrapMaterialDesign();   
}

// Take our hue groups and make a select
function listGroups() {
    const gs = $('#dsGroup');
    gs.html("");
    let i = 0;
    if (hueAuth) {
        if (hueGroups !== null) {
            if (hueGroup === -1 && hueGroups.length > 0) {
                hueGroup = hueGroups[0][id];
                console.log("Setting default group to " + hueGroup);
            }
            $.each(hueGroups, function() {
                let val = $(this)[0].id;
                let txt = $(this)[0].name;
                let selected = (val === hueGroup) ? " selected" : "";
                gs.append(`<option value="${val}" ${selected}>${txt}</option>`);
                i++;
            });
        }
    }
}

// Not used, probably won't. This will eventually get replaced by a websocket
function getMode() {
    $.get("./api/DreamData/getMode", function(data) {
       console.log("DATA: ", data); 
    });
}

// Get a list of dreamscreen devices
function listDreamDevices() {
    $.get("./api/DreamData/action?action=connectDreamScreen", function (data) {
        console.log("Dream devices: ", data);
        buildDevList(data);
    });
}

// Take our DS devices and make a select
function buildDevList(data) {
    const devList = $('#dsIp');
    devList.html("");
    $.each(data, function () {
        const dev = $(this)[0];
        const name = dev.name;
        const ip = dev.ipAddress;
        const type = dev.tag;
        if (name !== undefined && ip !== undefined && type.includes("DreamScreen")) {
            const selected = (ip === dsIp) ? "selected" : "";
            devList.append(`<option value='${ip}' ${selected}>${name}: ${ip}</option>`);
        }
    });
}

// Update the UI with emulator device data
function loadDeviceData() {
    $('#dsName').html(deviceData.name);
    if (deviceData.hasOwnProperty('groupName')) $('#dsGroupName').html(deviceData.groupName);
    emulationType = deviceData.tag;
    $('#dsType').html();
    let modestr = "";
    $('.modeBtn').removeClass('active');
    switch (deviceData.mode) {
        case 0:
            modestr = "Off";
            break;
        case 1:
            modestr = "Video";
            break;
        case 2:
            modestr = "Audio";
            break;
        case 3:
            modestr = "Ambient";
            break;
    }
    $('#mode' + deviceData.mode).addClass('active');
    $('#dsMode').html(modestr);
    if (emulationType === "Connect") {
        $('#iconWrap').addClass("Connect").removeClass("SideKick");
        console.log("Connect");
    } else {
        $('#iconWrap').addClass("SideKick").removeClass("Connect");
        console.log("Sidekick");
    }
    $('#emuType option[value=' + emulationType + ']').attr('selected', true);
    $('#emuTypeText').html(emulationType);
}

// Load new bridge data and update UI with selected bridge
function loadBridges(value) {
    bridges = value;
    loadBridge(bridgeInt);
}

// Update UI with specific bridge data
function loadBridge(bridgeIndex) {
    // Get our UI elements
    const hIp = $('#hueIp');
    const lImg = $('#linkImg');
    const lHint = $('#linkHint');
    const lBtn = $('#linkBtn');

    // We cant load a bridge if this stuff ain't right
    if (bridges === null || bridges === undefined) return false;
    if (bridgeIndex >= bridges.length) return false;
    // This is our bridge. There are many others like it...but this one is MINE.
    let b = bridges[bridgeIndex];
    console.log("Loaded bridge: ", b);
    // Now we've got it.
    bridge = b;
    hueIp = b["ip"];
    hIp.html(b["ip"]);        
    hueGroup = b["selectedGroup"];
    hueGroups = b["groups"];
    if (hueGroup === -1 && hueGroups.length > 0) {
        hueGroup = hueGroups[0]["id"];
        console.log("Updated group to " + hueGroup);
    }
    hueLights = b["lights"];
    hueAuth = (b["user"] !== null && b["key"] !== null);            
    lImg.removeClass('linked unlinked linking');
    if (hueAuth) {
        lImg.addClass('linked');
        lHint.html("Your Hue Bridge is linked.");
        lBtn.css('cursor', 'default');
    } else {
        if (linking) {
            lImg.addClass('linking');
            lHint.html("Press the link button on your Bridge.");
        } else {
            lImg.addClass('unlinked');
            lHint.html("Click here to link your bridge.");
        }
        lBtn.css('cursor', 'pointer');
    }
    console.log("Loaded", bridge, hueGroup, hueGroups, hueLights);
    listGroups();
    mapLights();
}

// Get a group by group ID
function findGroup(id) {
    console.log("Looking for group with ID " + id);
    let res = false;
    if (id === -1 || id === "-1") {
        console.log("We don't have a group", hueGroups[0]);        
        return hueGroups[0];    
    }
    $.each(hueGroups, function () {
        console.log("ID, this ID", id, $(this)[0].id);

        if (id === $(this)[0].id) {
            res = $(this)[0];
        }        
    });
    return res;
}

// Runs a loop to detect if our hue device is linked
function linkHue() {
    console.log("Authorized: ", hueAuth);
    if (!hueAuth && !linking) {
        linking = true;
        console.log("Trying to authorize with hue.");
        $('#circleBar').show();
        const bar = new ProgressBar.Circle(circleBar, {
            strokeWidth: 15,
            easing: 'easeInOut',
            duration: 0,
            color: '#0000FF',
            trailColor: '#eee',
            trailWidth: 0,
            svgStyle: null,
            value: 1
        });

        let x = 0;
        hueAuth = false;
        const intervalID = window.setInterval(function () {
            checkHueAuth();
            bar.animate((x / 30));            
            if (x++ === 30 || hueAuth) {
                window.clearInterval(intervalID);
                $('#circleBar').hide();
                linking = false;
            }
        }, 1000);

        setTimeout(function () {
            let cb = $('#circleBar');
            cb.html("");
            cb.hide();
            linking = false;
        }, 30000);
    } else {
        console.log("Already authorized.");
    }
}

