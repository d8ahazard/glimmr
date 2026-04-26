using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Models.FullSpectrum;
using Glimmr.Services;
using Microsoft.AspNetCore.Mvc;

namespace Glimmr.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RoomMapController : ControllerBase {
	private readonly RoomMapService _roomMapService;
	private readonly CalibrationService _calibrationService;

	public RoomMapController(RoomMapService roomMapService, CalibrationService calibrationService) {
		_roomMapService = roomMapService;
		_calibrationService = calibrationService;
	}

	/// <summary>
	///     Get the current EmitterGraph.
	/// </summary>
	[HttpGet("")]
	public ActionResult<EmitterGraph> GetMap() {
		return _roomMapService.GetGraph();
	}

	/// <summary>
	///     Add an emitter to the room map.
	/// </summary>
	[HttpPost("emitters")]
	public ActionResult<RoomMapEmitter> AddEmitter([FromBody] RoomMapEmitter emitter) {
		var added = _roomMapService.AddEmitter(emitter);
		return CreatedAtAction(nameof(GetMap), added);
	}

	/// <summary>
	///     Update an emitter's position and/or orientation.
	/// </summary>
	[HttpPut("emitters/{id}")]
	public ActionResult<RoomMapEmitter> UpdateEmitter(string id, [FromBody] EmitterUpdateRequest request) {
		var updated = _roomMapService.UpdateEmitter(id, request.Position, request.Orientation);
		if (updated == null) return NotFound();
		return updated;
	}

	/// <summary>
	///     Remove an emitter from the room map.
	/// </summary>
	[HttpDelete("emitters/{id}")]
	public ActionResult RemoveEmitter(string id) {
		var removed = _roomMapService.RemoveEmitter(id);
		if (!removed) return NotFound();
		return NoContent();
	}

	/// <summary>
	///     Import emitters from a provider's device data.
	/// </summary>
	[HttpPost("import/{providerId}")]
	public ActionResult<List<RoomMapEmitter>> ImportFromProvider(string providerId) {
		var imported = _roomMapService.ImportFromProvider(providerId);
		return Ok(imported);
	}

	/// <summary>
	///     Flash a single emitter for calibration identification.
	/// </summary>
	[HttpPost("calibrate/flash/{emitterId}")]
	public ActionResult<RoomMapEmitter> FlashEmitter(string emitterId, [FromBody] RgbColor color) {
		var result = _calibrationService.FlashEmitter(emitterId, color);
		if (result == null) return NotFound();
		return result;
	}

	/// <summary>
	///     Run a chase sequence across the given emitter IDs.
	/// </summary>
	[HttpPost("calibrate/chase")]
	public async Task<ActionResult<List<string>>> ChaseSequence([FromBody] string[] emitterIds) {
		var chased = await _calibrationService.ChaseSequence(emitterIds);
		return Ok(chased);
	}

	/// <summary>
	///     Solve 3D positions from calibration data.
	/// </summary>
	[HttpPost("calibrate/solve")]
	public ActionResult<Dictionary<string, Vector3>> SolvePositions([FromBody] CalibrationInput input) {
		var solved = _calibrationService.SolvePositions(input);
		return Ok(solved);
	}

	/// <summary>
	///     List available spatial effect modes.
	/// </summary>
	[HttpGet("effects/modes")]
	public ActionResult<IReadOnlyList<string>> GetEffectModes() {
		return Ok(SpatialEffectsEngine.AvailableModes.Select(m => m.ToString()).ToList());
	}
}

public sealed class EmitterUpdateRequest {
	public Vector3? Position { get; init; }
	public Vector3? Orientation { get; init; }
}
