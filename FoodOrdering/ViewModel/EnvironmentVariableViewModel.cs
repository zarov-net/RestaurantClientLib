using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FoodOrdering.ViewModel;

public class EnvironmentVariableViewModel : INotifyPropertyChanged
{
    private string _value;
    private string _comment;


    public string Comment
    {
        get => _comment;
        set
        {
            if (_comment != value)
            {
                _comment = value;
                OnPropertyChanged();
            }
        }
    }
    public string Name { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                var oldValue = _value;
                _value = value;
                OnPropertyChanged();
                ValueChanged?.Invoke(this, new ValueChangedEventArgs(Name, oldValue, value));
            }
        }
    }

    public event EventHandler<ValueChangedEventArgs> ValueChanged;
    public event PropertyChangedEventHandler PropertyChanged;

    public EnvironmentVariableViewModel(string name, string value, string comment = "")
    {
        Name = name;
        _value = value;
        _comment = comment;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ValueChangedEventArgs : EventArgs
{
    public string VariableName { get; }
    public string OldValue { get; }
    public string NewValue { get; }

    public ValueChangedEventArgs(string variableName, string oldValue, string newValue)
    {
        VariableName = variableName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}