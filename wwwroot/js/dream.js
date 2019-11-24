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

    $('#dsGroup').change(function () {
        console.log("Gchange?");
        var id = $(this).val();
        var newGroup = findGroup(id);
        if (newGroup) {
            console.log("Remapping");
            mapLights(newGroup, lightMap, lights);
        }
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
                    $('#hue_auth').val("Authorized")
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
            } else {
                console.log("Setting value for #" + id, value);
                if (id == 'ds_ip') {
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
    var height = ($('#lightPreview').width() / 16) * 5;
    $('#lightPreview').css('height', height);
}

function mapLights(group, map, lights) {
    console.log("GML", group, map, lights);
    var lightGroup = document.getElementById("lightGroup");
    lightGroup.innerHTML = "";
    var ids = group.lights;
    for (var l in lights) {
        var id = lights[l]['Key'];
        var name = lights[l]['Value'];
        console.log("Name and ID are ", name, id, ids);
        if ($.inArray(id.toString(), ids) !== -1) {
            var newSelect = document.createElement('select');
            newSelect.className = "col-3 col-sm-4";
            newSelect.setAttribute('id', 'lightMap' + id);
            newSelect.setAttribute('name', 'lightMap' + id);
            var selection = -1;
            for (var m in map) {
                if (map[m]['Key'] == id) {
                    selection = map[m]['Value'];
                    console.log("We have a selection?", selection);
                }
            }

            var i;
            var opt = document.createElement("option");
            opt.value = -1;
            opt.innerHTML = "";
            if (selection == -1) opt.setAttribute('selected', true);
            newSelect.appendChild(opt);
            for (i = 0; i < 13; i++) {
                var opt = document.createElement("option");
                opt.value = i;
                opt.innerHTML = i;
                if (selection == i) opt.setAttribute('selected', true);
                newSelect.appendChild(opt);
            }
            var lightLabel = document.createElement('div');
            lightLabel.className += " col-6 col-sm-12 row";

            lightLabel.id = id;
            lightLabel.setAttribute('data-name', name);
            lightLabel.setAttribute('data-id', id);
            var label = document.createElement('label');
            label.className = "col-9 col-sm-8";
            label.innerHTML = name + ":  ";
            lightLabel.appendChild(label);
            lightLabel.appendChild(newSelect);
            lightGroup.appendChild(lightLabel);
        }
    }
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
    });
}

function listLights() {
    $.get("./api/DreamData/action?action=listLights", function (data) {
        lights = data;
    });
}

function findGroup(id) {
    $.each(groups, function () {
        console.log("Findloop", id, $(this)[0]);
        if (id == $(this)[0].id) return $(this)[0];
    });
    return false;
}