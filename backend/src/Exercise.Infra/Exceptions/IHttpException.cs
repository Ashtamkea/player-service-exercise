using System.Net;

namespace Exercise.Infra.Exceptions
{
    public interface IHttpException
    {
        HttpStatusCode StatusCode { get; }
    }
}
