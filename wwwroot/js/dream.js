var syncToggle;
var groups;
var lights;
var lightMap;

$(function () {

    listLights();
    listGroups();
    
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

    $('#hue_authorize').on('click', function() {
        console.log("Trying to authorize with hue.");
        $.get("./api/DreamData/action?action=authorizeHue", function (data) {
            if (data.indexOf("Success: ") !== -1) {
                $('#hue_auth').val("Authorized")
                $('#hue_authorize').hide();                
            } else {
                $('#hue_auth').val("Not authorized");
                $('#hue_authorize').show();
            }
            alert(data);
        });
    });

    $('#ds_find').on('click', function() {
        console.log("Trying to find dreamscreen.");
        $.get("./api/DreamData/action?action=connectDreamScreen", function (data) {
            if (data.indexOf("Success: ") !== -1) {
                var result = $.trim(data.replace("Success: ", ""));
                if (result != "0.0.0.0") {                    
                    $('#ds_ip').val(result)
                    $('#ds_find').hide();
                } else {
                    $('#ds_find').show();
                }
                alert(data);
            }            
        });
    });

    $('.dsGroup').change(function () {
        console.log("Gchange?");
        var id = $(this).val();
        var newGroup = findGroup(id);
        if (newGroup) {
            console.log("Remapping");
            mapLights(newGroup, lightMap, lights);
        }
    });

    $('.dsType').change(function () {
        var val = $(this).val();
        var dsIcon = $('#dsIcon');
        console.log("Val is " + val);
        if (val == "SideKick") {
            dsIcon.attr('src', './img/sidekick_icon.png');
        } else {
            dsIcon.attr('src', './img/connect_icon.png');
        }
    });

    $(document).on('focusin', '.mapSelect', function () {
        console.log("Saving sel value " + $(this).val());
        $(this).data('val', $(this).val());
    }).on('change', '.mapSelect', function () {
        var prev = $(this).data('val');
        var current = $(this).val();
        if (current != -1) {
            console.log("Flipping the script?", prev);
            var target = $('#sector' + current);
            if (!target.hasClass('checked')) target.addClass('checked');
            $('#sector' + prev).removeClass('checked');
            $(this).data('val', $(this).val());
        }
        console.log("Prev value " + prev);
        console.log("New value " + current);
    });

   
    $('#load_lights').on('click', function() {
        $.get("./api/DreamData/action?action=getLights", function (data) {
            console.log("Data: ", data);
            if (data.indexOf("Error: ") === -1) {
                console.log("Mapping lights", data);
                mapLights({}, data);
                data = "Light data retrieved.";
            }
            alert(data);
        });
    });
    fetchJson();
    $('body').bootstrapMaterialDesign();   
});

function fetchJson() {
    $.get('./api/DreamData/json', function (config) {
        console.log("We have some config", config);
        var hueLights = false;
        var group = false;
        

        for (var v in config) {
            key = v;
            value = config[v];
            var id = key;
            console.log("id, value", id, value)

            if (id == "hueAuth") {
                hueAuth = value;
                if (value) {
                    $('#hueAuth').val("Authorized")
                    $('#hue_authorize').hide();
                } else {
                    $('#hue_auth').val("Not authorized, press the link button on your hue bridge and click below.");
                }
            } else if (id == "hueSync") {
                hueSync = value;
            } else if (id == "hueMap") {
                lightMap = value;
            } else if (id == "hueLights") {
                hueLights = value;
            } else if (id == "entertainmentGroup") {
                group = value;
            } else if (id == "entertainmentGroups") {
                groups = value;
            } else if (id == "dreamState") {
                $('#dsName').html(value.name);
                $('#dsGroupName').html(value.groupName);
                $('#dsType').html(value.type);
                var modestr = (value.mode == 0) ? "Off" : ((value.mode == 1) ? "Video" : ((value.mode == 2) ? "Music" : ((value.mode == 3) ? "Ambient" : "WTF")));
                $('#dsMode').html(modestr);
            } else {
                console.log("Setting value for #" + id, value);
                if (id == 'dsIp') {
                    dsIp = value;
                    if (value != "0.0.0.0") {
                         $('#ds_find').hide();
                    }
                }
                $('#' + id).val(value);
            }
        }       

        console.log("GROUPS: ", groups);
        if (group == null && groups.length) {
            console.log("SETGROUP");
            group = groups[0];
        }

        if (hueLights && lightMap && group) {
            console.log("Mapping lights");
            mapLights(group, lightMap, hueLights);
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
                if (map[m]['Key'] == id) {
                    selection = map[m]['Value'];
                }
            }

            // Create the blank "unmapped" option
            var opt = document.createElement("option");
            opt.value = -1;
            opt.innerHTML = "";

            // Set it to selected if we don't have a mapping
            if (selection == -1) {
                opt.setAttribute('selected', true);
            } else {
                var checkDiv = $('#sector' + selection);
                if (!checkDiv.hasClass('checked')) checkDiv.addClass('checked');
            }
            newSelect.appendChild(opt);

            // Add the options for our regions
            for (var i = 0; i < 12; i++) {
                var opt = document.createElement("option");
                opt.value = i;
                opt.innerHTML = "<BR>" + (i + 1);
                // Mark it selected if it's mapped
                if (selection == i) opt.setAttribute('selected', true);
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
    $.get("./api/DreamData/action?action=listGroups", function (data) {
        groups = data;
        var gs = $('#dsGroup');
        gs.html("");
        var i = 0;
        $.each(data, function () {
            console.log($(this)[0]);
            var val = $(this)[0].id;
            var txt = $(this)[0].name;
            var selected = (i == 0) ? "" : " selected";
            gs.append(`<option value="${val}"${selected}>${txt}</option>`);
            i++;
        });
        return groups;
    });
}

function listLights() {
    $.get("./api/DreamData/action?action=listLights", function (data) {
        lights = data;
        return data;
    });
}

function findGroup(id) {
    var res = false;
    $.each(groups, function () {
        console.log("Findloop", id, $(this)[0].id);
        if (id === $(this)[0].id) {
            console.log("Group match");
            res = $(this)[0];
        }
    });
    return res;
}