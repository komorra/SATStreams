using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    /// <summary>
    /// Simple visualisation HTTP server, returning a JSON representation of the SATStream state.
    /// </summary>
    internal class VisualizationServer
    {
        private int port;
        private SATSolver solver;

        public VisualizationServer(SATSolver solver, int port)
        {
            this.port = port;
            this.solver = solver;

            var state = JsonConvert.SerializeObject(solver);
        }

        public async Task Start()
        {
            // Start the HTTP server on the specified port
            // This is a placeholder for actual HTTP server code
            // You can use libraries like HttpListener or ASP.NET Core to implement this
            Console.WriteLine($"Starting visualization server on port {port}...");
            // Example: Use HttpListener to listen for requests
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var pageToOpen = "HTML\\SATStreams.html";
            var psi = new ProcessStartInfo
            {
                FileName = pageToOpen,
                UseShellExecute = true,                
            };
            Process.Start(psi);

            while (true)
            {
                try
                {
                    // Wait for incoming requests and respond with JSON state
                    var context = await listener.GetContextAsync();
                    var response = context.Response;
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.ContentType = "application/json";
                    string jsonState = GetJsonState();
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonState);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                catch
                {
                }              
            }
        }

        private string GetJsonState()
        {
            var state = JsonConvert.SerializeObject(solver);

            return state;
        }
    }
}
