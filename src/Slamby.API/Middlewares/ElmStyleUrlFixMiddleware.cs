using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;

namespace Slamby.API.Middlewares
{
    public class ElmStyleUrlFixMiddleware
    {
        readonly RequestDelegate next;

        public ElmStyleUrlFixMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)

        {
            if (!context.Request.Path.Value.StartsWith("/elm", System.StringComparison.OrdinalIgnoreCase))
            {
                await next.Invoke(context);
                return;
            }

            using (var responseStream = new MemoryStream())
            {
                var originalStream = context.Response.Body;
                context.Response.Body = responseStream;

                await next.Invoke(context);

                responseStream.Seek(0, SeekOrigin.Begin);

                string content = string.Empty;
                using (var reader = new StreamReader(responseStream))
                {
                    content = await reader.ReadToEndAsync();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    content = content.Replace("\\n", "").Replace("\\r", "");
                    content = content.Replace("<a href=\"/elm/", "<a href=\"elm/");
                    content = content.Replace("<%$ include: Shared.css % > <%$ include: DetailsPage.css % >",
                        "body{font-family:'Segoe UI',Tahoma,Arial,Helvtica,sans-serif;line-height:1.4em}" +
                        "h1{font-family:'Segoe UI',Helvetica,sans-serif;font-size:2.5em;padding-bottom:10px}" +
                        "td{text-overflow:ellipsis;overflow:hidden}tr:nth-child(2n){background-color:#F6F6F6}" +
                        ".critical{background-color:red;color:#fff}.error{color:red}.information{color:#00f}" +
                        ".debug{color:#000}.warning{color:orange}body{font-size:.9em;width:90%;margin:0 auto}" +
                        "h2{font-weight:400}table{border-spacing:0;width:100%;border-collapse:collapse;border:1px solid #000;white-space:pre-wrap}" +
                        "th{font-family:Arial}td,th{padding:8px}#cookieTable,#headerTable{border:none;height:100%}" +
                        "#headerTd{white-space:normal}#label{width:20%;border-right:1px solid #000}" +
                        "#logs{margin-top:10px;margin-bottom:20px}#logs>tbody>tr>td{border-right:1px dashed #d3d3d3}" +
                        "#logs>thead>tr>th{border:1px solid #000}");
                }

                using (var writer = new StreamWriter(originalStream))
                {
                    await writer.WriteAsync(content);
                }

                context.Response.Body = originalStream;
            }
        }
    }
}
