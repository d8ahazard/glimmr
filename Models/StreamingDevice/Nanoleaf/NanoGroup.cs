using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Nanoleaf.Client;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice.Nanoleaf {
	public sealed class NanoGroup : IStreamingDevice, IDisposable {
		private string _token;
		private string _basePath;
		private NanoLayout _layout;
		private int _streamMode;
		private bool _disposed;
		private bool _sending;
		private int _captureMode;
		public bool Enable { get; set; }
		StreamingData IStreamingDevice.Data {
			get => Data;
			set => Data = (NanoData) value;
		}

		public NanoData Data { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		private HttpClient _hc;
		private readonly Socket _sender;


		public NanoGroup(string ipAddress, string token = "") {
			_captureMode = DataUtil.GetItem<int>("captureMode");
			IpAddress = ipAddress;
			_token = token;
			_hc = new HttpClient();
			_basePath = "http://" + IpAddress + ":16021/api/v1/" + _token;
			_disposed = false;
		}

		public NanoGroup(NanoData n, HttpClient hc, Socket hs) {
			_captureMode = DataUtil.GetItem<int>("captureMode");
			if (n != null) {
				SetData(n);
				_hc = hc;
				_sender = hs;
			}

			_disposed = false;
		}


		public void ReloadData() {
			var newData = DataUtil.GetCollectionItem<NanoData>("Dev_NanoLeaf", Id);
			SetData(newData);
		}

		private void SetData(NanoData n) {
			Data = n;
			_captureMode = DataUtil.GetItem<int>("captureMode");
			IpAddress = n.IpAddress;
			_token = n.Token;
			_layout = n.Layout;
			Brightness = n.Brightness;
			var nanoType = n.Type;
			_streamMode = nanoType == "NL29" ? 2 : 1;
			_basePath = "http://" + IpAddress + ":16021/api/v1/" + _token;
			Id = n.Id;
		}
        
		public bool IsEnabled() {
			return Data.Enable;
		}

        
		public bool Streaming { get; set; }

		public async void StartStream(CancellationToken ct) {
			if (!Data.Enable) return;
			LogUtil.Write($@"Nanoleaf: Starting panel: {IpAddress}");
			// Turn it on first.
			//var currentState = NanoSender.SendGetRequest(_basePath).Result;
			//await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
			//"state");
			var controlVersion = "v" + _streamMode;
			var body = new
				{write = new {command = "display", animType = "extControl", extControlVersion = controlVersion}};

			await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
				"state");
			await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(body), "effects");
			LogUtil.Write("Nanoleaf: Streaming is active...");
			_sending = true;
			while (!ct.IsCancellationRequested) {
				Streaming = true;
			}
			_sending = false;
			StopStream();
		}

		public void StopStream() {
			Streaming = false;
			NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = false}}), "state")
				.ConfigureAwait(false);
			LogUtil.Write($@"Nanoleaf: Stopped panel: {IpAddress}");

		}


		public void SetColor(List<Color> colors, double fadeTime = 1) {
			int ft = (int) fadeTime;
			if (!Streaming) {
				LogUtil.Write("Streaming is  not active?");
				return;
			}

			var capCount = _captureMode == 0 ? 12 : 28;
            
			if (colors == null || colors.Count < capCount) {
				throw new ArgumentException("Invalid color list.");
			}

			var byteString = new List<byte>();
			if (_streamMode == 2) {
				byteString.AddRange(ByteUtils.PadInt(_layout.NumPanels));
			} else {
				byteString.Add(ByteUtils.IntByte(_layout.NumPanels));
			}
			foreach (var pd in _layout.PositionData) {
				var id = pd.PanelId;
				var colorInt = _captureMode == 0 ?  pd.TargetSector - 1 : pd.TargetSectorV2 - 1;
				if (_streamMode == 2) {
					byteString.AddRange(ByteUtils.PadInt(id));
				} else {
					byteString.Add(ByteUtils.IntByte(id));
				}

				if (pd.TargetSector == -1) continue;
				//LogUtil.Write("Sector for light " + id + " is " + pd.Sector);
				var color = colors[colorInt];
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
				byteString.AddRange(_streamMode == 2 ? ByteUtils.PadInt(ft) : ByteUtils.PadInt(ft, 1));
			}

			Task.Run(() => {
				SendUdpUnicast(byteString.ToArray());
			}); 
                
		}


     
		public async Task<UserToken> CheckAuth() {
			var nanoleaf = new NanoleafClient(IpAddress);
			UserToken result = null;
			try {
				result = await nanoleaf.CreateTokenAsync().ConfigureAwait(false);
				LogUtil.Write("Authorized.");
			} catch (AggregateException e) {
				LogUtil.Write("Unauthorized Exception: " + e.Message);
			}

			nanoleaf.Dispose();
			return result;
		}

		private void SendUdpUnicast(byte[] data) {
			if (!_sending) return;
			var ep = IpUtil.Parse(IpAddress, 60222);
			if (ep != null) _sender.SendTo(data, ep);
		}

		public async Task<NanoLayout> GetLayout() {
			if (string.IsNullOrEmpty(_token)) return null;
			var fLayout = await NanoSender.SendGetRequest(_basePath, "panelLayout/layout").ConfigureAwait(false);
			var lObject = JsonConvert.DeserializeObject<NanoLayout>(fLayout);
			return lObject;
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