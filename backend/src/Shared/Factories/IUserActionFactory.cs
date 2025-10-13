namespace Shared.Factories;

public interface IUserActionFactory
{
    UserActionMetadata GetMetadata(string action);
}