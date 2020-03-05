namespace Arrowgene.ENet
{
    public interface IENetInterceptCallback
    {
        int Intercept(ENetHost host, ENetEvent eNetEvent);
    }
}
