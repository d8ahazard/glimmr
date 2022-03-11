using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Image2Scene;
using Newtonsoft.Json;

string? path;
if (args.Length != 0) {
	path = args[0];
	Console.WriteLine("Processing " + path);
} else {
	Console.WriteLine("Enter the filename of an image to process.");
	path = Console.ReadLine();
}
	

if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
	try {
		ProcessImage(path);
	} catch (Exception e) {
		Console.WriteLine("Exception: " + e.Message);
	}
}

void ProcessImage(string? iMpath) {
	Console.WriteLine("Enter target width (default: 45)");
	var wString = Console.ReadLine();
	var width = 45;
	if (!string.IsNullOrEmpty(wString)) {
		int.TryParse(wString,out width);
	}
	
	var h = (int) Math.Round(width * 0.6666666666f);
	var og = CvInvoke.Imread(iMpath);
	var img = new Mat();
	CvInvoke.Resize(og, img, new Size(width, h));
	var output = new List<string[]>();
	if (img != null) {
		Console.WriteLine("GO!");
		var image = img.ToImage<Bgr, byte>();
		for (var i = 0; i < image.Rows; i++) {
			var row = new List<string>();
			for (var c = 0; c < image.Cols; c++) {
				Bgr b = image[i, c];
				var col = Color.FromArgb((int)b.Red, (int)b.Green, (int)b.Blue);
				var hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
				row.Add(hex);
			}
			output.Add(row.ToArray());
		}
		var name = Path.GetFileNameWithoutExtension(iMpath)??"Scene";
		name = name.Length == 1 ? char.ToUpper(name[0]).ToString() : char.ToUpper(name[0]) + name.Substring(1);
		Console.WriteLine("Please enter the display name: (default: " + name + ")");
		var nIn = Console.ReadLine();
		if (!string.IsNullOrEmpty(nIn)) {
			name = nIn;
		}
		
		Console.WriteLine("Please select an animation direction (0-6) (default: 0)");
		Console.WriteLine("0: Random");
		Console.WriteLine("1: Left to Right");
		Console.WriteLine("2: Top to Bottom");
		Console.WriteLine("3: Right to Left");
		Console.WriteLine("4: Bottom to Top");
		Console.WriteLine("5: Clockwise");
		Console.WriteLine("6: Counterclockwise");
		var dir = Console.ReadLine();
		var direction = 0;
		if (!string.IsNullOrEmpty(dir)) {
			int.TryParse(dir, out direction);
		}

		var mdd = (MatrixDirection) direction; 
		var md = mdd.ToString();
		Console.WriteLine("Please input an animation time (default: 0.75)");
		var delString = Console.ReadLine();
		var delay = 0.75f;
		if (!string.IsNullOrEmpty(delString)) {
			float.TryParse(delString, out delay);
		}
		Console.WriteLine("How many pixels should the image move per 'frame' (default: 5)");
		var stepString = Console.ReadLine();
		var step = 5;
		if (!string.IsNullOrEmpty(delString)) {
			int.TryParse(stepString, out step);
		}

		var ass = new AmbientScene(name, output.ToArray(), md, delay, step);
		var outfile = Path.ChangeExtension(path, "json");
		Console.WriteLine("Processing complete, saving to " + outfile);
		File.WriteAllText(outfile, JsonConvert.SerializeObject(ass, Formatting.Indented));
	}
	img?.Dispose();
}

enum MatrixDirection {
	/// <summary>
	/// Direction changes each frame cycle
	/// </summary>
	Random = 0,
	/// <summary>
	/// Left-to-right
	/// </summary>
	LTR = 1,
	/// <summary>
	/// Top-to-bottom
	/// </summary>
	TTB = 2,
	/// <summary>
	/// Right-to-left
	/// </summary>
	RTL = 3,
	/// <summary>
	/// Bottom to top
	/// </summary>
	BTT = 4,
	/// <summary>
	/// Rotate clockwise
	/// </summary>
	CW = 5,
	/// <summary>
	/// Rotate counter-clockwise
	/// </summary>
	CCW = 6
}
