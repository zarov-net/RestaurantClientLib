using System.IO;
using FoodOrdering.Interfaces;
using FoodOrdering.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FoodOrdering.Services;

public class EnvironmentService : IEnvironmentService
{
    
 private readonly List<string> _variableNames;
    private readonly ILoggingService _loggingService;
    private readonly string _configFilePath;
    private readonly string _commentsFilePath;

    public EnvironmentService(IConfiguration configuration, ILoggingService loggingService)
    {
        _variableNames = configuration.GetSection("EnvironmentVariables").Get<List<string>>() ?? new List<string>();
        _loggingService = loggingService;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _commentsFilePath = Path.Combine(AppContext.BaseDirectory, "environment_comments.json");

        // Создаем файл комментариев, если его нет
        if (!File.Exists(_commentsFilePath))
        {
            File.WriteAllText(_commentsFilePath, "{}");
        }
    }

    public IEnumerable<EnvironmentVariable> GetVariables(IEnumerable<string> variableNames)
    {
        // Загружаем комментарии
        var commentsJson = File.ReadAllText(_commentsFilePath);
        var comments = JsonConvert.DeserializeObject<Dictionary<string, string>>(commentsJson)
                       ?? new Dictionary<string, string>();

        var names = variableNames?.ToList() ?? _variableNames;

        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ?? string.Empty;
            comments.TryGetValue(name, out var comment);

            yield return new EnvironmentVariable
            {
                Name = name,
                Value = value,
                Comment = comment ?? string.Empty
            };
        }
    }

    public void SetVariables(IEnumerable<EnvironmentVariable> variables)
    {
        // Загружаем текущие комментарии
        var commentsJson = File.ReadAllText(_commentsFilePath);
        var comments = JsonConvert.DeserializeObject<Dictionary<string, string>>(commentsJson)
                       ?? new Dictionary<string, string>();

        var variablesToUpdate = variables.ToList();
        bool configUpdated = false;
        bool commentsUpdated = false;

        foreach (var variable in variablesToUpdate)
        {
            // Сохраняем значение переменной
            var oldValue = Environment.GetEnvironmentVariable(variable.Name, EnvironmentVariableTarget.User);
            if (oldValue != variable.Value)
            {
                Environment.SetEnvironmentVariable(variable.Name, variable.Value, EnvironmentVariableTarget.User);
                _loggingService.LogVariableChange(variable.Name, oldValue, variable.Value);
            }

            // Сохраняем комментарий
            if (comments.TryGetValue(variable.Name, out var currentComment))
            {
                if (currentComment != variable.Comment)
                {
                    comments[variable.Name] = variable.Comment;
                    commentsUpdated = true;
                }
            }
            else if (!string.IsNullOrWhiteSpace(variable.Comment))
            {
                comments[variable.Name] = variable.Comment;
                commentsUpdated = true;
            }

            // Обновляем конфиг если это новая переменная
            if (!_variableNames.Contains(variable.Name))
            {
                _variableNames.Add(variable.Name);
                configUpdated = true;
            }
        }

        // Сохраняем комментарии если они изменились
        if (commentsUpdated)
        {
            File.WriteAllText(_commentsFilePath, JsonConvert.SerializeObject(comments, Formatting.Indented));
        }

        // Обновляем конфиг если нужно
        if (configUpdated)
        {
            UpdateConfigFile();
        }
    }

   

    public void SetVariable(EnvironmentVariable variable)
    {
        var oldValue = Environment.GetEnvironmentVariable(variable.Name, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(variable.Name, variable.Value, EnvironmentVariableTarget.User);
        _loggingService.LogVariableChange(variable.Name, oldValue, variable.Value);
            
        // Обновляем конфигурацию, если это новая переменная
        if (!_variableNames.Contains(variable.Name))
        {
            _variableNames.Add(variable.Name);
            UpdateConfigFile();
        }
    }

// Удаляем старый SetVariable и заменяем его на новый метод
    public void AddVariable(EnvironmentVariable variable)
    {
        if (!_variableNames.Contains(variable.Name))
        {
            _variableNames.Add(variable.Name);
            UpdateConfigFile();
        }
        SetVariable(variable);
    }
    private void UpdateConfigFile()
    {
        var config = new
        {
            EnvironmentVariables = _variableNames
        };
            
        File.WriteAllText(_configFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
    }
    public void RefreshVariables()
    {
        
    }
}