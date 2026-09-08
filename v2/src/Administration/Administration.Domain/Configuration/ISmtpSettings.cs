namespace Administration.Domain.Configuration;

public interface ISmtpSettings
{
    string Domain { get; }
    string Username { get; }
    string Password { get; }
    ushort Port { get; }
    string Email { get; }
    bool EnableSsl { get; }
}