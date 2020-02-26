// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here


#pragma unmanaged

#include <unicode/regex.h>

#pragma comment(lib, "ICU-min\\lib64\\icuin.lib")
//#pragma comment(lib, "ICU-min\\lib64\\icuio.lib")
//#pragma comment(lib, "ICU-min\\lib64\\icutu.lib")
#pragma comment(lib, "ICU-min\\lib64\\icuuc.lib")

#pragma managed


#include <msclr\marshal_cppstd.h>


#endif //PCH_H
