using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ParallelProgrammingClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var baseAddress = "https://localhost:7278/"; // Replace with your API's base address
            using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };

            // Fetch the segment count from the API
            HttpResponseMessage segmentCountResponse = await httpClient.GetAsync("api/segmentcount");
            segmentCountResponse.EnsureSuccessStatusCode();
            int totalSegments = int.Parse(await segmentCountResponse.Content.ReadAsStringAsync());

            await SingleThreadClientAsync(httpClient, totalSegments, "singlethread.txt");            

            MultiThreadClientAsync(httpClient, totalSegments, "multithread.txt");            

            ThreadPoolClientAsync(httpClient, totalSegments, "threadpool.txt");            

            await MultiTaskClientAsync(httpClient, totalSegments, "multitask.txt");

            ParallelConstructClientAsync(httpClient, totalSegments, "Parallel.txt");

            await PLINQClientAsync(httpClient, totalSegments, "plinq.txt");
        }

        public static async Task SingleThreadClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
           
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < segmentCount; i++)
            {
                var response = await httpClient.GetAsync($"/api/segment/{i}");
                response.EnsureSuccessStatusCode();

                byte[] segmentBytes = await response.Content.ReadAsByteArrayAsync();

                await outputFileStream.WriteAsync(segmentBytes, 0, segmentBytes.Length);

                // Print the current thread ID
                Console.WriteLine($"Processed segment {i} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }

            stopwatch.Stop();
            Console.WriteLine($"Sequential approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }

        public static void MultiThreadClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create a list to store the threads and fetched segments
            List<Thread> threads = new List<Thread>();
            List<(int index, byte[] segmentBytes)> fetchedSegments = new List<(int index, byte[] segmentBytes)>(new (int, byte[])[segmentCount]);

            // Define a lock object for thread-safe access to fetchedSegments
            object lockObj = new object();

            // Start a new thread for each segment
            for (int i = 0; i < segmentCount; i++)
            {
                int currentIndex = i;
                Thread newThread = new Thread(() =>
                {
                    var response = httpClient.GetAsync($"/api/segment/{currentIndex}").Result;
                    response.EnsureSuccessStatusCode();
                    byte[] segmentBytes = response.Content.ReadAsByteArrayAsync().Result;

                    lock (lockObj)
                    {
                        fetchedSegments[currentIndex] = (currentIndex, segmentBytes);
                    }

                    Console.WriteLine($"Processed segment {currentIndex} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                });

                threads.Add(newThread);
                newThread.Start();
            }

            // Wait for all threads to complete
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Write the fetched segments to the output file
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var fetchedSegment in fetchedSegments)
            {
                byte[] segmentBytes = fetchedSegment.segmentBytes;
                outputFileStream.Write(segmentBytes, 0, segmentBytes.Length);
            }

            stopwatch.Stop();
            Console.WriteLine($"Multi-threaded approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }

        public static void ThreadPoolClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create a list to store the fetched segments
            List<(int index, byte[] segmentBytes)> fetchedSegments = new List<(int index, byte[] segmentBytes)>(new (int, byte[])[segmentCount]);

            // Define a lock object for thread-safe access to fetchedSegments
            object lockObj = new object();

            // Create a countdown event to track the completion of ThreadPool tasks
            CountdownEvent countdownEvent = new CountdownEvent(segmentCount);

            // Start a new ThreadPool task for each segment
            for (int i = 0; i < segmentCount; i++)
            {
                int currentIndex = i;
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    var response = await httpClient.GetAsync($"/api/segment/{currentIndex}");
                    response.EnsureSuccessStatusCode();
                    byte[] segmentBytes = await response.Content.ReadAsByteArrayAsync();

                    lock (lockObj)
                    {
                        fetchedSegments[currentIndex] = (currentIndex, segmentBytes);
                    }

                    Console.WriteLine($"Processed segment {currentIndex} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");

                    // Signal the completion of the task
                    countdownEvent.Signal();
                });
            }

            // Wait for all ThreadPool tasks to complete
            countdownEvent.Wait();

            // Write the fetched segments to the output file
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var fetchedSegment in fetchedSegments)
            {
                byte[] segmentBytes = fetchedSegment.segmentBytes;
                outputFileStream.Write(segmentBytes, 0, segmentBytes.Length);
            }

            stopwatch.Stop();
            Console.WriteLine($"ThreadPool approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }

        public static async Task MultiTaskClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create a list to store the tasks for each thread
            List<Task<(int index, byte[] segmentBytes)>> tasks = new List<Task<(int index, byte[] segmentBytes)>>();

            // Start a new task for each segment
            for (int i = 0; i < segmentCount; i++)
            {
                int currentIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    var response = await httpClient.GetAsync($"/api/segment/{currentIndex}");
                    response.EnsureSuccessStatusCode();
                    byte[] segmentBytes = await response.Content.ReadAsByteArrayAsync();
                    Console.WriteLine($"Processed segment {currentIndex} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                    return (currentIndex, segmentBytes);
                }));
            }

            // Wait for all tasks to complete and fetch the results
            var results = await Task.WhenAll(tasks);

            // Write the fetched segments to the output file
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var result in results)
            {
                await outputFileStream.WriteAsync(result.segmentBytes, 0, result.segmentBytes.Length);
            }

            stopwatch.Stop();
            Console.WriteLine($"Multi-tasked approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }

        public static void ParallelConstructClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create a list to store the fetched segments
            List<(int index, byte[] segmentBytes)> fetchedSegments = new List<(int index, byte[] segmentBytes)>(new (int, byte[])[segmentCount]);

            // Define a lock object for thread-safe access to fetchedSegments
            object lockObj = new object();

            // Create a range of segment indices
            var segmentIndices = Enumerable.Range(0, segmentCount);

            // Process the segments in parallel using Parallel.ForEach
            Parallel.ForEach(segmentIndices, currentIndex =>
            {
                // Wrap the async calls in Task.Run for proper synchronization
                var downloadTask = Task.Run(async () =>
                {
                    var response = await httpClient.GetAsync($"/api/segment/{currentIndex}");
                    response.EnsureSuccessStatusCode();
                    byte[] segmentBytes = await response.Content.ReadAsByteArrayAsync();

                    lock (lockObj)
                    {
                        fetchedSegments[currentIndex] = (currentIndex, segmentBytes);
                    }

                    Console.WriteLine($"Processed segment {currentIndex} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                });

                // Wait for the downloadTask to complete before moving to the next iteration
                downloadTask.Wait();
            });

            // Write the fetched segments to the output file
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var fetchedSegment in fetchedSegments)
            {
                byte[] segmentBytes = fetchedSegment.segmentBytes;
                outputFileStream.Write(segmentBytes, 0, segmentBytes.Length);
            }

            stopwatch.Stop();
            Console.WriteLine($"Parallel construct approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }


        public static async Task PLINQClientAsync(HttpClient httpClient, int segmentCount, string outputFilePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create a range of segment indices
            var segmentIndices = Enumerable.Range(0, segmentCount);

            // Process the segments in parallel using PLINQ
            var fetchedSegments = segmentIndices.AsParallel()
                                               .AsOrdered()
                                               .Select(async currentIndex =>
                                               {
                                                   var response = await httpClient.GetAsync($"/api/segment/{currentIndex}");
                                                   response.EnsureSuccessStatusCode();
                                                   byte[] segmentBytes = await response.Content.ReadAsByteArrayAsync();

                                                   Console.WriteLine($"Processed segment {currentIndex} on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                                                   return (index: currentIndex, segmentBytes);
                                               })
                                               .ToList();

            // Wait for all tasks to complete
            var fetchedSegmentResults = await Task.WhenAll(fetchedSegments);

            // Write the fetched segments to the output file
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var fetchedSegment in fetchedSegmentResults)
            {
                byte[] segmentBytes = fetchedSegment.segmentBytes;
                outputFileStream.Write(segmentBytes, 0, segmentBytes.Length);
            }

            stopwatch.Stop();
            Console.WriteLine($"PLINQ approach took {stopwatch.ElapsedMilliseconds} milliseconds.");
        }
    }
}