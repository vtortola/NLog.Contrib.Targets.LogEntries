# NLog.Contrib.Targets.LogEntries

Simple LogEntries NLog target that works properly on Linux. 

 - Uses SSL. 
 - Multiplexes multiple targets (even with different tokens) into the same connection.
 - It is asynchronous.
 - The token can be set in an environment variable.
 - The UTF8 conversion is done using a static array and not creating arrays dynamically.
 - It does not do additional string concatenations to format the data in the expected protocol format.
 - The [official NLog target from Rapid7](https://github.com/rapid7/le_dotnet) uses an API that is not supported in Linux [(`IOControl`)](https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.iocontrol(v=vs.110).aspx). Since the `KeepAlive` is not configured to a shorter interval, in some cloud environments the connections gets half-open after [some minutes of inactivity](//github.com/rapid7/le_dotnet/blob/master/src/LogentriesCore/LeClient.cs#L96). This component assumes that if the connection has been idle for more than 30 seconds, the connection is dead and a new one is created. Since all the targets in the application are multiplexed through the same connection, this situation may never happen in production.


## Installation
It is available in Nuget: [NLog.Contrib.Targets.LogEntries](https://www.nuget.org/packages/NLog.Contrib.Targets.LogEntries/)
```
dotnet add package NLog.Contrib.Targets.LogEntries --version 1.0.0-pre-03
```

## Usage
There  is two ways of providing the token. The traditional one, providing the token hardcoded in the `.config` file using the `token` parameter in the `target` configuration; and also it is possible to provide the name of an enviroment variable in which the token is stored with the `tokenEnvVar` parameter, which is handy for using it in Docker containers.

### Providing the token directly in the configuration
```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <extensions>
    <add assembly="NLog.Contrib.Targets.LogEntries"/>
  </extensions>
  <targets>
    <target name="logentries" type="LogEntries" token="[your LogEntries token]"
            layout="${date:format=ddd MMM dd} ${time:format=HH:mm:ss} ${date:format=zzz yyyy} ${logger} : ${LEVEL}, ${message}"/>
  </targets>
  <rules>
    <logger name="*" minlevel="Trace" writeTo="logentries" />
  </rules>
</nlog>
```

### Providing the token in an environment variable
```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <extensions>
    <add assembly="NLog.Contrib.Targets.LogEntries"/>
  </extensions>
  <targets>
    <target name="logentries" type="LogEntries" tokenEnvVar="[ENV VAR NAME]"
            layout="${date:format=ddd MMM dd} ${time:format=HH:mm:ss} ${date:format=zzz yyyy} ${logger} : ${LEVEL}, ${message}"/>
  </targets>
  <rules>
    <logger name="*" minlevel="Trace" writeTo="logentries" />
  </rules>
</nlog>
```

