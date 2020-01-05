using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;

namespace HueDream.Models.Nanoleaf {

    public class Aurora {
        
        public object AuthToken;
        
        public string BaseUrl;

        public object IpAddress;
        
        public Aurora(string ipAddress, string authToken) {
            BaseUrl = "http://" + ipAddress + ":16021/api/v1/" + authToken + "/";
            IpAddress = ipAddress;
            AuthToken = authToken;
        }
        
           
        public virtual JObject __put(object endpoint, JObject data) {
            var url = BaseUrl + endpoint;
            try {
                var r = Requests.Put(url, data);
                return CheckError(r);
            } catch (Exception e){
                Console.WriteLine(e.Message);
                return null;
            }
        }
        
        public virtual JObject __get(string endpoint = "") {
            var url = BaseUrl + endpoint;
            try {
                var r = Requests.Get(url);
                return CheckError(r);
            } catch (Exception e) {
                Console.WriteLine(e.Message);
                return null;
            }
                
        }
        
        public virtual JObject __delete(string endpoint = "") {
            var url = BaseUrl + endpoint;
            try {
                var r = Requests.Delete(url);
                return CheckError(r);
            } catch (Exception e) {
                Console.WriteLine(e.Message);
                return null;
            }
        }
        
        public virtual JObject CheckError(RequestObj r) {
            switch (r.StatusCode) {
                case 200 when r.Text == "":
                    return null;
                case 200:
                    return r.Json;
                case 204:
                    return null;
                case 403:
                    Console.WriteLine(@"Error 400: Bad request! (" + IpAddress + @")");
                    break;
                case 401:
                    Console.WriteLine(@"Error 401: Not authorized! This is an invalid token for this Aurora (" + IpAddress + @")");
                    break;
                case 404:
                    Console.WriteLine(@"Error 404: Resource not found! (" + IpAddress + @")");
                    break;
                case 422:
                    Console.WriteLine(@"Error 422: Unprocessable Entity (" + IpAddress + @")");
                    break;
                case 500:
                    Console.WriteLine(@"Error 500: Internal Server Error (" + IpAddress + @")");
                    break;
                default:
                    Console.WriteLine(@"ERROR! UNKNOWN ERROR " + r.StatusCode + @". Please post an issue on the GitHub page: https://github.com/software-2/nanoleaf/issues");
                    break;
            }

            return null;
        }
        
        // Returns the full Aurora Info request. 
        //         
        //         Useful for debugging since it's just a fat dump.
        public object Info {
            get {
                return __get();
            }
        }
        
        // Returns the current color mode.
        public object ColorMode {
            get {
                return __get("state/colorMode");
            }
        }
        
        //##########################################
        // General functionality methods
        //##########################################
        // Briefly flash the panels on and off
        public virtual object Identify() {
            return __put("identify", new JObject());
        }
        
        // Returns the firmware version of the device
        public object Firmware {
            get {
                return __get("firmwareVersion");
            }
        }
        
        // Returns the model number of the device. (Always returns 'NL22')
        public object Model {
            get {
                return __get("model");
            }
        }
        
        // Returns the serial number of the device
        public object SerialNumber {
            get {
                return __get("serialNo");
            }
        }
        
        // CAUTION: Revokes your auth token from the device.
        public virtual object delete_user() {
            return __delete();
        }
        
        // Returns True if the device is on, False if it's off
        // Turns the device on/off. True = on, False = off
        public bool On {
            get {
                var res = __get("state/on/value");
                Console.WriteLine(@"Res: " + res);
                return (bool) res.GetValue("value").ToObject(typeof(bool));
            }
            set {
                var data = new Dictionary<object, object> {
                {
                    "on",
                    value}};
                __put("state", JObject.FromObject(data));
            }
        }
        
        
        //##########################################
        // On / Off methods
        //##########################################
        // Switches the on/off state of the device
        public void ToggleState() {
            On = !On;
        }
        
        // Returns the brightness of the device (0-100)
        // Sets the brightness to the given level (0-100)
        public int Brightness {
            get {
                return (int) __get("state/brightness/value").GetValue("value").ToObject(typeof(int));
            } set {
                var data = new Dictionary<object, object> {
                {
                    "brightness",
                    new Dictionary<object, object> {
                    {
                        "value",
                        value}}}};
                __put("state", JObject.FromObject(data));
            }
        }
        
        // Returns the minimum brightness possible. (This always returns 0)
        public int BrightnessMin {
            get { return int.Parse(__get("state/brightness/min").GetValue("value").ToString()); }
        }
        
        // Returns the maximum brightness possible. (This always returns 100)
        public object BrightnessMax {
            get { return int.Parse(__get("state/brightness/max").GetValue("value").ToString()); }
        }
        
        //##########################################
        // Brightness methods
        //##########################################
        // Raise the brightness of the device by a relative amount (negative lowers brightness)
        public JObject brightness_raise(int level) {
            var data = new Dictionary<object, object> {
            {
                "brightness",
                new Dictionary<object, object> {
                {
                    "increment",
                    level}}}};
            return __put("state", JObject.FromObject(data));
        }
        
        // Lower the brightness of the device by a relative amount (negative raises brightness)
        public void brightness_lower(int level) {
            brightness_raise(level * -1);
        }
        
        // Returns the hue of the device (0-360)
        // Sets the hue to the given level (0-360)
        public int Hue {
            get {
                return int.Parse(__get("state/hue/value").GetValue("value").ToString());
            }
            set {
                var data = new Dictionary<object, object> {
                {
                    "hue",
                    new Dictionary<object, object> {
                    {
                        "value",
                        value}}}};
                __put("state", JObject.FromObject(data));
            }
        }
        
        // Returns the minimum hue possible. (This always returns 0)
        public int HueMin {
            get {
                return int.Parse(__get("state/hue/min").GetValue("value").ToString());
            }
        }
        
        // Returns the maximum hue possible. (This always returns 360)
        public int HueMax {
            get {
                return int.Parse(__get("state/hue/max").GetValue("value").ToString());
            }
        }
        
        //##########################################
        // Hue methods
        //##########################################
        // Raise the hue of the device by a relative amount (negative lowers hue)
        public JObject hue_raise(int level) {
            var data = new Dictionary<object, object> {
            {
                "hue",
                new Dictionary<object, object> {
                {
                    "increment",
                    level}}}};
            return __put("state", JObject.FromObject(data));
        }
        
        // Lower the hue of the device by a relative amount (negative raises hue)
        public JObject hue_lower(int level) {
            return hue_raise(-level);
        }
        
        // Returns the saturation of the device (0-100)
        // Sets the saturation to the given level (0-100)
        public int Saturation {
            get {
                return int.Parse(__get("state/sat/value").GetValue("value").ToString());
            }
            set {
                var data = new Dictionary<object, object> {
                {
                    "sat",
                    new Dictionary<object, object> {
                    {
                        "value",
                        value}}}};
                __put("state", JObject.FromObject(data));
            }
        }
        
        // Returns the minimum saturation possible. (This always returns 0)
        public int SaturationMin {
            get {
                return int.Parse(__get("state/sat/min").GetValue("value").ToString());
            }
        }
        
        // Returns the maximum saturation possible. (This always returns 100)
        public int SaturationMax {
            get {
                return int.Parse(__get("state/sat/max").GetValue("value").ToString());
            }
        }
        
        //##########################################
        // Saturation methods
        //##########################################
        // Raise the saturation of the device by a relative amount (negative lowers saturation)
        public JObject saturation_raise(int level) {
            var data = new Dictionary<object, object> {
            {
                "sat",
                new Dictionary<object, object> {
                {
                    "increment",
                    level}}}};
            return __put("state", JObject.FromObject(data));
        }
        
        // Lower the saturation of the device by a relative amount (negative raises saturation)
        public JObject saturation_lower(int level) {
            return saturation_raise(-level);
        }
        
        // Returns the color temperature of the device (0-100)
        // Sets the color temperature to the given level (0-100)
        public int ColorTemperature {
            get {
                return int.Parse(__get("state/ct/value").GetValue("value").ToString());
            }
            set {
                var data = new Dictionary<object, object> {
                {
                    "ct",
                    new Dictionary<object, object> {
                    {
                        "value",
                        value}}}};
                this.__put("state", JObject.FromObject(data));
            }
        }
        
        // Returns the minimum color temperature possible. (This always returns 1200)
        public int ColorTemperatureMin {
            get {
                return int.Parse(__get("state/ct/min").GetValue("value").ToString());
            }
        }
        
        // Returns the maximum color temperature possible. (This always returns 6500)
        public int ColorTemperatureMax {
            get {
                return int.Parse(__get("state/ct/max").GetValue("value").ToString());
            }
        }
        
        //##########################################
        // Color Temperature methods
        //##########################################
        // Raise the color temperature of the device by a relative amount (negative lowers color temperature)
        public JObject color_temperature_raise(int level) {
            var data = new Dictionary<object, object> {
            {
                "ct",
                new Dictionary<object, object> {
                {
                    "increment",
                    level}}}};
            return __put("state", JObject.FromObject(data));
        }
        
        // Lower the color temperature of the device by a relative amount (negative raises color temperature)
        public JObject color_temperature_lower(int level) {
            return color_temperature_raise(-level);
        }
        
        // The color of the device, as represented by 0-255 RGB values
        // Set the color of the device, as represented by either a hex string or a list of 0-255 RGB values
        public RGBColor Rgb {
            get {
                var hue = Hue;
                var saturation = Saturation;
                var brightness = Brightness;
                var rgb = HsvToRgb(hue / 360.0, saturation / 100.0, brightness / 100.0);
                return new RGBColor(Convert.ToInt32(rgb[0] * 255), Convert.ToInt32(rgb[1] * 255), Convert.ToInt32(rgb[2] * 255));
            }
            set {
                var hue = value.GetHue();
                var saturation = value.GetSaturation();
                var brightness = value.GetBrightness();
                var data = new Dictionary<object, object> {
                    {
                        "hue",
                        new Dictionary<object, object> {
                        {
                            "value",
                            hue}}},
                    {
                        "sat",
                        new Dictionary<object, object> {
                        {
                            "value",
                            saturation}}},
                    {
                        "brightness",
                        new Dictionary<object, object> {
                        {
                            "value",
                            brightness}}}};
                __put("state", JObject.FromObject(data));
            }
        }
        
        // Returns the orientation of the device (0-360)
        public JObject Orientation {
            get {
                return __get("panelLayout/globalOrientation/value");
            }
        }
        
        // Returns the minimum orientation possible. (This always returns 0)
        public JObject OrientationMin {
            get {
                return __get("panelLayout/globalOrientation/min");
            }
        }
        
        // Returns the maximum orientation possible. (This always returns 360)
        public JObject OrientationMax {
            get {
                return __get("panelLayout/globalOrientation/max");
            }
        }
        
        // Returns the number of panels connected to the device
        public int PanelCount {
            get {
                return int.Parse(__get("panelLayout/layout/numPanels").GetValue("value").ToString());
            }
        }
        
        // Returns the length of a single panel. (This always returns 150)
        public int PanelLength {
            get {
                return int.Parse(__get("panelLayout/layout/sideLength").GetValue("value").ToString());
            }
        }
        
        // Returns a list of all panels with their attributes represented in a dict.
        //         
        //         panelId - Unique identifier for this panel
        //         x - X-coordinate
        //         y - Y-coordinate
        //         o - Rotational orientation
        //         
        public JObject PanelPositions {
            get {
                return __get("panelLayout/layout/positionData");
            }
        }
        
        public List<string> ReservedEffectNames = new List<string> {
            "*Static*",
            "*Dynamic*",
            "*Solid*"
        };
        
        // Returns the active effect
        // Sets the active effect to the name specified
        public string Effect {
            get {
                return __get("effects/select").GetValue("value").ToString();

            }
            set {
                var data = new Dictionary<object, object> {
                {
                    "select",
                    value}};
                __put("effects", JObject.FromObject(data));
            }
        }
        
        // Returns a list of all effects stored on the device
        public JObject EffectsList {
            get {
                return __get("effects/effectsList");
            }
        }
        
        // Sets the active effect to a new random effect stored on the device.
        //         
        //         Returns the name of the new effect.
        /*public JObject effect_random() {
            var effectList = EffectsList;
            var activeEffect = Effect;
            if (!ReservedEffectNames.Contains(activeEffect)) {
                // #TODO: Parse the returned effects into a proper list so we can do this
                //effectList.remove(this.Effect);
            }
            var new_effect = random.choice(effectList);
            this.Effect = new_effect;
            return new_effect;
        }*/
        
        // Sends a raw dict containing effect data to the device.
        // 
        //         The dict given must match the json structure specified in the API docs.
        public JObject effect_set_raw(JObject effectData) {
            var data = new Dictionary<object, object> {
            {
                "write",
                effectData}};
            return __put("effects", JObject.FromObject(data));
        }
        
        // Returns the dict containing details for the effect specified
        public JObject effect_details(string name) {
            var data = new Dictionary<object, object> {
            {
                "write",
                new Dictionary<object, object> {
                    {
                        "command",
                        "request"},
                    {
                        "animName",
                        name}}}};
            return __put("effects", JObject.FromObject(data));
        }
        
        // Returns a dict containing details for all effects on the device
        public JObject effect_details_all() {
            var data = new Dictionary<object, object> {
            {
                "write",
                new Dictionary<object, object> {
                {
                    "command",
                    "requestAll"}}}};
            return __put("effects", JObject.FromObject(data));
        }
        
        // Removed the specified effect from the device
        public JObject effect_delete(string name) {
            var data = new Dictionary<object, object> {
            {
                "write",
                new Dictionary<object, object> {
                    {
                        "command",
                        "delete"},
                    {
                        "animName",
                        name}}}};
            return __put("effects", JObject.FromObject(data));
        }
        
        // Renames the specified effect saved on the device to a new name
        public JObject effect_rename(string oldName, string newName) {
            var data = new Dictionary<object, object> {
            {
                "write",
                new Dictionary<object, object> {
                    {
                        "command",
                        "rename"},
                    {
                        "animName",
                        oldName},
                    {
                        "newName",
                        newName}}}};
            return __put("effects", JObject.FromObject(data));
        }
        
        public double[] HsvToRgb(double h, double s, double v)
        {    
            while (h < 0) { h += 360; }

            while (h >= 360) { h -= 360; }

            double r, g, b;
            if (v <= 0)
            { r = g = b = 0; }
            else if (s <= 0)
            {
                r = g = b = v;
            }
            else
            {
                double hf = h / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = v * (1 - s);
                double qv = v * (1 - s * f);
                double tv = v * (1 - s * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        r = v;
                        g = tv;
                        b = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        r = qv;
                        g = v;
                        b = pv;
                        break;
                    case 2:
                        r = pv;
                        g = v;
                        b = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        r = pv;
                        g = qv;
                        b = v;
                        break;
                    case 4:
                        r = tv;
                        g = pv;
                        b = v;
                        break;

                    // Red is the dominant color

                    case 5:
                        r = v;
                        g = pv;
                        b = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        r = v;
                        g = tv;
                        b = pv;
                        break;
                    case -1:
                        r = v;
                        g = pv;
                        b = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //FATAL("i Value error in Pixel conversion, Value is %d", i);
                        r = g = b = v; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(r * 255.0));
            g = Clamp((int)(g * 255.0));
            b = Clamp((int)(b * 255.0));
            return new[] {r,g,b};
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
    
    
    
}