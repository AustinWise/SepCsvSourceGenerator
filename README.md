# A source generator for Sep CSV parsing

This is an experiment in creating a C# source generator for generating strongly typed parsing of CSV
files using the excellent [Sep CSV library](https://github.com/nietras/Sep).

## TODO

* Expend README.md and include in package.
* Publish on Nuget.org from CI. This involves figuring a new package name prefix, as the current one
  is reserved by someone.
* Consider adding support for list-like types for properties (like arrays or List\<T\>).
