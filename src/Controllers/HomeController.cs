using System.Diagnostics;
using Glimmr.Models;
using Microsoft.AspNetCore.Mvc;

namespace Glimmr.Controllers {
	public class HomeController : Controller {
		public IActionResult Index() {
			return View();
		}

		public IActionResult Error() {
			return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
		}
	}
}