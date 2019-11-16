using HueDream.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace HueDream.Controllers {
    public class HomeController : Controller {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger) {
            _logger = logger;
        }

        public IActionResult Index() {
            Console.WriteLine("Returning index.");
            return View();
        }

        public IActionResult Privacy() {
            return View();
        }

        public IActionResult connectHue() {
            return View();
        }


        [HttpPost]
        public void Post([FromBody] string value) {
            _logger.LogDebug("Post received.", value);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
