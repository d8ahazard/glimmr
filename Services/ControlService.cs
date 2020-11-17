using System;
using Common.Logging.Configuration;

namespace Glimmr.Services {
	public class ControlService {
		public event ArgUtils.Action ReloadData = delegate { };
		public event ArgUtils.Action TriggerRefresh = delegate { };
		public event Action<int> SetMode = delegate { };
		
		public void Reload() {
			ReloadData();
		}

		public void Mode(int mode) {
			SetMode(mode);
		}

		public void RefreshDevices() {
			TriggerRefresh();
		}
	}
}