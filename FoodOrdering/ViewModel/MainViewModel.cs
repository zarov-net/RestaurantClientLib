using System.Collections;
using CommunityToolkit.Mvvm.Input;
using FoodOrdering.Interfaces;
using FoodOrdering.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Newtonsoft.Json;

namespace FoodOrdering.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IEnvironmentService _environmentService;
        private readonly ILoggingService _loggingService;
        private readonly IConfiguration _configuration;

        public ObservableCollection<EnvironmentVariableViewModel> Variables { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand AddNewVariableCommand { get; }
        public ICommand RefreshCommand { get; }

        public MainViewModel(IEnvironmentService environmentService,
            ILoggingService loggingService,
            IConfiguration configuration)
        {
            _environmentService = environmentService;
            _loggingService = loggingService;
            _configuration = configuration;

            SaveCommand = new AsyncRelayCommand(SaveChangesAsync, CanSaveChanges);
            AddNewVariableCommand = new RelayCommand(AddNewVariable);
            RefreshCommand = new RelayCommand(RefreshVariables);

            LoadVariables();
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Собираем все переменные для сохранения
                    var variablesToSave = Variables.Select(v => new EnvironmentVariable
                    {
                        Name = v.Name,
                        Value = v.Value,
                        Comment = v.Comment
                    }).ToList();

                    // Передаем все переменные разом
                    _environmentService.SetVariables(variablesToSave);
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error saving variables", ex);
                // Можно добавить отображение ошибки пользователю
            }
        }

        private void LoadVariables()
        {
            Variables.Clear();
    
            // Получаем все переменные среды пользователя
            var userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
    
            // Получаем список переменных из конфигурации для порядка отображения
            var configVariables = _configuration.GetSection("EnvironmentVariables").Get<List<string>>() ?? new List<string>();
    
            // Загружаем комментарии
            var commentsJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "environment_comments.json"));
            var comments = JsonConvert.DeserializeObject<Dictionary<string, string>>(commentsJson) 
                           ?? new Dictionary<string, string>();

            // Сначала добавляем переменные из конфигурации
            foreach (var name in configVariables)
            {
                if (userVariables.Contains(name))
                {
                    var value = userVariables[name]?.ToString() ?? string.Empty;
                    comments.TryGetValue(name, out var comment);
            
                    var vm = new EnvironmentVariableViewModel(name, value, comment ?? string.Empty);
                    vm.ValueChanged += OnVariableValueChanged;
                    Variables.Add(vm);
                }
            }
    
            // Затем добавляем остальные переменные
            foreach (DictionaryEntry entry in userVariables)
            {
                var name = entry.Key.ToString();
                if (!configVariables.Contains(name))
                {
                    var value = entry.Value?.ToString() ?? string.Empty;
                    comments.TryGetValue(name, out var comment);
            
                    var vm = new EnvironmentVariableViewModel(name, value, comment ?? string.Empty);
                    vm.ValueChanged += OnVariableValueChanged;
                    Variables.Add(vm);
                }
            }
        }
        private void OnVariableValueChanged(object sender, ValueChangedEventArgs e)
        {
            _loggingService.LogVariableChange(e.VariableName, e.OldValue, e.NewValue);
        }

        private bool CanSaveChanges()
        {
            return Variables.All(v => !string.IsNullOrWhiteSpace(v.Name));
        }

        private void AddNewVariable()
        {
            string newName;
            do
            {
                newName = "NEW_VAR_" + Guid.NewGuid().ToString("N")[..8];
            } while (Variables.Any(v => v.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)));

            var newVariable = new EnvironmentVariableViewModel(newName, "", "Новая переменная");
            newVariable.ValueChanged += OnVariableValueChanged;
            Variables.Add(newVariable);
            _environmentService.AddVariable(new EnvironmentVariable
            {
                Name = newVariable.Name,
                Value = newVariable.Value,
                Comment = newVariable.Comment
            });
        }

        private void RefreshVariables()
        {
            _environmentService.RefreshVariables();
            LoadVariables();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}