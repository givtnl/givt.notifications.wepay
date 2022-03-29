using Givt.Models;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Infrastructure.Helpers;

public static class TransactionStatusHelper
{
    public static TransactionStatus FromWePayTransactionStatus(StatusPaymentsResponse wePayStatus)
    {
        var status = TransactionStatus.Entered;
        switch (wePayStatus)
        {
            case StatusPaymentsResponse.Completed:
                status = TransactionStatus.Processed;
                break;
            case StatusPaymentsResponse.Failed:
            case StatusPaymentsResponse.Canceled:
                status = TransactionStatus.Cancelled;
                break;
            default:
                status = TransactionStatus.All;
                break;
        }

        return status;
    }
}
