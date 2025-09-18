using Microsoft.AspNetCore.Mvc;
using DirectPrintApi.Models;
using System.Diagnostics;

namespace DirectPrintApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintController : ControllerBase
{
    private static readonly List<PrintJob> _printJobs = new();

    [HttpGet("GetApp")]
    public ActionResult<string> GetApp()
    {
        return Ok("https://www.sumatrapdfreader.org/dl/rel/3.5.2/SumatraPDF-3.5.2-64-install.exe");
    }

    [HttpGet("printers")]
    public ActionResult<IEnumerable<string>> GetPrinters()
    {
        try
        {
            var printers = new List<string>();
            
            // For Windows, we can use PowerShell to get printer list
            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-Printer | Select-Object Name | ConvertTo-Json\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // Simple parsing of JSON output to extract printer names
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"Name\""))
                        {
                            var nameStart = line.IndexOf("\"Name\"") + 7;
                            var nameEnd = line.LastIndexOf("\"");
                            if (nameStart > 6 && nameEnd > nameStart)
                            {
                                var printerName = line.Substring(nameStart, nameEnd - nameStart);
                                printers.Add(printerName);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: try using wmic command
                    var wmicPsi = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "printer get name",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var wmicProcess = Process.Start(wmicPsi);
                    var wmicOutput = wmicProcess.StandardOutput.ReadToEnd();
                    wmicProcess.WaitForExit();

                    if (wmicProcess.ExitCode == 0)
                    {
                        var wmicLines = wmicOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in wmicLines.Skip(1)) // Skip header
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine))
                            {
                                printers.Add(trimmedLine);
                            }
                        }
                    }
                }
            }
            else
            {
                // For non-Windows systems, try using lpstat (Linux) or system_profiler (macOS)
                if (OperatingSystem.IsLinux())
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "lpstat",
                        Arguments = "-p",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("printer"))
                            {
                                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 1)
                                {
                                    printers.Add(parts[1]);
                                }
                            }
                        }
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "system_profiler",
                        Arguments = "SPPrintersDataType",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.Contains("Name:"))
                            {
                                var name = line.Replace("Name:", "").Trim();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    printers.Add(name);
                                }
                            }
                        }
                    }
                }
            }

            return Ok(printers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to get printers: {ex.Message}");
        }
    }

    [HttpGet]
    public ActionResult<IEnumerable<PrintJob>> GetPrintJobs()
    {
        return Ok(_printJobs);
    }

    [HttpGet("{id}")]
    public ActionResult<PrintJob> GetPrintJob(int id)
    {
        var printJob = _printJobs.FirstOrDefault(p => p.Id == id);
        if (printJob == null)
        {
            return NotFound();
        }
        return Ok(printJob);
    }

    [HttpPost]
    public ActionResult<PrintJob> CreatePrintJob(PrintJob printJob)
    {
        printJob.Id = _printJobs.Count + 1;
        printJob.Status = "Pending";
        printJob.CreatedAt = DateTime.UtcNow;
        _printJobs.Add(printJob);
        return CreatedAtAction(nameof(GetPrintJob), new { id = printJob.Id }, printJob);
    }

    [HttpPut("{id}")]
    public IActionResult UpdatePrintJob(int id, PrintJob printJob)
    {
        var existingJob = _printJobs.FirstOrDefault(p => p.Id == id);
        if (existingJob == null)
        {
            return NotFound();
        }

        existingJob.Status = printJob.Status;
        existingJob.DocumentName = printJob.DocumentName;
        existingJob.PrinterName = printJob.PrinterName;
        existingJob.UpdatedAt = DateTime.UtcNow;

        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult DeletePrintJob(int id)
    {
        var printJob = _printJobs.FirstOrDefault(p => p.Id == id);
        if (printJob == null)
        {
            return NotFound();
        }

        _printJobs.Remove(printJob);
        return NoContent();
    }
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAndPrint(IFormFile file, [FromForm] List<string> printerNames)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        Console.WriteLine(string.Join(",", printerNames)+" "+file.FileName);
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            foreach (var printer in printerNames)
            {
                PrintPdf(filePath, printer, "fit");
                await Task.Delay(2000);
            }
            return Ok("Printed successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Printing failed: {ex.Message}");
        }
        finally
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }

    [HttpPost("upload-with-scale")]
    public async Task<IActionResult> UploadAndPrintWithScale(IFormFile file, [FromForm] List<string> printerNames, [FromForm] string scale = "fit")
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        Console.WriteLine(string.Join(",", printerNames) + " " + file.FileName);
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            foreach (var printer in printerNames)
            {
                PrintPdf(filePath, printer, scale);
                await Task.Delay(2000);
            }
            return Ok("Printed successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Printing failed: {ex.Message}");
        }
        finally
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }

    [HttpPost("upload-with-scale-image")]
    public async Task<IActionResult> UploadAndPrintWithScaleImage(IFormFile file, [FromForm] List<string> printerNames, [FromForm] string scale = "fit")
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        Console.WriteLine(string.Join(",", printerNames) + " " + file.FileName);
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".png")
            return BadRequest("Only .png files are supported for this endpoint.");

        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            foreach (var printer in printerNames)
            {
                PrintPdf(filePath, printer, scale);
                await Task.Delay(2000);
            }
            return Ok("Image printed successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Printing failed: {ex.Message}");
        }
        finally
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }

    private void PrintPdf(string filePath, string printerName, string scale)
    {
        var sumatraPath = Environment.GetEnvironmentVariable("SUMATRA_PATH") ?? @"C:\Program Files\SumatraPDF\SumatraPDF.exe";
        Console.WriteLine(sumatraPath);
        if (!System.IO.File.Exists(sumatraPath))
            throw new FileNotFoundException("SumatraPDF.exe not found."+" "+sumatraPath);

        var psi = new ProcessStartInfo
        {
            FileName = sumatraPath,
            Arguments = $"-print-to \"{printerName}\" -print-settings \"{scale}\" \"{filePath}\"",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        };

        var process = Process.Start(psi);
        process.WaitForExit();
    }
}