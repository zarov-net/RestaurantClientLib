using FoodOrdering.Models;

namespace FoodOrdering.Interfaces;

public interface IEnvironmentService
{
    IEnumerable<EnvironmentVariable> GetVariables(IEnumerable<string> variableNames);
    void SetVariable(EnvironmentVariable variable);

    void SetVariables(IEnumerable<EnvironmentVariable> variables);
    void AddVariable(EnvironmentVariable variable);
    void RefreshVariables();
}