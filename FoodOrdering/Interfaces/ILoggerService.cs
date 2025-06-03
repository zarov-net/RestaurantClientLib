using System.IO;

namespace FoodOrdering.Interfaces;

public interface ILoggingService
{
    void LogVariableChange(string variableName, string oldValue, string newValue);
    void LogError(string message, Exception ex);
}

public class FileLoggingService : ILoggingService
{
    private readonly string _logDirectory;
    private readonly string _logFilePrefix = "test-sms-wpf-app-";
        
    public FileLoggingService()
    {
        _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public void LogVariableChange(string variableName, string oldValue, string newValue)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Variable changed: {variableName}\n" +
                         $"Old value: {oldValue ?? "null"}\n" +
                         $"New value: {newValue ?? "null"}\n";
            
        WriteToLog(logMessage);
    }

    public void LogError(string message, Exception ex)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}\n" +
                         $"Exception: {ex}\n";
            
        WriteToLog(logMessage);
    }

    private void WriteToLog(string message)
    {
        var logFilePath = Path.Combine(_logDirectory, $"{_logFilePrefix}{DateTime.Now:yyyyMMdd}.log");
        File.AppendAllText(logFilePath, message);
    }
}