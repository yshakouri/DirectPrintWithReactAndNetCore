namespace DirectPrintApi.Models;

public class PrintJob
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Copies { get; set; } = 1;
    public bool IsColor { get; set; }
    public string PaperSize { get; set; } = "A4";
} 