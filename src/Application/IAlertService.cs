namespace Application;

public interface IAlertService
{
    Task SendAsync(string message);
}
