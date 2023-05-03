
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ParallelProgrammingAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Parse command-line arguments
            string filePath = null;
            int segmentCount = 0;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-f" && i + 1 < args.Length)
                {
                    filePath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-c" && i + 1 < args.Length)
                {
                    segmentCount = int.Parse(args[i + 1]);
                    i++;
                }
            }

            if (string.IsNullOrEmpty(filePath) || segmentCount == 0)
            {
                Console.WriteLine("Usage: [app] -f [filePath] -c [segmentCount]");
                return;
            }

            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            long fileLength = new FileInfo(filePath).Length;

            //split into segments
            var random = new Random();
            var segments = new List<(long offset, int length)>(segmentCount);

            long offset = 0;
            int averageSegmentLength = (int)(fileLength / segmentCount);
            int segmentLengthRange = averageSegmentLength / 2;

            for (int i = 0; i < segmentCount; i++)
            {
                int length;
                if (i == segmentCount - 1)
                {
                    // Make sure the last segment reaches the end of the file
                    length = (int)(fileLength - offset);
                }
                else
                {
                    // Generate segment length around the average, within the allowed range
                    int minLength = Math.Max(1, averageSegmentLength - segmentLengthRange);
                    int maxLength = Math.Min(averageSegmentLength + segmentLengthRange, (int)(fileLength - offset - (segmentCount - i - 1)));
                    length = random.Next(minLength, maxLength);
                }

                segments.Add((offset, length));
                offset += length;
            }

            // Endpoint to retrieve the nth segment
            app.MapGet("api/segment/{id}", async (int id) =>
            {
                if (id >= 0 && id < segments.Count)
                {
                    (long offset, int length) = segments[id];

                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] buffer = new byte[length];
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    await fileStream.ReadAsync(buffer, 0, length);

                    // Simulate 200ms latency
                    await Task.Delay(200);

                    string text = Encoding.UTF8.GetString(buffer);                    
                    return Results.Bytes(buffer, "application/octet-stream");
                }
                return Results.NotFound();
            });

            // Endpoint to retrieve the segment count
            app.MapGet("api/segmentcount", () =>
            {
                return Results.Ok(segmentCount);
            });

            app.Run();
        }
    }
}