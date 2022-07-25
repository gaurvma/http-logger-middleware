using System.Diagnostics;

namespace http_logger_middleware.Middlewares
{
    /// <summary>
    /// Middleware for Logging Request, Responses and Performance.    
    /// </summary>
    public class HttpLoggerMiddleware
    {
        private const string RESPONSE_HEADER_RES_TIME = "X-Response-Time-ms";
        private readonly ILogger<HttpLoggerMiddleware> _logger;
        private readonly RequestDelegate _next;

        public HttpLoggerMiddleware(RequestDelegate next, ILogger<HttpLoggerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
       
        public async Task Invoke(HttpContext context)
        {
            //Get incoming request
            context.Request.EnableBuffering();
            var bodyAsText = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var requestText = $"{context.Request.Scheme} {context.Request.Host}{context.Request.Path} {context.Request.QueryString} {bodyAsText}";           

            context.Request.Body.Seek(0, SeekOrigin.Begin);            

            //Copy  pointer to the original response body stream
            Stream originalBody = context.Response.Body;

            var reqId = Guid.NewGuid().ToString();
            // Logging it
            _logger.LogInformation($"ReqId: {reqId}|requestText: {requestText}");

            string responseBody = "";
            var watch = new Stopwatch();
            watch.Start();
            try
            {                
                // Start the Timer using Stopwatch                 
                context.Response.OnStarting(() => {
                    // Stop the timer information and calculate the time   
                    watch.Stop();
                    var responseTimeForCompleteRequest = watch.ElapsedMilliseconds;
                    // Add the Response time information in the Response headers.   
                    context.Response.Headers[RESPONSE_HEADER_RES_TIME] = responseTimeForCompleteRequest.ToString();
                    return Task.CompletedTask;
                });
                // Call the next delegate/middleware in the pipeline   

                using (var memStream = new MemoryStream())
                {
                    context.Response.Body = memStream;

                    // Continue down the Middleware pipeline
                    await _next(context);

                    // Format the response from the server
                    memStream.Position = 0;
                    responseBody = new StreamReader(memStream).ReadToEnd();

                    //Copy the contents of the new memory stream, which contains the response to the original stream, which is then returned to the client.
                    memStream.Position = 0;
                    await memStream.CopyToAsync(originalBody);
                }
            }
            finally
            {
                context.Response.Body = originalBody;
                
                // Logging it
                _logger.LogInformation($"ReqId: {reqId}|StatusCode: {context.Response.StatusCode}|ContentType: {context.Response.ContentType}|ContentLength: {context.Response.ContentLength}|ResponseText: {responseBody}| ElapsedMilliseconds: {watch.ElapsedMilliseconds}");

                
            }
        }
    }
}