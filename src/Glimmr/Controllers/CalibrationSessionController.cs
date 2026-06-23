using Glimmr.Services;
using Microsoft.AspNetCore.Mvc;

namespace Glimmr.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CalibrationSessionController : ControllerBase {
	private readonly CalibrationRtcService _rtc;

	public CalibrationSessionController(CalibrationRtcService rtc) {
		_rtc = rtc;
	}

	[HttpPost("session")]
	public ActionResult<CalibrationSessionState> Create([FromBody] CalibrationSessionCreateRequest? req) {
		var state = _rtc.CreateSession(req?.SceneId ?? "");
		return Ok(state);
	}

	[HttpGet("session/{sessionId}")]
	public ActionResult<CalibrationSessionState> Get(string sessionId) {
		var state = _rtc.GetSession(sessionId);
		if (state == null) return NotFound();
		return Ok(state);
	}

	[HttpPost("session/{sessionId}/frame")]
	public IActionResult AddFrame(string sessionId, [FromBody] CalibrationFrameUploadRequest? req) {
		if (req == null || string.IsNullOrWhiteSpace(req.DataUrl)) {
			return BadRequest("dataUrl required");
		}

		var ok = _rtc.AddFrame(sessionId, req.DataUrl);
		return ok ? Ok(new { ok = true }) : NotFound();
	}

	[HttpPost("session/{sessionId}/complete")]
	public IActionResult Complete(string sessionId) {
		var ok = _rtc.Complete(sessionId);
		return ok ? Ok(new { ok = true }) : NotFound();
	}

	// WebRTC signaling placeholders for client negotiation wiring.
	[HttpGet("session/{sessionId}/webrtc/offer")]
	public IActionResult Offer(string sessionId) {
		var state = _rtc.GetSession(sessionId);
		if (state == null) return NotFound();
		return Ok(new {
			type = "offer",
			sdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=GlimmrCalibration\r\nt=0 0\r\n",
			sessionId
		});
	}

	[HttpPost("session/{sessionId}/webrtc/answer")]
	public IActionResult Answer(string sessionId, [FromBody] object body) {
		var state = _rtc.GetSession(sessionId);
		if (state == null) return NotFound();
		return Ok(new { ok = true, sessionId, received = body != null });
	}
}
