CometD.NET
==========

CometD.NET is an implementation of the Bayeux client protocol in .NET.
The source code is converted from the java source code of cometd-2.6.0 found at http://cometd.org/.

DIRECTORY LAYOUT
----------------

<pre>
+ CometD.NET               - The Bayeux client library in .NET
  |_ Bayeux                - The Bayeux specification
  |_ Client                - The C# Bayeux client implementation
  |_ Common                - Classes from the java cometd-common directory

+ Salesforce.StreamingAPI  - An example demonstrates how to use CometD.NET library to work against a Bayeux server as Salesforce Streaming API
</pre>

BUILDING COMETD.NET
-------------------

This project was built on Visual Studio 2010 with [NuGet Package Manager](http://visualstudiogallery.msdn.microsoft.com/27077b70-9dad-4c64-adcf-c7cf6bc9970c).
It is set to compile with .NET Framework 3.5 with 2 external libraries: log4net and Newtonsoft.Json.

USAGE
-----

The CometD.NET client only work with a Bayeux (CometD) server to connect to.
You can use your installed CometD server (download from http://cometd.org/), or you can use an existing Bayeux server like Salesforce Streaming API.

Usage of this library is basically the same as in the java-client (http://cometd.org/documentation/cometd-java/client),
example:

    class Program
    {
        static void OnMessageReceived(IClientSessionChannel channel, IMessage message, BayeuxClient client)
        {
            // Handles the message
            Console.WriteLine(message);
        }
    
        static void Main(string[] args)
        {
            // Initializes a new BayeuxClient
            string url = "http://localhost:8080/cometd";
            var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                // CometD server socket timeout during connection: 2 minutes
                { ClientTransport.MaxNetworkDelayOption, 120000 }
            };
    
            using ( BayeuxClient client = new BayeuxClient(url, new LongPollingTransport(options)) )
            {
                // Connects to the Bayeux server
                if (client.Handshake(null, 30000))  // Handshake timeout: 30 seconds
                {
                    // Subscribes to channels
                    IClientSessionChannel channel = client.GetChannel("/service/echo");
                    channel.Subscribe( new CallbackMessageListener<BayeuxClient>(OnMessageReceived, client) );
    
                    // Publishes to channels
                    var data = new Dictionary<string, string>()
                    {
                        { "bar", "baz" }
                    };
                    channel.Publish(data);
    
                    Thread.Sleep(120000);
    
                    // Disconnects
                    client.disconnect(30000);
                }
            }
        }
    }

Salesforce Streaming API Demo
=============================

The example: Salesforce.StreamingAPI demonstrates how a streaming client works against the Salesforce Streaming API.

PREREQUISITES
-------------

You need at least a Salesforce Developer account (registered at http://www.developerforce.com/events/regular/registration.php),
or Enterprise, or Unlimited,.. account that support Salesforce Streaming API.

Then, you should use the [Workbench tool](https://workbench.developerforce.com/streaming.php) to create 2 new Push-Topics:
    "AllLeads"         => "Select Id, Name, Company, Phone, MobilePhone, LeadSource From Lead"
    "AllOpportunities" => "Select Id, Name, LeadSource, StageName, Type From Opportunity"

* Refer to: http://www.salesforce.com/us/developer/docs/api_streaming/Content/code_sample_java_prereqs.htm for more information.

CONFIGURATION AND RUN
---------------------

Updates your Salesforce account information (username, password) into the configuration file: Salesforce.StreamingAPI\App.config

* You should add your Internet IP into Salesforce trusted IP ranges (Setup -> Administration Setup -> Security Controls -> Network Access)
to make your password works with this sample.

Finally, builds and runs the example, then watches the Salesforce.StreamingAPI\bin\Debug\*.log files (TopicMessages, ApiClient)
to see all pushed messages that was received from your Salesforce Streaming API.
