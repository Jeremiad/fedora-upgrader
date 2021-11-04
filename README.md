# fedora-upgrader

Tool for running Fedora upgrade and Post-upgrade process described on page: https://docs.fedoraproject.org/en-US/quick-docs/dnf-system-upgrade/

## Upgrade

``` fedora-upgrade upgrade ```

## Optional post-upgrade tasks

``` fedora-upgrade post-upgrade ```

## Usage problems

On error:
```
Process terminated. Couldn't find a valid ICU package installed on the system. Set the configuration flag System.Globalization.Invariant to true if you want to run with no globalization support.
   at System.Environment.FailFast(System.String)
   at System.Globalization.GlobalizationMode.GetGlobalizationInvariantMode()
   at System.Globalization.GlobalizationMode..cctor()
   at System.Globalization.CultureData.CreateCultureWithInvariantData()
   at System.Globalization.CultureData.get_Invariant()
   at System.Globalization.CultureInfo..cctor()
   at System.Globalization.CultureInfo.get_InvariantCulture()
   at Serilog.Parsing.PropertyToken..ctor(System.String, System.String, System.String, System.Nullable`1<Serilog.Parsing.Alignment>, Serilog.Parsing.Destructuring, Int32)
   at Serilog.Parsing.MessageTemplateParser.ParsePropertyToken(Int32, System.String, Int32 ByRef)
   at Serilog.Parsing.MessageTemplateParser+<Tokenize>d__1.MoveNext()
   at System.Collections.Generic.LargeArrayBuilder`1[[System.__Canon, System.Private.CoreLib, Version=5.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]].AddRange(System.Collections.Generic.IEnumerable`1<System.__Canon>)
   at System.Collections.Generic.EnumerableHelpers.ToArray[[System.__Canon, System.Private.CoreLib, Version=5.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
   at System.Linq.Enumerable.ToArray[[System.__Canon, System.Private.CoreLib, Version=5.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
   at Serilog.Events.MessageTemplate..ctor(System.String, System.Collections.Generic.IEnumerable`1<Serilog.Parsing.MessageTemplateToken>)
   at Serilog.Parsing.MessageTemplateParser.Parse(System.String)
   at Serilog.Sinks.SystemConsole.Output.OutputTemplateRenderer..ctor(Serilog.Sinks.SystemConsole.Themes.ConsoleTheme, System.String, System.IFormatProvider)
   at Serilog.ConsoleLoggerConfigurationExtensions.Console(Serilog.Configuration.LoggerSinkConfiguration, Serilog.Events.LogEventLevel, System.String, System.IFormatProvider, Serilog.Core.LoggingLevelSwitch, System.Nullable`1<Serilog.Events.LogEventLevel>, Serilog.Sinks.SystemConsole.Themes.ConsoleTheme, Boolean, System.Object)
   at fedora_upgrader.Program+<Main>d__1.MoveNext()
   at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[[fedora_upgrader.Program+<Main>d__1, fedora-upgrader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]](<Main>d__1 ByRef)
   at System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start[[fedora_upgrader.Program+<Main>d__1, fedora-upgrader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]](<Main>d__1 ByRef)
   at fedora_upgrader.Program.Main(System.String[])
   at fedora_upgrader.Program.<Main>(System.String[])
Aborted (core dumped)
```

Run

```
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
```

Or

```
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 fedora-upgrade <command>
```
Or install missing package

```
sudo dnf install libicu
```

[![.NET](https://github.com/Jeremiad/fedora-upgrader/actions/workflows/build.yml/badge.svg)](https://github.com/Jeremiad/fedora-upgrader/actions/workflows/build.yml)
