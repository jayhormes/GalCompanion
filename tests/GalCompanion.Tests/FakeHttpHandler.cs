using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GalCompanion.Tests
{
    internal sealed class RecordedRequest
    {
        public string Method;
        public string Url;
        public string Body;
        public string Authorization;
    }

    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        public readonly List<RecordedRequest> Requests = new List<RecordedRequest>();
        private readonly Queue<HttpResponseMessage> responses = new Queue<HttpResponseMessage>();

        public void Enqueue(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(body ?? string.Empty) });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest
            {
                Method = request.Method.Method,
                Url = request.RequestUri.ToString(),
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(),
                Authorization = request.Headers.TryGetValues("Authorization", out var values)
                    ? values.FirstOrDefault()
                    : null
            };
            Requests.Add(recorded);

            return responses.Count > 0
                ? responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
        }
    }
}
