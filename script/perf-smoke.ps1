param(
	[string]$HostUrl = "http://127.0.0.1:5000",
	[int]$DurationSeconds = 30,
	[int]$SampleEverySeconds = 5,
	[switch]$ResetFirst
)

$base = "$HostUrl/api/Glimmr"

function Get-Json([string]$url) {
	return Invoke-RestMethod -Method Get -Uri $url
}

if ($ResetFirst) {
	Invoke-RestMethod -Method Post -Uri "$base/stats/perf/reset" | Out-Null
}

$samples = @()
$iterations = [Math]::Max(1, [Math]::Floor($DurationSeconds / [Math]::Max(1, $SampleEverySeconds)))

for ($i = 0; $i -lt $iterations; $i++) {
	$perf = Get-Json "$base/stats/perf"
	$samples += [pscustomobject]@{
		Timestamp = (Get-Date).ToString("o")
		FramesProcessed = $perf.pipeline.framesProcessed
		FramesDropped = $perf.pipeline.framesDropped
		AvgUpdateMs = $perf.pipeline.avgUpdateMs
		AvgComputeMs = $perf.pipeline.avgComputeMs
		AvgDispatchMs = $perf.pipeline.avgDispatchMs
		AvgPreviewMs = $perf.pipeline.avgPreviewMs
		MaxUpdateMs = $perf.pipeline.maxUpdateMs
		PreviewFrames = $perf.preview.framesSent
		PreviewBytes = $perf.preview.bytesSent
		PreviewAvgMs = $perf.preview.avgEncodeAndSendMs
		PreviewMaxMs = $perf.preview.maxEncodeAndSendMs
		TargetCount = @($perf.targets.PSObject.Properties).Count
	}
	Start-Sleep -Seconds $SampleEverySeconds
}

$final = Get-Json "$base/stats/perf"
[pscustomobject]@{
	Host = $HostUrl
	DurationSeconds = $DurationSeconds
	Samples = $samples
	Final = $final
} | ConvertTo-Json -Depth 8
