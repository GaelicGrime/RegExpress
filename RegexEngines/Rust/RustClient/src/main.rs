#![allow(non_snake_case)]
#![allow(unused_imports)]
//#![allow(unused_variables)]
#![allow(unreachable_code)]

use std::io;
use url::Url;
//use regex::Regex;
use std::collections::HashMap;
use rustc_version_runtime::version;


fn main()
{
	let mut query = String::new();

	let r = io::stdin().read_line( & mut query );

	if r.is_err()
	{
		let err = r.unwrap_err();

		eprintln!("Failed to read line from 'stdin'");
		eprintln!("{}", err);
	}

	if query.trim() == "v"
	{
		let v = rustc_version_runtime::version();
		println!("{}.{}.{}", v.major, v.minor, v.patch);

		return;
	}

println!("D: '{:?}'", query); //

	let mut url_to_parse = "http://unused.com?".to_owned();
	url_to_parse.push_str(&query);

	let url = Url::parse(&url_to_parse).unwrap();
	let map: HashMap<String, String> = url.query_pairs().into_owned().collect();

	let empty = String::from("");

	let pattern = map.get("p").unwrap_or(&empty);
	let text = map.get("t").unwrap_or(&empty);
	let structure = map.get("s").unwrap_or(&empty);
	let options = map.get("o").unwrap_or(&empty);

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

		let s = map.get("sl").unwrap_or(&empty);
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
	
		let s = map.get("dsl").unwrap_or(&empty);
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

		let s = map.get("nl").unwrap_or(&empty);
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
