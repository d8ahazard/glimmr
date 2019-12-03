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

    $('#linkBtn').on('click', function () {
        if (!hueAuth && !linking) {
            linkHue();
        }
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
        if (hueAuth) {
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
        }
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
            if (hueAuth) {
                fetchJson();
            }
            setLinkStatus();
        }
    });
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
                setLinkStatus();
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
                if (value.tag === "Connect") {
                    $('#iconWrap').addClass("Connect").removeClass("SideKick");
                    console.log("Connect");
                } else {
                    $('#iconWrap').addClass("SideKick").removeClass("Connect");
                    console.log("Sidekick");
                }
                $('#dsType').val(value.tag);
                $('#dsType option[value='+value.tag+']').attr('selected', true);
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
    lights = lights.sort(function (a, b) {
        return a.Value.localeCompare(b.Value);
    });
    for (var l in lights) {
        var id = lights[l]['Key'];
        if ($.inArray(id.toString(), ids) !== -1) {
            var name = lights[l]['Value'];
            var brightness = 100;
            var override = false;

            // Create the label for select
            var label = document.createElement('label');
            label.innerHTML = name + ":  ";
            label.setAttribute("for", "lightMap" + id);

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
                    brightness = map[m].brightness;
                    override = map[m].overrideBrightness;
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

            var selDiv = document.createElement('div');
            selDiv.className += "form-group";
            selDiv.appendChild(label);
            selDiv.appendChild(newSelect);

            // Create label for brightness
            var brightLabel = document.createElement('label');
            brightLabel.innerHTML = "Brightness: ";
            brightLabel.setAttribute('for', 'brightness' + id);

            // Create the brightness slider
            var newRange = document.createElement("input");
            newRange.className = "mapBrightness form-control";
            newRange.setAttribute("type", "range");
            newRange.setAttribute('id', 'brightness' + id);
            newRange.setAttribute('name', 'brightness' + id);
            newRange.setAttribute('min', 0);
            newRange.setAttribute('max', 100);
            newRange.setAttribute('value', brightness);

            var rangeDiv = document.createElement('div');
            rangeDiv.className += "form-group";
            rangeDiv.appendChild(brightLabel);
            rangeDiv.appendChild(newRange);


            // Create label for override check
            var checkLabel = document.createElement('label');
            checkLabel.innerHTML = "Override";
            checkLabel.className += "form-check-label";
            checkLabel.setAttribute('for', 'overrideBrightness' + id);


            // Create a checkbox
            var newCheck = document.createElement("input");
            newCheck.className += "overrideBright form-check-input";
            newCheck.setAttribute('id', 'overrideBrightness' + id);
            newCheck.setAttribute('name', 'overrideBrightness' + id);
            newCheck.setAttribute("type", "checkbox");
            if (override) newCheck.setAttribute("checked", 'checked');
            
            // Create the div to hold it all
            var lightDiv = document.createElement('div');
            lightDiv.className += "delSel col-xs-4 col-sm-4 col-md-3";
            lightDiv.id = id;
            lightDiv.setAttribute('data-name', name);
            lightDiv.setAttribute('data-id', id);

            var chkDiv = document.createElement('div');
            chkDiv.className += "form-check";
            chkDiv.appendChild(newCheck);
            chkDiv.appendChild(checkLabel);

            // Congratulations, it's a boy!
            lightDiv.appendChild(selDiv);
            lightDiv.appendChild(chkDiv);
            lightDiv.appendChild(rangeDiv);

            // Add it to our document
            lightGroup.appendChild(lightDiv);
        }
    }
    $('.delSel').bootstrapMaterialDesign();   
}

function listGroups() {
    var gs = $('#dsGroup');
    gs.html("");
    var i = 0;
    if (hueAuth) {
        if (hueGroups !== undefined) {
            if (hueGroup === undefined && hueGroups.length > 0) hueGroup = hueGroups[0];
            $.each(hueGroups, function() {
                var val = $(this)[0].id;
                var txt = $(this)[0].name;
                var selected = (val === hueGroup.id) ? " selected" : "";
                gs.append(`<option value="${val}"${selected}>${txt}</option>`);
                i++;
            });
        }
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

function setLinkStatus() {
    var lImg = $('#linkImg');
    var lHint = $('#linkHint');
    var lBtn = $('#linkBtn');
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
}

function linkHue() {
    console.log("Authorized: ", hueAuth);

    if (!hueAuth && !linking) {
        linking = true;
        console.log("Trying to authorize with hue.");
        $('#circleBar').show();
        setLinkStatus();
        var bar = new ProgressBar.Circle(circleBar, {
            strokeWidth: 15,
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
                setLinkStatus();
            }
        }, 1000);

        setTimeout(function () {
            $('#circleBar').html("");
            $('#circleBar').hide();
            linking = false;
            setLinkStatus();
        }, 30000);
    } else {
        console.log("Already authorized.");
    }

}

