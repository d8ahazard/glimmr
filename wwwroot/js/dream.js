let dsIp;
let emulationType = "SideKick";
let deviceData = null;
let linking = false;
let nanoLinking = false;
let bridges = [];
let leaves = [];
// Set this from config load, wham, bam, thank you maaam...
let bridge = null;
// Set all of these based on bridge, or remove them
let hueAuth = false;
let nanoAuth = false;
let bridgeInt = 0;
let hueGroups;
let hueLights;
let lightMap;
let hueGroup;
let hueIp = "";
let nanoIp = "";
let devices = {};
let nanoDrag = null;

// Not actually used yet
let webSocketProtocol = location.protocol === "https:" ? "wss:" : "ws:";
let webSocketURI = webSocketProtocol + "//" + location.host + "/ws";
let socket = new WebSocket(webSocketURI);


$(function () {

    listDevices();
    $('#nanoCard').hide();
    $('#hueCard').hide();
    $('dsCard').hide();
    // Initialize BMD
    $('body').bootstrapMaterialDesign();
    
    setDrag();

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

    $('#nanoBtn').on('click', function () {
        if (!nanoAuth && !nanoLinking) {
            linkNano();
        }
    });
    
    // Emulator type change
    $(document).on('change', '.emuType', function() {
        deviceData.name = deviceData.name.replace(deviceData.tag, $(this).val());
        deviceData.tag = $(this).val();        
        loadDsData();
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


    // On Device Click
    $(document).on('click', '.devSelect', function (event) {
        console.log("Device click select.");
        let id = $(this).data('device');
        console.log("Selecting " + id);
        $.each(devices, function() {
            if ($(this)[0]['id'] === id) {
                showDevicePanel($(this)[0]);
            }
        });
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
    
});

// This gets called in loop by hue auth to see if we've linked our bridge.
function checkHueAuth() {
    $.get("./api/DreamData/action?action=authorizeHue&value=" + hueIp, function (data) {
        if (data === "Success: Bridge Linked." || data === "Success: Bridge Already Linked.") {
            console.log("LINKED");
            hueAuth = true;
            if (hueAuth) {
                listDevices();
            }
        }
    });
}

function checkNanoAuth() {
    $.get("./api/DreamData/action?action=authorizeNano&value=" + nanoIp, function (data) {
        if (data === "Success: Nanoleaf Linked." || data === "Success: Nanoleaf Already Linked.") {
            console.log("LINKED");
            nanoAuth = true;
            if (nanoAuth) {
                listDevices();
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

function setDrag() {
    if (nanoDrag !== null) {
        nanoDrag.forEach(item => {
            item.disable();
        });
    }
    nanoDrag = subjx('#nanoCanvas').drag({
        proportions: true,
        resizable: true,
        rotatable: true,
        container: '#canvasDiv',
        onMove(dx, dy) {
            console.log("Moved: ", dx, dy);
            // fires on moving
        },
        onResize(dx, dy, handle) {
            console.log("Resized: ", dx, dy, handle);
            // fires on resizing
        },
        onRotate(rad) {
            console.log("Rotated: ", rad);
            // fires on rotation
        },
        onDrop(e, el) {
            console.log("Dropped: ", e, el);
            // fires on drop
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
    $.get("./api/DreamData/action?action=findDreamDevices", function (data) {
        console.log("Dream devices: ", data);
        buildDevList(data);
    });
}


function listDevices() {
    $.get("./api/DreamData/action?action=listDevices", function (data) {
        console.log("Dream devices: ", data);
        buildLists(data);
    });
}


function buildLists(data) {
    devices = [];
    let groups = {};
    let dsDevs = [];
    leaves = data['leaves'];
    bridges = data['bridges'];
    // Push dreamscreen devices to groups first, so they appear on top. The, do sidekicks, nanoleaves, then bridges.
    $.each(data['ds'], function() {
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
    // Sort other DS Devices
    groups = sortDevices(dsDevs, groups, false, false);
    // Sort nanoleaves
    groups = sortDevices(data['leaves'], groups, "NanoLeaf", "NanoLeaf");
    // Sort bridges
    groups = sortDevices(data['bridges'], groups, "HueBridge", "Hue Bridge");
    $('#devGroup').html("");
    console.log("Groups: ", groups);
    let unSorted = false;
    $.each(groups, function () {
        let item = $(this)[0];
        console.log("Group: ", item);
        if (item['id'] !== 0) {
            sorted.push(item);
        } else {
            unSorted = item;
        }        
    });
    if (unSorted !== false) appendDeviceGroup(unSorted);
    $('#devGroup').append($('<li class="spacer">Groups</li>'));

    if (sorted.length > 0) {
        $.each(sorted, function () {
            appendDeviceGroup($(this)[0]);
        });
    }
    
}

function appendDeviceGroup(item) {
    let name = item['name'];
    let elements = item['items'];
    let devGroup = $('#devGroup');
    if (item['id'] === 0) {
        if (item['id'] === undefined) item['id'] = item['ipAddress'];
        if (item['id'] === undefined) item['id'] = item['ipV4Address'];
        $.each(elements, function () {
            let element = $(this)[0];
            devices.push(element);
            devGroup.append('<li class="devSelect" data-device="' + element.id + '"><img class="devIcon" src="./img/' + element.tag + '_icon.png"><span class="devName">' + element.name + '<span></li>');
        });
    } else {        
        let list = $('<li  class="nav-header groupHeader devSelect" data-device="#group' + item['id'] + '"></li>');
        list.append($('<img src="./img/group_icon.png" class="devIcon">'));
        list.append($('<span class="devName">' + name + '</span>'));
        let container = $('<ul id="group' + item['id'] + '" class="nav-list groupList"></ul>');
        $.each(elements, function () {
            let element = $(this)[0];
            devices.push(element);
            container.append('<li class="devSelect" data-device="' + element.id + '"><img class="devIcon" src="./img/' + element.tag + '_icon.png"><span class="devName">' + element.name + '<span></li>');
        });
        list.append(container);
        console.log("Appending: ", list);
        devGroup.append(list);
    }
}


function sortDevices(data, groups, tag, name) {
    $.each(data, function () {
        let item = $(this)[0];
        let groupNumber = (item['groupNumber'] === undefined) ? 0 : item['groupNumber'];
        let groupName = (item['groupName'] === undefined) ? "undefined" : item['groupName'];
        if (groups[groupNumber] === undefined) {
            groups[groupNumber] = {};
            groups[groupNumber]['name'] = groupName;
            groups[groupNumber]['id'] = groupNumber;
            groups[groupNumber]['items'] = [];
        }
        if (tag !== false) item.tag = tag;
        if (item.name === undefined && name !== false) item.name = name;
        groups[groupNumber]['items'].push(item);
    });
    return groups;
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

function showDevicePanel(data) {
    console.log("Showing panel data: ", data);
    let nanoCard = $('#nanoCard');
    let hueCard = $('#hueCard');
    let dsCard = $('#dsCard');
    $('#navTitle').html(data.name);
    switch (data.tag) {
        case "SideKick":
        case "Connect":
        case "DreamScreen":
        case "DreamScreen4K":
        case "DreamScreenSolo":
            loadDsData(data);
            nanoCard.slideUp();
            hueCard.slideUp();
            dsCard.slideDown();
            break;
        case "HueBridge":
            loadBridgeData(data);
            nanoCard.slideUp();
            dsCard.slideUp();
            hueCard.slideDown();
            break;
        case "NanoLeaf":
            loadNanoData(data);
            dsCard.slideUp();
            hueCard.slideUp();
            nanoCard.slideDown();
            break;
    }
}

// Update the UI with emulator device data
function loadDsData(data) {
    console.log("Loading: ", data);
    deviceData = data;
    $('#dsName').html(deviceData.name);
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
    $('#iconWrap').removeClass().addClass("col-2 col-sm-4 " + emulationType);
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
    hueIp = b["ipV4Address"];
    hIp.html(b["ipV4Address"]);        
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
    drawNanoShapes(data['layout']);
    hIp.html(n["ipV4Address"]);    
    nanoAuth = (n["token"] !== null && n["token"] !== undefined);
    lImg.removeClass('linked unlinked linking');
    if (nanoAuth) {
        lImg.addClass('linked');
        lHint.html("Your Nanoleaf is linked.");
        lBtn.css('cursor', 'default');
    } else {
        if (nanoLinking) {
            lImg.addClass('linking');
            lHint.html("Press the link button on your Nanoleaf.");
        } else {
            lImg.addClass('unlinked');
            lHint.html("Click here to link your nanoleaf.");
        }
        lBtn.css('cursor', 'pointer');
    }
    console.log("Loaded Nano data? ", data);    
}

function drawNanoShapes(layout) {
    let c = document.getElementById("nanoCanvas");
    let ctx = c.getContext("2d");    
    ctx.clearRect(0, 0, c.width, c.height);
    ctx.strokeStyle = "#cc0000";
    ctx.lineWidth = 2;
    console.log("Drawing layout: ", layout);
    let count = layout['numPanels'];
    let sideLength = layout['sideLength'];
    
    let gridAdjust = count * sideLength;
    c.width = gridAdjust * 2;
    c.height = gridAdjust * 2;
    let positions = layout['positionData'];
    let minX = 0;
    let minY = 0;
    let maxX = 0;
    let maxY = 0;
    for (let panel in positions) {
        let data = positions[panel];
        if (data.x > maxX) maxX = data.x;
        if ((data.y * -1) > maxY) maxY = (data.y * -1);
        if (data.x < minX) minX = data.x;
        if ((data.y * -1) < minY) minY = (data.y * -1);
    }
    let triHeight = sideLength * (Math.sqrt(3)/2);
    maxX += triHeight;
    maxY += triHeight * 2;
    minX -= triHeight;
    minY -= triHeight;
    let xAdjust = maxX - minX;
    let yAdjust = maxY - minY;
    c.width = xAdjust;
    c.height = yAdjust;
    console.log("Range adjust is ", xAdjust, yAdjust);
    for (let panel in positions) {
        let data = positions[panel];
        console.log("Draw panel: ", data);
        let shape = data.shapeType;
        let x = data.x + Math.abs(minX);
        let y = (data.y * -1) + Math.abs(minY);
        let o = data.o;
        ctx.strokeStyle = "#ccff00";
        ctx.beginPath();
        ctx.arc(x, y, 5, 0, 2 * Math.PI);
        ctx.stroke();
        ctx.closePath();
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
                let halfHeight = h /2;
                ctx.beginPath();
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
                ctx.moveTo(x0, y0);
                ctx.lineTo(x1, y1);
                ctx.lineTo(x2, y2);
                ctx.lineTo(x0, y0);
                ctx.closePath();
                ctx.stroke();                
                break;
            case 2:
            case 3:
            case 4:          
                let tx = x - (sideLength / 2);
                let ty = y - (sideLength / 2);
                ctx.beginPath();
                console.log("Draw a square.", tx, ty, sideLength);
                ctx.rect(tx, ty, sideLength, sideLength);
                ctx.stroke();
                ctx.closePath();
                break;
            case 5:
                console.log("Draw a power supply??");
                break;
        }
        
    }
    setTimeout(function(){setDrag();}, 200);
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

