let dsIp;
let emulationType = "SideKick";
let bridges = [];
let leaves = [];
let devices = [];
let dsDevs = [];
let captureMode = 0;
let bridgeInt = 0;
let linking = false;
let nanoLinking = false;
let hueAuth = false;
let nanoAuth = false;
let refreshing = false;
let hueGroups;
let hueLights;
let lightMap;
let hueGroup;
let hueIp = "";
let nanoIp = "";
let selectedDevice = null;
let ledData = null;
let bridge = null;
let deviceData = null;
let targetDs = null;
let datastore = null;
let vLedCount = 0;
let hLedCount = 0;
let postResult = null;


$(function () {
    loadData();
    $('#nanoCard').hide();
    $('#hueCard').hide();
    $('dsCard').hide();
    // Initialize BMD
    $('body').bootstrapMaterialDesign();
    
   
    
    $('#showSettings').click(function(){
        hidePanels();
        $('.navbar-toggler').click();
        $('#navTitle').html("Settings");
        selectedDevice = null;
        $('#settingsCard').slideDown();        
    });

    $('#refreshDevices').click(function(){
        $('.navbar-toggler').click();
        RefreshData();
    });

    $('#dsName').on('input', function(){
                
    });

    $("#dsName").blur(function() {
        let nVal = $(this).text();
        let ip = $(this).data('ip');
        let group = $(this).data('group');
        let out = {
            ip: ip,
            group: group,
            name: nVal
        };
            postData("devname", out);
            console.log("I AM CHANGED: " + nVal);    //Here you can write the code to run when the content change
            RefreshData();
    });

    // On capture mode btn click
    $('.capModeBtn').click(function() {
       let target = $(this).data('target');
       setCaptureMode(target);
        
    });

    // Link the hue
    $('#linkBtn').on('click', function () {
        if (!hueAuth && !linking) {
            linkHue();
        }
    });

    // Link the nano
    $('#nanoBtn').on('click', function () {
        if (!nanoAuth && !nanoLinking) {
            linkNano();
        }
    });
    
    $('.nanoFlip').on('click', function() {
       let flipVal = $(this).val() === "on";
       let flipDir = $(this).data('orientation');
       console.log("Setting da flip for " + flipDir + " to " + flipVal);       
       if (flipDir === "h") {
           selectedDevice['mirrorX'] = flipVal;
       } else {
           selectedDevice['mirrorY'] = flipVal;
       }
       postData("flipNano", {dir: flipDir, val: flipVal, id: selectedDevice.id});
       setTimeout(function() {
           let newNano = postResult;
           console.log("New nano: ", newNano);
           drawNanoShapes(newNano);
       }, 1000);
        
    });
    
    // Emulator type change #TODO Post directly
    $(document).on('change', '.emuType', function() {
        deviceData.name = deviceData.name.replace(deviceData.tag, $(this).val());
        deviceData.tag = $(this).val();        
        loadDsData();
    });

    // On light map change #TODO Post directly
    $(document).on('change', '.mapSelect', function() {
        let myId = $(this).attr('id').replace("lightMap", "");
        let newVal = $(this).val().toString();
        updateLightProperty(myId, "targetSector", newVal);
    });

    // Resize device panel on window resize
    $(window).resize(function() {
        if (selectedDevice != null) {
            showDevicePanel(selectedDevice);
        }
    });

    // On brightness slider change
    $(document).on('change', '.mapBrightness', function() {
        let myId = $(this).attr('id').replace("brightness", "");
        let newVal = $(this).val();
        updateLightProperty(myId, "brightness", newVal);
    });

    // On dev brightness slider change
    $(document).on('change', '.devBrightness', function() {
        selectedDevice.brightness = $(this).val();
        saveSelectedDevice();
        postData('brightness', selectedDevice);
    });
    
    $('#dsIpSelect').change( function() {
        let dsIp = $(this).val();
        $.each(devices, function() {
           if ($(this)[0].ipAddress === dsIp) {
               targetDs = $(this)[0];
               if (captureMode === 0) {
                   vLedCount = $(this)[0].flexSetup[0];
                   hLedCount = $(this)[0].flexSetup[1];
                   $('#vCount').val(vLedCount);
                   $('#hCount').val(hLedCount);
               }
           } 
        });
        postData("dsIp", dsIp);
    });
    
    $('#cameraType').change(function() {
       let cType = $(this).val();
       postData('camType', cType); 
    });
    
    $('.ledCount').change(function() {
        let lCount = $(this).val();
        let type = $(this).data('type');
        if (type === "h") {
            vLedCount = lCount;
            postData("vcount", lCount);
        } else {
            hLedCount = lCount;
            postData("hcount", lCount);
        }
    });
    
    // On Override click
    $(document).on('click', '.overrideBright', function() {
        let myId = $(this).attr('id').replace("overrideBrightness", "");
        let newVal = ($(this).val() === "on");
        console.log("Change func for override!", myId, newVal);
        updateLightProperty(myId, "overrideBrightness", newVal);
    });


    // On Device Click
    $(document).on('click', '.devSelect', function (event) {
        console.log("Device click select.");
        let id = $(this).data('device');
        id = id.replace("#group", "");
        console.log("Selecting " + id);
        $.each(devices, function() {
            if ($(this)[0]['id'] == id) {
                showDevicePanel($(this)[0]);
            }
        });
        $('.navbar-toggler').click();
        event.stopPropagation();
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
        $(".modeBtn").removeClass("active");
        $(this).addClass('active');
        const mode = $(this).data('mode');
        selectedDevice.mode = mode;
        saveSelectedDevice();
        postData("mode", mode);        
    });
    
    // On group selection change
    $('.dsGroup').change(function () {
        const id = $(this).val();
        hueGroup = id;
        bridges[bridgeInt]["selectedGroup"] = id;
        postData("bridges", bridges);
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

    setInterval(function(){RefreshData();}, 60000);    

});

// This gets called in loop by hue auth to see if we've linked our bridge.
function checkHueAuth() {
    $.get("./api/DreamData/action?action=authorizeHue&value=" + hueIp, function (data) {
        console.log("Bridge data:", data);
        if (data.key !== null && data.key !== undefined) {
            console.log("Bridge is linked!");
            hueAuth = true;
            if (hueAuth) {
                loadBridgeData(data);
            }
        } else {
            console.log("Bridge is not linked yet.");
        }        
    });
}

function checkNanoAuth() {
    $.get("./api/DreamData/action?action=authorizeNano&value=" + nanoIp, function (data) {
        if (data.token !== null && data.token !== undefined) {
            nanoAuth = true;
            if (nanoAuth) {
                loadNanoData(data);
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
            postResult = data;
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
    postData("bridges", bridges);
}

// Update our pretty table so we can map the lights
function mapLights() {
    let group = findGroup(hueGroup);
    let lights = hueLights;
    console.log("Mapping lights: ", lights);
    console.log("Target group: ", group);
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
                if (selection == -1) {
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
                    if (selection == i) opt.setAttribute('selected', 'selected');
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
    $.get("./api/DreamData/action?action=findDreamDevices", function (data) {
        console.log("Dream devices: ", data);
        buildDevList(data);
    });
}


function saveSelectedDevice() {
    for (let q = 0; q < devices.length; q++) {
        let tDev = devices[q];
        if (tDev.id == selectedDevice.id) {
            devices[q] = selectedDevice;
        }
    }
}


function loadData() {
    $.get("./api/DreamData/action?action=loadData", function (data) {
        console.log("Dream data: ", data);
        datastore = data;
        buildLists(data);
    });
}

function RefreshData() {
    if (!refreshing) {
        refreshing = true;
        console.log("Refreshing data.");
        $.get("./api/DreamData/action?action=refreshDevices", function (data) {
            console.log("Dream data: ", data);
            datastore.devices = data.devices;
            datastore.bridges = data.bridges;
            datastore.leaves = data.leaves;
            buildLists(data);
            refreshing = false;
        });
    }
}

function buildLists(data) {
    dsDevs = [];
    let groups = [];
    devices = data['devices'];
    leaves = data['leaves'];
    bridges = data['bridges'];
    deviceData = data['myDevice'];
    dsIp = data['dsIp'];
    ledData = data['ledData'];
    captureMode = data['captureMode'];
    let mode = selectCaptureMode(captureMode);
    emulationType = data['emuType'];
    buildDevList(data['devices']);
    setCaptureMode(mode);

    // Push dreamscreen devices to groups first, so they appear on top. The, do sidekicks, nanoleaves, then bridges.
    $.each(devices, function() {
        let item = $(this)[0];
        if (item['id'] === undefined && item['ipAddress'] !== undefined) item['id'] = item['ipAddress'];
        if (item['id'] === undefined && item['ipV4Address'] !== undefined) item['id'] = item['ipV4Address'];
        if (this.tag.includes("DreamScreen")) {
            let groupNumber = (item['groupNumber'] === undefined) ? 0 : item['groupNumber'];
            let groupName = (item['groupName'] === undefined) ? "undefined" : item['groupName'];
            if (groups[groupNumber] === undefined) {
                groups[groupNumber] = {};
                groups[groupNumber]['name'] = groupName;
                groups[groupNumber]['id'] = groupNumber;
                groups[groupNumber]['items'] = [];
            }
            groups[groupNumber]['items'].push(item);
        } else {
            dsDevs.push(item);
        }
    });

    const sorted = [];
    const unsorted = [];
    // Sort other DS Devices
    groups = sortDevices(dsDevs, groups, false, false);
    // Sort nanoleaves
    groups = sortDevices(data['leaves'], groups, "NanoLeaf", "NanoLeaf");
    // Sort bridges
    groups = sortDevices(data['bridges'], groups, "HueBridge", "Hue Bridge");
    $('#devGroup').html("");
    console.log("Groups: ", groups);
    $.each(groups, function () {
        let item = $(this)[0];
        console.log("Group: ", item);
        if (item['screenX'] === undefined) {
            if (item['id'] !== 0) {
                sorted.push(item);
            } else {
                appendDeviceGroup(item);
            }
        }
    });
    
    
    $('#devGroup').append($('<li class="spacer">Groups</li>'));

    if (sorted.length > 0) {
        $.each(sorted, function () {
            appendDeviceGroup($(this)[0]);
        });
    }
    
}


function appendDeviceGroup(item) {
    console.log("Appending group: ", item);
    let name = item['name'];
    let elements = item['items'];
    let devGroup = $('#devGroup');
    // This is not a group
    if (item['id'] === 0) {
        $.each(elements, function () {
            let element = $(this)[0];
            if (element['id'] === undefined) element['id'] = element['bridgeId'];
            if (element['id'] === undefined) element['id'] = element['ipAddress'];
            if (element['id'] === undefined) element['id'] = element['ipV4Address'];
            console.log("Adding unsorted element:", element);
            devices.push(element);
            devGroup.append('<li class="devSelect" data-device="' + element.id + '"><img class="devIcon" src="./img/' + element.tag.toLowerCase() + '_icon.png"><span class="devName">' + element.name + '<span></li>');
        });
    } else {        
        let list = $('<li  class="nav-header groupHeader devSelect" data-device="#group' + item['id'] + '"></li>');
        list.append($('<img src="./img/group_icon.png" class="devIcon">'));
        list.append($('<span class="devName">' + name + '</span>'));
        let container = $('<ul id="group' + item['id'] + '" class="nav-list groupList"></ul>');
        $.each(elements, function () {
            let element = $(this)[0];
            console.log("Adding sorted element: ", element);
            if (element.tag.includes("DreamScreen")) {
                item.mode = element.mode;
                item.brightness = element.brightness;
                item.saturation = element.saturation;
            }
            devices.push(element);
            container.append('<li class="devSelect" data-device="' + element.id + '"><img class="devIcon" src="./img/' + element.tag.toLowerCase() + '_icon.png"><span class="devName">' + element.name + '<span></li>');
        });
        console.log("PUSHING GROUP: ", item);        
        item['tag'] = "group";
        item['groupNumber'] = item['id'];
        item['groupName'] = item['name'];
        item.ipAddress = "255.255.255.0";
        devices.push(item);
        list.append(container);
        console.log("Appending: ", list);
        devGroup.append(list);
    }
}

function selectCaptureMode(targetInt) {
    switch(targetInt) {
        case 0:
            return "dsCapPane";
        case 1:
            return "cameraCapPane";
        case 2:
            return "hdmiCapPane";
        case 3:
            return "screenCapPane";
    }
    return null;
}

function setCaptureMode(target) {
    if (target === "dsCapPane") {
        captureMode = 0;
    } else if (target === "cameraCapPane") {
        captureMode = 1;
    } else if (target === "hdmiCapPane") {
        captureMode = 2;
    } else if (target === "screenCapPane") {
        captureMode = 3;
    }
    postData("capturemode", captureMode);
    $('.capModeBtn.active').removeClass('active');
    $('#' + target + 'Btn').addClass('active');
    let hCount = 0;
    let vCount = 0;
    if (captureMode === 0) {
        hCount = ledData.hCountDs;
        vCount = ledData.vCountDs;
    } else {
        hCount = ledData.hCount;
        vCount = ledData.vCount;
    }
    vLedCount = vCount;
    hLedCount = hCount;
    console.log("Hcount, vcount", hCount, vCount);
    let hc = $('#hCount');
    let vc = $('#vCount');
    hc.val(hCount);
    vc.val(vCount);
    hc.parent().addClass("is-filled");
    vc.parent().addClass("is-filled");
    $('.capPane').slideUp();
    $('#' + target).slideDown();
}

function sortDevices(data, groups, tag, name) {
    $.each(data, function () {
        let item = $(this)[0];
        let gn = item['groupNumber'];
        let gName = item['groupName'];
        let groupNumber = (gn === undefined || gn === null) ? 0 : gn; 
        let groupName = (gName === undefined || gName === null) ? "undefined" : gName;
        if (groups[groupNumber] === undefined) {
            groups[groupNumber] = {};
            groups[groupNumber]['name'] = groupName;
            groups[groupNumber]['id'] = groupNumber;
            groups[groupNumber]['items'] = [];
        }
        if (tag !== false) item.tag = tag;
        if (name !== false) {
            if (item.name === undefined || item.name === null) {
                item.name = name;
            }
        }
        groups[groupNumber]['items'].push(item);
    });
    return groups;
}

// Take our DS devices and make a select
function buildDevList(data) {
    const devList = $('#dsIpSelect');
    devList.html("");
    $.each(data, function () {
        const dev = $(this)[0];
        if (selectedDevice == null) {
            selectedDevice = dev;
            showDevicePanel(dev);
        }
        const name = dev.name;
        const ip = dev.ipAddress;
        const type = dev.tag;
        if (name !== undefined && ip !== undefined && type.includes("DreamScreen")) {
            const selected = (ip === dsIp) ? "selected" : "";
            if (selected !== "") {
                targetDs = dev;
            }
            devList.append(`<option value='${ip}' ${selected}>${name}: ${ip}</option>`);
        }
    });
}


function hidePanels() {
    let nanoCard = $('#nanoCard');
    let hueCard = $('#hueCard');
    let dsCard = $('#dsCard');
    let settingsCard = $('#settingsCard');
    nanoCard.slideUp();
    hueCard.slideUp();
    dsCard.slideUp();
    settingsCard.slideUp();
}

function showDevicePanel(data) {
    console.log("Showing panel data: ", data);
    let nanoCard = $('#nanoCard');
    let hueCard = $('#hueCard');
    let dsCard = $('#dsCard');
    hidePanels();
    setTimeout(function(){
        $('#navTitle').html(data.name);
        selectedDevice = data;
        switch (data.tag) {
            case "SideKick":
            case "Connect":
            case "DreamScreen":
            case "DreamScreen4K":
            case "DreamScreenSolo":
            case "group":
                loadDsData(data);            
                dsCard.slideDown();
                break;
            case "HueBridge":
                loadBridgeData(data);
                hueCard.slideDown();
                break;
            case "NanoLeaf":
                loadNanoData(data);
                nanoCard.slideDown();
                break;
        }
    },200);
}

// Show a group setting panel
function showGroupPanel(groupId) {
    
}

// Update the UI with emulator device data
function loadDsData(data) {
    console.log("Loading: ", data);
    deviceData = data;
    $('#dsName').html(deviceData.name);
    $('#dsName').data("ip", deviceData.ipAddress);
    $('#dsName').data('group', deviceData.groupNumber);
    
    $('#dsIp').html(deviceData.ipAddress);
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
    $('#iconWrap').removeClass().addClass(emulationType);
}

// Update UI with specific bridge data
function loadBridgeData(data) {
    // Get our UI elements
    const hIp = $('#hueIp');
    const lImg = $('#linkImg');
    const lHint = $('#linkHint');
    const lBtn = $('#linkBtn');

    // This is our bridge. There are many others like it...but this one is MINE.
    console.log("Loaded bridge: ", data);
    // Now we've got it.
    let b = data;
    hueIp = b["ipAddress"];
    hIp.html(b["ipAddress"]);        
    hueGroup = b["selectedGroup"];
    hueGroups = b["groups"];
    if ((hueGroup === -1 && hueGroups.length > 0) || hueGroup === null || hueGroup === undefined) {
        hueGroup = hueGroups[0]["id"];
        bridges[bridgeInt].selectedGroup = hueGroup;
        console.log("Updated group to " + hueGroup);
        postData("bridges", bridges);
    }
    hueLights = b["lights"];
    hueAuth = (b["user"] !== null || b["key"] !== null);            
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
            lHint.html("Click above to link.");
        }
        lBtn.css('cursor', 'pointer');
    }
    console.log("Loaded", bridge, hueGroup, hueGroups, hueLights);
    listGroups();
    mapLights();
}

// Load nanoleaf data
function loadNanoData(data) {
    // Get our UI elements
    const hIp = $('#nanoIp');
    const lImg = $('#nanoImg');
    const lHint = $('#nanoHint');
    const lBtn = $('#nanoBtn');

    // This is our bridge. There are many others like it...but this one is MINE.
    console.log("Loaded nanodata: ", data);
    // Now we've got it.
    let n = data;
    nanoIp = n["ipV4Address"];
    
    hIp.html(n["ipV4Address"]);    
    nanoAuth = (n["token"] !== null && n["token"] !== undefined);
    lImg.removeClass('linked unlinked linking');
    if (nanoAuth) {
        lImg.addClass('linked');
        lHint.html("Your Nanoleaf is linked.");
        lBtn.css('cursor', 'default');
        drawNanoShapes(data);
    } else {
        if (nanoLinking) {
            lImg.addClass('linking');
            lHint.html("Press the link button on your Nanoleaf.");
        } else {
            lImg.addClass('unlinked');
            lHint.html("Click above to link.");
        }
        lBtn.css('cursor', 'pointer');
    }
    console.log("Loaded Nano data? ", data);    
}


function drawNanoShapes(panel) {
    // Wipe it out
    $('#canvasDiv').remove();
    $('#nanoContainer').append('<div id="canvasDiv"></div>');
    // Get window width
    let width = window.innerWidth;
    let height = window.innerHeight;

    // Create our stage
    let stage = new Konva.Stage({
        container: 'canvasDiv',
        width: width,
        height: height
    });

    // Shape layer
    let cLayer = new Konva.Layer();
    stage.add(cLayer);

    // Get layout data from panel
    let pX = panel['x'];
    let pY = panel['y'];
    let pScale = panel['scale'];
    let pRot = panel['rotation'];
    let mirrorX = panel['mirrorX'];
    let mirrorY = panel['mirrorY'];    
    let layout = panel['layout'];
    let sideLength = layout['sideLength'];

    // Set our TV image width
    let tvWidth = (hLedCount / 4) * sideLength;
    let tvHeight = (vLedCount / 4) * sideLength;

    // If window is less than 500px, divide our scale by half
    let halfScale = false;
    if (width < 500) {
        halfScale = true;
        height /= 2;
        pScale /= 4;
        tvWidth /= 4;
        tvHeight /= 4;
    } else {
        tvWidth /= 2;
        tvHeight /= 2;
        pScale /= 2;
    }
    
    console.log("TvWidth, height", tvWidth, tvHeight);

    // Determine TV x/y position
    let tvX = (width - tvWidth) / 2;
    let tvY = (height - tvHeight) / 2;
    let centerX = width / 2;
    let centerY = height / 2;
    
   
    // Group for the shapes
    let shapeGroup = new Konva.Group({       
        rotation: pRot,
        draggable: true,
        scaleX: pScale,
        scaleY: pScale
    });

    cLayer.add(shapeGroup);
    let snaps = [];
    for (let q = 0; q <= 360; q+=10) {
        snaps.push(q);
    }
    // Transform for scaling

    let tr2 = new Konva.Transformer({
        keepRatio: true,
        enabledAnchors: [],
        rotationSnaps: snaps
    });

    cLayer.add(tr2);
    // Attach to group
    tr2.attachTo(shapeGroup);
    tr2.zIndex(0);

    cLayer.draw();


    // Drag listener
    shapeGroup.on('dragend', function(e) {
        doTheThing();
    });
    
    // Transform listener
    shapeGroup.on('transformend', function(e) {
       doTheThing();
    });
    
    // Transform values and post them
    function doTheThing() {
        // Group x and y position
        let gX = shapeGroup.x();
        let gY = shapeGroup.y();
        let sW = tr2.width();
        let sH = tr2.height();
        console.log("GX, GY:", gX, gY);
        console.log("HW, HH:", sW, sH);
        console.log("CX, CY:", centerX, centerY);
        gX += (sW / 2);
        gY += (sH / 2);
        gX = gX - centerX;
        gY = gY - centerY;
        gY *= -1;
        if (halfScale) {
            gX *= 4;
            gY *= 4;
        } else {
            gX *= 2;
            gY *= 2;
        }
        selectedDevice.x = gX;
        selectedDevice.y = gY;
        selectedDevice.scale = 1;
        selectedDevice.rotation = shapeGroup.rotation();
        saveSelectedDevice();
        postData("leaf", selectedDevice);
    }
    
    
    let positions = layout['positionData'];
    let minX = 0;
    let minY = 0;
    
    // Calculate the min/max range for each tile
    for (let panel in positions) {
        let data = positions[panel];
        if (data.x < minX) minX = data.x;
        if ((data.y * -1) < minY) minY = (data.y * -1);
    }
    
    let triHeight = sideLength * (Math.sqrt(3)/2);
    minX -= triHeight;
    minY -= triHeight;
    
    
    for (let panel in positions) {
        let data = positions[panel];
        console.log("Draw panel: ", data);
        let shape = data['shapeType'];
        let x = data.x;
        let y = data.y;
        if (mirrorX) x *= -1;
        if (!mirrorY) y *= -1;
        x += Math.abs(minX);
        y += Math.abs(minY);
        
        let sText = new Konva.Text({
            x: x,
            y: y,
            text: data.panelId,
            fontSize: 30,
            fontFamily: 'Calibri'
        });
        let o = data['o'];
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
                    closed: true
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
                shapeGroup.add(rect1);
                break;
            case 5:
                console.log("Draw a power supply??");
                break;
        }
        sText.offsetX(sText.width() / 2);
        shapeGroup.add(sText);
    }
    
    // add the layer to the stage
    tr2.forceUpdate();
    let nW = tr2.width();
    let nH = tr2.height();
    
    console.log("New width, height", nW, nH);
    console.log("CX, CY: ", centerX, centerY);
    if (halfScale) {
        pY /=4;
        pX /=4;
    } else {
        pY /= 2;
        pX /= 2;
    }
    
    pY *= -1;
    pY += centerY;
    pX += centerX;
    pY -= (nH / 2);
    pX -= (nW / 2);
    console.log("Adjusted XY", pX, pY);
    shapeGroup.x(pX);
    shapeGroup.y(pY);
    cLayer.draw();
    let iLayer = new Konva.Layer();
    stage.add(iLayer);
    
    console.log("TV width and height are " + tvWidth + " and " + tvHeight);
    // Add the tv image
    Konva.Image.fromURL('../img/sectoring_tv2.png', function(tvImage) {
        tvImage.setAttrs({
            x: tvX,
            y: tvY,
            width: tvWidth,
            height: tvHeight
        });
        iLayer.add(tvImage);
        iLayer.zIndex(1);
        iLayer.batchDraw();
    });
    cLayer.zIndex(0);
}

// Get a group by group ID
function findGroup(id) {
    console.log("Looking for group with ID " + id);
    let res = false;
    if (id === -1 || id === "-1" || id === null) {
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


// Runs a loop to detect if our hue device is linked
function linkNano() {
    console.log("Authorized: ", nanoAuth);
    if (!nanoAuth && !nanoLinking) {
        nanoLinking = true;
        console.log("Trying to authorize with nanoleaf.");
        $('#nanoBar').show();
        const bar = new ProgressBar.Circle(nanoBar, {
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
        nanoAuth = false;
        const intervalID = window.setInterval(function () {
            checkNanoAuth();
            bar.animate((x / 30));
            if (x++ === 30 || hueAuth) {
                window.clearInterval(intervalID);
                $('#nanoBar').hide();
                nanoLinking = false;
            }
        }, 1000);

        setTimeout(function () {
            let cb = $('#nanoBar');
            cb.html("");
            cb.hide();
            nanoLinking = false;
        }, 30000);
    } else {
        console.log("Already authorized.");
    }
}

