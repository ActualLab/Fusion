using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if NETCOREAPP
using Microsoft.AspNetCore.Mvc;
#else
using System.Web.Http;
using ControllerBase = System.Web.Http.ApiController;
#endif
using Stl.Fusion.Server;
using Stl.Serialization;

namespace Stl.Fusion.Tests.Services
{
    public class RestEaseController : ControllerBase
    {
        [HttpGet]
        public Task<string> GetFromQueryImplicit(string str, CancellationToken cancellationToken)
        {
            return Task.FromResult(str);
        }
        
#if NETCOREAPP
        [HttpGet]
        public Task<string> GetFromQuery([FromQuery]string str, CancellationToken cancellationToken)
#else
        [HttpGet]
        public Task<string> GetFromQuery([FromUri]string str, CancellationToken cancellationToken)
#endif 
        {
            return Task.FromResult(str);
        }
        
        [HttpGet]
        public Task<JsonString> GetJsonString(string str, CancellationToken cancellationToken)
        {
            var jsonString = (JsonString)str;
            return Task.FromResult(jsonString);
        }
        
#if NETCOREAPP
        [HttpGet("api/restease/getFromPath/{str}")]
        public Task<string> GetFromPath(string str, CancellationToken cancellationToken)
#else
        [HttpGet]
        [Route("api/restease/getFromPath/{str}")]
        public Task<string> GetFromPath(string str, CancellationToken cancellationToken)
#endif 
        {
            return Task.FromResult(str);
        }
        
        [HttpPost]
        public Task<JsonString> PostFromQueryImplicit(string str, CancellationToken cancellationToken)
        {
            var jsonString = new JsonString(str);
            return Task.FromResult(jsonString);
        }
        
#if NETCOREAPP
        [HttpPost]
        public Task<JsonString> PostFromQuery([FromQuery]string str, CancellationToken cancellationToken)
#else
        [HttpPost]
        public Task<JsonString> PostFromQuery([FromUri]string str, CancellationToken cancellationToken)
#endif 
        {
            var jsonString = new JsonString(str);
            return Task.FromResult(jsonString);
        }
        
#if NETCOREAPP
        [HttpPost("api/restease/postFromPath/{str}")]
#else
        [HttpPost]
        [Route("api/restease/postFromPath/{str}")]
#endif 
        public Task<JsonString> PostFromPath(string str, CancellationToken cancellationToken)

        {
            var jsonString = new JsonString(str);
            return Task.FromResult(jsonString);
        }
        
        [HttpPost]
        public Task<JsonString> PostWithBody([FromBody]StringWrapper wrapper, CancellationToken cancellationToken)
        {
            var jsonString = new JsonString(wrapper.Body);
            return Task.FromResult(jsonString);
        }
        
#if NETCOREAPP
        [HttpPost("api/restease/concatQueryAndPath/{b}")]
        public Task<JsonString> ConcatQueryAndPath(string a, string b, CancellationToken cancellationToken)
#else
        [HttpPost]
        [Route("api/restease/concatQueryAndPath/{b}")]
        public Task<JsonString> ConcatQueryAndPath(string a, string b, CancellationToken cancellationToken)
#endif
        {
            var str = string.Concat(a, b);
            var jsonString = new JsonString(str);
            return Task.FromResult(jsonString);
        }
        
#if NETCOREAPP
        [HttpPost("api/restease/concatPathAndBody/{a}")]
        public async Task<JsonString> ConcatPathAndBody(string a, CancellationToken cancellationToken)
#else
        [HttpPost]
        [Route("api/restease/concatPathAndBody/{a}")]
        public async Task<JsonString> ConcatPathAndBody(string a, CancellationToken cancellationToken)
#endif
        {
#if NETCOREAPP
            using var reader = new StreamReader(Request.Body);
            var b = await reader.ReadToEndAsync();
#else
            var b = await Request.Content.ReadAsStringAsync();
#endif            
            var str = string.Concat(a, b);
            var jsonString = new JsonString(str);
            return jsonString;
        }
    }
    
    public record StringWrapper(string Body);
}
