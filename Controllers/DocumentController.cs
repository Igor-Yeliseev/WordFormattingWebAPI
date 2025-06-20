using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WordDocumentFormattingChecker;

namespace DocumentFormattingWebAPI.Controllers
{
    [ApiController]
    [Route("word-formatting-api")]
    public class DocumentController : ControllerBase
    {
        private static readonly object fileLock = new object();
        public DocumentController() { }

        [HttpPost("check-doc")]
        public async Task<IActionResult> CheckDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (GetDocumentType(file.FileName) != DocumentType.DOCX)
                return BadRequest("Wrong file extension.");

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    // Асинхронная проверка
                    memoryStream.Position = 0;
                    await CheckDocFormatting(memoryStream);
                    // await Task.Delay(2300);

                    // Подготовка имени файла с датой и временем
                    var extension = Path.GetExtension(file.FileName);
                    var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    var timeStamp = DateTime.Now.ToString("yyyy.MM.dd HH-mm"); // Формат: Год.Месяц.День Часы-Минуты
                    var checkedFileName = $"{fileName} (checked {timeStamp}){extension}";

                    memoryStream.Position = 0;
                    var fileBytes = memoryStream.ToArray();

                    return File(fileBytes, "application/octet-stream", checkedFileName);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing file: {ex.Message}");
            }
        }

        /// <summary> Получает текущие правила форматирования из JSON-файла </summary>
        /// <returns> JSON с правилами форматирования или сообщение об ошибке </returns>
        [HttpGet("get-rules")]
        public IActionResult GetFormattingRules()
        {
            try
            {
                var resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources");
                var filePath = Path.Combine(resourcesPath, "formatting-rules.json");
                string jsonString = string.Empty;

                lock (fileLock)
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        jsonString = System.IO.File.ReadAllText(filePath);
                    }
                }

                if (string.IsNullOrEmpty(jsonString))
                {
                    return NotFound("Файл с правилами не найден.");
                }

                return Content(jsonString, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        /// <summary> Принимает json-объект с правилами форматирования, передаёт в обработчик и возвращает результат </summary>
        /// <returns> Сообщение об успехе/ошибке </returns>
        [HttpPost("setup-rules")]
        public IActionResult SetupFormattingRules([FromBody] string rawJson)
        {
            try
            {
                var rulesJson = JsonConvert.DeserializeObject<JObject>(rawJson);

                if (rulesJson == null || !rulesJson.HasValues)
                {
                    return BadRequest("Пустой json-объект.");
                }

                var formattedJson = JsonConvert.SerializeObject(rulesJson, Formatting.Indented);

                var resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources");
                var filePath = Path.Combine(resourcesPath, "formatting-rules.json");

                lock (fileLock)
                {
                    System.IO.File.WriteAllText(filePath, formattedJson);
                }

                return Ok(new { message = "Правила успешно сохранены." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        /// <summary> Проверить форматирование документа </summary>
        /// <param name="fileStream"> Поток данных </param>
        /// <exception cref="ArgumentNullException"></exception>
        private async Task CheckDocFormatting(Stream fileStream)
        {
            var resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources");
            var filePath = Path.Combine(resourcesPath, "formatting-rules.json");

            string formattingRules = string.Empty;

            // Синхронизация чтения файла правил форматирования
            lock (fileLock)
            {
                if (System.IO.File.Exists(filePath))
                {
                    formattingRules = System.IO.File.ReadAllText(filePath);
                }
            }

            // Если правила не были найдены, используем валидатор по умолчанию
            if (string.IsNullOrEmpty(formattingRules))
            {
                using (var validator = new AcademicReportFormattingValidator(fileStream))
                {
                    await Task.Run(() => validator.Validate()); // Асинхронная обёртка для синхронной операции
                }
            }
            else
            {
                using (var validator = new AcademicReportFormattingValidator(fileStream, formattingRules))
                {
                    await Task.Run(() => validator.Validate()); // Асинхронная обёртка для синхронной операции
                }
            }
        }

        private DocumentType GetDocumentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            switch (extension)
            {
                case ".pdf":
                    return DocumentType.PDF;
                case ".docx":
                    return DocumentType.DOCX;
                case ".txt":
                    return DocumentType.TXT;
                default:
                    throw new ArgumentException("Unsupported file type.");
            }
        }

        /// <summary> Извлекает правила форматирования из загруженного Word-документа </summary>
        /// <param name="file"> Word-документ (.docx) </param>
        /// <returns>Json с извлечёнными правилами или сообщение об ошибке</returns>
        [HttpPost("get-rules-from-file")]
        public async Task<IActionResult> GetRulesFromFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не загружен.");

            if (Path.GetExtension(file.FileName).ToLower() != ".docx")
                return BadRequest("Поддерживаются только файлы .docx.");

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                   await file.CopyToAsync(memoryStream);
                   memoryStream.Position = 0;

                   using (var document = WordprocessingDocument.Open(memoryStream, true))
                   {
                       var extractor = new FormattingRulesExtractor(document);
                       var formattingRules = extractor.ExtractRules();
                       return Ok(formattingRules);
                   }
                }
                // return Ok(new { fontName = "Times New Roman", fontSize = 14, indentation = 1.25 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }
    }
}
