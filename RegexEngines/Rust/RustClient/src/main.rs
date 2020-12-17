#![allow(non_snake_case)]
#![allow(unused_imports)]
//#![allow(unused_variables)]
#![allow(unreachable_code)]

use std::io;
use url::Url;
use regex::Regex;
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

	let mut url_to_parse = "http://unused.com?".to_owned();
	url_to_parse.push_str(&query);

	let url = Url::parse(&url_to_parse).unwrap();
	let map: HashMap<String, String> = url.query_pairs().into_owned().collect();

	let pattern = map.get("p").unwrap();
	let text = map.get("t").unwrap();

	//println!("Pattern: {}", pattern);
	//println!("Text: {}", text);

	let re = Regex::new(pattern);

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
