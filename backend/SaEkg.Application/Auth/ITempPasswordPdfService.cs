namespace SaEkg.Application.Auth;

public interface ITempPasswordPdfService
{
    byte[] Render(string email, string temporaryPassword);
}
