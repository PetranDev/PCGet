using System.Security.Principal;

try
{
    using var identity =
        WindowsIdentity.GetCurrent();

    var principal =
        new WindowsPrincipal(identity);

    var isAdministrator =
        principal.IsInRole(
            WindowsBuiltInRole.Administrator);

    Console.WriteLine(
        isAdministrator
            ? "AppDeck.Elevated is running as Administrator."
            : "AppDeck.Elevated is NOT running as Administrator.");

    return isAdministrator ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}