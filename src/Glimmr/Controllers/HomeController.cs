#region

using System.Diagnostics;
using Glimmr.Models;
using Glimmr.Models.Helper;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace Glimmr.Controllers;

public class HomeController : Controller {
	public IActionResult Index() {
		return View();
	}

	public IActionResult Error() {
		return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
	}
}