using Givt.Integrations.Interfaces;

namespace Givt.Notifications.WePay.Accounts.Models;

public class OrganizationAccountProblemEmail : EmailContent
{
    public string AccountIssues { get; set; }
    
    public OrganizationAccountProblemEmail(string country, string language) : base(country, language)
    { }
}
