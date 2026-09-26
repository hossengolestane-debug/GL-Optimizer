namespace GLOptimizer.App.Services;

public interface IUserConfirmation
{
    bool Confirm(string title, string message);
}
