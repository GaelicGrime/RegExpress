import std.compiler;
import std.format;
import std.stdio;
import std.json;
import std.regex;
import std.range; //?
import std.algorithm; //?


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

			JSONValue result = JSONValue(["v" : v]);

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

			foreach(name; re.namedCaptures)
			{
				writeln(name);
			}

			JSONValue[] matches;

			foreach(match; std.regex.matchAll(text, re))
			{
				// ('match' is 'std.regex.Captures')

				if( match.empty) continue;

				JSONValue[] groups;

				foreach( capture; match) // "groups"
				{
					// ( 'capture' is 'string')

					if( capture.empty) 
					{
						groups ~= JSONValue([-1, 0]);
					}
					else
					{
//writeln("  Cap:", capture);
//writeln("  Position:", capture.ptr - text.ptr);
//writeln("  Length:", capture.length);

						groups ~= JSONValue([capture.ptr - text.ptr, capture.length]);
					}
				}

				matches ~= JSONValue( groups);
			}

			JSONValue result = [ "matches": matches ];

			writeln(result.toString());

			return;
		}

		stderr.writefln("Unsupported command: '%s'", command);
	}
	catch (Exception exc)
	{
		stderr.writeln(exc);
	}
}
