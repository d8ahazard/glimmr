using System.Collections.Generic;
using Glimmr.Models.FullSpectrum;
using Microsoft.AspNetCore.Mvc;

namespace Glimmr.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FullSpectrumController : ControllerBase {
	/// <summary>
	///     Returns the provider, protocol, ecosystem, Room Map, UX, and validation catalog for the full-spectrum roadmap.
	/// </summary>
	[HttpGet("")]
	public ActionResult<FullSpectrumCatalog> GetCatalog() {
		return FullSpectrumCatalogProvider.GetCatalog();
	}

	/// <summary>
	///     Returns every current ColorTarget provider manifest.
	/// </summary>
	[HttpGet("targets")]
	public ActionResult<IReadOnlyList<ProviderManifest>> GetTargets() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().Targets);
	}

	/// <summary>
	///     Returns one current ColorTarget provider manifest.
	/// </summary>
	[HttpGet("targets/{id}")]
	public ActionResult<ProviderManifest> GetTarget(string id) {
		var target = FullSpectrumCatalogProvider.GetTarget(id);
		return target == null ? NotFound() : target;
	}

	/// <summary>
	///     Returns every current ColorSource provider manifest.
	/// </summary>
	[HttpGet("sources")]
	public ActionResult<IReadOnlyList<ProviderManifest>> GetSources() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().Sources);
	}

	/// <summary>
	///     Returns one current ColorSource provider manifest.
	/// </summary>
	[HttpGet("sources/{id}")]
	public ActionResult<ProviderManifest> GetSource(string id) {
		var source = FullSpectrumCatalogProvider.GetSource(id);
		return source == null ? NotFound() : source;
	}

	/// <summary>
	///     Returns prioritized realtime and bridge protocol manifests.
	/// </summary>
	[HttpGet("protocols")]
	public ActionResult<IReadOnlyList<ProtocolManifest>> GetProtocols() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().Protocols);
	}

	/// <summary>
	///     Returns ranked ecosystem integration manifests.
	/// </summary>
	[HttpGet("ecosystems")]
	public ActionResult<IReadOnlyList<EcosystemManifest>> GetEcosystems() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().Ecosystems);
	}

	/// <summary>
	///     Returns the Room Map data, calibration, importer, and spatial effects manifest.
	/// </summary>
	[HttpGet("roomMap")]
	public ActionResult<RoomMapManifest> GetRoomMap() {
		return FullSpectrumCatalogProvider.GetCatalog().RoomMap;
	}

	/// <summary>
	///     Returns an empty emitter graph using the Room Map contract shape.
	/// </summary>
	[HttpGet("roomMap/emitterGraph")]
	public ActionResult<EmitterGraph> GetEmitterGraphSchema() {
		return new EmitterGraph();
	}

	/// <summary>
	///     Returns an empty signal frame using the source-to-spatial-engine contract shape.
	/// </summary>
	[HttpGet("signals/frame")]
	public ActionResult<SignalFrame> GetSignalFrameSchema() {
		return new SignalFrame();
	}

	/// <summary>
	///     Returns an empty output scheduler profile using the latency compensation contract shape.
	/// </summary>
	[HttpGet("scheduler/profile")]
	public ActionResult<OutputSchedulerProfile> GetSchedulerProfileSchema() {
		return new OutputSchedulerProfile();
	}

	/// <summary>
	///     Returns the onboarding, dashboard, and preview UX manifest.
	/// </summary>
	[HttpGet("ux")]
	public ActionResult<IReadOnlyList<UserExperienceManifest>> GetUserExperience() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().UserExperience);
	}

	/// <summary>
	///     Returns validation standards for providers, ecosystems, Room Map, and privacy.
	/// </summary>
	[HttpGet("validation")]
	public ActionResult<IReadOnlyList<ValidationManifest>> GetValidation() {
		return Ok(FullSpectrumCatalogProvider.GetCatalog().Validation);
	}

	/// <summary>
	///     Runs executable validation checks against the full-spectrum catalog.
	/// </summary>
	[HttpGet("validation/report")]
	public ActionResult<ValidationReport> GetValidationReport() {
		return FullSpectrumValidator.Validate(FullSpectrumCatalogProvider.GetCatalog());
	}
}
