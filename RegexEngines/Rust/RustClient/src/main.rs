#![allow(non_snake_case)]
#![allow(unused_imports)]
//#![allow(unused_variables)]
#![allow(unreachable_code)]

use std::io::Read;
use std::collections::HashMap;


fn main()
{
	let mut input = String::new();

	let r = std::io::stdin().read_to_string( & mut input );

	if r.is_err()
	{
		let err = r.unwrap_err();

		eprintln!("Failed to read from 'stdin'");
		eprintln!("{}", err);

		return;
	}

	let input = input.trim();

//println!("D: Input '{}'", input);

	if input == "v"
	{
		let v = rustc_version_runtime::version();
		println!("{}.{}.{}", v.major, v.minor, v.patch);

		return;
	}

	let parsed = json::parse(&input);

	if parsed.is_err()
	{
		let err = parsed.unwrap_err();

		eprintln!("Failed to parse input: {}", err);
		eprintln!("Input: '{}'", input);

		return;
	}

	let parsed = parsed.unwrap();

	println!("D: JSon: '{:?}'", parsed);

	if ! parsed.is_object()
	{
		eprintln!("Bad json: {}", input);

		return;
	}

	let structure = parsed["s"].as_str().unwrap_or("");
	let pattern = parsed["p"].as_str().unwrap_or("");
	let text = parsed["t"].as_str().unwrap_or("");
	let options = parsed["o"].as_str().unwrap_or("");

println!("D: pattern {:?}", pattern);

	let re; //: std::result::Result<regex::Regex, regex::Error>;

	if structure == "" || structure == "Regex"
	{
		re = regex::Regex::new(pattern);
	}
	else if structure == "RegexBuilder"
	{
		let mut reb : regex::RegexBuilder = regex::RegexBuilder::new(pattern);

		reb.case_insensitive(options.find('i').is_some());
		reb.multi_line(options.find('m').is_some());
		reb.dot_matches_new_line (options.find('s').is_some());
		reb.swap_greed(options.find('U').is_some());
		reb.ignore_whitespace (options.find('x').is_some());
		reb.unicode(options.find('u').is_some());
		reb.octal(options.find('O').is_some());


		let s = parsed["sl"].as_str().unwrap_or("");

		if s != ""
		{
			let n = s.parse::<usize>();
			if n.is_err()
			{
				eprintln!("Invalid 'size_limit': '{}'", s);
				return;
			}

			reb.size_limit( n.unwrap());
		}

		let s = parsed["dsl"].as_str().unwrap_or("");

		if s != ""
		{
			let n = s.parse::<usize>();
			if n.is_err()
			{
				eprintln!("Invalid 'dfa_size_limit': '{}'", s);
				return;
			}

			reb.dfa_size_limit( n.unwrap());
		}

		let s = parsed["nl"].as_str().unwrap_or("");

		if s != ""
		{
			let n = s.parse::<u32>();
			if n.is_err()
			{
				eprintln!("Invalid 'nest_limit': '{}'", s);
				return;
			}

			reb.nest_limit( n.unwrap());
		}

		re = reb.build();
	}
	else
	{
		eprintln!("Invalid 's': {:?}", structure);

		return;
	}

	if re.is_err()
	{
		let err = re.unwrap_err();

		//eprintln!("Failed to parse the pattern.");
		eprintln!("{}", err);

		return;
	}

	let re = re.unwrap();

	for name in re.capture_names()
	{
		println!("N: {}", name.unwrap_or(""));
	}

	for cap in re.captures_iter(text) 
	{
		println!("--M--");
		//println!("C: {:?}", cap);

		for g in cap.iter()
		{
			print!("   G:");
			if g.is_some()
			{
				let g = g.unwrap();
				println!(" {} {}", g.start(), g.end());
			}
			else
			{
				println!();
			}
		}
	}
}
