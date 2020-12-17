#![allow(non_snake_case)]
//#![allow(unused_imports)]
//#![allow(unused_variables)]


use std::io;
use url::Url;
use regex::Regex;
use std::collections::HashMap;
use std::error::Error;


fn main() //-> io::Result<()>
{
	let mut query = String::new();

	io::stdin().read_line( & mut query ).expect("Failed to read line from 'stdin'");

	let mut url_to_parse = "http://unused.com?".to_owned();
	url_to_parse.push_str(&query);

	let url = Url::parse(&url_to_parse).unwrap();
	let map: HashMap<String, String> = url.query_pairs().into_owned().collect();

	let pattern = map.get("p").unwrap();
	let text = map.get("t").unwrap();

//let pattern = r".(?P<n1>.)(x)?(?P<n2>.).";
//let text = "abxefghjk";

//println!("Pattern: {}", pattern);
//println!("Text: {}", text);

	let re = Regex::new(pattern);

	if re.is_err()
	{
		let err = re.unwrap_err();

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
