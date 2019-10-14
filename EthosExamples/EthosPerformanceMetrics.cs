using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

// This class originally developed by Julian Hooker @ Texas Tech

// The idea behind the class is to create some functions to test and record the metrics of Ethos Integration Performance.
//    It's not meant to be production code. It's meant to be a tool for measuring. 

// In order to use these functions, just add one or the other of these functions to the main in Program.cs, like this:
//
//    static void Main()
//    {
//      EthosPerformanceMetrics.TimeSinglePersonsSync();  // runs performance method for a single lookup
//        OR // You can run both, but they run at the same time intermingle their output
//      EthosPerformanceMetrics.TimeBulkPersonsSync();    // runs performance method for a bulk lookup
//
//      Console.WriteLine("Waiting for Ethos data to return. Press any key to end program.");
//      Console.ReadKey();    // Keeps the console window up while the trials run
//    }
//
// For Config: 
//    Assign your Ethos Integration Application API Key to apiKey below, like this (replace << API Key from Ethos Integration >>):
//
//      private static readonly string apiKey = "00000000-0000-0000-0000-000000000000";
//
//    Assign the GUID of a person to myFavGUID, like this (replace the text << a persons GUID >>):
//
//      private static readonly string myFavGUID = "00000000-0000-0000-0000-000000000000";


namespace EthosExamples
{
    class EthosPerformanceMetrics
    {
        #region Config
        // Ethos Config
        private static readonly string ethosURI = "https://integrate.elluciancloud.com/";
        private static readonly string apiKey = "<< API Key from Ethos Integration >>";
        private static readonly string myFavGUID = "<< a persons GUID >>";

        // Trials config
        private static readonly int numGUIDIterations = 50;
        private static readonly int[] bulkRowsToReturn = new int[] { 5, 10, 30, 50, 100, 500 };
        private static readonly int numOfBulkIterations = 30;
        private static readonly int[] numAsyncIterations = new int[] { 10, 20, 30 };
        private static readonly string criteria = "{\"names\":[{\"lastName\":\"smith\"}]}"; // for searching for a group of persons
        private static readonly string bulkCriteria = "{\"names\":[{\"lastName\":\"smith\"}]}"; // for searching for a group of persons
        private static readonly string resourceName = "persons";

        private static EthosClient ec = new EthosClient(ethosURI, apiKey);

        #endregion  

        #region IndividualLookup

        /// <summary>
        /// This method will use Ethos Integration to lookup many individual GUIDS with the persons resource and report an average of how long it takes. 
        /// </summary>
        /// <returns></returns>
        internal static async Task TimeSinglePersonsSync()
        {
            // Print out the configuration used to generate the numbers for this run
            Console.WriteLine("Method: Start TimeSinglePersonsSync()");
            Console.WriteLine("  ** numGUIDIterations: " + numGUIDIterations);
            Console.WriteLine("  ** resourceName: " + resourceName);
            Console.WriteLine("  ** criteria: " + criteria);
            Console.WriteLine("  ** Time of run: " + DateTime.Now.ToString("MM/dd/yyyy h:mm:ss"));

            string[] guids = new string[numGUIDIterations];

            // Setup as parameters on the URL query for the GetAll call
            Dictionary<string, string> searchParams = new Dictionary<string, string>();
            searchParams.Add("criteria", criteria);

            if (await ec.Authenticate())
            {
                // First, lookup a large group of GUIDs so we can look them up individually
                var watch = System.Diagnostics.Stopwatch.StartNew();
                GetResponse GUIDsResponse = ec.GetAll(resourceName, searchParams, offset: 0, limit: numGUIDIterations); // Get an array of results from Ethos
                watch.Stop();
                Console.WriteLine("  Wait time for lookup on criteria ** " + criteria + " **: " + watch.ElapsedMilliseconds + " msec.");

                JArray a = JArray.Parse(GUIDsResponse.Data);

                if (a.Count < numGUIDIterations)
                {
                    Console.WriteLine("    Based on the search criteria, only " + a.Count + " records were returned.");
                }

                // Put the returned GUIDs in an array
                for (int i = 0; i < numGUIDIterations && i < a.Count; i++)
                {
                    JObject o = (JObject)a[i];

                    string guid = (string)o["id"];

                    guids[i] = guid;
                }

                long totalGUIDLookupTime = 0;
                int j = 1;

                // Now, one by one, let's look them up and time each run. Add those together as we go and get the average. 
                for (j = 0; j < numGUIDIterations && j < a.Count; j++)
                {
                    watch = System.Diagnostics.Stopwatch.StartNew();
                    GetResponse singlePersonResponse = ec.Get(new Guid(guids[j]), resourceName, "application/vnd.hedtech.integration.v12+json"); // Get a single result from Ethos, for version 12
                    watch.Stop();
                    totalGUIDLookupTime += watch.ElapsedMilliseconds;

                    JObject o = JObject.Parse(singlePersonResponse.Data);

                    string fullname = (string)o["names"][0]["fullName"];

                    Console.WriteLine("  Wait time for lookup on GUID " + guids[j] + ": " + watch.ElapsedMilliseconds + " msec - name: " + fullname);

                    if (j == 0)
                        Console.WriteLine(o); // I used this during my testing of Ethos Extend to make sure the extensions were active
                }

                Console.WriteLine("  Average wait time for call to Ethos: " + totalGUIDLookupTime / j + " msec.");
            }
            else
            {
                Console.WriteLine("  Didn't authenticate");
            }

            ec.Dispose();

            Console.WriteLine("Method: End TimeSinglePersonsSync()");
        }

        #endregion

        #region BulkLookup

        /// <summary>
        /// This method will use Ethos Integration to lookup many groups of GUIDS with the persons resource and report an average of how long it takes. 
        /// </summary>
        /// <returns></returns>
        internal static async Task TimeBulkPersonsSync()
        {
            // Print out the configuration used to generate the numbers for this run
            Console.WriteLine("Method: Start TimeBulkPersonsSync()");
            Console.Write("  ** numBulkIterations: ");
            foreach (int num in bulkRowsToReturn)
            {
                Console.Write(" " + num);
            }
            Console.WriteLine();
            Console.WriteLine("  ** resourceName: " + resourceName);
            Console.WriteLine("  ** criteria: " + bulkCriteria);
            Console.WriteLine("  ** Time of run: " + DateTime.Now.ToString("MM/dd/yyyy h:mm:ss"));

            if (await ec.Authenticate())
            {
                Dictionary<string, string> bulkSearchParams = new Dictionary<string, string>();
                bulkSearchParams.Add("criteria", bulkCriteria);

                // Setup as parameters on the URL query for the GetAll call
                GetResponse GUIDsResponse = ec.GetAll(resourceName, bulkSearchParams, offset: 0, limit: -1);
                JArray a = JArray.Parse(GUIDsResponse.Data);

                Console.WriteLine("  Bulk: Based on criteria " + bulkCriteria + " there are " + a.Count + " records in the result set.");

                foreach (int rowsToReturn in bulkRowsToReturn)
                {
                    long totalGUIDLookupTime = 0;
                    int i = 0;

                    for (i = 1; i <= numOfBulkIterations; i++)
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        GUIDsResponse = ec.GetAll(resourceName, bulkSearchParams, offset: 0, limit: rowsToReturn); // Get an array of results from Ethos
                        watch.Stop();
                        totalGUIDLookupTime += watch.ElapsedMilliseconds;

                        //Console.WriteLine("Wait time for lookup of " + rowsToReturn + " records, based on " + bulkSearchCriteria + ": " + watch.ElapsedMilliseconds + " msec");
                    }

                    int j = i - 1;

                    var average = totalGUIDLookupTime / --i;

                    Console.WriteLine("  Average wait time for call to Ethos getting " + rowsToReturn + " records (over " + numOfBulkIterations + " iterations): " + average + " msec.");
                    Console.WriteLine("  Total time for this run: " + totalGUIDLookupTime + " msec.");
                }
            }
            else
            {
                Console.WriteLine("  Didn't authenticate");
            }

            ec.Dispose();

            Console.WriteLine("Method: End TimeBulkPersonsSync()");
        }

        #endregion

        #region AsyncStuff

        //  I put this section together to test to see if running a bunch of threads asynchronusly would give a 
        //    big performance increase over running the calls synchronously. Turns out the answer is not really. 
        //
        //  The overhead of the async/await is pretty heafty. Plus, in our environment at least, hitting the server
        //    with the same all the requests at the same time slowed down the response time some. 
        //
        //  I am abandoning this code, but still want it around in case I want to pick it up again. 

        /// <summary>
        /// This method looks up a single GUID and reports the time it took to get it, asynchronously.
        /// </summary>
        /// <param name="guid">The GUID to be looked up via Ethos Integration.</param>
        /// <param name="order">A value to indicated when the was called.</param>
        /// <returns></returns>
        private static async Task GetOnePersonsFromGUIDAsync(Guid guid, int order)
        {
            Console.WriteLine("  Attempt " + order);

            double totalGUIDLookupTime = 0;
            double totalAuthTime = 0;
            var authWatch = System.Diagnostics.Stopwatch.StartNew();

            if (await ec.Authenticate())
            {
                authWatch.Stop();
                totalAuthTime = authWatch.Elapsed.TotalMilliseconds;
                Console.WriteLine("  Attempt " + order + " authenticated in " + totalAuthTime + " msec.");

                var lookupWatch = System.Diagnostics.Stopwatch.StartNew();
                GetResponse singlePersonResponse = ec.Get(guid, resourceName, "application/vnd.hedtech.integration.v12+json"); // Get a single result from Ethos, for version 12
                lookupWatch.Stop();
                totalGUIDLookupTime += lookupWatch.Elapsed.TotalMilliseconds;


                JObject o = JObject.Parse(singlePersonResponse.Data);
                string fullname = (string)o["names"][0]["fullName"];

                Console.WriteLine("  Trial " + order + " | Auth time: " + totalAuthTime + " msec. Lookup time " + totalGUIDLookupTime + " msec. Total time " + (totalAuthTime + totalGUIDLookupTime) + " msec.");
            }
        }

        /// <summary>
        /// This method calls GetOnePersonsFromGUIDAsync and then times all the calls. The calls are asynchronous. The timer waits for all the calls to complete. 
        /// </summary>
        internal static async void TimeSeveralPersonsFromGUIDAsync()
        {
            Console.WriteLine("Method: Start TimeSeveralPersonsFromGUIDAsync()");
            Console.Write("  ** numBulkIterations: ");
            Console.WriteLine();
            Console.WriteLine("  ** resourceName: " + resourceName);
            Console.WriteLine("  ** guid: " + myFavGUID);
            Console.WriteLine("  ** Time of run: " + DateTime.Now.ToString("MM/dd/yyyy h:mm:ss"));

            foreach (int numToRun in numAsyncIterations)
            {
                Console.WriteLine("  Running " + numToRun + " asynchronous calls.");

                List<Task> l = new List<Task>();

                var initiateWatch = System.Diagnostics.Stopwatch.StartNew();
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // looping and using the same GUID each time, under the theory it will have a similar return time. 
                for (int i = 1; i <= numToRun; i++)
                {
                    l.Add(GetOnePersonsFromGUIDAsync(new Guid(myFavGUID), i));
                }

                initiateWatch.Stop();
                Console.WriteLine("  Initiated " + numToRun + " trials in " + initiateWatch.ElapsedMilliseconds + " msec.");

                await Task.WhenAll(l); // wait for all lines to complete
                watch.Stop();

                Console.WriteLine("  Overall time for " + numToRun + " trials: " + watch.ElapsedMilliseconds + " msec.");
            }

            Console.WriteLine("Method: Start TimeSeveralPersonsFromGUIDAsync()");
        }

        #region TestingAsyncAwait

        private static async void OneAuth(HttpClient client, int order)
        {
            var innerWatch = System.Diagnostics.Stopwatch.StartNew();
            client.PostAsync("https://integrate.elluciancloud.com/auth", null);
            innerWatch.Stop();

            Console.WriteLine("Time for trial " + order + " without await is " + innerWatch.Elapsed.TotalMilliseconds);
        }

        private static async void OneAuthAwait(HttpClient client, int order)
        {
            var innerWatch = System.Diagnostics.Stopwatch.StartNew();
            await client.PostAsync("https://integrate.elluciancloud.com/auth", null);
            innerWatch.Stop();

            Console.WriteLine("Time for trial " + order + " with await is " + innerWatch.Elapsed.TotalMilliseconds);
        }

        private static async void TestAuthTiming()
        {
            HttpClient Client = new HttpClient();
            Client.PostAsync("https://integrate.elluciancloud.com/auth", null);

            int j = 5;
            List<Task> l = new List<Task>();

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // looping and using the same GUID each time, under the theory it will have a similar return time. 
            for (int i = 1; i <= j; i++)
            {
                OneAuth(Client, i);
            }

            await Task.WhenAll(l); // wait for all lines to complete
            watch.Stop();

            Console.WriteLine("Overall time without await: " + watch.Elapsed.TotalMilliseconds + " msec.");

            watch = System.Diagnostics.Stopwatch.StartNew();

            // looping and using the same GUID each time, under the theory it will have a similar return time. 
            for (int i = 1; i <= j; i++)
            {
                OneAuthAwait(Client, i);
            }

            await Task.WhenAll(l); // wait for all lines to complete
            watch.Stop();

            Console.WriteLine("Overall time with await: " + watch.Elapsed.TotalMilliseconds + " msec.");

            Task.Delay(700).Wait();

            watch = System.Diagnostics.Stopwatch.StartNew();

            // looping and using the same GUID each time, under the theory it will have a similar return time. 
            for (int i = 1; i <= j; i++)
            {
                OneAuth(Client, i);
            }

            await Task.WhenAll(l); // wait for all lines to complete
            watch.Stop();

            Console.WriteLine("Overall time without await(2): " + watch.Elapsed.TotalMilliseconds + " msec.");
        }
        #endregion

        #endregion
    }
}
