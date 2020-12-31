import std.compiler;
import std.format;
import std.stdio;
import std.json;
import std.regex;
import std.range;
//import std.algorithm;


void main()
{
	try
	{
		string s;
		stdin.readf("%s", s);

		JSONValue json_value = parseJSON(s);

		const(JSONValue*) command_j = "c" in json_value;
		const string command = command_j == null ? "" : command_j.str;

//writef("Command: %s", command);

		if( command == "v")
		{
			string v = format("%s.%s", version_major, version_minor);

			JSONValue result = JSONValue(["version" : v]);

			writeln(result.toString());

			return;
		}

		if( command == "m" || command == "")
		{
			const string pattern = json_value["p"].str;
			const string text = json_value["t"].str;
			const(JSONValue*) flags_j = "f" in json_value;
			const string flags = flags_j == null ? "" : flags_j.str;

//writefln("Pattern: '%s'", pattern);
//writefln("Text: '%s'", text);
//writefln("Flags: '%s'", flags);

			auto re = regex(pattern, flags);

			JSONValue[] names;

			foreach(name; re.namedCaptures)
			{
				names ~= JSONValue(name);
			}

			JSONValue[] matches;
			JSONValue[] empty_array0;
			JSONValue empty_array = JSONValue(empty_array0);

			foreach(match; std.regex.matchAll(text, re))
			{
				// ('match' is 'std.regex.Captures')

				if( match.empty) continue;

				JSONValue[] groups;

				foreach( capture; match) // "groups"
				{
					// ( 'capture' is 'string')

					if( capture == null) // failed?
					{
						groups ~= empty_array;
					}
					else
					{
//writeln("  Cap:", capture);
//writeln("  Position:", capture.ptr - text.ptr);
//writeln("  Length:", capture.length);
						groups ~= JSONValue([capture.ptr - text.ptr, capture.length]);
					}
				}

				long[] named_groups;

				foreach(name; re.namedCaptures)
				{
					if( match[name].empty) 
					{
						named_groups ~= -1;
					}
					else
					{
						named_groups ~= match[name].ptr - text.ptr;
					}
				}

				auto const i = match.hit.ptr - text.ptr;

				JSONValue one = [ 
					"i": JSONValue(i),
					"g": JSONValue(groups),
					"n": JSONValue(named_groups)
				];

				matches ~= one;
			}

			JSONValue result = [
				"names": names,
				"matches": matches
			];

			writeln(toJSON(result, /*pretty:=*/ false));

			return;
		}

		stderr.writefln("Unsupported command: '%s'", command);
	}
	catch (RegexException exc)
	{
		stderr.writeln(exc.message());
	}
	catch (Exception exc)
	{
		stderr.writeln(exc);
	}
}
