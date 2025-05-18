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
                PrintPdf(filePath, printer);
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

    private void PrintPdf(string filePath, string printerName)
    {
        var sumatraPath = Environment.GetEnvironmentVariable("SUMATRA_PATH") ?? @"C:\Program Files\SumatraPDF\SumatraPDF.exe";
        Console.WriteLine(sumatraPath);
        if (!System.IO.File.Exists(sumatraPath))
            throw new FileNotFoundException("SumatraPDF.exe not found."+" "+sumatraPath);

        var psi = new ProcessStartInfo
        {
            FileName = sumatraPath,
            Arguments = $"-print-to \"{printerName}\" \"{filePath}\"",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        };

        var process = Process.Start(psi);
        process.WaitForExit();
    }
}