# RegExpress
Unpretentious tester for Regular Expressions.

A .NET desktop application made in C#, based on Windows Presentation Foundation (WPF).

It includes several Regular Expression engines:

* **[_Regex_](https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=netframework-4.8)** class from .NET Framework 4.8
* **[_Regex_](https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=net-5.0)** class from .NET 5
* **[_wregex_](https://docs.microsoft.com/en-us/cpp/standard-library/regex)** class from Standard Template Library, MSVC 14.28.29912
* **[Boost.Regex](https://www.boost.org/doc/libs/1_75_0/libs/regex/doc/html/index.html)** from Boost C++ Libraries 1.76.0
* **[PCRE2](https://pcre.org/)** Open Source Regex Library 10.36
* **[RE2](https://github.com/google/re2)** C++ Library 2021-04-01 from Google
* **[Oniguruma](https://github.com/kkos/oniguruma)** Regular Expression Library 6.9.7.1
* **[ICU Regular Expressions](http://site.icu-project.org/)** 69.1
* **[SubReg](https://github.com/mattbucknall/subreg)** 2020-01-04
* **[Perl](http://strawberryperl.com/)** 5.32.0.1
* **[Python](https://www.python.org/)** 3.9.5
* **[Rust](https://docs.rs/regex)** 1.52.1 (*Regex* and *RegexBuilder* structs)
* **[D](https://dlang.org/articles/regular-expression.html)** 2.096.1 (*std.regex* module)

<br/>

Sample:

<br/>


![Screenshot of RegExpress](Misc/Screenshot2.png)

<br/>

You can press “➕” to open more tabs.

The contents is saved and reloaded automatically.

<br/>

* [Download Latest Release](https://github.com/Viorel/RegExpress/releases)

<br/>

The archive, which is attached to each release, contains an executable that runs in Windows 10 (64-bit).

The sources contain code written in C#, C, C++ and C++/CLI. Can be compiled with Visual Studio 2019 that includes the next workloads:

* .NET desktop development
* Desktop development with C++, incuding C++/CLI support.

The Regular Expression libraries (minimal parts) are included. No additional installations are needed, however to alter the helper Rust component, the Cargo package manager is required. To alter the helper D component, the DMD compiler is required.

Only “x64” platform is supported.

<br/>
