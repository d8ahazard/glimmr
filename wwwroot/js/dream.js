let dsIp;
let emulationType = "SideKick";
let bridges = [];
let leaves = [];
let devices = [];
let dsDevs = [];
let wleds = [];
let captureMode = 0;
let bridgeInt = 0;
let linking = false;
let resizeTimer;
let loadTimer;
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
let wledIp = "";
let selectedDevice = null;
let ledData = null;
let bridge = null;
let lifx = null;
let deviceData = null;
let targetDs = null;
let datastore = null;
let leftLedCount = 0;
let topLedCount = 0;
let bottomLedCount = 0;
let rightLedCount = 0;
let postResult = null;
let sTarget = [];
let socketLoaded = false;
let allDevices = [];
let canvasTask = null;
let nanoX = 0;
let nanoY = 0;
let tvX = 0;
let tvY = 0;
let nanoTarget = -1;
let nanoSector = -1;
let nanoSectorV2 = -1;
let mode = 0;
let ambientMode = 0;
let ambientShow = 0;
let posting = false;
let colorTimer;

let websocket = new signalR.HubConnectionBuilder()
    .configureLogging(signalR.LogLevel.Information)
    .withUrl("/socket")
    .build();

let pickr = Pickr.create({
    el: '.color-picker',
    container: '.color-picker',
    disabled: false,
    showAlways: true,
    comparison: false,
    inline: true,
    position: "middle",
    theme: 'classic', // or 'monolith', or 'nano'

    swatches: [
        'rgb(255,0,0)',
        'rgb(255,0,79)',
        'rgb(255,0,128)',
        'rgb(255,79,128)',
        'rgb(255,128,128)',
        'rgb(255,0,255)',
        'rgb(53,0,255)',
        'rgb(0,128,255)',
        'rgb(0,255,216)',
        'rgb(0,255,81)',
        'rgb(128,255,0)',
        'rgb(255,255,0)',
        'rgb(255,235,0)',
        'rgb(255,193,0)',
        'rgb(255,79,0)'
    ],

    components: {

        // Main components
        preview: false,
        hue: true,

        // Input / output Options
        interaction: {
            hex: true,
            rgba: true,
            hsla: true,
            hsva: true,
            cmyk: true,
            input: true,
        }
    }
});

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


$(function () {
    setSocketListeners();
    loadSocket();
    setPreviewTimer();
    $('#nanoCard').hide();
    $('#hueCard').hide();
    $('dsCard').hide();
    $('.settingExpand').slideUp();
    // Initialize BMD
    $('body').bootstrapMaterialDesign();
    setListeners();
    $('.devSelect').sortableLists();    
});

function setPreviewTimer() {
    let inputElement = document.getElementById('inputPreview');
    let croppedElement = document.getElementById('outputPreview');
    
    inputElement.src = './img/_preview_input.jpg?rand=' + Math.random();
    croppedElement.src = './img/_preview_output.jpg?rand=' + Math.random();
    
        setInterval(function() {
            let inputElement = document.getElementById('inputPreview');
            let croppedElement = document.getElementById('outputPreview');
            if (captureMode === 0 || mode === 0) {
                inputElement.src = './img/no_preview.jpg';
                croppedElement.src = './img/no_preview.jpg';
            } else {
                inputElement.src = './img/_preview_input.jpg?rand=' + Math.random();
                croppedElement.src = './img/_preview_output.jpg?rand=' + Math.random();
            }
        }, 5000);
}

function sendMessage(endpoint, data, encode=true) {
    if (encode) data = JSON.stringify(data);
    websocket.invoke(endpoint, data).then(
        console.log("Do the damned thang.")
    ).catch(function (err) {
        return console.error(err.toString());
    });
}
    
    

function updateDsProperty(property, value) {
    if (posting) return;
    console.log("Updating DS property: ", property, value);
    if (selectedDevice.hasOwnProperty(property)) {
        console.log("We have a valid property.");
        selectedDevice[property] = value;
        saveSelectedDevice();
        postData("updateDs",{
           id: selectedDevice.id,
           property: property,
           value: value 
        });
    } else {
        console.log("Invalid property name: ", property);
    }
}


function loadSocket() {
    if (socketLoaded) return;
    console.log("Trying to connect to socket.");
    websocket.start().then(function () {
        console.log("Connected.");
        socketLoaded = true;
        loadData();
    }).catch(function (err) {
        console.error(err.toString());
    });
}


function setSocketListeners() {
    websocket.on("ReceiveMessage", function (message) {
        console.log("RecMsg: " + message);
    });
    
    websocket.on("mode", function (mode) {
        console.log("Socket has set mode to " + mode);
        $('.modeBtn').removeClass('active');
        $("#mode" + mode).addClass('active');
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
        let cb = $('#circleBar');

        switch (value) {
            case "start":
                bar.animate(0);
                cb.show();
                break;
            case "stop":                
            case "authorized":
                cb.hide();
                break;
            default:
                if (Number.isInteger(value)) {
                    bar.animate((value / 30));                    
                }
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
        console.log("Socket connected.");
        socketLoaded = true;
    });

    websocket.on('olo', function(stuff) {
        posting = true;
        stuff = stuff.replace(/\\n/g, '');
        let foo = JSON.parse(stuff);
        console.log("Received updated data: ", foo);
        if (!loadTimer) {
            datastore = foo;
            console.log("Building list...");
            buildLists();
            reloadDevice();
            socketLoaded = true;
            refreshing = false;
        } else {
            console.log("Not using updated data because we just loaded...");
            loadTimer = null;
            posting = false;
        }
    });

    websocket.onclose(function() {
        console.log("Disconnected...");
        socketLoaded = false;        
        let i = 0;
        let intr = setInterval(function() {
            loadSocket();
            if (++i >= 100 || socketLoaded) clearInterval(intr);
        }, 5000);
    })
}


function setListeners() {        
    pickr.on('change', instance => {
        clearTimeout(colorTimer);
        colorTimer = setTimeout(function() {
            if (posting) return;
            let colStr = instance.toHEXA()[0] + instance.toHEXA()[1] + instance.toHEXA()[2];
            postData("ambientColor",{device: selectedDevice["Id"], group: selectedDevice["GroupNumber"], color: colStr});    // Do the ajax stuff
        }, 500);
        
    });
    
    $('.devSaturation').on('input', function(){
        let r = $('.devSaturation[data-color="r"]').val();
        let g = $('.devSaturation[data-color="g"]').val();
        let b = $('.devSaturation[data-color="b"]').val();
        let val = $(this).val();
        let col = $(this).data('color');
        if (col === "r") r = val;
        if (col === "g") g = val;
        if (col === "b") b = val;
        let newColor = rgbToHex(r,g,b);
        updateDsProperty("saturation", newColor);
    });
    
    $('.dsSlider').click(function(){
       let checked = $(this).prop('checked');
       updateDsProperty($(this).data('attribute'),checked ? 1 : 0);
       
    });
    
    $('.colorBoost').click(function(){
        let boost = $(this).data('mode');
        $('.colorBoost').removeClass('selected');
        $(this).addClass('selected');
        updateDsProperty("colorBoost", boost);
    });
    
    $('.dsDetection').click(function(){
        mode = $(this).data('mode');
        $('.dsDetection').removeClass('selected');
        $(this).addClass('selected');
        updateDsProperty('letterboxingEnable', parseInt(mode));
    });

    $('.devFadeRate').change(function(){
        let rate = $(this).val();
        updateDsProperty('fadeRate', rate);
    });

    $('.devLuminosity').change(function(){
        let lum = $(this).val();
        updateDsProperty('minimumLuminosity', lum);
    });
    
    $('.hintbtn').click(function(){
        let gp = $(this).parent().parent();
        let hint = gp.find('.hint');
        if (hint.css('display') === 'none') {
            hint.slideDown();
        } else {
            hint.slideUp();
        }
    });

    $('#showSettings').click(function(){
        hidePanels();
        $('.navbar-toggler').click();
        $('#navTitle').html("Settings");
        selectedDevice = null;
        $('#settingsCard').slideDown();
    });

    $('.settingsBtn').click(function () {
        let target = $(this).data('target');
        let tDiv = $("#" + target);
        let sDiv = tDiv.parent().find(".devDisplay");
        sTarget.push(sDiv, tDiv);
        sDiv.slideUp();
        tDiv.slideDown();
        $(this).hide();
        $('#drawerOpen').hide();
        $('#settingsBack').show();
    });

    $('.s1').click(function(){
        let parent = $(this).parent();
        let sTarget2 = $('#' +$(this).data('target'));
        sTarget.push(parent, sTarget2);
        parent.slideUp();
        sTarget2.slideDown();
    });

    $('#settingsBack').click(function() {
        if (sTarget.length >= 2) {
            let toHide = sTarget.pop();
            let toShow = sTarget.pop();
            toHide.slideUp();
            toShow.slideDown();
            if (sTarget.length === 0) {
                $('#drawerOpen').show();
                $('#settingsBack').hide();
                $('.settingsBtn').show();
            }
        }
    });

    $('#refreshDevices').click(function(){
        $('.navbar-toggler').click();
        RefreshData();
    });

    $('#dsName').on('input', function(){

    });

    $("#dsName").blur(function() {
        let nVal = $(this).text();
        selectedDevice["Name"] = nVal;
        postData("updateDevice", selectedDevice);
    });

    $("#wlName").blur(function() {
        let nVal = $(this).text();
        selectedDevice["Name"] = nVal;
        postData("updateDevice", selectedDevice);
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
    
    $('.ambientModeBtn').on('click', function() {
        ambientMode = $(this).data('value');
        toggleAmbientSection();
        let payLoad = {
            id: selectedDevice.id,
            mode: ambientMode
        };
        postData("ambientMode", payLoad);
    });
    
    $('.showBtn').on('click', function() {
       ambientShow = $(this).data('show');
       toggleAmbientSection();
        let payLoad = {
            id: selectedDevice.id,
            scene: ambientShow
        };
        postData("ambientShow", payLoad);
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
        if (flipDir === "h") {
            selectedDevice['mirrorX'] = flipVal;
        } else {
            selectedDevice['mirrorY'] = flipVal;
        }
        postData("updateDevice", selectedDevice);
        setTimeout(function() {
            let newNano = postResult;
            drawNanoShapes(selectedDevice);
        }, 1000);

    });

    // Emulator type change #TODO Post directly
    $(document).on('change', '.emuType', function() {
        deviceData.name = deviceData.name.replace(deviceData.tag, $(this).val());
        deviceData.tag = $(this).val();
        loadDsData();
    });

    // On light map change
    $(document).on('change', '.mapSelect', function() {
        let myId = $(this).attr('id').replace("lightMap", "");
        let newVal = $(this).val().toString();
        if (captureMode === 0) {
            updateLightProperty(myId, "targetSector", newVal);    
        } else {
            updateLightProperty(myId, "targetSectorV2", newVal);
        }
        
    });

    $(window).on('resize', function(e) {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function() {
            if (selectedDevice != null) {
                showDevicePanel(selectedDevice);
            }
        }, 250);

    });

    // On brightness slider change for hue
    $(document).on('change', '.mapBrightness', function() {
        let myId = $(this).attr('id').replace("brightness", "");
        let newVal = $(this).val();
        updateLightProperty(myId, "brightness", newVal);
    });

    // On dev brightness slider change
    $(document).on('change', '.devBrightness', function() {
        selectedDevice.brightness = $(this).val();
        saveSelectedDevice();  
        console.log("SdeviceId: ", selectedDevice["_id"]);
        console.log("MyDevId: ", deviceData["IpAddress"]);
        if (selectedDevice["_id"] === deviceData["IpAddress"]) {
            ledData["Brightness"] = $(this).val();
            postData("updateLed",ledData);
        }
        postData('updateDevice', selectedDevice);
    });

    $('#dsIpSelect').change( function() {
        let dsIp = $(this).val();
        $.each(devices, function() {
            if ($(this)[0].ipAddress === dsIp) {
                targetDs = $(this)[0];
                if (captureMode === 0) {
                    leftLedCount = $(this)[0]["flexSetup"][0];
                    topLedCount = $(this)[0]["flexSetup"][1];
                    $('#leftCount').val(leftLedCount);
                    $('#topCount').val(topLedCount);
                }
            }
        });
        postData("dsIp", dsIp);
    });

    $('#cameraType').change(function() {
        let cType = $(this).val();
        postData('camType', cType);
    });

    $('#stripType').change(function() {
        console.log("STRIP CHANGE.");
        let cType = $(this).val();
        postData('stripType', cType);
    });
    
    $(document).on('change', '.ledCount', function() {
        console.log("LED COUNT CHANGE.")
        let lCount = $(this).val();
        let type = $(this).data('type');
        switch(type) {
            case "topCount":
                topLedCount = lCount;
                break;
            case "leftCount":
                leftLedCount = lCount;
                break;
            case "rightCount":
                rightLedCount = lCount;
                break;
            case "bottomCount":
                bottomLedCount = lCount;
                break;
        }
        ledData["LeftCount"] = leftLedCount;
        ledData["RightCount"] = rightLedCount;
        ledData["TopCount"] = topLedCount;
        ledData["BottomCount"] = bottomLedCount;

        postData("updateLed", ledData);
    });

    // On Override click
    $(document).on('click', '.overrideBright', function() {
        let myId = $(this).attr('id').replace("overrideBrightness", "");
        let newVal = ($(this).val() === "on");
        updateLightProperty(myId, "overrideBrightness", newVal);
    });

    $(document).on('change', '.wlInput', function() {
        let property = $(this).data("value");
        let newVal = $(this).val();
        console.log("We should update wled property to ",property, newVal);
        if (property === "count" || property === "rgbw") {
            if (property === "count") {
                selectedDevice["LedCount"] = newVal;
            }
            selectedDevice["State"]["info"]["leds"][property] = newVal;
        } else {
            selectedDevice[property] = newVal;
        }
        postData("updateDevice", selectedDevice);
    });


    // On Device Click
    $(document).on('click', '.devSelect', function (event) {
        let id = $(this).data('device');
        $.each(devices, function() {
            if ($(this)[0]['_id'] === id) {
                showDevicePanel($(this)[0]);
            }
        });
        $('.navbar-toggler').click();
        event.stopPropagation();
    });

    

    $('.modeBtn').click(function () {
        $(".modeBtn").removeClass("active");
        $(this).addClass('active');
        mode = $(this).data('mode');
        let id = selectedDevice.id;
        selectedDevice.mode = mode;
        saveSelectedDevice();
        toggleAmbientSection();
        postData("mode", {
            id: id,
            mode: mode,
            tag: selectedDevice.tag
        });
    });

    $('.dsModeBtn').click(function () {
        $(".dsModeBtn").removeClass("active");
        $(this).addClass('active');
        mode = $(this).data('mode');
        let id = selectedDevice.id;
        selectedDevice.mode = mode;
        saveSelectedDevice();
        toggleAmbientSection();
        postData("mode", {
            id: id,
            mode: mode,
            tag: selectedDevice.tag
        });
    });


    $('.lifxRegion').click(function() {
        if (!$(this).hasClass('checked')) {
            $('.lifxRegion').removeClass('checked');
            $(this).addClass('checked');
            let val =$(this).data('region');
            selectedDevice.targetSector = val;
            postData("updateDevice", selectedDevice);
        }
    });

    $('.lifxRegionV2').click(function() {
        if (!$(this).hasClass('checked')) {
            $('.lifxRegionV2').removeClass('checked');
            $(this).addClass('checked');
            let val =$(this).data('region');
            selectedDevice.targetSectorV2 = val;
            postData("updateDevice", selectedDevice);
        }
    });

    $('.nanoRegion').click(function() {
        if (!$(this).hasClass('checked')) {
            $('.nanoRegion').removeClass('checked');
            $(this).addClass('checked');
            let val=$(this).data('region');
            nanoSector = val;
            let sTarget = -1;
            let sectors = selectedDevice["layout"]["positionData"];
            for (let q=0; q < sectors.length; q++) {
                if (sectors[q]["panelId"] === nanoTarget) {
                    sTarget = q;
                }
            }
            
            if (sTarget !== -1) {
                selectedDevice["layout"]["positionData"][sTarget]["targetSector"] = nanoSector;
                postData("updateDevice", selectedDevice);    
            }
            $('#nanoModal').modal('toggle');            
        }
    });

    $('.nanoRegionV2').click(function() {
        if (!$(this).hasClass('checked')) {
            $('.nanoRegionV2').removeClass('checked');
            $(this).addClass('checked');
            let val=$(this).data('region');
            nanoSectorV2 = val;
            let sTarget = -1;
            let sectors = selectedDevice["layout"]["positionData"];
            for (let q=0; q < sectors.length; q++) {
                if (sectors[q]["panelId"] === nanoTarget) {
                    sTarget = q;
                }
            }

            if (sTarget !== -1) {
                selectedDevice["layout"]["positionData"][sTarget]["targetSectorV2"] = nanoSectorV2;
                postData("updateDevice", selectedDevice);
            }
            $('#nanoModal').modal('toggle');
        }
    });


    // On group selection change
    $('.dsGroup').change(function () {
        const id = $(this).val();
        hueGroup = id;
        bridges[bridgeInt]["SelectedGroup"] = id;
        postData("updateDevice", bridges[bridgeInt]);
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

}

function toggleAmbientSection() {
    if (mode === 3) {
        $('#ambientDiv').slideDown();
        $('.ambientModeBtn').removeClass("selected");
        $('.ambientModeBtn[data-value="'+ambientMode+'"]').addClass("selected");
        if (ambientMode === 1) {
            $('#ambientColorDiv').slideUp();
            $('#ambientSceneDiv').slideDown();
            $(".showBtn").removeClass("selected");
            $('.showBtn[data-show="'+ambientShow+'"]').addClass("selected");
        } else {
            $('#ambientColorDiv').slideDown();
            $('#ambientSceneDiv').slideUp();
        }
    } else {
        $('#ambientDiv').slideUp();
    }
}

// This gets called in loop by hue auth to see if we've linked our bridge.
function checkHueAuth() {    
    $.get("./api/DreamData/action?action=authorizeHue&value=" + hueIp, function (data) {
        if (data.key !== null && data.key !== undefined) {
            hueAuth = true;
            if (hueAuth) {
                loadBridgeData(data);
            }
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

// Update our stored setting data for various light values
function updateLightProperty(myId, propertyName, value) {
    console.log("Updating light " + propertyName + " for " + myId, value);
    for(let k in hueLights) {
        if (hueLights.hasOwnProperty(k)) {
            hueLights[k]["Id"] = parseInt(hueLights[k]["_id"]);
            if (hueLights[k]["_id"] === myId) {
                hueLights[k]["Id"] = parseInt(myId);
                hueLights[k][propertyName] = value;
            }
        }
    }    
    console.log("Updated light data: ", hueLights);
    bridges[bridgeInt]["Lights"] = hueLights;
    let fGroup = bridges[bridgeInt]["Groups"];
    let nGroup = [];
    for (let g in fGroup) {
        if (fGroup.hasOwnProperty(g)) {
            fGroup[g]["Id"] = fGroup[g]["_id"];
            nGroup.push(fGroup[g]);    
        }
        
    }
    bridges[bridgeInt]["Groups"] = nGroup;
    postData("updateDevice", bridges[bridgeInt]);
}

// Update our pretty table so we can map the lights
function mapLights() {
    let group = findGroup(hueGroup);
    if (!group) return;
    let lights = hueLights;
    console.log("Mapping lights: ", lights);
    // Get the main light group
    const lightGroup = document.getElementById("mapSel");
    // Clear it
    $('div').remove('.delSel');
    // Clear the light region checked status
    $('.lightRegion').removeClass("checked");
    $('.lightRegionV2').removeClass("checked");
    // Get the list of lights for the selected group
    if (!group.hasOwnProperty('Lights')) return false;
    const ids = group["Lights"];
    
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
            if ($.inArray(id, ids) !== -1) {
                const name = light['Name'];
                let brightness = light["Brightness"];
                let override = light["OverrideBrightness"];
                let selection = light["TargetSector"];
                let selectionV2 = light["TargetSectorV2"];

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
                    $('.lightRegion[data-region="' + selection +'"]').addClass('checked');
                    $('.lightRegionV2[data-region="' + selectionV2 +'"]').addClass('checked');
                }
                newSelect.appendChild(opt);

                // Add the options for our regions
                if (captureMode === 0) {
                    for (let i = 1; i < 13; i++) {
                        opt = document.createElement("option");
                        opt.value = (i).toString();
                        opt.innerHTML = "<BR>" + (i);
                        // Mark it selected if it's mapped
                        if (selection === i) opt.setAttribute('selected', 'selected');
                        newSelect.appendChild(opt);
                    }
                } else {
                    for (let i = 1; i < 29; i++) {
                        opt = document.createElement("option");
                        opt.value = (i).toString();
                        opt.innerHTML = "<BR>" + (i);
                        // Mark it selected if it's mapped
                        if (selectionV2 === i) opt.setAttribute('selected', 'selected');
                        newSelect.appendChild(opt);
                    }
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
        if (hueGroups !== null && hueGroups !== undefined) {
            if (hueGroup === -1 && hueGroups.length > 0) {
                hueGroup = hueGroups[0]["_id"];
            }
            $.each(hueGroups, function() {
                let val = $(this)[0]["_id"];
                let txt = $(this)[0]["Name"];
                let selected = (val === hueGroup) ? " selected" : "";
                gs.append(`<option value="${val}" ${selected}>${txt}</option>`);
                i++;
            });
        }
    }
}


function saveSelectedDevice() {
    for (let q = 0; q < devices.length; q++) {
        let tDev = devices[q];
        if (tDev.id === selectedDevice.id) {
            devices[q] = selectedDevice;
        }
    }
}


function loadData() {
    if (socketLoaded) {
        $.get("./api/DreamData/action?action=loadData");
    } else {
        $.get("./api/DreamData/action?action=loadData", function (data) {
            console.log("Dream data: ", data);
            datastore = data;
            buildLists();
            RefreshData();
        });
    }
}

function RefreshData() {
    if (!refreshing) {
        refreshing = true;
        console.log("Refreshing data.");
        if (socketLoaded) {
            $.get("./api/DreamData/action?action=refreshDevices");            
        } else {
            $.get("./api/DreamData/action?action=refreshDevices", function (data) {
                datastore = data;
                buildLists();
                refreshing = false;
            });
        }
    }
}

function buildLists() {
    let dg = $('#devGroup');
    dsDevs = [];
    let groups = [];
    console.log("Reading datastore...");
    devices = datastore['Dev_DreamScreen'];
    leaves = datastore['Dev_NanoLeaf'];
    bridges = datastore['Dev_Hue'];
    wleds = datastore['Dev_Wled'];
    deviceData = datastore['MyDevice'][0];
    lifx = datastore['Dev_Lifx'];
    dsIp = datastore['DsIp'][0]["value"];
    ledData = datastore['LedData'][0];
    captureMode = datastore['CaptureMode'][0]["value"];
    console.log("CapMode: " + captureMode);
    if (captureMode === 0) {
        $('.regions').show();
        $('.regionsV2').hide();
    } else {
        $('.regions').hide();
        $('.regionsV2').show();
    }
    mode = selectCaptureMode(captureMode);
    emulationType = datastore['DevType'][0]["value"];
    buildDevList(datastore['Dev_DreamScreen']);
    setCaptureMode(mode, false);
    console.log("Capmodes set...");
    setMode(deviceData.mode);
    console.log("Mode set.");
    // Push dreamscreen devices to groups first, so they appear on top. The, do sidekicks, nanoleaves, then bridges.
    $.each(devices, function() {
        console.log("Looping for device: ", $(this)[0]);
        let item = $(this)[0];
        if (item === null || item === undefined) return;
        if (item['Id'] === undefined && item['IpAddress'] !== undefined) item['Id'] = item['IpAddress'];
        if (this["Tag"].includes("DreamScreen")) {
            console.log("DS");
            let groupNumber = (item['GroupNumber'] === undefined) ? 0 : item['GroupNumber'];
            let groupName = (item['GroupName'] === undefined) ? "undefined" : item['GroupName'];
            let ambientColor = (item['AmbientColor'] === undefined) ? "FFFFFF" : item['AmbientColor'];
            let ambientShow = (item['AmbientShowType'] === undefined) ? 0 : item['AmbientShowType'];
            let ambientMode = (item['AmbientModeType'] === undefined) ? 0 : item['AmbientModeType'];
            let mode = (item['Mode'] === undefined) ? 0 : item['Mode'];
            let brightness = (item['Brightness'] === undefined) ? 0 : item['Brightness'];
            if (groups[groupNumber] === undefined) {
                groups[groupNumber] = {};
                groups[groupNumber]['Name'] = groupName;
                groups[groupNumber]['Id'] = groupNumber;
                groups[groupNumber]['Items'] = [];
                groups[groupNumber]['AmbientColor'] = ambientColor;
                groups[groupNumber]['AmbientShowType'] = ambientShow;
                groups[groupNumber]['AmbientModeType'] = ambientMode;
                groups[groupNumber]['Mode'] = mode;
                groups[groupNumber]['Brightness'] = brightness;
            }
            groups[groupNumber]['Items'].push(item);
        } else {
            dsDevs.push(item);
        }
    });
    console.log("Sorting devices...");
    const sorted = [];
    // Sort other DS Devices
    allDevices = [];
    groups = sortDevices(dsDevs, groups, false, false);
    console.log("DS");
    // Sort nanoleaves
    groups = sortDevices(leaves, groups, "NanoLeaf", "NanoLeaf");
    console.log("Nano");
    // Sort bridges
    groups = sortDevices(bridges, groups, "HueBridge", "Hue Bridge");
    console.log("Hue");
    // Sort lifx
    groups = sortDevices(lifx, groups, "Lifx", "Lifx Bulb");
    console.log("Lifx");
    // Sort wled
    groups = sortDevices(wleds, groups, "WLed", "Wireless LEDs")
    console.log("wled");
    dg.html("");
    console.log("Final sorted groups: ", groups);
    $.each(groups, function () {
        let item = $(this)[0];
        console.log("Appending: ", item);
        if (item['ScreenX'] === undefined) {
            if (item['Id'] > 0) {
                sorted.push(item);
            } else {
                appendDeviceGroup(item);
            }
        }
    });
    console.log("Starting to append groups");
    
    dg.append($('<li class="spacer">Groups</li>'));

    if (sorted.length > 0) {
        $.each(sorted, function () 
        {
            console.log("Appending: ",$(this)[0]);
            appendDeviceGroup($(this)[0]);
        })
    }
    
}


function appendDeviceGroup(item) {
    let name = item['Name'];
    let elements = item['Items'];
    let devGroup = $('#devGroup');
    // This is not a group
    if (item['id'] === 0) {
        $.each(elements, function () {
            let element = $(this)[0];
            if (element['Id'] === undefined) element['Id'] = element['BridgeId'];
            if (element['Id'] === undefined) element['Id'] = element['IpAddress'];
            if (element['Id'] === undefined) element['Id'] = element['IpV4Address'];
            devices.push(element);
            devGroup.append('<li class="devSelect" data-device="' + element["Id"] + '"><img class="devIcon" src="./img/' + element["Tag"].toLowerCase() + '_icon.png" alt="device icon"><span class="devName">' + element["Name"] + '<span></li>');
        });
    } else {        
        let list = $('<li  class="nav-header groupHeader devSelect" data-device="' + item['Id'] + '"></li>');
        list.append($('<img src="./img/group_icon.png" class="devIcon" alt="group icon">'));
        list.append($('<span class="devName">' + name + '</span>'));
        let container = $('<ul id="group' + item['_id'] + '" class="nav-list groupList"></ul>');
        $.each(elements, function () {
            let element = $(this)[0];
            if (element["Tag"].includes("DreamScreen")) {
                item.Mode = element.Mode;
                item.Brightness = element.Brightness;
                item.Saturation = element.Saturation;
            }
            devices.push(element);
            container.append('<li class="devSelect" data-device="' + element._id + '"><img class="devIcon" src="./img/' + element["Tag"].toLowerCase() + '_icon.png" alt="device icon"><span class="devName">' + element.Name + '<span></li>');
        });
        item['Tag'] = "group";
        item['GroupNumber'] = item['Id'];
        item['GroupName'] = item['Name'];
        item.ipAddress = "255.255.255.0";
        devices.push(item);
        list.append(container);
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

function setMode(devMode) {
    $('.modeBtn').removeClass('active');
    $('#mode' + devMode).addClass('active');
}

function setCaptureMode(target, post=true) {
    if (target === "dsCapPane") {
        captureMode = 0;
    } else if (target === "cameraCapPane") {
        captureMode = 1;
    } else if (target === "hdmiCapPane") {
        captureMode = 2;
    } else if (target === "screenCapPane") {
        captureMode = 3;
    }
    if (post) postData("capturemode", captureMode);
    $('.capModeBtn.active').removeClass('active');
    $('#' + target + 'Btn').addClass('active');
    let topCount = 0;
    let leftCount = 0;
    let rightCount = 0;
    let bottomCount = 0;
    if (captureMode === 0 && ledData.hasOwnProperty("HCountDs") && ledData.hasOwnProperty("VCountDs")) {
        topCount = ledData.HCountDs;
        leftCount = ledData.VCountDs;
    } else if (ledData.hasOwnProperty("TopCount") && ledData.hasOwnProperty("LeftCount")) {
        topCount = ledData.TopCount;
        leftCount = ledData.LeftCount;
        rightCount = ledData["RightCount"]
        bottomCount = ledData["BottomCount"];        
    }
    if (rightCount === 0) rightCount = leftCount;
    if (bottomCount === 0) bottomCount = topCount;
    let stripType = 0;
    if (ledData.hasOwnProperty("stripType")) stripType = ledData.StripType;
    leftLedCount = leftCount;
    topLedCount = topCount;
    bottomLedCount = bottomCount;
    rightLedCount = rightCount;
    let hc = $('#leftCount');
    let vc = $('#topCount');
    let bc = $('#bottomCount');
    let rc = $('#rightCount');
    $('#stripType').val(stripType);
    hc.val(topLedCount);
    vc.val(leftLedCount);
    rc.val(rightLedCount);
    bc.val(bottomLedCount);
    hc.parent().addClass("is-filled");
    vc.parent().addClass("is-filled");
    $('.capPane').slideUp();
    $('#' + target).slideDown();
}

function sortDevices(data, groups, tag, name) {    
    $.each(data, function () {        
        let item = $(this)[0];
        allDevices.push(item);
        let gn = item['GroupNumber'];
        let gName = item['GroupName'];
        let groupNumber = (gn === undefined || gn === null || gn === -1) ? 0 : gn; 
        let groupName = (gName === undefined || gName === null || groupNumber === 0) ? "Unsorted" : gName;
        if (groups[groupNumber] === undefined) {
            groups[groupNumber] = {};
            groups[groupNumber]['Name'] = groupName;
            groups[groupNumber]['Id'] = groupNumber;
            groups[groupNumber]['Items'] = [];
        }
        if (tag !== false) item.Tag = tag;
        if (name !== false) {
            if (item.Name === undefined || item.Name === null) {
                item.Name = name;
            }
        }
        groups[groupNumber]['Items'].push(item);
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
        const name = dev.Name;
        const ip = dev["IpAddress"];
        const type = dev["Tag"];
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
    let lifxCard = $('#lifxCard');
    let wledCard = $('#wledCard');
    let settingsCard = $('#settingsCard');
    nanoCard.slideUp();
    hueCard.slideUp();
    dsCard.slideUp();
    lifxCard.slideUp();
    wledCard.slideUp();
    settingsCard.slideUp();
}

function showDevicePanel(data) {
    console.log("Showing panel data: ", data);
    let nanoCard = $('#nanoCard');
    let hueCard = $('#hueCard');
    let dsCard = $('#dsCard');
    let lifxCard = $('#lifxCard');
    let wledCard = $('#wledCard');
    let modeGroup = $(".modeGroup");
    selectedDevice = data;
    if (!resizeTimer) hidePanels();
    loadTimer = setTimeout(function(){
        let title = data.Tag;
        $('#navTitle').html(title);
        
        switch (data.Tag) {
            case "SideKick":
            case "Connect":
            case "DreamScreen":
            case "DreamScreen4K":
            case "DreamScreenSolo":
            case "group":
                loadDsData(data);
                modeGroup.hide();
                if (!resizeTimer) dsCard.slideDown();
                break;
            case "HueBridge":
                loadBridgeData(data);
                modeGroup.show();
                if (!resizeTimer) hueCard.slideDown();
                break;
            case "NanoLeaf":
                loadNanoData(data);
                modeGroup.show();
                if (!resizeTimer) nanoCard.slideDown();
                break;
            case "Lifx":
                loadLifxData(data);
                modeGroup.show();
                if (!resizeTimer) lifxCard.slideDown();
                break;
            case "WLed":
                loadWledData(data);
                modeGroup.show();
                if (!resizeTimer) wledCard.slideDown();
                break;
        }

        resizeTimer = null;
    },200);
}

function reloadDevice() {
    let id = selectedDevice["Id"];
    let data = null;
    for (let q=0; q < allDevices.length; q++ ) {
        if(allDevices[q]["_id"] === selectedDevice["_id"]) {
            data = allDevices[q];
        }
    }
    console.log("Reloading panel data: ", data);
    if (data != null) {
        $('#navTitle').html(data["Tag"]);
        selectedDevice = data;
        switch (data["Tag"]) {
            case "SideKick":
            case "Connect":
            case "DreamScreen":
            case "DreamScreen4K":
            case "DreamScreenSolo":
            case "group":
                loadDsData(data);
                break;
            case "HueBridge":
                loadBridgeData(data);
                break;
            case "NanoLeaf":
                loadNanoData(data);
                break;
            case "Lifx":
                loadLifxData(data);
                break;
        }
    } else {
        console.log("ERROR FETCHING DATA, FOO.");
    }
    posting = false;
}

// Update the UI with emulator device data
function loadDsData(data) {
    let dsName = $('#dsName');
    deviceData = data;
    dsName.html(deviceData["Name"]);
    dsName.data("ip", deviceData["IpAddress"]);
    dsName.data('group', deviceData["GroupNumber"]);
    $('#dsBrightness').val(deviceData["Brightness"]);
    $('#dsIp').html(deviceData["IpAddress"]);
    emulationType = deviceData["Tag"];
    let satVal = hexToRgb(deviceData["Saturation"]);
    $('.devSaturation[data-color="r"]').val(satVal.r);
    $('.devSaturation[data-color="g"]').val(satVal.g);
    $('.devSaturation[data-color="b"]').val(satVal.b);
    let settingTarget = "dsSettings";
    if (emulationType === "SideKick") {
        settingTarget = "sidekickSettings";
    } else if (emulationType === "Connect") {
        settingTarget = "ConnectSettings";
    } else {
        $('.colorBoost').removeClass('selected');
        $('.colorBoost[data-mode="' + deviceData['ColorBoost'] + '"').addClass('selected');
        $('.dsDetection').removeClass('selected');
        $('.dsDetection[data-mode="' + deviceData['LetterboxingEnable'] + '"]').addClass('selected');
        $('.devFadeRate').val(deviceData['fadeRate']);
        if (deviceData.hasOwnProperty('MinimumLuminosity')) $('.devLuminosity').val(deviceData['MinimumLuminosity'][0]);
        let boolProps = ['CecPassthroughEnable', 'CecPowerEnable','CecSwitchinEnable','HpdEnable','UsbPowerEnable','HdrToneRemapping'];
        for (let i=0; i< boolProps.length; i++) {
            let prop = boolProps[i];
            $('.dsSlider[data-attribute="'+prop+'"]').prop('checked',deviceData[prop] === 1);
        }
    }
    $('#dsSettingsBtn').data('target',settingTarget);
    $('#dsType').html();
    let modestr = "";
    $('.dsModeBtn').removeClass('active');
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
    mode = deviceData.mode;
    ambientMode = deviceData['AmbientModeType'];
    ambientShow = deviceData['AmbientShowType'];
    let ambientColor = deviceData['AmbientColor'];
    if (ambientColor !== null) {
        let set = pickr.setColor('#' + ambientColor);
    }
    toggleAmbientSection();
    $('#dsMode' + deviceData.mode).addClass('active');
    $('#iconWrap').removeClass().addClass(emulationType);
}

// Update UI with specific bridge data
function loadBridgeData(data) {
    // Get our UI elements
    const hIp = $('#hueIp');
    const lImg = $('#linkImg');
    const lHint = $('#linkHint');
    const lBtn = $('#linkBtn');
    const bSlider = $('#hueBrightness');
    let b = data;
    hueIp = b["_id"];
    bSlider.val(b["Brightness"]);
    hIp.html(b["IpAddress"]);        
    hueGroup = b["SelectedGroup"];
    if (b.hasOwnProperty("Groups")) {
        hueGroups = b["Groups"];
        if ((hueGroup == -1 && hueGroups.length > 0) || hueGroup === null || hueGroup === undefined) {
            if (hueGroups[0] !== undefined) {
                hueGroup = hueGroups[0]["_id"];
                bridges[bridgeInt]["SelectedGroup"] = hueGroup;
                postData("updateData", bridges[bridgeInt]);
            }
        }
    }
    hueLights = b["Lights"];
    hueAuth = (b["User"] !== null && b["Key"] !== null && b["Key"] !== undefined);
    console.log("Hue Auth: ", hueAuth);
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
    listGroups();
    mapLights();
}

function hexToRgb(hex) {
    let result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
        r: parseInt(result[1], 16),
        g: parseInt(result[2], 16),
        b: parseInt(result[3], 16)
    } : null;
}

function rgbToHex(r, g, b) {
    let rs = Number(r).toString(16);
    let gs = Number(g).toString(16);
    let bs = Number(b).toString(16);
    if (rs.length < 2) rs = "0" + rs;
    if (gs.length < 2) gs = "0" + gs;
    if (bs.length < 2) bs = "0" + bs;
    return rs + gs + bs;
}

// Load nanoleaf data
function loadNanoData(data) {
    // Get our UI elements
    const hIp = $('#nanoIp');
    const lImg = $('#nanoImg');
    const lHint = $('#nanoHint');
    const lBtn = $('#nanoBtn');
    const nBrightness = $("#nanoBrightness");
    // This is our bridge. There are many others like it...but this one is MINE.
    // Now we've got it.
    let n = data;
    nBrightness.val(n["Brightness"]);
    nanoIp = n["IpV4Address"];
    
    hIp.html(n["IpV4Address"]);    
    nanoAuth = (n["Token"] !== null && n["Token"] !== undefined);
    lImg.removeClass('linked unlinked linking');
    if (nanoAuth) {
        lImg.addClass('linked');
        lHint.html("Your Nanoleaf is linked.");
        lBtn.css('cursor', 'default');
        if (nanoX !== data.x && nanoY !== data.y) {            
            nanoX = data.x;
            nanoY = data.y;
        }
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
}

function loadLifxData(data) {
    // Get our UI elements
    const hIp = $('#lifxIp');
    const lName = $('#lifxName');
    const lBrightness = $("#lifxBrightness");
    let n = data;
    nanoIp = n["HostName"];
    lName.html(nanoIp);
    hIp.html(data.Id);
    $('.lifxRegion').removeClass('checked');
    $('.lifxRegion[data-region="' + data.TargetSector +'"]').addClass('checked');
    $('.lifxRegionV2').removeClass('checked');
    $('.lifxRegionV2[data-region="' + data.TargetSectorV2 +'"]').addClass('checked');
    lBrightness.val(data["Brightness"]);    
}

function loadWledData(data) {
    // Get our UI elements
    const hIp = $('#wledIp');
    const wName = $('#wledName');
    const wBrightness = $("#wledBrightness");
    let dsName = $('#wlName');
    deviceData = data;
    dsName.html(deviceData["Name"]);
    dsName.data("id", deviceData["IpAddress"]);
    wledIp = data["HostName"];
    wName.html(wledIp);
    hIp.html(data.Id);
    $('.wledRegion').removeClass('checked');
    $('.wledRegion[data-region="' + data.TargetSector +'"]').addClass('checked');
    $('.wledRegionV2').removeClass('checked');
    $('.wledRegionV2[data-region="' + data.TargetSectorV2 +'"]').addClass('checked');
    $('#whCount').val(data["HCount"]);
    $('#wvCount').val(data["VCount"]);
    let ledCount = data["State"]["info"]["leds"]["count"];
    let isRgbw = data["State"]["info"]["leds"]["rgbw"];
    wBrightness.val(data["State"]["state"]["bri"]);
    $("#wstripType").val(isRgbw);
    $("#wOffset").val(data["Offset"]);
    $("#wDirection").val(data["StripDirection"]);
    $("#wTotal").val(ledCount);
}

function drawNanoShapes(panel) {
    // Wipe it out
    $('#canvasDiv').remove();
    $('#nanoContainer').append('<div id="canvasDiv"></div>');

    let snaps = [];
    for (let q = 0; q <= 360; q+=10) {
        snaps.push(q);
    }

    // Get window width
    let width = window.innerWidth;
    let height = width * .5625;
    
    // Get layout data from panel
    let pX = panel['X'];
    let pY = panel['Y'];
    let pScale = panel['Scale'];
    let pRot = panel['Rotation'];
    let mirrorX = panel['MirrorX'];
    let mirrorY = panel['MirrorY'];    
    let layout = panel['Layout'];
    let sideLength = layout['SideLength'];

    // Set our TV image width
    let tvWidth = topLedCount * 25;
    let tvHeight = leftLedCount * 25;

    // If window is less than 500px, divide our scale by half
    let halfScale = false;
    if (width < 500) {
        halfScale = 2;
        pScale /= 4;
        tvWidth /= 4;
        tvHeight /= 4;     
        pX /= 2;
        pY /= 2;
    } else {
        tvWidth /= 2;
        tvHeight /= 2;
        pScale /= 2;
    }
    
    // Determine TV x/y position
    tvX = (width - tvWidth) / 2;
    tvY = (height - tvHeight) / 2;
    pX += tvX;
    pY += tvY;
    // Create our stage
    let stage = new Konva.Stage({
        container: 'canvasDiv',
        width: width,
        height: height
    });
    
    // Shape layer
    let cLayer = new Konva.Layer();
    stage.add(cLayer);

    // Group for the shapes
    let shapeGroup = new Konva.Group({       
        rotation: pRot,
        draggable: true,
        scaleX: pScale,
        scaleY: pScale
    });

    cLayer.add(shapeGroup);
    
    // Transform for scaling
    let tr2 = new Konva.Transformer({
        keepRatio: true,
        resizeEnabled: false,
        rotationSnaps: snaps
    });

    cLayer.add(tr2);
    // Attach to group
    tr2.attachTo(shapeGroup);
    tr2.zIndex(0);

    cLayer.draw();

    // Drag listener
    shapeGroup.on('dragend', function(e) {
        if (canvasTask !== null) {
            clearTimeout(canvasTask);
        }
        canvasTask = setTimeout(function(){
            doTheThing();    
        }, 500);            
            
    });
    
   
    
    // Transform values and post them
    function doTheThing() {
        // Get the top-left x,y coordinates
        let gX = shapeGroup.x() - tvX;
        let gY = shapeGroup.y() - tvY;
        if (halfScale) {
            gX*=2;
            gY*=2;
        }
        if (nanoX !== gX || nanoY !== gY) {
            nanoX = gX;
            nanoY = gY;
        } else {
            return;
        }
        selectedDevice.X = gX;
        selectedDevice.Y = gY;
        selectedDevice.Scale = 1;
        selectedDevice.Rotation = shapeGroup.Rotation();
        saveSelectedDevice();
        postData("updateDevice", selectedDevice);
    }
    
    
    let positions = layout['positionData'];
    let minX = 1000;
    let minY = 1000;
    let maxX = 0;
    let maxY = 0;
    
    // Calculate the min/max range for each tile
    for (let i=0; i< positions.length; i++) {
        let data = positions[i];
        if (data.x < minX) minX = data.x;
        if ((data.y * -1) < minY) minY = (data.y * -1);
        if (data.x > maxX) maxX = data.x;
        if ((data.y * -1) > maxY) maxY = (data.y * -1);
    }
    
    for (let i=0; i < positions.length; i++) {
        let data = positions[i];
        let shape = data['shapeType'];
        let x = data.x;
        let y = data.y;
        if (mirrorX) x *= -1;
        if (!mirrorY) y *= -1;
        //x += Math.abs(minX / 2);
        //y += Math.abs(minY / 2);
        
        let sText = new Konva.Text({
            x: x,
            y: y,
            text: data["PanelId"],
            fontSize: 30,
            fontFamily: 'Calibri'
        });
        
        let sectorText = data['TargetSector'];
        if (captureMode !== 0) {
            sectorText = data['TargetSectorV2'];
        }
        let sText2 = new Konva.Text({
            x: x,
            y: y - 35,
            text: sectorText,
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
                    closed: true,
                    id: data["panelId"]
                });
                poly.on('click', function(){
                    setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
                });
                poly.on('tap', function(){
                    console.log("POLY TAP")
                    setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
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
                    setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
                });
                rect1.on('tap', function(){
                    setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
                });
                shapeGroup.add(rect1);
                break;
            case 5:
                console.log("Draw a power supply??");
                break;
        }
        sText.offsetX(sText.width() / 2);
        sText2.offsetX(sText2.width() / 2);
        sText.on('click', function(){
            setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
            
        });
        sText2.on('click', function(){
            setNanoMap(data['PanelId'], data['TargetSector'], data['TargetSectorV2']);
        });
        sText.on('tap', function () {
            console.log("Stext tap.")
        });
        sText2.on('tap', function () {
            console.log("Stext2 tap.")
        });
        shapeGroup.add(sText);
        shapeGroup.add(sText2);
    }
    
    // add the layer to the stage
    tr2.forceUpdate();
    
    shapeGroup.x(pX);
    shapeGroup.y(pY);
    cLayer.draw();
    let iLayer = new Konva.Layer();
    stage.add(iLayer);
    
    // Add the tv image
    Konva.Image.fromURL('../img/sectoring_tv2.png', function(tvImage) {
        tvImage.setAttrs({
            x: tvX,
            y: tvY,
            width: tvWidth,
            height: tvHeight
        });
        iLayer.add(tvImage);
        iLayer.zIndex(0);
        iLayer.batchDraw();
    });
    cLayer.zIndex(0);
}

function setNanoMap(id, current, v2) {
    nanoTarget = id;
    nanoSector = current;
    nanoSectorV2 = v2;
    // Add the options for our regions
    $('.nanoRegion').removeClass('checked');
    if (current !== -1) {
        $('.nanoRegion[data-region="'+current+'"]').addClass("checked");
    }
    $('.nanoRegionV2').removeClass('checked');
    if (v2 !== -1) {
        $('.nanoRegionV2[data-region="'+v2+'"]').addClass("checked");
    }
    $('#nanoModal').modal({
        show: true
    });
}

// Get a group by group ID
function findGroup(id) {
    let res = false;
    if (hueGroups === null || hueGroups === undefined) return res;
    if (id === -1 || id === "-1" || id === null) {
        return hueGroups[0];    
    }
    $.each(hueGroups, function () {
        if (id === $(this)[0]["_id"]) {
            res = $(this)[0];
        }        
    });
    return res;
}

// Runs a loop to detect if our hue device is linked
function linkHue() {
    if (!hueAuth && !linking) {
        linking = true;
        $('#circleBar').show();
       
        let x = 0;
        hueAuth = false;
        if (socketLoaded) {
            console.log("Trying to auth using ip: " + hueIp);
            websocket.invoke("AuthorizeHue", hueIp);    
        } else {
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
        }
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
        
        let x = 0;
        nanoAuth = false;
        const intervalID = window.setInterval(function () {
            if (!nanoAuth) checkNanoAuth();
            bar.animate((x / 30));
            if (x++ === 30 || nanoAuth) {
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

