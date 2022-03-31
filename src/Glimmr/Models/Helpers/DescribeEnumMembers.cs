#region

using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

#endregion

namespace Glimmr.Models.Helpers;

public class DescribeEnumMembers : ISchemaFilter {
	private readonly XDocument? _xmlComments;

	/// <summary>
	///     Initialize schema filter.
	/// </summary>
	/// <param name="xmlPath">Path of our xml file.</param>
	public DescribeEnumMembers(string xmlPath) {
		if (File.Exists(xmlPath)) {
			_xmlComments = XDocument.Load(xmlPath);
		}
	}

	/// <summary>
	///     Apply this schema filter.
	/// </summary>
	/// <param name="argSchema">Target schema object.</param>
	/// <param name="argContext">Schema filter context.</param>
	public void Apply(OpenApiSchema argSchema, SchemaFilterContext argContext) {
		if (_xmlComments == null) {
			return;
		}

		var enumType = argContext.Type;
		if (!enumType.IsEnum) {
			return;
		}

		var sb = new StringBuilder(argSchema.Description);
		sb.AppendLine("<p>Possible values:</p>");
		sb.AppendLine("<ul>");

		var names = Enum.GetNames(enumType);
		var values = Enum.GetValues(enumType);
		for (var i = 0; i < names.Length; i++) {
			var enumMemberName = names[i];
			var enumValue = (int)(values.GetValue(i) ?? 0);
			var fullEnumMemberName = $"F:{enumType.FullName}.{enumMemberName}";

			var enumMemberDescription = _xmlComments.XPathEvaluate(
				$"normalize-space(//member[@name = '{fullEnumMemberName}']/summary/text())"
			) as string;

			if (string.IsNullOrEmpty(enumMemberDescription)) {
				continue;
			}

			sb.AppendLine($"<li><b>{enumMemberName} = {enumValue}</b> ({enumMemberDescription})</li>");
		}

		sb.AppendLine("</ul>");
		argSchema.Description = sb.ToString();
	}
}