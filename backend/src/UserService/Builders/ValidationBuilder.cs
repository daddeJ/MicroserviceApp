namespace UserService.Builders;

public class ValidationBuilder<T>
{
    private readonly T _model;
    private readonly List<Func<(bool IsValid, string? Field, string? ErrorMessage)>> _rules = new();

    public ValidationBuilder(T model)
    {
        _model = model;
    }

    public ValidationBuilder<T> Rule(Func<T, bool> predicate, string field, string errorMessage)
    {
        _rules.Add(() =>
        {
            var valid = predicate(_model);
            return (valid, field, valid ? null : errorMessage);
        });
        return this;
    }
    
    public (bool IsValid, string? Field, string? ErrorMessage)? Validate()
    {
        foreach (var rule in _rules)
        {
            var result = rule();
            if (!result.IsValid)
                return result;
        }
        return null;
    }
}