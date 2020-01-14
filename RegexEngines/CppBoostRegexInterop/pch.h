// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here

//#define BOOST_REGEX_DYN_LINK
#define BOOST_REGEX_NO_LIB
//#define BOOST_REGEX_NO_FASTCALL

#define BOOST_REGEX_WIDE_INSTANTIATE
#define BOOST_REGEX_NARROW_INSTANTIATE

#include "boost/regex.hpp"

#include <msclr\marshal_cppstd.h>


#endif //PCH_H
