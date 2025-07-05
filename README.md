# A source generator for Sep CSV parsing

This is an experiment in creating a C# source generator for generating strongly typed parsing of CSV
files using the excellent [Sep CSV library](https://github.com/nietras/Sep).

> WARNING:
This is also also currently a way for me to experiment with LLM based code generation. I have not yet
validated that the code actually parses CSV files (lol), but I have validated that the generated
code looks cool. I'll integrate this parser into some personal projects before I publish it for
wider use.

## TODO

* Validate the all property types are valid, i.e. implement ISpanParsable\<T\>.
* Consider adding support for list-like types for properties (like arrays or List\<T\>).
* Consider implementing this library as a pure source generator with no class library. That is, the
  source generator would generate and embed the attributes. See
  [this Roslyn issue](https://github.com/dotnet/roslyn/issues/76584)
  for some hints.
