#region

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

#endregion

namespace GlimmrControl.Core {
	/*
	 *  Source: https://stackoverflow.com/questions/2434534/serialize-an-object-to-string
	 */

	//convert GlimmrDevice list <-> string
	internal static class Serialization {
		public static string SerializeObject<T>(T toSerialize) {
			var xmlSerializer = new XmlSerializer(toSerialize.GetType());

			var ws = new XmlWriterSettings {
				NewLineHandling = NewLineHandling.None,
				Indent = false
			};
			var stringBuilder = new StringBuilder();
			using (var xmlWriter = XmlWriter.Create(stringBuilder, ws)) {
				xmlSerializer.Serialize(xmlWriter, toSerialize);
				return stringBuilder.ToString();
			}
		}

		public static ObservableCollection<GlimmrDevice> Deserialize(string toDeserialize) {
			Debug.WriteLine(toDeserialize);

			try {
				var xmlSerializer = new XmlSerializer(typeof(ObservableCollection<GlimmrDevice>));
				using (var textReader = new StringReader(toDeserialize)) {
					return xmlSerializer.Deserialize(textReader) as ObservableCollection<GlimmrDevice>;
				}
			} catch {
				return null;
			}
		}
	}
}