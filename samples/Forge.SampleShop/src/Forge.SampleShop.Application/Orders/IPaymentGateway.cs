namespace Forge.SampleShop.Application.Orders;

public interface IPaymentGateway
{
    void Authorize(string orderNumber);
}
