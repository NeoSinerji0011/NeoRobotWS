using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using testApp.Models;

namespace testApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {

            return View();
        }
        [HttpPost]
        public IActionResult GetAuthenticatorCode()
        {
            var image = Request.Form.Files[0];
            var url = "http://localhost:51501/mobilapi/GetAuthenticatorCode";

            string responseString = "";

            var method = "POST"; // If your endpoint expects a GET then do it.

            Stream fs = image.OpenReadStream();
            var bmpStoredImage = (Bitmap)Bitmap.FromStream(fs);
            byte[] response_data;
            using (MemoryStream ms = new MemoryStream())
            {
                bmpStoredImage.Save(ms, ImageFormat.Png);
                response_data = ms.ToArray();
            }
            var jsondata = new { FormFile = "" };//canay
            var dataString = JsonConvert.SerializeObject(jsondata);
            try
            {
                // WebClient client = new WebClient();

                //client.Headers[HttpRequestHeader.ContentType] = "multipart/form-data";

                //response_data = client.(url, method, Encoding.UTF8.GetBytes(dataString));
                //responseString = UnicodeEncoding.UTF8.GetString(response_data);
                dynamic result = JsonConvert.DeserializeObject(responseString);

                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {

                        var fileContent = new StreamContent(fs);

                        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                        content.Add(fileContent, "file", "test.txt");

                        var response = client.PostAsync(url, content);
                        response.Wait();
                        var res = response.Result;


                    }
                }
            }
            catch (Exception ex)
            { }

            return Ok(true);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
    class AuthenticatorRequest
    {
        public int TvmKodu { get; set; }
        public IFormFile FormFile { get; set; }

    }
}
