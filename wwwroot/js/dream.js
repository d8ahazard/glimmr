var syncToggle;
var hueGroups;
var hueLights;
var lightMap;
var hueGroup;
var dsIp;
var hueAuth = false;
var linking = false;

$(function () {

    
    $('#settingsForm').submit(function (e) {
        e.preventDefault();
        var data = $(this).serialize();
        $.ajax({
            url: "./api/DreamData",
            type: "POST",
            data: data,
            success: function (data) {
                console.log("Posted!", data);
                fetchJson();
            }
        }); 
    });

    $('.linkBtn').on('click', function () {
        linkHue();        
    });

    $('#ds_find').on('click', function() {
        console.log("Trying to find dreamscreen.");
        $.get("./api/DreamData/action?action=connectDreamScreen", function (data) {
            if (data.indexOf("Success: ") !== -1) {
                var result = $.trim(data.replace("Success: ", ""));
                if (result !== "0.0.0.0") {                    
                    $('#ds_ip').val(result);
                    $('#ds_find').hide();
                } else {
                    $('#ds_find').show();
                }
                alert(data);
            }            
        });
    });

    $('.modeBtn').click(function () {
        $(".modeBtn").removeClass("active");
        $(this).addClass('active');
        var mode = $(this).data('mode');
        $.ajax({
            type: "POST",
            contentType: "application/json",
            url: "./api/DreamData/mode/",
            dataType: "json",
            data: JSON.stringify(mode),
            success: function (response) {
                console.log("Mode is " + response);
            }
        });
    });

    $('.dsGroup').change(function () {
        var id = $(this).val();
        var newGroup = findGroup(id);
        if (newGroup) {
            hueGroup = newGroup;
            mapLights(newGroup, lightMap, hueLights);
        }
    });

    $('.dsType').change(function () {
        var val = $(this).val();
        var dsIcon = $('#iconWrap');
        if (val === "SideKick") {
            dsIcon.css('background-image', './img/sidekick_icon.png');
        } else {
            dsIcon.css('background-image', './img/connect_icon.png');
        }
    });

    $(document).on('focusin', '.mapSelect', function () {
        $(this).data('val', $(this).val());
    }).on('change', '.mapSelect', function () {
        var prev = $(this).data('val');
        var current = $(this).val();
        if (current !== -1) {
            var target = $('#sector' + current);
            if (!target.hasClass('checked')) target.addClass('checked');
            $('#sector' + prev).removeClass('checked');
            $(this).data('val', $(this).val());
        }
    });   
    
    fetchJson();
    listDreamDevices();

    $('body').bootstrapMaterialDesign();   
});

function checkHueAuth() {
    $.get("./api/DreamData/action?action=authorizeHue", function (data) {
        if (data === "Success: Bridge Linked." || data === "Success: Bridge Already Linked.") {
            console.log("LINKED");
            hueAuth = true;
        }
    });
    if (hueAuth) {
        $('#linkBtn').css('background-image', 'url("../img/hue_bridge_v2_linked.png")');
        $('#linkHint').html("Bridge successfully linked");
        fetchJson();
    } else {
        $('#linkBtn').css('background-image', 'url("../img/hue_bridge_v2_unlinked.png")');
        $('#linkHint').html("Click here to link bridge");
    }    
}

function fetchJson() {
    $.get('./api/DreamData/json', function (config) {
        console.log("We have some config", config);
        
        for (var v in config) {
            key = v;
            value = config[v];
            var id = key;
            

            if (id === "hueAuth") {
                authorized = value;
                var lb = $('#linkBtn');
                hueAuth = value;
                if (value) {
                    lb.css('background-image', 'url("../img/hue_bridge_v2_linked.png")');
                    lb.removeClass('unlinked');
                } else {
                    lb.css('background-image', 'url("../img/hue_bridge_v2_unlinked.png")');
                }
            } else if (id === "hueSync") {
                hueSync = value;
            } else if (id === "hueMap") {
                lightMap = value;
            } else if (id === "hueLights") {
                hueLights = value;
            } else if (id === "entertainmentGroup") {
                hueGroup = value;
            } else if (id === "entertainmentGroups") {
                hueGroups = value;
            } else if (id === "myDevice") {
                $('#dsName').html(value.name);
                $('#dsGroupName').html(value.groupName);
                $('#dsType').html(value.tag);
                var modestr = "";
                $('.modeBtn').removeClass('active');
                switch (value.mode) {
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
                $('#mode' + value.mode).addClass('active');
                $('#dsMode').html(modestr);
            } else if (id === "myDevices") {
                if (value !== null) buildDevList(value);
            } else {
                if (id === 'dsIp') {
                    dsIp = value;
                    if (value !== "0.0.0.0") {
                         $('#ds_find').hide();
                    }
                }
                $('#' + id).val(value);
            }
        }       

        if (hueGroup !== null && hueGroups.length) {
            hueGroup = hueGroups[0];
        }

        listGroups();
        if (hueLights && lightMap && hueGroup) {
            mapLights(hueGroup, lightMap, hueLights);
        }

    });
   
}

function mapLights(group, map, lights) {
    // Get the main light group
    var lightGroup = document.getElementById("mapSel");
    // Clear it
    $('div').remove('.delSel');
    // Clear the light region checked status
    $('.lightRegion').removeClass("checked");
    // Get the list of lights for the selected group
    var ids = group.lights;
    // Loop through our list of all lights that could be in ent group
    for (var l in lights) {
        var id = lights[l]['Key'];
        if ($.inArray(id.toString(), ids) !== -1) {
            var name = lights[l]['Value'];
            // Create a select for this light
            var newSelect = document.createElement('select');
            newSelect.className = "mapSelect form-control text-dark bg-light";
            newSelect.setAttribute('id', 'lightMap' + id);
            newSelect.setAttribute('name', 'lightMap' + id);
            // Assume it's not mapped
            var selection = -1;
            // Check to see if it is mapped
            for (var m in map) {
                if (map[m].lightId === id) {
                    selection = map[m].sectorId;
                }
            }

            // Create the blank "unmapped" option
            var opt = document.createElement("option");
            opt.value = -1;
            opt.innerHTML = "";

            // Set it to selected if we don't have a mapping
            if (selection === -1) {
                opt.setAttribute('selected', true);
            } else {
                var checkDiv = $('#sector' + selection);
                if (!checkDiv.hasClass('checked')) checkDiv.addClass('checked');
            }
            newSelect.appendChild(opt);

            // Add the options for our regions
            for (var i = 0; i < 12; i++) {
                opt = document.createElement("option");
                opt.value = i;
                opt.innerHTML = "<BR>" + (i + 1);
                // Mark it selected if it's mapped
                if (selection === i) opt.setAttribute('selected', true);
                newSelect.appendChild(opt);
            }

            // Create the label
            var label = document.createElement('label');
            label.innerHTML = name + ":  ";

            // Create the div to hold it all
            var lightDiv = document.createElement('div');
            lightDiv.className += "form-group delSel";
            lightDiv.id = id;
            lightDiv.setAttribute('data-name', name);
            lightDiv.setAttribute('data-id', id);

            // Congratulations, it's a boy!
            lightDiv.appendChild(label);
            lightDiv.appendChild(newSelect);
            lightGroup.appendChild(lightDiv);
        }
    }
    $('.delSel').bootstrapMaterialDesign();   
}

function listGroups() {
    var gs = $('#dsGroup');
    gs.html("");
    var i = 0;
    if (hueGroups !== null) {
        if (hueGroup === null && hueGroups.length > 0) hueGroup = hueGroups[0];
        $.each(hueGroups, function () {
            var val = $(this)[0].id;
            var txt = $(this)[0].name;
            var selected = (val === hueGroup.id) ? " selected" : "";
            gs.append(`<option value="${val}"${selected}>${txt}</option>`);
            i++;
        });
    }
}

function listDreamDevices() {
    $.get("./api/DreamData/action?action=connectDreamScreen", function (data) {
        console.log("Dream devices: ", data);
        buildDevList(data);
    });
}

function buildDevList(data) {
    var devList = $('#dsIp');
    devList.html("");
    $.each(data, function () {
        var dev = $(this)[0];
        var name = dev.name;
        var ip = dev.ipAddress;
        var type = dev.tag;
        if (name !== undefined && ip !== undefined && type.includes("DreamScreen")) {
            var selected = (ip === dsIp) ? "selected" : "";
            devList.append(`<option value='${ip}'${selected}>${name}: ${ip}</option>`);
        }
    });
}

function findGroup(id) {
    var res = false;
    $.each(hueGroups, function () {
        if (id === $(this)[0].id) {
            res = $(this)[0];
        }
    });
    return res;
}

function linkHue() {
    console.log("Authorized: ", hueAuth);

    if (!hueAuth && !linking) {
        linking = true;
        console.log("Trying to authorize with hue.");
        $('#circleBar').show();
        $('#linkBtn').css('background-image', 'url("../img/hue_bridge_v2_pushlink.png")');
        $('#linkHint').html("Press the link button on your Hue bridge");
        var bar = new ProgressBar.Circle(circleBar, {
            strokeWidth: 10,
            easing: 'easeInOut',
            duration: 0,
            color: '#0000FF',
            trailColor: '#eee',
            trailWidth: 0,
            svgStyle: null,
            value: 1
        });


        var x = 0;
        hueAuth = false;
        var intervalID = window.setInterval(function () {
            checkHueAuth();
            bar.animate((x / 30));
            if (++x === 30 || hueAuth) {
                window.clearInterval(intervalID);   
                $('#circleBar').hide();
                linking = false;
            }
        }, 1000);

        setTimeout(function () {
            $('#circleBar').hide();
            linking = false;
        }, 30000);
    } else {
        console.log("Already authorized.");
    }

}

