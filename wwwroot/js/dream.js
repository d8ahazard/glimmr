var syncToggle;

$(function () {
    syncToggle = $('#syncToggle');
    syncToggle.bootstrapToggle();

   
    
    $('#settingsForm').submit(function (e) {
        e.preventDefault();
        var data = $(this).serialize();
        $.ajax({
            url: "./api/HueData",
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
        $.get("./api/HueData/action?action=authorizeHue", function (data) {
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
        $.get("./api/HueData/action?action=connectDreamScreen", function (data) {
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

    $('#load_lights').on('click', function() {
        $.get("./api/HueData/action?action=getLights", function (data) {
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

    syncToggle.change(function () {
        var data = { 'hue_sync': $(this).prop('checked') };
        $.ajax({
            url: "./api/HueData",
            type: "POST",
            data: data,
            success: function (data) {
                console.log("Posted!", data);
            }
        });
    });
});

function fetchJson() {
    $.get('./api/HueData/json', function (config) {
        console.log("We have some config", config);
        var hueLights = false;
        var lightMap = false;
        var hueAuth = false;
        var dsIp = false;
        var hueSync = false;

        for (var v in config) {
            key = v;
            value = config[v];
            var id = key.toLowerCase();
            console.log("id, value", id, value)

            if (id == "hue_auth") {
                hueAuth = value;
                if (value) {
                    $('#hue_auth').val("Authorized")
                    $('#hue_authorize').hide();
                } else {
                    $('#hue_auth').val("Not authorized, press the link button on your hue bridge and click below.");
                }
            } else if (id == "hue_sync") {
                hueSync = value;
            } else if (id == "hue_map") {
                lightMap = value;
            } else if (id == "hue_lights") {
                hueLights = value;
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

        console.log("Stuff:", lightMap, dsIp, hueAuth);
        if (lightMap && hueAuth && dsIp) {
            syncToggle.bootstrapToggle('enable');
            if (hueSync) {
                syncToggle.bootstrapToggle('on');
            } else {
                syncToggle.bootstrapToggle('off');
            }
        } else {
            syncToggle.bootstrapToggle('disable');
        }

        if (hueLights && lightMap) {
            console.log("Mapping lights");
            mapLights(lightMap, hueLights);
        }
    });
    var height = ($('#lightPreview').width() / 16) * 5;
    $('#lightPreview').css('height', height);
}

function mapLights(map, lights) {
    var lightGroup = document.getElementById("lightGroup");
    lightGroup.innerHTML = "";
    var lightDiv = "";
    for (var l in lights) {
        var id = lights[l]['Key'];
        var name = lights[l]['Value'];
        console.log("Name and ID are ", name, id);
        var newSelect=document.createElement('select');
        newSelect.className = "col-3";
        newSelect.setAttribute('id','lightMap' + id);
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
            opt.value= -1;
            opt.innerHTML = "None";
            if (selection == -1) opt.setAttribute('selected', true);
            newSelect.appendChild(opt);
        for (i = 0; i < 13; i++) {
            var opt = document.createElement("option");
            opt.value= i;
            opt.innerHTML = i;
            if (selection == i) opt.setAttribute('selected', true);
            newSelect.appendChild(opt);
        }
        var lightLabel = document.createElement('div');
        lightLabel.className += " col-6 row";

        lightLabel.id = id;
        lightLabel.setAttribute('data-name', name);
        lightLabel.setAttribute('data-id', id);
        var label = document.createElement('label');
        label.className = "col-9";
        label.innerHTML = name + ":  ";
        lightLabel.appendChild(label);
        lightLabel.appendChild(newSelect);
        lightGroup.appendChild(lightLabel);
    }
}