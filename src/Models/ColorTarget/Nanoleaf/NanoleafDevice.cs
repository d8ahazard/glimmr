using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Nanoleaf.Client;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	public sealed class NanoleafDevice : IStreamingDevice, IDisposable {
		private string _token;
		private string _basePath;
		private NanoLayout _layout;
		private int _streamMode;
		private bool _disposed;
		private bool _sending;
		public bool Enable { get; set; }
		StreamingData IStreamingDevice.Data {
			get => Data;
			set => Data = (NanoleafData) value;
		}

		public NanoleafData Data { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		private readonly UdpClient _sender;
		private readonly HttpClient _client;

		

		/// <summary>
		/// Use this for discovery only 
		/// </summary>
		/// <param name="data">NL Data</param>
		/// <param name="client">Client to discover with</param>
		public NanoleafDevice(NanoleafData data, HttpClient client) {
			IpAddress = data.IpAddress;
			_token = data.Token;
			_basePath = "http://" + IpAddress + ":16021/api/v1/" + _token;
			_disposed = false;
			_client = client;
		}
		

		/// <summary>
		/// Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="socket"></param>
		/// <param name="client"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, UdpClient socket, HttpClient client, ColorService colorService) {
			DataUtil.GetItem<int>("captureMode");
			colorService.ColorSendEvent += SetColor;
			if (n != null) {
				SetData(n);
				_sender = socket;
				_client = client;
			}

			_disposed = false;
		}
		
		public void StartStream(CancellationToken ct) {
			if (!Enable || Streaming) return;
			Streaming = true;

			Log.Debug($@"Nanoleaf: Starting panel: {IpAddress}");
			var controlVersion = "v" + _streamMode;
			var body = new
				{write = new {command = "display", animType = "extControl", extControlVersion = controlVersion}};

			_ = SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
				"state").Result;
			_ = SendPutRequest(_basePath, JsonConvert.SerializeObject(body), "effects").Result;
			
		}

		public void StopStream() {
			if (!Streaming || !Enable) return;
			Streaming = false;
			SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = false}}), "state")
				.ConfigureAwait(false);
			Log.Debug($@"Nanoleaf: Stopped panel: {IpAddress}");

		}


		public void SetColor(List<Color> _, List<Color> colors, double fadeTime = 1) {
			if (!Streaming || !Enable || Testing) {
				return;
			}

			if (colors == null) {
				return;
			}
			
			var byteString = new List<byte>();
			if (_streamMode == 2) {
				byteString.AddRange(ByteUtils.PadInt(_layout.NumPanels));
			} else {
				byteString.Add(ByteUtils.IntByte(_layout.NumPanels));
			}
			foreach (var pd in _layout.PositionData) {
				var id = pd.PanelId;
				var colorInt = pd.TargetSector - 1;
				if (_streamMode == 2) {
					byteString.AddRange(ByteUtils.PadInt(id));
				} else {
					byteString.Add(ByteUtils.IntByte(id));
				}

				var color = Color.FromArgb(0, 0, 0, 0);
				if (pd.TargetSector != -1) {
					color = colors[colorInt];
				}
				
				if (Brightness < 100) {
					color = ColorTransformUtil.ClampBrightness(color, Brightness);
				}

				// Add rgb values
				byteString.Add(ByteUtils.IntByte(color.R));
				byteString.Add(ByteUtils.IntByte(color.G));
				byteString.Add(ByteUtils.IntByte(color.B));
				// White value
				byteString.AddRange(ByteUtils.PadInt(0, 1));
				// Pad duration time
				byteString.AddRange(_streamMode == 2 ? ByteUtils.PadInt((int)fadeTime) : ByteUtils.PadInt((int)fadeTime, 1));
			}
			SendUdpUnicast(byteString.ToArray());
		}



		public void ReloadData() {
			var newData = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nanoleaf", Id);
			SetData(newData);
		}

		private void SetData(NanoleafData n) {
			Data = n;
			DataUtil.GetItem<int>("captureMode");
			IpAddress = n.IpAddress;
			_token = n.Token;
			_layout = n.Layout;
			Brightness = n.Brightness;
			var nanoType = n.Type;
			Enable = n.Enable;
			_streamMode = nanoType == "NL29" ? 2 : 1;
			_basePath = "http://" + IpAddress + ":16021/api/v1/" + _token;
			Id = n.Id;
		}

		public void FlashColor(Color color) {
			var byteString = new List<byte>();
			if (_streamMode == 2) {
				byteString.AddRange(ByteUtils.PadInt(_layout.NumPanels));
			} else {
				byteString.Add(ByteUtils.IntByte(_layout.NumPanels));
			}
			foreach (var pd in _layout.PositionData) {
				var id = pd.PanelId;
				if (_streamMode == 2) {
					byteString.AddRange(ByteUtils.PadInt(id));
				} else {
					byteString.Add(ByteUtils.IntByte(id));
				} 
				
				// Add rgb values
				byteString.Add(ByteUtils.IntByte(color.R));
				byteString.Add(ByteUtils.IntByte(color.G));
				byteString.Add(ByteUtils.IntByte(color.B));
				// White value
				byteString.AddRange(ByteUtils.PadInt(0, 1));
				// Pad duration time
				byteString.AddRange(_streamMode == 2 ? ByteUtils.PadInt(0) : ByteUtils.PadInt(0, 1));
			}
			SendUdpUnicast(byteString.ToArray());
		}

		public bool IsEnabled() {
			return Enable;
		}

        
		public bool Streaming { get; set; }

		

     
		public async Task<UserToken> CheckAuth() {
			var nanoleaf = new NanoleafClient(IpAddress);
			UserToken result = null;
			try {
				result = await nanoleaf.CreateTokenAsync().ConfigureAwait(false);
				Log.Debug("Authorized.");
			} catch (AggregateException e) {
				Log.Debug("Unauthorized Exception: " + e.Message);
			}

			nanoleaf.Dispose();
			return result;
		}

		private void SendUdpUnicast(byte[] data) {
			if (_sending) return;
			_sending = true;
			var ep = IpUtil.Parse(IpAddress, 60222);
			if (ep != null) _sender.SendAsync(data, data.Length, ep);
			_sending = false;
		}

		public async Task<NanoLayout> GetLayout() {
			if (string.IsNullOrEmpty(_token)) return null;
			var fLayout = await SendGetRequest(_basePath, "panelLayout/layout").ConfigureAwait(false);
			var lObject = JsonConvert.DeserializeObject<NanoLayout>(fLayout);
			return lObject;
		}

		private async Task<string> SendPutRequest(string basePath, string json, string path = "") {
            var authorizedPath = new Uri(basePath + "/" + path);
            try {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var responseMessage = await _client.PutAsync(authorizedPath, content).ConfigureAwait(false);
                if (!responseMessage.IsSuccessStatusCode) {
                    HandleNanoleafErrorStatusCodes(responseMessage);
                }

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        private async Task<string> SendGetRequest(string basePath, string path = "") {
            var authorizedPath = basePath + "/" + path;
            var uri = new Uri(authorizedPath);
            try {
                using var responseMessage = await _client.GetAsync(uri).ConfigureAwait(false);
                if (responseMessage.IsSuccessStatusCode)
                    return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Debug("Error contacting nanoleaf: " + responseMessage.Content);
                HandleNanoleafErrorStatusCodes(responseMessage);

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        private static void HandleNanoleafErrorStatusCodes(HttpResponseMessage responseMessage) {
	        Log.Warning("Error with nano request: ", responseMessage);
        }


        public void Dispose() {
			Dispose(true);
		}

		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (!disposing) return;
			_disposed = true;
		}
	}
}