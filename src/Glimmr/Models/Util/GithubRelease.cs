#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Util;

public class GithubRelease {
	[JsonProperty] public Author author { get; set; } = new();
	[JsonProperty] public bool draft { get; set; }
	[JsonProperty] public bool prerelease { get; set; }
	[JsonProperty] public DateTime created_at { get; set; }
	[JsonProperty] public DateTime published_at { get; set; }
	[JsonProperty] public int id { get; set; }
	[JsonProperty] public List<Asset> assets { get; set; } = new();
	[JsonProperty] public string assets_url { get; set; } = "";
	[JsonProperty] public string body { get; set; } = "";
	[JsonProperty] public string html_url { get; set; } = "";
	[JsonProperty] public string name { get; set; } = "";
	[JsonProperty] public string node_id { get; set; } = "";
	[JsonProperty] public string tag_name { get; set; } = "";
	[JsonProperty] public string tarball_url { get; set; } = "";
	[JsonProperty] public string target_commitish { get; set; } = "";
	[JsonProperty] public string upload_url { get; set; } = "";
	[JsonProperty] public string url { get; set; } = "";
	[JsonProperty] public string zipball_url { get; set; } = "";

	public class Author {
		[JsonProperty] public bool site_admin { get; set; }
		[JsonProperty] public int id { get; set; }
		[JsonProperty] public string avatar_url { get; set; } = "";
		[JsonProperty] public string events_url { get; set; } = "";
		[JsonProperty] public string followers_url { get; set; } = "";
		[JsonProperty] public string following_url { get; set; } = "";
		[JsonProperty] public string gists_url { get; set; } = "";
		[JsonProperty] public string gravatar_id { get; set; } = "";
		[JsonProperty] public string html_url { get; set; } = "";
		[JsonProperty] public string login { get; set; } = "";
		[JsonProperty] public string node_id { get; set; } = "";
		[JsonProperty] public string organizations_url { get; set; } = "";
		[JsonProperty] public string received_events_url { get; set; } = "";
		[JsonProperty] public string repos_url { get; set; } = "";
		[JsonProperty] public string starred_url { get; set; } = "";
		[JsonProperty] public string subscriptions_url { get; set; } = "";
		[JsonProperty] public string type { get; set; } = "";
		[JsonProperty] public string url { get; set; } = "";
	}

	public class Uploader {
		[JsonProperty] public bool site_admin { get; set; }
		[JsonProperty] public int id { get; set; }
		[JsonProperty] public string avatar_url { get; set; } = "";
		[JsonProperty] public string events_url { get; set; } = "";
		[JsonProperty] public string followers_url { get; set; } = "";
		[JsonProperty] public string following_url { get; set; } = "";
		[JsonProperty] public string gists_url { get; set; } = "";
		[JsonProperty] public string gravatar_id { get; set; } = "";
		[JsonProperty] public string html_url { get; set; } = "";
		[JsonProperty] public string login { get; set; } = "";
		[JsonProperty] public string node_id { get; set; } = "";
		[JsonProperty] public string organizations_url { get; set; } = "";
		[JsonProperty] public string received_events_url { get; set; } = "";
		[JsonProperty] public string repos_url { get; set; } = "";
		[JsonProperty] public string starred_url { get; set; } = "";
		[JsonProperty] public string subscriptions_url { get; set; } = "";
		[JsonProperty] public string type { get; set; } = "";
		[JsonProperty] public string url { get; set; } = "";
	}

	public class Asset {
		[JsonProperty] public DateTime created_at { get; set; }
		[JsonProperty] public DateTime updated_at { get; set; }
		[JsonProperty] public int download_count { get; set; }
		[JsonProperty] public int id { get; set; }
		[JsonProperty] public int size { get; set; }
		[JsonProperty] public object label { get; set; } = "";
		[JsonProperty] public string browser_download_url { get; set; } = "";
		[JsonProperty] public string content_type { get; set; } = "";
		[JsonProperty] public string name { get; set; } = "";
		[JsonProperty] public string node_id { get; set; } = "";
		[JsonProperty] public string state { get; set; } = "";
		[JsonProperty] public string url { get; set; } = "";
		[JsonProperty] public Uploader uploader { get; set; } = new();
	}
}