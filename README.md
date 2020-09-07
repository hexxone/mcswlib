# mcswlib
This package can ping a large variety of different Minecraft-Server versions and get status informations using different patterns.

## Installation:

Available on nuget: https://www.nuget.org/packages/mcswlib/

### Examples:

**Simple Single-use:**

    using(var factory = new ServerStatusFactory())
    {
		var inst = factory.Make("mc.server.me", 25565);
		var res = inst.Updater.Ping();
		Console.WriteLine("Result: " + res);
    }

**Notice:** 
The Factory can re-use a given *ServerStatusUpdater* for the same server to avoid multiple pings with different deriving classes

    // create two instances with the same base
    var a = factory.Make("mc.server.me", 25565, false, "One");
    var b = factory.Make("mc.server.me", 25565, false, "Two");
    Console.WriteLine(a.Updater.Equals(b.Updater)); // prints: true
    
    var c = factory.Make("mc.server.me", 25565, true, "Three");
    Console.WriteLine(a.Updater.Equals(c.Updater)); // prints: false

**Multi-use:**

    var factory = new ServerStatusFactory();
    factory.Make("mc.server.me", 25565, false, "One");
    factory.Make("mc.server.com", 25565, false, "Two");

	while(true) {
		factory.PingAll();
		foreach (var srv in factory.Entries) {
			var events = srv.Update();
			foreach (var evt in events)
				Console.WriteLine($"Server {srv.Label} Event:\r\n{evt}");
		}
		// ...Sleep some time or do something else...
	}


**Async / Event-based**:

	var factory = new ServerStatusFactory();
    
	factory.Changed += (object sender, EventBase[] e) => {
		var srv = (ServerStatus)sender;
		Console.WriteLine("Got new Events for server: " + srv.Label);
		foreach (var evt in e)
			Console.WriteLine(evt);
	};

	factory.Make("mc.server.me", 25565, false, "One");

	factory.StartAutoUpdate();
    
	// continue doing something else



### Credits:

I have done a lot of research on the minecraft-server protocol so most of the code is actually self-written. I have however taken some inspiration from [this gist](https://gist.github.com/csh/2480d14fbbb33b4bbae3) for example.

For detailed info on minecraft protocol versions go here: https://wiki.vg/Protocol_version_numbers


### Libraries:
- [.NET Standard 2.0](https://docs.microsoft.com/de-de/dotnet/standard/net-standard) runtime
- [Microsoft.CSharp](https://docs.microsoft.com/de-de/dotnet/api/microsoft.csharp) for dynamic member access
- [System.Drawing](https://docs.microsoft.com/de-de/dotnet/api/system.drawing?view=netcore-2.1) for parsing the server-icon
- [Newtonsoft.JSON](https://github.com/JamesNK/Newtonsoft.Json) for (de-)serializing the server info and settings
