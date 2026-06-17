using Cetee.Data;
using Cetee.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Cetee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
       
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        { 
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }
    }
}
