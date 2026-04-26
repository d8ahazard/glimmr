#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#endregion

namespace GlimmrControl.Core {
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class RoomCalibrationPage : ContentPage {
		private readonly GlimmrDevice _device;
		private readonly string _baseUrl;
		private readonly HttpClient _http;
		private int _currentStep;
		private CalibrationResult _lastResult;

		private readonly BoxView[] _stepDots;

		private enum CalibrationStep {
			Welcome = 0,
			OpenCamera = 1,
			BrightnessCalibration = 2,
			SequenceDetection = 3,
			Results = 4
		}

		public RoomCalibrationPage(GlimmrDevice device) {
			InitializeComponent();
			_device = device;
			_baseUrl = "http://" + device.NetworkAddress;
			_http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
			_currentStep = 0;

			topMenuBar.LeftButtonTapped += OnBackTapped;

			_stepDots = new[] { Step1Dot, Step2Dot, Step3Dot, Step4Dot, Step5Dot };
			UpdateUI();
		}

		private async void OnBackTapped(object sender, EventArgs e) {
			await Navigation.PopModalAsync(false);
		}

		private async void OnActionButtonClicked(object sender, EventArgs e) {
			ActionButton.IsEnabled = false;
			try {
				switch ((CalibrationStep)_currentStep) {
					case CalibrationStep.Welcome:
						await MoveToStep(1);
						break;
					case CalibrationStep.OpenCamera:
						await MoveToStep(2);
						await RunBrightnessCalibration();
						break;
					case CalibrationStep.BrightnessCalibration:
						break;
					case CalibrationStep.SequenceDetection:
						break;
					case CalibrationStep.Results:
						await Navigation.PopModalAsync(false);
						return;
				}
			} catch (Exception ex) {
				Debug.WriteLine("Calibration error: " + ex.Message);
				await DisplayAlert("Error", "Calibration failed: " + ex.Message, "OK");
			} finally {
				ActionButton.IsEnabled = true;
			}
		}

		private async Task MoveToStep(int step) {
			_currentStep = step;
			UpdateUI();
			await Task.Delay(100);
		}

		private void UpdateUI() {
			for (var i = 0; i < _stepDots.Length; i++) {
				_stepDots[i].Color = i == _currentStep ? Color.FromHex("#4488FF") :
					i < _currentStep ? Color.FromHex("#226699") : Color.FromHex("#444");
			}

			InstructionOverlay.IsVisible = false;
			ProgressOverlay.IsVisible = false;
			ResultsOverlay.IsVisible = false;
			CameraView.IsVisible = false;

			switch ((CalibrationStep)_currentStep) {
				case CalibrationStep.Welcome:
					InstructionOverlay.IsVisible = true;
					StepIcon.Text = "\U0001F4F7";
					StepTitle.Text = "Room Calibration";
					StepDescription.Text =
						"This wizard will flash your lights in sequence and use the camera " +
						"to detect their positions. Make sure all your Glimmr-connected lights " +
						"are visible from where you're standing.";
					ActionButton.Text = "Start Calibration";
					break;

				case CalibrationStep.OpenCamera:
					InstructionOverlay.IsVisible = true;
					StepIcon.Text = "\U0001F3AF";
					StepTitle.Text = "Position Your Phone";
					StepDescription.Text =
						"Hold your phone so the camera can see all your lights and your TV/screen. " +
						"Try to get the whole room in frame. The camera feed will open on the Glimmr box.";
					ActionButton.Text = "I'm Ready - Start Flashing";
					CameraView.IsVisible = true;
					CameraView.Source = _baseUrl + "/api/RoomMap/calibrate/camera";
					break;

				case CalibrationStep.BrightnessCalibration:
					ProgressOverlay.IsVisible = true;
					ProgressLabel.Text = "Calibrating brightness...";
					ProgressDetail.Text = "Flashing lights to find optimal camera exposure";
					CalibrationProgress.Progress = 0;
					ActionButton.Text = "Please wait...";
					ActionButton.IsEnabled = false;
					break;

				case CalibrationStep.SequenceDetection:
					ProgressOverlay.IsVisible = true;
					ProgressLabel.Text = "Detecting light positions...";
					ProgressDetail.Text = "Running test sequences";
					CalibrationProgress.Progress = 0.3;
					ActionButton.Text = "Please wait...";
					ActionButton.IsEnabled = false;
					break;

				case CalibrationStep.Results:
					ResultsOverlay.IsVisible = true;
					ActionButton.Text = "Done";
					ActionButton.IsEnabled = true;
					ActionButton.BackgroundColor = Color.FromHex("#22AA44");
					if (_lastResult != null && _lastResult.Success) {
						ResultIcon.Text = "\u2705";
						ResultTitle.Text = "Calibration Complete!";
						ResultDetail.Text = $"Mapped {_lastResult.EmittersMapped} lights in your room.";
						ResultEmitters.Text = _lastResult.Summary;
					} else {
						ResultIcon.Text = "\u274C";
						ResultTitle.Text = "Calibration Issue";
						ResultDetail.Text = _lastResult?.ErrorMessage ?? "Could not complete calibration.";
						ResultEmitters.Text = "You can try again or manually adjust positions in the web UI.";
					}

					break;
			}
		}

		private async Task RunBrightnessCalibration() {
			await MoveToStep(2);

			try {
				var emitters = await GetEmitterList();
				if (emitters == null || emitters.Count == 0) {
					await DisplayAlert("No Lights", "No lights found. Make sure devices are linked.", "OK");
					await MoveToStep(0);
					return;
				}

				CalibrationProgress.Progress = 0.1;
				ProgressDetail.Text = $"Found {emitters.Count} lights. Flashing all to white...";

				await FlashAllEmitters(emitters, "#FFFFFF");
				await Task.Delay(1500);

				CalibrationProgress.Progress = 0.2;
				ProgressDetail.Text = "Dimming all lights...";
				await FlashAllEmitters(emitters, "#000000");
				await Task.Delay(500);

				await RunSequenceDetection(emitters);
			} catch (Exception ex) {
				Debug.WriteLine("Brightness calibration error: " + ex.Message);
				_lastResult = new CalibrationResult {
					Success = false,
					ErrorMessage = "Brightness calibration failed: " + ex.Message
				};
				await MoveToStep(4);
			}
		}

		private async Task RunSequenceDetection(List<string> emitters) {
			await MoveToStep(3);
			var totalSteps = emitters.Count + 2;
			var mappedCount = 0;

			try {
				ProgressDetail.Text = "Running chase sequence...";
				await PostCalibrationCommand("/api/RoomMap/calibrate/chase",
					JsonConvert.SerializeObject(emitters));
				await Task.Delay(500);

				for (var i = 0; i < emitters.Count; i++) {
					var emitterId = emitters[i];
					var progress = (double)(i + 1) / totalSteps;
					CalibrationProgress.Progress = 0.3 + progress * 0.5;
					ProgressDetail.Text = $"Flashing light {i + 1} of {emitters.Count}...";

					await PostCalibrationCommand(
						$"/api/RoomMap/calibrate/flash/{Uri.EscapeDataString(emitterId)}",
						null);

					await Task.Delay(800);
					mappedCount++;
				}

				CalibrationProgress.Progress = 0.85;
				ProgressDetail.Text = "Solving positions...";

				var solveResult = await PostCalibrationCommand("/api/RoomMap/calibrate/solve", "{}");
				CalibrationProgress.Progress = 1.0;

				_lastResult = new CalibrationResult {
					Success = true,
					EmittersMapped = mappedCount,
					Summary = $"Detected {mappedCount} of {emitters.Count} lights.\n" +
					          "Open the web UI to fine-tune positions."
				};
			} catch (Exception ex) {
				_lastResult = new CalibrationResult {
					Success = false,
					EmittersMapped = mappedCount,
					ErrorMessage = "Detection failed: " + ex.Message
				};
			}

			await MoveToStep(4);
		}

		private async Task<List<string>> GetEmitterList() {
			try {
				var response = await _http.GetAsync(_baseUrl + "/api/RoomMap");
				if (!response.IsSuccessStatusCode) {
					return new List<string>();
				}

				var json = await response.Content.ReadAsStringAsync();
				var graph = JsonConvert.DeserializeObject<EmitterGraphResponse>(json);
				return graph?.EmitterIds ?? new List<string>();
			} catch (Exception ex) {
				Debug.WriteLine("Error getting emitters: " + ex.Message);

				var devices = await GetDeviceList();
				return devices;
			}
		}

		private async Task<List<string>> GetDeviceList() {
			try {
				var response = await _http.GetAsync(_baseUrl + "/api/Glimmr/devices");
				if (!response.IsSuccessStatusCode) {
					return new List<string>();
				}

				var json = await response.Content.ReadAsStringAsync();
				var devices = JsonConvert.DeserializeObject<List<DeviceStub>>(json);
				var ids = new List<string>();
				if (devices != null) {
					foreach (var d in devices) {
						if (!string.IsNullOrEmpty(d.Id) && d.Enable) {
							ids.Add(d.Id);
						}
					}
				}

				return ids;
			} catch {
				return new List<string>();
			}
		}

		private async Task FlashAllEmitters(List<string> emitters, string hexColor) {
			foreach (var id in emitters) {
				try {
					await PostCalibrationCommand(
						$"/api/RoomMap/calibrate/flash/{Uri.EscapeDataString(id)}",
						null);
				} catch {
					// best-effort
				}
			}
		}

		private async Task<string> PostCalibrationCommand(string endpoint, string jsonBody) {
			try {
				HttpResponseMessage response;
				if (jsonBody != null) {
					var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
					response = await _http.PostAsync(_baseUrl + endpoint, content);
				} else {
					response = await _http.PostAsync(_baseUrl + endpoint, null);
				}

				return await response.Content.ReadAsStringAsync();
			} catch (Exception ex) {
				Debug.WriteLine($"Calibration command {endpoint} failed: " + ex.Message);
				throw;
			}
		}

		private class EmitterGraphResponse {
			[JsonProperty("emitters")]
			public List<EmitterStub> Emitters { get; set; }

			[JsonIgnore]
			public List<string> EmitterIds {
				get {
					var ids = new List<string>();
					if (Emitters != null) {
						foreach (var e in Emitters) {
							if (!string.IsNullOrEmpty(e.Id)) {
								ids.Add(e.Id);
							}
						}
					}

					return ids;
				}
			}
		}

		private class EmitterStub {
			[JsonProperty("id")] public string Id { get; set; }
			[JsonProperty("providerId")] public string ProviderId { get; set; }
			[JsonProperty("deviceId")] public string DeviceId { get; set; }
		}

		private class DeviceStub {
			[JsonProperty("id")] public string Id { get; set; }
			[JsonProperty("enable")] public bool Enable { get; set; }
			[JsonProperty("tag")] public string Tag { get; set; }
		}

		private class CalibrationResult {
			public bool Success { get; set; }
			public int EmittersMapped { get; set; }
			public string Summary { get; set; }
			public string ErrorMessage { get; set; }
		}
	}
}
