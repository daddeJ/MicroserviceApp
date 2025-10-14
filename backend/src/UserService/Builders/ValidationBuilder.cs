using Shared.Constants;
using UserService.Services;

namespace UserService.Builders;

public class ValidationBuilder<T>
{
    private readonly T _model;
    private readonly List<Func<Task<(bool IsValid, string? Field, string? ErrorMessage)>>> _rules 
        = new List<Func<Task<(bool, string?, string?)>>>();
    private readonly IPublisherService _publisherService;

    public ValidationBuilder(T model, IPublisherService publisherService)
    {
        _model = model;
        _publisherService = publisherService;
    }

    public ValidationBuilder<T> Rule(Func<T, bool> predicate, string field, string errorMessage)
    {
        _rules.Add(async () =>
        {
            var valid = predicate(_model);
            if (!valid)
            {
                await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.ModelValidation);
            }
            return (valid, field, valid ? null : errorMessage);
        });
        return this;
    }
    
    public async Task<(bool IsValid, string Field, string ErrorMessage)?> ValidateAsync()
    {
        foreach (var rule in _rules)
        {
            var result = await rule();
            if (!result.IsValid)
                return result;
        }
        return null;
    }
}