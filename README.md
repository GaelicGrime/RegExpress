# RegExpress
Tester for Regular Expressions.

A .NET desktop application made in C#, based on Windows Presentation Foundation (WPF).

It includes several Regular Expression engines:

* **_Regex_** class from .NET Framework 4.8 \[[link](https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=netframework-4.8)\]
* **_wregex_** class from Standard Template Library, MSVC 14.25.28610 \[[link](https://docs.microsoft.com/en-us/cpp/standard-library/regex)\]
* **Boost.Regex** from Boost C++ Libraries 1.73.0 \[[link](https://www.boost.org/doc/libs/1_73_0/libs/regex/doc/html/index.html)\]
* **PCRE2** Open Source Regex Library 10.35 \[[link](https://pcre.org/)\]
* **RE2** C++ Library 2020-05-01 from Google \[[link](https://github.com/google/re2)\]
* **Oniguruma** Regular Expression Library 6.9.5 revised 1 \[[link](https://github.com/kkos/oniguruma)\]
* **ICU Regular Expressions** 66.1 \[[link](http://site.icu-project.org/)\]
* **SubReg** 2020-01-04 \[[link](https://github.com/mattbucknall/subreg)\]
* **Perl** 5.30.2 \[[link](http://strawberryperl.com/)\]
* **Python** 3.8.2 \[[link](https://www.python.org/)\]

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

The Regular Expression libraries (minimal parts) are included.

Only “x64” platform is supported.

<br/>
