using System.Diagnostics;
using HueDream.Models;
using Microsoft.AspNetCore.Mvc;

namespace HueDream.Controllers {
    public class HomeController : Controller {
        public IActionResult Index() {
            return View();
        }
        
        public IActionResult ConnectHue() {
            return View();
        }
        
        public IActionResult Index2() {
            return View();
        }
        
        public IActionResult Error() {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}