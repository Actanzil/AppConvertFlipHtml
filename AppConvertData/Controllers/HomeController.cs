using AppConvertData.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Text;
using Syncfusion.PdfToImageConverter;
using System.IO.Compression;
using Newtonsoft.Json;

namespace AppConvertData.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, IWebHostEnvironment env)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _env = env; 
        }

        private string WwwRootPath => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        private string OutputFolderPath => Path.Combine(WwwRootPath, "Output");
        private string ComponentPath => Path.Combine(WwwRootPath, "Component");

        public IActionResult Index()
        {

            var subfolders = Directory.GetDirectories(OutputFolderPath);

            var allFiles = new List<string>();

            foreach (var subfolder in subfolders)
            {
                var indexPath = Path.Combine(subfolder, "index.html");

                if (System.IO.File.Exists(indexPath))
                {
                    allFiles.Add(indexPath);
                }
            }

            ViewData["WwwRootPath"] = WwwRootPath;
            return View(allFiles);
        }


        [HttpPost]
        public IActionResult ExportToImage(IFormFile pdfFile)
        {
            if (pdfFile == null || Path.GetExtension(pdfFile.FileName).ToLower() != ".pdf")
            {
                TempData["ErrorMessage"] = "Silahkan unggah file dengan format .pdf";
                return RedirectToAction("Index");
            }

            try
            {
                
                string baseDirectory = "wwwroot/Output";
                string subfolderName = DateTime.Now.ToString("yyyyMMddHHmmss");
                string outputSubFolderPath = Path.Combine(baseDirectory, subfolderName);

                if (!Directory.Exists(outputSubFolderPath))
                {
                    Directory.CreateDirectory(outputSubFolderPath);
                }

                PdfToImageConverter imageConverter = new PdfToImageConverter();

                using (Stream pdfFileStream = pdfFile.OpenReadStream())
                {
                    imageConverter.Load(pdfFileStream);

                    float dpiX = 100;
                    float dpiY = 100;

                    string pagesFolderPath = Path.Combine(outputSubFolderPath, "packages", "pages");

                    if (!Directory.Exists(pagesFolderPath))
                    {
                        Directory.CreateDirectory(pagesFolderPath);
                    }

                    for (int pageIndex = 0; pageIndex < imageConverter.PageCount; pageIndex++)
                    {
                        Stream outputStream = imageConverter.Convert(pageIndex, dpiX, dpiY, false, false);

                        string imageName = $"{pageIndex + 1}.jpeg";
                        string imagePath = Path.Combine(pagesFolderPath, imageName);
                        System.IO.File.WriteAllBytes(imagePath, ((MemoryStream)outputStream).ToArray());
                    }
                }

                string templatePath = Path.Combine(ComponentPath, "index.html");
                string packagesFolder = Path.Combine(ComponentPath, "packages");
                string outputHtmlPath = Path.Combine(outputSubFolderPath, "index.html");
                string templateContent = System.IO.File.ReadAllText(templatePath);
                StringBuilder htmlContent = new StringBuilder(templateContent);

                htmlContent.Replace("{folderName}", subfolderName);

                htmlContent.Replace("pages: 12", $"pages: {imageConverter.PageCount}");

                System.IO.File.WriteAllText(outputHtmlPath, htmlContent.ToString());

                FileSystem.CopyDirectory(packagesFolder, Path.Combine(outputSubFolderPath, "packages"), UIOption.AllDialogs);

                TempData["Notification"] = "Proses konversi file berhasil!!!";

                var subfolders = Directory.GetDirectories(baseDirectory);
                TempData["OutputList"] = subfolders.Select(subfolder => Path.GetFileName(subfolder)).ToList();

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        public IActionResult DownloadFile(string subfolderName)
        {

            var OutputSubFolderPath = Path.Combine(OutputFolderPath, subfolderName);

            if (Directory.Exists(OutputSubFolderPath))
            {
                var memoryStream = new MemoryStream();

                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var filesInSubfolder = Directory.GetFiles(OutputSubFolderPath, "*", System.IO.SearchOption.AllDirectories);

                    foreach (var filePath in filesInSubfolder)
                    {
                        var relativePath = Path.GetRelativePath(OutputSubFolderPath, filePath);
                        var entryName = Path.Combine(subfolderName, relativePath.Replace("\\", "/"));

                        zipArchive.CreateEntryFromFile(filePath, entryName);
                    }
                }

                memoryStream.Seek(0, SeekOrigin.Begin);

                var zipFileName = $"{subfolderName}.zip";

                return File(memoryStream, "application/zip", zipFileName);
            }

            return NotFound();
        }

        public IActionResult DeleteFile(string subfolderName)
        {

            var OutputSubFolderPath = Path.Combine(OutputFolderPath, subfolderName);

            if (Directory.Exists(OutputSubFolderPath))
            {
                Directory.Delete(OutputSubFolderPath, true);
                TempData["Notification"] = "Data berhasil dihapus!!!";
            }
            else
            {
                TempData["ErrorMessage"] = "Data tidak ditemukan!!!";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult SaveCoordinates(string subfolderName, 
            string imageName, string xCoordinate, string yCoordinate, string widthCoordinate, 
            string heightCoordinate, IFormFile videoFile)
        {
            try
            {
                var OutputSubFolderPath = Path.Combine(OutputFolderPath, subfolderName);
                var outputPackageJson = Path.Combine(OutputSubFolderPath, "packages", "regions");
                var outputPackageVideos = Path.Combine(OutputSubFolderPath, "packages", "videos");

                string jsonTemplatePath = Path.Combine(WwwRootPath, "Component", "packages", "regions", "regions.json");
                string jsonTemplate = System.IO.File.ReadAllText(jsonTemplatePath);

                jsonTemplate = jsonTemplate.Replace("isi disini untuk nilai x", xCoordinate)
                                           .Replace("isi disini untuk nilai y", yCoordinate)
                                           .Replace("isi disini untuk nilai width", widthCoordinate)
                                           .Replace("isi disini untuk nilai height", heightCoordinate)
                                           .Replace("(isi disini untuk nama videos)", Path.GetFileNameWithoutExtension(imageName));

                string jsonFileName = $"{Path.GetFileNameWithoutExtension(imageName)}-regions.json";
                string jsonFilePath = Path.Combine(outputPackageJson, jsonFileName);
                System.IO.File.WriteAllText(jsonFilePath, jsonTemplate);

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imageName);

                if (videoFile != null && videoFile.Length > 0)
                {
                    var videoFileName = $"{fileNameWithoutExtension}-videos.mp4";
                    var videoFilePath = Path.Combine(outputPackageVideos, videoFileName);

                    using (var stream = new FileStream(videoFilePath, FileMode.Create))
                    {
                        videoFile.CopyTo(stream);
                    }
                }

                TempData["SuccessMessage"] = "Koordinat dan video berhasil diunggah.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Gagal mengunggah koordinat atau video: " + ex.Message;
            }

            return RedirectToAction("Customize", new { subfolderName = subfolderName, fileName = $"{imageName}.jpeg" });
        }

        public IActionResult Customize(string subfolderName, string fileName)
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
            ViewBag.ErrorMessage = TempData["ErrorMessage"] as string;

            var subfolderPath = Path.Combine(WwwRootPath, "Output", subfolderName, "packages", "pages");
            var imagePath = $"~/Output/{subfolderName}/packages/pages/{fileName}";
            ViewBag.ImagePath = imagePath;

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            ViewData["FileNameWithoutExtension"] = fileNameWithoutExtension;
            ViewData["WwwRootPath"] = WwwRootPath;
            ViewData["SubfolderName"] = subfolderName;

            var regionsFilePath = Path.Combine(WwwRootPath, "Output", subfolderName, "packages", "regions", $"{fileNameWithoutExtension}-regions.json");
            var videoFilePath = Path.Combine(WwwRootPath, "Output", subfolderName, "packages", "videos", $"{fileNameWithoutExtension}-videos.mp4");

            ViewData["RegionsFilePath"] = System.IO.File.Exists(regionsFilePath) ? regionsFilePath : null;
            ViewData["VideoFilePath"] = System.IO.File.Exists(videoFilePath) ? videoFilePath : null;

            if (System.IO.File.Exists(videoFilePath))
            {
                var videoFileInfo = new FileInfo(videoFilePath);
                ViewData["VideoFileName"] = Path.GetFileName(videoFileInfo.FullName);
                ViewData["VideoCreationTime"] = videoFileInfo.CreationTime;
            }

            return View();
        }

        public IActionResult DeleteFiles(string subfolderName, string fileNameWithoutExtension, string deleteOption)
        {

            var OutputSubFolderPath = Path.Combine(OutputFolderPath, subfolderName);
            var regionsFilePath = Path.Combine(OutputSubFolderPath, "packages", "regions", $"{fileNameWithoutExtension}-regions.json");
            var videoFilePath = Path.Combine(OutputSubFolderPath, "packages", "videos", $"{fileNameWithoutExtension}-videos.mp4");

            if (deleteOption == "both" || deleteOption == "regions")
            {
                if (System.IO.File.Exists(regionsFilePath))
                {
                    System.IO.File.Delete(regionsFilePath);
                }
            }

            if (deleteOption == "both" || deleteOption == "video")
            {
                if (System.IO.File.Exists(videoFilePath))
                {
                    System.IO.File.Delete(videoFilePath);
                }
            }

            TempData["SuccessMessage"] = "File berhasil dihapus.";
            return RedirectToAction("Customize", new { subfolderName, fileName = $"{fileNameWithoutExtension}.jpeg" });
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
}
